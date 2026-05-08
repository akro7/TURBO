using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MKVenomTool
{
    public partial class MainWindow : Window
    {
        private enum FlashMode { Fastboot, Odin, Sideload, Tools, BackupRestore }

        private FlashMode _mode = FlashMode.Fastboot;
        private readonly ObservableCollection<FlashRow> _fbRows = new();
        private readonly ObservableCollection<FlashRow> _odinRows = new();
        private readonly ObservableCollection<BackupAppRow> _backupApps = new();
        private readonly ObservableCollection<BackupSetRow> _backupSets = new();

        private string _pitFilePath = "";
        private bool _deviceChecked;
        private bool _deviceConnected;
        private bool _uiReady;

        public MainWindow()
        {
            InitializeComponent();
            _uiReady = true;

            BuildFastbootRows();
            BuildOdinRows();

            BindItemsSources();

            ApplyTheme("Blue");
            SwitchMode(FlashMode.Fastboot);
            ShowTab("cmd");

            AppendLog("MK Venom Tool ready.");
            AppendLog($"platform-tools (adb)      : {(ToolsManager.ExeExists("platform-tools", "adb") ? "✓ ready" : "✗ missing")}");
            AppendLog($"platform-tools (fastboot) : {(ToolsManager.ExeExists("platform-tools", "fastboot") ? "✓ ready" : "✗ missing")}");
            AppendLog($"odin engine (ekoflash)    : {(ToolsManager.ExeExists("odin", "ekoflash") ? "✓ ready" : "✗ missing")}");
            AppendLog($"zadig                     : {(ToolsManager.ExeExists("zadig", "zadig") ? "✓ ready" : "✗ missing")}");

            UpdateCommandPreview();
        }

        private T? Ui<T>(string name) where T : class => FindName(name) as T;

        private bool IsChecked(params string[] names)
        {
            foreach (var n in names)
            {
                if (Ui<CheckBox>(n) is CheckBox cb && cb.IsChecked == true)
                    return true;
            }
            return false;
        }

        private void BindItemsSources()
        {
            if (Ui<ItemsControl>("RowsList") is ItemsControl fbList) fbList.ItemsSource = _fbRows;
            if (Ui<ItemsControl>("OdinRowsList") is ItemsControl odinList) odinList.ItemsSource = _odinRows;
            if (Ui<ListView>("BackupAppsList") is ListView apps) apps.ItemsSource = _backupApps;
            if (Ui<ListView>("BackupSetsList") is ListView sets) sets.ItemsSource = _backupSets;
        }

        // ===== Theme =====
        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string t)
            {
                ApplyTheme(t);
                AppendLog($"Theme: {t}");
            }
        }

        private void ApplyTheme(string theme)
        {
            var map = new Dictionary<string, (Color ac, Color acSoft, Color border, Color panel, Color success, Color danger)>
            {
                ["Blue"]    = (Color.FromRgb(0x37,0xCF,0xFF), Color.FromArgb(0x7A,0x35,0xCF,0xFF), Color.FromArgb(0x4C,0x52,0xBF,0xFF), Color.FromArgb(0x3A,0x0D,0x1B,0x33), Color.FromArgb(0x5A,0x1C,0xE1,0x7A), Color.FromArgb(0x5A,0xFF,0x4C,0x78)),
                ["Purple"]  = (Color.FromRgb(0xB6,0x7B,0xFF), Color.FromArgb(0x80,0xA1,0x4D,0xFF), Color.FromArgb(0x4C,0xB1,0x86,0xFF), Color.FromArgb(0x3A,0x17,0x12,0x3A), Color.FromArgb(0x5A,0x38,0xD9,0x9E), Color.FromArgb(0x5A,0xFF,0x5E,0x98)),
                ["Emerald"] = (Color.FromRgb(0x43,0xF2,0xC2), Color.FromArgb(0x7A,0x16,0xC4,0x98), Color.FromArgb(0x4C,0x5F,0xE4,0xC2), Color.FromArgb(0x3A,0x0A,0x1C,0x1A), Color.FromArgb(0x5A,0x27,0xE8,0x9D), Color.FromArgb(0x5A,0xFF,0x6C,0x85)),
                ["Crimson"] = (Color.FromRgb(0xFF,0x6D,0xA8), Color.FromArgb(0x7A,0xFF,0x5B,0x93), Color.FromArgb(0x4C,0xFF,0x91,0xC2), Color.FromArgb(0x3A,0x20,0x0E,0x24), Color.FromArgb(0x5A,0x38,0xE0,0x9A), Color.FromArgb(0x5A,0xFF,0x4A,0x72)),
                ["Gold"]    = (Color.FromRgb(0xFF,0xB8,0x30), Color.FromArgb(0x7A,0xFF,0xA0,0x20), Color.FromArgb(0x55,0xFF,0xC0,0x50), Color.FromArgb(0x38,0x1E,0x14,0x05), Color.FromArgb(0x5A,0x1C,0xE1,0x7A), Color.FromArgb(0x5A,0xFF,0x4C,0x78))
            };

            if (!map.TryGetValue(theme, out var t)) return;

            // نفس Keys بتاعة تصميمك القديم
            Resources["AccentBrush"] = new SolidColorBrush(t.ac);
            Resources["AccentSoftBrush"] = new SolidColorBrush(t.acSoft);
            Resources["BorderBrush"] = new SolidColorBrush(t.border);
            Resources["PanelBrush"] = new SolidColorBrush(t.panel);
            Resources["SuccessBrush"] = new SolidColorBrush(t.success);
            Resources["DangerBrush"] = new SolidColorBrush(t.danger);

            SetModeButtonVisual();
        }

        // ===== Tabs =====
        private void TabCmd_Click(object sender, RoutedEventArgs e) => ShowTab("cmd");
        private void TabOptions_Click(object sender, RoutedEventArgs e) => ShowTab("options");

        private void ShowTab(string tab)
        {
            if (Ui<FrameworkElement>("TabCmdPanel") is FrameworkElement cmdPanel)
                cmdPanel.Visibility = tab == "cmd" ? Visibility.Visible : Visibility.Collapsed;

            if (Ui<FrameworkElement>("TabOptionsPanel") is FrameworkElement optPanel)
                optPanel.Visibility = tab == "options" ? Visibility.Visible : Visibility.Collapsed;

            var accent = Resources["AccentBrush"] as Brush ?? Brushes.Cyan;
            var muted = Resources["TextMutedBrush"] as Brush ?? Brushes.LightGray;

            if (Ui<Button>("TabCmdBtn") is Button cmdBtn)
            {
                cmdBtn.Foreground = tab == "cmd" ? accent : muted;
                cmdBtn.BorderBrush = tab == "cmd" ? accent : Brushes.Transparent;
            }

            if (Ui<Button>("TabOptionsBtn") is Button optBtn)
            {
                optBtn.Foreground = tab == "options" ? accent : muted;
                optBtn.BorderBrush = tab == "options" ? accent : Brushes.Transparent;
            }
        }

        // ===== Modes =====
        private void FastbootMode_Click(object sender, RoutedEventArgs e) => SwitchMode(FlashMode.Fastboot);
        private void OdinMode_Click(object sender, RoutedEventArgs e) => SwitchMode(FlashMode.Odin);
        private void SideloadMode_Click(object sender, RoutedEventArgs e) => SwitchMode(FlashMode.Sideload);
        private void ToolsMode_Click(object sender, RoutedEventArgs e) => SwitchMode(FlashMode.Tools);
        private void BackupRestoreMode_Click(object sender, RoutedEventArgs e) => SwitchMode(FlashMode.BackupRestore);

        private void SwitchMode(FlashMode mode)
        {
            _mode = mode;
            _deviceChecked = false;
            _deviceConnected = false;
            SetModeButtonVisual();

            if (Ui<FrameworkElement>("PanelFastboot") is FrameworkElement p1)
                p1.Visibility = mode == FlashMode.Fastboot ? Visibility.Visible : Visibility.Collapsed;

            if (Ui<FrameworkElement>("PanelOdin") is FrameworkElement p2)
                p2.Visibility = mode == FlashMode.Odin ? Visibility.Visible : Visibility.Collapsed;

            if (Ui<FrameworkElement>("PanelSideload") is FrameworkElement p3)
                p3.Visibility = mode == FlashMode.Sideload ? Visibility.Visible : Visibility.Collapsed;

            if (Ui<FrameworkElement>("PanelTools") is FrameworkElement p4)
                p4.Visibility = mode == FlashMode.Tools ? Visibility.Visible : Visibility.Collapsed;

            if (Ui<FrameworkElement>("PanelBackupRestore") is FrameworkElement p5)
                p5.Visibility = mode == FlashMode.BackupRestore ? Visibility.Visible : Visibility.Collapsed;

            if (Ui<TextBlock>("DeviceStatusText") is TextBlock st)
            {
                st.Text = "Not checked";
                st.Foreground = Resources["WarningBrush"] as Brush ?? Brushes.Orange;
            }

            UpdateCommandPreview();
            AppendLog($"Mode -> {mode}");
        }

        private void SetModeButtonVisual()
        {
            var on = Resources["AccentSoftBrush"] as Brush ?? Brushes.DodgerBlue;
            var off = new SolidColorBrush(Color.FromArgb(0x2D, 0x18, 0x25, 0x3D));

            foreach (var kv in new Dictionary<string, FlashMode>
            {
                ["FastbootBtn"] = FlashMode.Fastboot,
                ["OdinBtn"] = FlashMode.Odin,
                ["SideloadBtn"] = FlashMode.Sideload,
                ["ToolsBtn"] = FlashMode.Tools,
                ["BackupRestoreBtn"] = FlashMode.BackupRestore
            })
            {
                if (Ui<Button>(kv.Key) is Button b)
                    b.Background = _mode == kv.Value ? on : off;
            }
        }

        // ===== Build rows =====
        private void BuildFastbootRows()
        {
            _fbRows.Clear();
            foreach (var e in new[] { ("boot","BOOT"), ("recovery","RECOVERY"), ("system","SYSTEM"), ("vendor","VENDOR"), ("product","PRODUCT"), ("vbmeta","VBMETA"), ("vendor_boot","VENDOR_BOOT"), ("userdata","USERDATA") })
                _fbRows.Add(new FlashRow { Key = e.Item1, Label = e.Item2 });

            foreach (var r in _fbRows)
                r.PropertyChanged += (_, ev) => { if (ev.PropertyName == nameof(FlashRow.FilePath)) UpdateCommandPreview(); };
        }

        private void BuildOdinRows()
        {
            _odinRows.Clear();
            foreach (var e in new[] { ("BL","BL"), ("AP","AP"), ("CP","CP"), ("CSC","CSC"), ("USERDATA","USERDATA") })
                _odinRows.Add(new FlashRow { Key = e.Item1, Label = e.Item2 });

            foreach (var r in _odinRows)
                r.PropertyChanged += (_, ev) => { if (ev.PropertyName == nameof(FlashRow.FilePath)) UpdateCommandPreview(); };
        }

        // ===== Browse =====
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _fbRows.FirstOrDefault(r => r.Key == key);
            if (row == null) return;

            var dlg = new OpenFileDialog { Filter = "Flash files (*.img;*.bin;*.zip)|*.img;*.bin;*.zip|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true)
            {
                row.FilePath = dlg.FileName;
                AppendLog($"FB {row.Label}: {row.FilePath}");
                UpdateCommandPreview();
            }
        }

        private void BrowseOdin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _odinRows.FirstOrDefault(r => r.Key == key);
            if (row == null) return;

            var dlg = new OpenFileDialog { Filter = "Flash files (*.tar;*.md5;*.img;*.bin)|*.tar;*.md5;*.img;*.bin|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true)
            {
                row.FilePath = dlg.FileName;
                AppendLog($"Odin {row.Label}: {row.FilePath}");
                UpdateCommandPreview();
            }
        }

        private void BrowsePit_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PIT files (*.pit)|*.pit|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true)
            {
                _pitFilePath = dlg.FileName;
                if (Ui<TextBox>("PitPathBox") is TextBox pit) pit.Text = _pitFilePath;
                AppendLog($"PIT: {_pitFilePath}");
                UpdateCommandPreview();
            }
        }

        private void ClearPit_Click(object sender, RoutedEventArgs e)
        {
            _pitFilePath = "";
            if (Ui<TextBox>("PitPathBox") is TextBox pit) pit.Text = "";
            AppendLog("PIT cleared.");
            UpdateCommandPreview();
        }

        private void BrowseSideload_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true)
            {
                if (Ui<TextBox>("SideloadPathBox") is TextBox t) t.Text = dlg.FileName;
                AppendLog($"Sideload: {dlg.FileName}");
                UpdateCommandPreview();
            }
        }

        private void BrowseApk_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "APK files (*.apk)|*.apk|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true)
            {
                if (Ui<TextBox>("ApkPathBox") is TextBox t) t.Text = dlg.FileName;
                AppendLog($"APK: {dlg.FileName}");
            }
        }

        // ===== Detect =====
        private async void DetectDevice_Click(object sender, RoutedEventArgs e)
        {
            if (Ui<TextBlock>("DeviceStatusText") is TextBlock st)
            {
                st.Text = "Checking...";
                st.Foreground = Resources["WarningBrush"] as Brush ?? Brushes.Orange;
            }

            var r = await DetectAsync();
            _deviceChecked = true;
            _deviceConnected = r.ok;

            if (Ui<TextBlock>("DeviceStatusText") is TextBlock status)
            {
                status.Text = r.text;
                status.Foreground = r.ok ? new SolidColorBrush(Color.FromRgb(77, 255, 154)) : new SolidColorBrush(Color.FromRgb(255, 122, 122));
            }

            foreach (var line in r.log.Split('\n'))
                if (!string.IsNullOrWhiteSpace(line)) AppendLog(line.Trim());
        }

        private async Task<(bool ok, string text, string log)> DetectAsync()
        {
            if (_mode == FlashMode.Fastboot || _mode == FlashMode.Tools)
            {
                var r = await RunAsync("platform-tools", "fastboot", "devices", 12000);
                bool ok = (r.Out + r.Err).ToLowerInvariant().Contains("fastboot");
                return ok ? (true, "FASTBOOT CONNECTED", "Fastboot device detected.") : (false, "NO FASTBOOT DEVICE", "No fastboot device found.");
            }

            if (_mode == FlashMode.Sideload || _mode == FlashMode.BackupRestore)
            {
                var r = await RunAsync("platform-tools", "adb", "devices", 12000);
                var m = (r.Out + r.Err).ToLowerInvariant();
                bool ok = m.Contains("\tdevice") || m.Contains("\tsideload");
                return ok ? (true, "ADB CONNECTED", "ADB device detected.") : (false, "NO ADB DEVICE", "No ADB device found.");
            }

            // Odin mode -> ekoflash list
            var list = await RunAsync("odin", "ekoflash", "--list", 15000);
            var full = (list.Out + "\n" + list.Err).Trim();
            var lower = full.ToLowerInvariant();

            if (list.Code == 0 &&
                !lower.Contains("no device") &&
                !lower.Contains("not found") &&
                !lower.Contains("0 device"))
            {
                var nonEmpty = full.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length > 0;
                if (nonEmpty) return (true, "DOWNLOAD MODE READY", full);
            }

            // Fallback USB probe for Samsung VID
            var pnp = await RunProcessAsync("pnputil.exe", "/enum-devices /connected", 12000);
            bool samsung = (pnp.Out + pnp.Err).ToUpperInvariant().Contains("VID_04E8");
            return samsung
                ? (true, "DOWNLOAD MODE READY", "Samsung USB detected (VID_04E8).")
                : (false, "NO DOWNLOAD DEVICE", "No Samsung download-mode device found.");
        }

        // ===== Flash =====
        private async void FlashOne_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _fbRows.FirstOrDefault(r => r.Key == key);
            if (row == null || string.IsNullOrWhiteSpace(row.FilePath))
            {
                AppendLog($"No file for {key}.");
                return;
            }

            await FlashFastbootAsync(new List<FlashRow> { row });
        }

        private async void FlashOneOdin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _odinRows.FirstOrDefault(r => r.Key == key);
            if (row == null || string.IsNullOrWhiteSpace(row.FilePath))
            {
                AppendLog($"No file for {key}.");
                return;
            }

            await FlashOdinAsync(new List<FlashRow> { row });
        }

        private async void StartFlashing_Click(object sender, RoutedEventArgs e)
        {
            if (!_deviceChecked)
            {
                AppendLog("Press Detect Device first.");
                return;
            }

            if (!_deviceConnected)
            {
                AppendLog("Device not connected.");
                return;
            }

            if (_mode == FlashMode.Fastboot)
            {
                var sel = _fbRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).ToList();
                if (sel.Count == 0) { AppendLog("No files selected."); return; }
                await FlashFastbootAsync(sel);
            }
            else if (_mode == FlashMode.Odin)
            {
                var sel = _odinRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).ToList();
                if (sel.Count == 0) { AppendLog("No files selected."); return; }
                await FlashOdinAsync(sel);
            }
            else if (_mode == FlashMode.Sideload)
            {
                await StartSideloadInternal();
            }
        }

        private async Task FlashFastbootAsync(List<FlashRow> rows)
        {
            int total = rows.Count;
            int i = 0;

            foreach (var row in rows)
            {
                i++;
                string args = $"flash {row.Key.ToLowerInvariant()} \"{row.FilePath}\"";
                AppendLog($"> fastboot {args}");
                AppendLog($"[{row.Label}] 1/3 Prepare ({i}/{total})");
                AppendLog($"[{row.Label}] 2/3 Flashing...");

                var r = await RunAsync("platform-tools", "fastboot", args, 30 * 60 * 1000);

                if (r.Code != 0)
                {
                    AppendLog($"[{row.Label}] FAILED (exit {r.Code})");
                    if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.Trim());
                    if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.Trim());
                    return;
                }

                AppendLog($"[{row.Label}] 3/3 Flash completed");
            }

            AppendLog("Fastboot flashing complete.");
            if (IsChecked("AutoRebootCheck"))
            {
                await RunAsync("platform-tools", "fastboot", "reboot", 15000);
                AppendLog("Rebooted.");
            }
        }

        private string BuildOdinCommandArgs(IEnumerable<FlashRow> rows)
        {
            var sb = new StringBuilder();

            foreach (var row in rows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)))
            {
                switch (row.Key.ToUpperInvariant())
                {
                    case "BL":
                        sb.Append($" -b \"{row.FilePath}\"");
                        break;
                    case "AP":
                        sb.Append($" -a \"{row.FilePath}\"");
                        break;
                    case "CP":
                        sb.Append($" -c \"{row.FilePath}\"");
                        break;
                    case "CSC":
                        sb.Append($" -s \"{row.FilePath}\"");
                        break;
                    case "USERDATA":
                        sb.Append($" -u \"{row.FilePath}\"");
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(_pitFilePath))
                sb.Append($" --use-pit \"{_pitFilePath}\"");

            bool autoReboot = IsChecked("OptAutoReboot", "AutoRebootCheck");
            if (!autoReboot) sb.Append(" --no-reboot");

            return sb.ToString().Trim();
        }

        private async Task FlashOdinAsync(List<FlashRow> rows)
        {
            if (!ToolsManager.ExeExists("odin", "ekoflash"))
            {
                AppendLog("ekoflash.exe missing. Cannot flash Odin files.");
                return;
            }

            string args = BuildOdinCommandArgs(rows);
            if (string.IsNullOrWhiteSpace(args))
            {
                AppendLog("No Odin files selected.");
                return;
            }

            if (IsChecked("OptRePartition", "RepartitionChk"))
                AppendLog("Note: Re-Partition option ignored (not supported by current ekoflash CLI).");
            if (IsChecked("OptNandErase"))
                AppendLog("Note: Nand Erase option ignored (not supported by current ekoflash CLI).");
            if (IsChecked("OptFResetTime", "FResetTimeChk"))
                AppendLog("Note: F. Reset Time option ignored (not supported by current ekoflash CLI).");
            if (IsChecked("OptDeviceInfo"))
                AppendLog("Note: Device Info option ignored (not supported by current ekoflash CLI).");
            if (IsChecked("OptFlashLock"))
                AppendLog("Note: Flash Lock option ignored (not supported by current ekoflash CLI).");
            if (IsChecked("OptDecompressData"))
                AppendLog("Note: Decompress Data option ignored (not supported by current ekoflash CLI).");

            AppendLog($"> ekoflash {args}");
            AppendLog("[ODIN] 1/3 Prepare");
            AppendLog("[ODIN] 2/3 Flashing...");

            var r = await RunAsync("odin", "ekoflash", args, 30 * 60 * 1000);

            if (r.Code != 0)
            {
                AppendLog($"[ODIN] FAILED (exit {r.Code})");
                if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.Trim());
                if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.Trim());
                return;
            }

            if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.Trim());
            if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.Trim());

            AppendLog("[ODIN] 3/3 Flash completed");
            AppendLog("Odin flashing complete.");
        }

        // ===== Sideload =====
        private async void StartSideload_Click(object sender, RoutedEventArgs e) => await StartSideloadInternal();

        private async Task StartSideloadInternal()
        {
            string path = Ui<TextBox>("SideloadPathBox")?.Text.Trim() ?? "";
            if (string.IsNullOrEmpty(path))
            {
                AppendLog("Select an OTA ZIP first.");
                return;
            }

            AppendLog($"> adb sideload \"{path}\"");
            var r = await RunAsync("platform-tools", "adb", $"sideload \"{path}\"", 30 * 60 * 1000);

            if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.Trim());
            if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.Trim());
            AppendLog(r.Code == 0 ? "Sideload complete ✓" : "Sideload FAILED.");
        }

        // ===== Tools =====
        private async void QuickCmd_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string cmd) return;
            AppendLog($"> {cmd}");

            var parts = cmd.Split(' ', 2);
            var exe = parts[0];
            var args = parts.Length > 1 ? parts[1] : "";

            var r = await RunAsync("platform-tools", exe, args, 30000);
            if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.TrimEnd());
            if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.TrimEnd());
        }

        private async void RunCustomAdb_Click(object sender, RoutedEventArgs e)
        {
            string args = Ui<TextBox>("CustomAdbBox")?.Text.Trim() ?? "";
            if (string.IsNullOrEmpty(args)) { AppendLog("Enter adb args."); return; }

            AppendLog($"> adb {args}");
            var r = await RunAsync("platform-tools", "adb", args, 60000);

            if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.TrimEnd());
            if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.TrimEnd());
        }

        private async void RunCustomFastboot_Click(object sender, RoutedEventArgs e)
        {
            string args = Ui<TextBox>("CustomFastbootBox")?.Text.Trim() ?? "";
            if (string.IsNullOrEmpty(args)) { AppendLog("Enter fastboot args."); return; }

            AppendLog($"> fastboot {args}");
            var r = await RunAsync("platform-tools", "fastboot", args, 60000);

            if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.TrimEnd());
            if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.TrimEnd());
        }

        private async void InstallApk_Click(object sender, RoutedEventArgs e)
        {
            string p = Ui<TextBox>("ApkPathBox")?.Text.Trim() ?? "";
            if (string.IsNullOrEmpty(p)) { AppendLog("Select APK first."); return; }

            AppendLog($"> adb install \"{p}\"");
            var r = await RunAsync("platform-tools", "adb", $"install \"{p}\"", 120000);

            if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.TrimEnd());
            if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.TrimEnd());
            AppendLog(r.Code == 0 ? "APK installed ✓" : "APK install FAILED.");
        }

        private void LaunchZadig_Click(object sender, RoutedEventArgs e)
        {
            string exe = ToolsManager.GetExePath("zadig", "zadig");
            if (!File.Exists(exe))
            {
                AppendLog("zadig.exe not found.");
                return;
            }

            AppendLog($"Launching Zadig: {exe}");
            try
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppendLog($"Launch failed: {ex.Message}");
            }
        }

        // ===== Action bar =====
        private async void WipeData_Click(object sender, RoutedEventArgs e)
        {
            if (_mode != FlashMode.Fastboot)
            {
                AppendLog("Wipe: Fastboot mode only.");
                return;
            }

            var r = await RunAsync("platform-tools", "fastboot", "-w", 600000);
            AppendLog(r.Code == 0 ? "Wipe done." : $"Wipe FAILED (exit {r.Code}). {r.Err.Trim()}");
        }

        private async void RebootSystem_Click(object sender, RoutedEventArgs e)
        {
            if (_mode == FlashMode.Fastboot || _mode == FlashMode.Tools)
            {
                await RunAsync("platform-tools", "fastboot", "reboot", 15000);
                AppendLog("Reboot: fastboot reboot");
            }
            else
            {
                await RunAsync("platform-tools", "adb", "reboot", 15000);
                AppendLog("Reboot: adb reboot");
            }
        }

        private void ResetAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in _fbRows) r.FilePath = "";
            foreach (var r in _odinRows) r.FilePath = "";
            _pitFilePath = "";

            if (Ui<TextBox>("PitPathBox") is TextBox pit) pit.Text = "";
            if (Ui<TextBox>("SideloadPathBox") is TextBox sideload) sideload.Text = "";
            if (Ui<TextBox>("ApkPathBox") is TextBox apk) apk.Text = "";
            if (Ui<TextBox>("CustomAdbBox") is TextBox cadb) cadb.Text = "";
            if (Ui<TextBox>("CustomFastbootBox") is TextBox cfb) cfb.Text = "";

            if (Ui<TextBox>("CommandPreviewBox") is TextBox cp) cp.Clear();

            AppendLog("All cleared.");
            UpdateCommandPreview();
        }

        // ===== Preview / log =====
        private void UpdateCommandPreview()
        {
            var lines = new List<string>();

            if (_mode == FlashMode.Fastboot)
            {
                lines.AddRange(_fbRows
                    .Where(r => !string.IsNullOrWhiteSpace(r.FilePath))
                    .Select(r => $"fastboot flash {r.Key.ToLower()} \"{r.FilePath}\""));
            }
            else if (_mode == FlashMode.Odin)
            {
                var selected = _odinRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).ToList();
                var args = BuildOdinCommandArgs(selected);
                if (!string.IsNullOrWhiteSpace(args))
                    lines.Add($"ekoflash {args}");
            }
            else if (_mode == FlashMode.Sideload)
            {
                var p = Ui<TextBox>("SideloadPathBox")?.Text;
                if (!string.IsNullOrWhiteSpace(p))
                    lines.Add($"adb sideload \"{p}\"");
            }
            else if (_mode == FlashMode.BackupRestore)
            {
                lines.Add("Backup/Restore mode uses adb shell su -c + tar + pull/push");
            }

            if (Ui<TextBox>("CommandPreviewBox") is TextBox box)
                box.Text = lines.Count == 0 ? "No command queued." : string.Join(Environment.NewLine, lines);
        }

        private void AppendLog(string msg)
        {
            if (!_uiReady) return;
            if (Ui<TextBox>("LogBox") is not TextBox log) return;

            log.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            log.ScrollToEnd();
        }

        // ===== Process =====
        private Task<(int Code, string Out, string Err)> RunAsync(string folder, string exe, string args, int ms)
            => RunProcessAsync(ToolsManager.GetExePath(folder, exe), args, ms);

        private async Task<(int Code, string Out, string Err)> RunProcessAsync(string fileName, string args, int ms)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var p = new Process { StartInfo = psi };
                var o = new StringBuilder();
                var er = new StringBuilder();

                p.OutputDataReceived += (_, ev) => { if (ev.Data != null) o.AppendLine(ev.Data); };
                p.ErrorDataReceived += (_, ev) => { if (ev.Data != null) er.AppendLine(ev.Data); };

                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                using var cts = new CancellationTokenSource(ms);
                try { await p.WaitForExitAsync(cts.Token); }
                catch (OperationCanceledException)
                {
                    try { p.Kill(true); } catch { }
                    return (-1, o.ToString(), $"Timeout {ms / 1000}s");
                }

                return (p.ExitCode, o.ToString(), er.ToString());
            }
            catch (Exception ex)
            {
                return (-1, "", ex.Message);
            }
        }

        // ===== Backup (minimal) =====
        private async void LoadApps_Click(object sender, RoutedEventArgs e)
        {
            _backupApps.Clear();
            AppendLog("Loading apps list...");
            var apps = await BackupService.GetInstalledAppsAsync(AppendLog);

            foreach (var a in apps)
                _backupApps.Add(new BackupAppRow { PackageName = a.PackageName, DisplayName = a.DisplayName });

            AppendLog($"Apps loaded: {_backupApps.Count}");
        }

        private void StartBackup_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Backup start requested.");
        }
    }

    public class FlashRow : INotifyPropertyChanged
    {
        private string _fp = "";
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string FilePath
        {
            get => _fp;
            set
            {
                if (_fp == value) return;
                _fp = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public class BackupAppRow : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string PackageName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string IconLetter { get; set; } = "?";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class BackupSetRow
    {
        public string BackupPath { get; set; } = "";
        public string PackageName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string BackupDateText { get; set; } = "";
        public string IconLetter { get; set; } = "?";
    }
}
