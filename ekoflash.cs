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
        private readonly ObservableCollection<FlashRow> _fbRows = new ObservableCollection<FlashRow>();
        private readonly ObservableCollection<FlashRow> _odinRows = new ObservableCollection<FlashRow>();
        private readonly ObservableCollection<BackupAppRow> _backupApps = new ObservableCollection<BackupAppRow>();
        private readonly ObservableCollection<BackupSetRow> _backupSets = new ObservableCollection<BackupSetRow>();

        private string _pitFilePath = "";
        private bool _deviceChecked;
        private bool _deviceConnected;
        private bool _uiReady;

        public MainWindow()
        {
            InitializeComponent();
            _uiReady = true;
            ApplyTheme("Blue");
            BuildFastbootRows();
            BuildOdinRows();
            SwitchMode(FlashMode.Fastboot);
            ShowTab("cmd");

            if (RowsList != null) RowsList.ItemsSource = _fbRows;
            if (OdinRowsList != null) OdinRowsList.ItemsSource = _odinRows;

            var appsList = GetBackupAppsList();
            var setsList = GetBackupSetsList();
            if (appsList != null) appsList.ItemsSource = _backupApps;
            if (setsList != null) setsList.ItemsSource = _backupSets;

            AppendLog("MK Venom Tool ready.");
            AppendLog($"platform-tools (adb) : {(ToolsManager.ExeExists("platform-tools", "adb") ? "✓ ready" : "✗ missing")}");
            AppendLog($"platform-tools (fastboot) : {(ToolsManager.ExeExists("platform-tools", "fastboot") ? "✓ ready" : "✗ missing")}");
            AppendLog($"odin engine (ekoflash) : {(ToolsManager.ExeExists("odin", "ekoflash") ? "✓ ready" : "✗ missing")}");
            AppendLog($"zadig : {(ToolsManager.ExeExists("zadig", "zadig") ? "✓ ready" : "✗ missing")}");
            UpdateCommandPreview();
        }

        private ListView GetBackupAppsList() => FindName("BackupAppsList") as ListView ?? FindName("AppsListView") as ListView;
        private ListView GetBackupSetsList() => FindName("BackupSetsList") as ListView ?? FindName("BackupsListView") as ListView;
        private Grid GetBackupPanel() => FindName("PanelBackupRestore") as Grid;
        private Button GetBackupModeButton() => FindName("BackupRestoreBtn") as Button;
        private CheckBox GetBkApkOpt() => FindName("BkOptApk") as CheckBox ?? FindName("BackupApkCheck") as CheckBox;
        private CheckBox GetBkDataOpt() => FindName("BkOptData") as CheckBox ?? FindName("BackupDataCheck") as CheckBox;
        private CheckBox GetBkUserDeOpt() => FindName("BkOptUserDe") as CheckBox ?? FindName("BackupUserDeCheck") as CheckBox;
        private CheckBox GetBkObbOpt() => FindName("BkOptObb") as CheckBox ?? FindName("BackupObbCheck") as CheckBox;

        private bool IsChecked(params string[] names)
        {
            foreach (var n in names)
            {
                var el = FindName(n) as CheckBox;
                if (el != null && el.IsChecked == true)
                    return true;
            }
            return false;
        }

        private void Swatch_Click(object s, RoutedEventArgs e)
        {
            if (s is Button b && b.Tag is string n)
            {
                ApplyTheme(n);
                AppendLog($"Theme: {n}");
            }
        }

        private void ApplyTheme(string theme)
        {
            var T = new Dictionary<string, (Color Ac, Color AS, Color Bo, Color Pa, Color Su, Color Da)>
            {
                ["Blue"] = (Color.FromRgb(0x37, 0xCF, 0xFF), Color.FromArgb(0x7A, 0x35, 0xCF, 0xFF), Color.FromArgb(0x4C, 0x52, 0xBF, 0xFF), Color.FromArgb(0x3A, 0x0D, 0x1B, 0x33), Color.FromArgb(0x5A, 0x1C, 0xE1, 0x7A), Color.FromArgb(0x5A, 0xFF, 0x4C, 0x78)),
                ["Purple"] = (Color.FromRgb(0xAA, 0x00, 0xFF), Color.FromArgb(0x7A, 0xAA, 0x00, 0xFF), Color.FromArgb(0x4C, 0xAA, 0x00, 0xFF), Color.FromArgb(0x3A, 0x30, 0x0D, 0x33), Color.FromArgb(0x5A, 0xAA, 0x00, 0xFF), Color.FromArgb(0x5A, 0xFF, 0x00, 0xAA)),
                ["Emerald"] = (Color.FromRgb(0x00, 0xFF, 0xAA), Color.FromArgb(0x7A, 0x00, 0xFF, 0xAA), Color.FromArgb(0x4C, 0x00, 0xFF, 0xAA), Color.FromArgb(0x3A, 0x00, 0x33, 0x1A), Color.FromArgb(0x5A, 0x00, 0xFF, 0xAA), Color.FromArgb(0x5A, 0xFF, 0x00, 0x55)),
                ["Crimson"] = (Color.FromRgb(0xFF, 0x40, 0x40), Color.FromArgb(0x7A, 0xFF, 0x40, 0x40), Color.FromArgb(0x4C, 0xFF, 0x40, 0x40), Color.FromArgb(0x3A, 0x33, 0x00, 0x00), Color.FromArgb(0x5A, 0xFF, 0x40, 0x40), Color.FromArgb(0x5A, 0xFF, 0x00, 0x00)),
                ["Gold"] = (Color.FromRgb(0xFF, 0xCC, 0x00), Color.FromArgb(0x7A, 0xFF, 0xCC, 0x00), Color.FromArgb(0x4C, 0xFF, 0xCC, 0x00), Color.FromArgb(0x3A, 0x33, 0x2A, 0x00), Color.FromArgb(0x5A, 0xFF, 0xCC, 0x00), Color.FromArgb(0x5A, 0xFF, 0xAA, 0x00))
            };
            if (!T.TryGetValue(theme, out var t)) return;
            Resources["AccentBrush"] = new SolidColorBrush(t.Ac);
            Resources["AccentSoftBrush"] = new SolidColorBrush(t.AS);
            Resources["BorderBrush"] = new SolidColorBrush(t.Bo);
            Resources["PanelBrush"] = new SolidColorBrush(t.Pa);
            Resources["SuccessBrush"] = new SolidColorBrush(t.Su);
            Resources["DangerBrush"] = new SolidColorBrush(t.Da);

            var map = new Dictionary<string, Button>
            {
                ["Blue"] = SwatchBlue,
                ["Purple"] = SwatchPurple,
                ["Emerald"] = SwatchEmerald,
                ["Crimson"] = SwatchCrimson,
                ["Gold"] = SwatchGold
            };
            foreach (var kv in map)
            {
                if (kv.Value == null) continue;
                kv.Value.BorderThickness = kv.Key == theme ? new Thickness(3) : new Thickness(1.5);
                kv.Value.Opacity = kv.Key == theme ? 1.0 : 0.6;
            }
            SetModeButtonVisual();
        }

        private void TabCmd_Click(object s, RoutedEventArgs e) => ShowTab("cmd");
        private void TabOptions_Click(object s, RoutedEventArgs e) => ShowTab("options");

        private void ShowTab(string tab)
        {
            if (TabCmdPanel == null) return;
            TabCmdPanel.Visibility = tab == "cmd" ? Visibility.Visible : Visibility.Collapsed;
            if (TabOptionsPanel != null)
                TabOptionsPanel.Visibility = tab == "options" ? Visibility.Visible : Visibility.Collapsed;

            var accent = (Brush)Resources["AccentBrush"];
            var muted = (Brush)Resources["TextMutedBrush"];
            if (TabCmdBtn != null)
            {
                TabCmdBtn.Foreground = tab == "cmd" ? accent : muted;
                TabCmdBtn.BorderBrush = tab == "cmd" ? accent : Brushes.Transparent;
            }
            if (TabOptionsBtn != null)
            {
                TabOptionsBtn.Foreground = tab == "options" ? accent : muted;
                TabOptionsBtn.BorderBrush = tab == "options" ? accent : Brushes.Transparent;
            }
        }

        private void FastbootMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Fastboot);
        private void OdinMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Odin);
        private void SideloadMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Sideload);
        private void ToolsMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Tools);
        private void BackupRestoreMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.BackupRestore);

        private void SwitchMode(FlashMode mode)
        {
            _mode = mode;
            _deviceChecked = false;
            _deviceConnected = false;
            SetModeButtonVisual();
            if (_uiReady)
            {
                if (PanelFastboot != null) PanelFastboot.Visibility = mode == FlashMode.Fastboot ? Visibility.Visible : Visibility.Collapsed;
                if (PanelOdin != null) PanelOdin.Visibility = mode == FlashMode.Odin ? Visibility.Visible : Visibility.Collapsed;
                if (PanelSideload != null) PanelSideload.Visibility = mode == FlashMode.Sideload ? Visibility.Visible : Visibility.Collapsed;
                if (PanelTools != null) PanelTools.Visibility = mode == FlashMode.Tools ? Visibility.Visible : Visibility.Collapsed;

                var backupPanel = GetBackupPanel();
                if (backupPanel != null) backupPanel.Visibility = mode == FlashMode.BackupRestore ? Visibility.Visible : Visibility.Collapsed;

                if (DeviceStatusText != null)
                {
                    DeviceStatusText.Text = "Not checked";
                    DeviceStatusText.Foreground = (Brush)Resources["WarningBrush"];
                }
            }
            UpdateCommandPreview();
            AppendLog($"Mode -> {mode}");
        }

        private void SetModeButtonVisual()
        {
            var on = (Brush)Resources["AccentSoftBrush"];
            var off = new SolidColorBrush(Color.FromArgb(0x2D, 0x18, 0x25, 0x3D));
            if (FastbootBtn != null) FastbootBtn.Background = _mode == FlashMode.Fastboot ? on : off;
            if (OdinBtn != null) OdinBtn.Background = _mode == FlashMode.Odin ? on : off;
            if (SideloadBtn != null) SideloadBtn.Background = _mode == FlashMode.Sideload ? on : off;
            if (ToolsBtn != null) ToolsBtn.Background = _mode == FlashMode.Tools ? on : off;

            var backupBtn = GetBackupModeButton();
            if (backupBtn != null) backupBtn.Background = _mode == FlashMode.BackupRestore ? on : off;
        }

        private void BuildFastbootRows()
        {
            _fbRows.Clear();
            var items = new[] { 
                ("boot", "BOOT"),
                ("recovery", "RECOVERY"),
                ("system", "SYSTEM"),
                ("vendor", "VENDOR"),
                ("product", "PRODUCT"),
                ("vbmeta", "VBMETA"),
                ("vendor_boot", "VENDOR_BOOT"),
                ("userdata", "USERDATA")
            };
            foreach (var e in items)
                _fbRows.Add(new FlashRow { Key = e.Item1, Label = e.Item2 });
            foreach (var r in _fbRows)
                r.PropertyChanged += (_, ev) => { if (ev.PropertyName == nameof(FlashRow.FilePath)) UpdateCommandPreview(); };
        }

        private void BuildOdinRows()
        {
            _odinRows.Clear();
            var items = new[] {
                ("BL", "BL"),
                ("AP", "AP"),
                ("CP", "CP"),
                ("CSC", "CSC"),
                ("USERDATA", "USERDATA")
            };
            foreach (var e in items)
                _odinRows.Add(new FlashRow { Key = e.Item1, Label = e.Item2 });
            foreach (var r in _odinRows)
                r.PropertyChanged += (_, ev) => { if (ev.PropertyName == nameof(FlashRow.FilePath)) UpdateCommandPreview(); };
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string key)
            {
                var row = _fbRows.FirstOrDefault(r => r.Key == key);
                if (row != null)
                {
                    var dlg = new OpenFileDialog { Filter = "Flash files (*.img;*.bin;*.zip)|*.img;*.bin;*.zip|All files (*.*)|*.*", CheckFileExists = true };
                    if (dlg.ShowDialog() == true)
                    {
                        row.FilePath = dlg.FileName;
                        AppendLog($"FB {row.Label}: {row.FilePath}");
                        UpdateCommandPreview();
                    }
                }
            }
        }

        private void BrowseOdin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string key)
            {
                var row = _odinRows.FirstOrDefault(r => r.Key == key);
                if (row != null)
                {
                    var dlg = new OpenFileDialog { Filter = "Flash files (*.tar;*.md5;*.img;*.bin)|*.tar;*.md5;*.img;*.bin|All files (*.*)|*.*", CheckFileExists = true };
                    if (dlg.ShowDialog() == true)
                    {
                        row.FilePath = dlg.FileName;
                        AppendLog($"Odin {row.Label}: {row.FilePath}");
                        UpdateCommandPreview();
                    }
                }
            }
        }

        private void BrowsePit_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PIT files (*.pit)|*.pit|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true)
            {
                _pitFilePath = dlg.FileName;
                if (PitPathBox != null) PitPathBox.Text = _pitFilePath;
                AppendLog($"PIT: {_pitFilePath}");
                UpdateCommandPreview();
            }
        }

        private void ClearPit_Click(object s, RoutedEventArgs e)
        {
            _pitFilePath = "";
            if (PitPathBox != null) PitPathBox.Text = "";
            AppendLog("PIT cleared.");
            UpdateCommandPreview();
        }

        private void BrowseSideload_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true && SideloadPathBox != null)
            {
                SideloadPathBox.Text = dlg.FileName;
                AppendLog($"Sideload: {dlg.FileName}");
                UpdateCommandPreview();
            }
        }

        private void BrowseApk_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "APK files (*.apk)|*.apk|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true && FindName("ApkPathBox") is TextBox apk)
            {
                apk.Text = dlg.FileName;
                AppendLog($"APK: {dlg.FileName}");
            }
        }

        private async void DetectDevice_Click(object s, RoutedEventArgs e)
        {
            if (DeviceStatusText != null)
            {
                DeviceStatusText.Text = "Checking...";
                DeviceStatusText.Foreground = (Brush)Resources["WarningBrush"];
            }
            var (ok, text, log) = await DetectDeviceStatusAsync();
            _deviceChecked = true;
            _deviceConnected = ok;
            if (DeviceStatusText != null)
            {
                DeviceStatusText.Text = text;
                DeviceStatusText.Foreground = ok ? new SolidColorBrush(Color.FromRgb(77, 255, 154)) : new SolidColorBrush(Color.FromRgb(255, 122, 122));
            }
            AppendLog(log);
        }

        private async Task<(bool ok, string text, string log)> DetectDeviceStatusAsync()
        {
            if (_mode == FlashMode.Fastboot || _mode == FlashMode.Tools)
            {
                var r = await RunProcessAsync("platform-tools", "fastboot", "devices", 12000);
                bool ok = (r.Out + r.Err).ToLowerInvariant().Contains("fastboot");
                return (ok, ok ? "FASTBOOT CONNECTED" : "NO FASTBOOT DEVICE", ok ? "Fastboot device detected." : "No fastboot device found.");
            }
            if (_mode == FlashMode.Sideload || _mode == FlashMode.BackupRestore)
            {
                var r = await RunProcessAsync("platform-tools", "adb", "devices", 12000);
                var m = (r.Out + r.Err).ToLowerInvariant();
                bool ok = m.Contains("\tdevice") || m.Contains("\tsideload");
                return (ok, ok ? "ADB CONNECTED" : "NO ADB DEVICE", ok ? "ADB device detected." : "No ADB device found.");
            }
            if (!ToolsManager.ExeExists("odin", "ekoflash"))
                return (false, "EKOFLASH MISSING", "ekoflash.exe not found.");

            var od = await RunProcessAsync("odin", "ekoflash", "--list", 15000);
            var full = (od.Out + " " + od.Err).Trim();
            var l = full.ToLowerInvariant();

            if (od.Code == 0 && !l.Contains("no device") && !l.Contains("not found") && !l.Contains("0 device"))
                return (true, "DOWNLOAD MODE READY", string.IsNullOrWhiteSpace(full) ? "Download mode detected." : full);

            var pnp = await RunProcessAsync("pnputil.exe", "/enum-devices /connected", 12000);
            bool samsung = (pnp.Out + pnp.Err).ToUpperInvariant().Contains("VID_04E8");
            return samsung ? (true, "DOWNLOAD MODE READY", "Samsung USB detected (VID_04E8).") : (false, "NO DOWNLOAD DEVICE", "No Samsung download-mode device found.");
        }

        private async void FlashOne_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string key)
            {
                var row = _fbRows.FirstOrDefault(r => r.Key == key);
                if (row != null && !string.IsNullOrWhiteSpace(row.FilePath))
                    await FlashFastbootAsync(new List<FlashRow> { row });
                else
                    AppendLog($"No file for {key}.");
            }
        }

        private async void FlashOneOdin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string key)
            {
                var row = _odinRows.FirstOrDefault(r => r.Key == key);
                if (row != null && !string.IsNullOrWhiteSpace(row.FilePath))
                    await FlashOdinAsync(new List<FlashRow> { row });
                else
                    AppendLog($"No file for {key}.");
            }
        }

        private async void StartFlashing_Click(object s, RoutedEventArgs e)
        {
            if (!_deviceChecked) { AppendLog("Press Detect Device first."); return; }
            if (!_deviceConnected) { AppendLog("Device not connected."); return; }
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
                var r = await RunProcessAsync("platform-tools", "fastboot", args, 30 * 60 * 1000);
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
                await RunProcessAsync("platform-tools", "fastboot", "reboot", 15000);
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
                    case "BL": sb.Append($" --bl \"{row.FilePath}\""); break;
                    case "AP": sb.Append($" --ap \"{row.FilePath}\""); break;
                    case "CP": sb.Append($" --cp \"{row.FilePath}\""); break;
                    case "CSC": sb.Append($" --csc \"{row.FilePath}\""); break;
                    case "USERDATA": sb.Append($" --userdata \"{row.FilePath}\""); break;
                }
            }
            if (!string.IsNullOrWhiteSpace(_pitFilePath)) sb.Append($" --pit \"{_pitFilePath}\"");
            if (!IsChecked("AutoRebootCheck", "OptAutoReboot")) sb.Append(" --no-reboot");
            return sb.ToString().Trim();
        }

        private async Task FlashOdinAsync(List<FlashRow> rows)
        {
            if (!ToolsManager.ExeExists("odin", "ekoflash")) { AppendLog("ekoflash.exe missing. Cannot flash Odin files."); return; }
            string args = BuildOdinCommandArgs(rows);
            if (string.IsNullOrWhiteSpace(args)) { AppendLog("No Odin files selected."); return; }
            AppendLog($"> ekoflash {args}");
            var r = await RunProcessAsync("odin", "ekoflash", args, 30 * 60 * 1000);
            if (r.Code != 0)
            {
                AppendLog($"[ODIN] FAILED (exit {r.Code})");
                if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.Trim());
                if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.Trim());
                return;
            }
            if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.Trim());
            if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.Trim());
            AppendLog("Odin flashing complete.");
        }

        private async Task StartSideloadInternal()
        {
            string path = SideloadPathBox?.Text.Trim() ?? "";
            if (string.IsNullOrEmpty(path)) { AppendLog("Select an OTA ZIP first."); return; }
            AppendLog($"> adb sideload \"{path}\"");
            var r = await RunProcessAsync("platform-tools", "adb", $"sideload \"{path}\"", 30 * 60 * 1000);
            if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.Trim());
            if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.Trim());
            AppendLog(r.Code == 0 ? "Sideload complete ✓" : "Sideload FAILED.");
        }

        private async void StartSideload_Click(object s, RoutedEventArgs e) => await StartSideloadInternal();

        private async void QuickCmd_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string cmd)
            {
                AppendLog($"> {cmd}");
                var parts = cmd.Split(' ', 2);
                var r = await RunProcessAsync("platform-tools", parts[0], parts.Length > 1 ? parts[1] : "", 30000);
                if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.TrimEnd());
                if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.TrimEnd());
            }
        }

        private async void RunCustomAdb_Click(object sender, RoutedEventArgs e)
        {
            if (FindName("CustomAdbBox") is TextBox box)
            {
                string args = box.Text.Trim();
                if (string.IsNullOrEmpty(args)) { AppendLog("Enter adb args."); return; }
                AppendLog($"> adb {args}");
                var r = await RunProcessAsync("platform-tools", "adb", args, 60000);
                if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.TrimEnd());
                if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.TrimEnd());
            }
            else AppendLog("CustomAdbBox not found.");
        }

        private async void RunCustomFastboot_Click(object sender, RoutedEventArgs e)
        {
            if (FindName("CustomFastbootBox") is TextBox box)
            {
                string args = box.Text.Trim();
                if (string.IsNullOrEmpty(args)) { AppendLog("Enter fastboot args."); return; }
                AppendLog($"> fastboot {args}");
                var r = await RunProcessAsync("platform-tools", "fastboot", args, 60000);
                if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.TrimEnd());
                if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.TrimEnd());
            }
            else AppendLog("CustomFastbootBox not found.");
        }

        private async void InstallApk_Click(object sender, RoutedEventArgs e)
        {
            if (FindName("ApkPathBox") is TextBox box)
            {
                string p = box.Text.Trim();
                if (string.IsNullOrEmpty(p)) { AppendLog("Select APK first."); return; }
                AppendLog($"> adb install \"{p}\"");
                var r = await RunProcessAsync("platform-tools", "adb", $"install \"{p}\"", 120000);
                if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.TrimEnd());
                if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.TrimEnd());
                AppendLog(r.Code == 0 ? "APK installed ✓" : "APK install FAILED.");
            }
            else AppendLog("ApkPathBox not found.");
        }

        private void LaunchZadig_Click(object s, RoutedEventArgs e)
        {
            string exe = ToolsManager.GetExePath("zadig", "zadig");
            if (!File.Exists(exe)) { AppendLog("zadig.exe not found."); return; }
            AppendLog($"Launching Zadig: {exe}");
            try { Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true }); }
            catch (Exception ex) { AppendLog($"Launch failed: {ex.Message}"); }
        }

        private async void FlashAll_Click(object sender, RoutedEventArgs e) => await StartFlashing_Click(sender, e);
        private void Cancel_Click(object sender, RoutedEventArgs e) { AppendLog("Cancel requested."); Close(); }

        private void UpdateCommandPreview()
        {
            if (CommandPreviewBox == null) return;
            var lines = new List<string>();
            if (_mode == FlashMode.Fastboot)
                lines.AddRange(_fbRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).Select(r => $"fastboot flash {r.Key.ToLowerInvariant()} \"{r.FilePath}\""));
            else if (_mode == FlashMode.Odin)
            {
                var args = BuildOdinCommandArgs(_odinRows);
                if (!string.IsNullOrWhiteSpace(args)) lines.Add($"ekoflash {args}");
            }
            else if (_mode == FlashMode.Sideload && SideloadPathBox != null && !string.IsNullOrWhiteSpace(SideloadPathBox.Text))
                lines.Add($"adb sideload \"{SideloadPathBox.Text}\"");
            CommandPreviewBox.Text = lines.Count == 0 ? "No command queued." : string.Join(Environment.NewLine, lines);
        }

        private void AppendLog(string msg)
        {
            if (!_uiReady || LogBox == null) return;
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            LogBox.ScrollToEnd();
        }

        private Task<(int Code, string Out, string Err)> RunProcessAsync(string folder, string exe, string args, int ms)
        {
            return RunProcessAsync(ToolsManager.GetExePath(folder, exe), args, ms);
        }

        private async Task<(int Code, string Out, string Err)> RunProcessAsync(string fileName, string args, int ms)
        {
            try
            {
                var psi = new ProcessStartInfo { FileName = fileName, Arguments = args, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                using var p = new Process { StartInfo = psi };
                var o = new StringBuilder();
                var er = new StringBuilder();
                p.OutputDataReceived += (_, e) => { if (e.Data != null) o.AppendLine(e.Data); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) er.AppendLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                using var cts = new CancellationTokenSource(ms);
                try { await p.WaitForExitAsync(cts.Token); }
                catch (OperationCanceledException) { try { p.Kill(true); } catch { } return (-1, o.ToString(), $"Timeout {ms / 1000}s"); }
                return (p.ExitCode, o.ToString(), er.ToString());
            }
            catch (Exception ex) { return (-1, "", ex.Message); }
        }

        private async void LoadApps_Click(object sender, RoutedEventArgs e)
        {
            _backupApps.Clear();
            AppendLog("Loading apps list...");
            var apps = await BackupService.GetInstalledAppsAsync(AppendLog);
            foreach (var a in apps)
            {
                string firstLetter = string.IsNullOrWhiteSpace(a.DisplayName) ? "?" : a.DisplayName.Substring(0, 1).ToUpperInvariant();
                _backupApps.Add(new BackupAppRow { PackageName = a.PackageName, DisplayName = a.DisplayName, IconLetter = firstLetter });
            }
            AppendLog($"Apps loaded: {_backupApps.Count}");
        }

        private void StartBackup_Click(object sender, RoutedEventArgs e) => AppendLog("Backup start requested.");
    }

    // تعريفات الكلاسات المساعدة يجب أن تكون موجودة (تم تضمينها أعلاه ضمن namespace)
    // ولكن بعضها قد يكون مفقوداً مثل ToolsManager, BackupService
    // سأضيف تعريفات وهمية لتجنب أخطاء أخرى، لكن الأفضل أن تكون موجودة في ملفات منفصلة.

    public static class ToolsManager
    {
        public static bool ExeExists(string folder, string exe) => File.Exists(GetExePath(folder, exe));
        public static string GetExePath(string folder, string exe) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder, exe);
    }

    public static class BackupService
    {
        public static async Task<List<BackupAppInfo>> GetInstalledAppsAsync(Action<string> log)
        {
            // محاكاة
            await Task.Delay(100);
            return new List<BackupAppInfo> { new BackupAppInfo { PackageName = "com.example.app", DisplayName = "Example App" } };
        }
    }

    public class BackupAppInfo
    {
        public string PackageName { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }
}
