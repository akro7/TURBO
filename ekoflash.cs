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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MKVenomTool
{
    public partial class MainWindow : Window
    {
        private enum FlashMode
        {
            Fastboot,
            Odin,
            Sideload,
            Tools,
            Backup
        }

        private static readonly Dictionary<string, string> OdinArgMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["BL"] = "-b",
            ["AP"] = "-a",
            ["CP"] = "-c",
            ["CSC"] = "-s",
            ["USERDATA"] = "-u"
        };

        private static readonly string[] OdinPids = { "6601", "685D", "68C3", "6860" };

        private FlashMode _mode = FlashMode.Fastboot;
        private readonly ObservableCollection<FlashRow> _fbRows = new();
        private readonly ObservableCollection<FlashRow> _odinRows = new();

        private bool _deviceConnected;
        private bool _deviceChecked;
        private CancellationTokenSource? _cts;

        private CancellationTokenSource? _backupCts;
        private string _backupTab = "backup";
        private bool _materialDesignEnabled = false;

        public MainWindow()
        {
            InitializeComponent();

            ApplyTheme("Blue");
            BuildFastbootRows();
            BuildOdinRows();

            RowsList.ItemsSource = _fbRows;
            OdinRowsList.ItemsSource = _odinRows;

            SwitchMode(FlashMode.Fastboot);
            ShowTab("cmd");

            AppendLog("===============================================");
            AppendLog(" TURBO FLASH TOOL v1.0 READY");
            AppendLog(" Developed by: AHMED YOUNIS & Mohamed Khaled");
            AppendLog("===============================================");

            CheckRequirements();
            UpdateCommandPreview();
        }

        private void CheckRequirements()
        {
            AppendLog($"[SYSTEM] ADB : {Chk("platform-tools", "adb")}");
            AppendLog($"[SYSTEM] Fastboot : {Chk("platform-tools", "fastboot")}");
            AppendLog($"[SYSTEM] ekoflash : {Chk("odin", "ekoflash")}");
        }

        private static string Chk(string dir, string exe) =>
            ToolsManager.ExeExists(dir, exe) ? "OK" : "MISSING";

        private void Swatch_Click(object s, RoutedEventArgs e)
        {
            if (s is Button b && b.Tag is string name)
            {
                ApplyTheme(name);
                AppendLog($"[THEME] Switched to {name}");
            }
        }

        private void ApplyTheme(string theme)
        {
            var map = new Dictionary<string, (Color Ac, Color AS, Color Bo, Color Pa)>
            {
                ["Blue"] =
                (
                    Color.FromRgb(0x00, 0xE5, 0xFF),
                    Color.FromArgb(0x33, 0x00, 0xE5, 0xFF),
                    Color.FromArgb(0x4C, 0x52, 0xBF, 0xFF),
                    Color.FromArgb(0xCC, 0x0D, 0x1B, 0x33)
                ),
                ["Purple"] =
                (
                    Color.FromRgb(0xB6, 0x7B, 0xFF),
                    Color.FromArgb(0x33, 0xB6, 0x7B, 0xFF),
                    Color.FromArgb(0x4C, 0xB1, 0x86, 0xFF),
                    Color.FromArgb(0xCC, 0x17, 0x12, 0x3A)
                ),
                ["Crimson"] =
                (
                    Color.FromRgb(0xFF, 0x00, 0x55),
                    Color.FromArgb(0x33, 0xFF, 0x00, 0x55),
                    Color.FromArgb(0x4C, 0xFF, 0x91, 0xC2),
                    Color.FromArgb(0xCC, 0x20, 0x0E, 0x24)
                )
            };

            if (!map.TryGetValue(theme, out var c))
                return;

            Resources["AccentBrush"] = new SolidColorBrush(c.Ac);
            Resources["AccentSoftBrush"] = new SolidColorBrush(c.AS);
            Resources["BorderBrush"] = new SolidColorBrush(c.Bo);
            Resources["PanelBrush"] = new SolidColorBrush(c.Pa);

            SetModeButtonVisual();
        }

        private void ApplyMaterialDesignTheme()
        {
            // ✨ Material Design Theme - Flat, Modern, Clean
            var mtBg = Color.FromRgb(0xF5, 0xF5, 0xF5);
            var mtAccent = Color.FromRgb(0x2F, 0x84, 0xFF);
            var mtText = Color.FromRgb(0x21, 0x21, 0x21);
            var mtPanel = Color.FromRgb(0xFF, 0xFF, 0xFF);

            Resources["MainBgBrush"] = new SolidColorBrush(mtBg);
            Resources["AccentBrush"] = new SolidColorBrush(mtAccent);
            Resources["AccentSoftBrush"] = new SolidColorBrush(Color.FromArgb(0x44, 0x2F, 0x84, 0xFF));
            Resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            Resources["PanelBrush"] = new SolidColorBrush(mtPanel);
            Resources["FgBright"] = new SolidColorBrush(mtText);
            Resources["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75));

            // تحديث الخلفيات والألوان
            Background = new SolidColorBrush(mtBg);
            Foreground = new SolidColorBrush(mtText);

            AppendLog("[DESIGN] Material Design Theme Applied ✨");
            SetModeButtonVisual();
        }

        private void ApplyNeonTheme()
        {
            // استعادة الثيم النيون الأصلي
            ApplyTheme("Blue");
            AppendLog("[DESIGN] Neon Theme Restored 🌌");
        }

        private void MaterialDesign_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk)
            {
                _materialDesignEnabled = chk.IsChecked == true;

                if (_materialDesignEnabled)
                    ApplyMaterialDesignTheme();
                else
                    ApplyNeonTheme();
            }
        }

        private void ShowTab(string tab)
        {
            TabCmdPanel.Visibility = tab == "cmd" ? Visibility.Visible : Visibility.Collapsed;
            TabOptionsPanel.Visibility = tab == "options" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TabCmd_Click(object s, RoutedEventArgs e) => ShowTab("cmd");
        private void TabOptions_Click(object s, RoutedEventArgs e) => ShowTab("options");

        private void SwitchMode(FlashMode mode)
        {
            _mode = mode;
            _deviceChecked = false;
            _deviceConnected = false;

            PanelFastboot.Visibility = mode == FlashMode.Fastboot ? Visibility.Visible : Visibility.Collapsed;
            PanelOdin.Visibility = mode == FlashMode.Odin ? Visibility.Visible : Visibility.Collapsed;
            PanelSideload.Visibility = mode == FlashMode.Sideload ? Visibility.Visible : Visibility.Collapsed;
            PanelTools.Visibility = mode == FlashMode.Tools ? Visibility.Visible : Visibility.Collapsed;
            PanelBackup.Visibility = mode == FlashMode.Backup ? Visibility.Visible : Visibility.Collapsed;

            DeviceStatusText.Text = "NOT CHECKED";
            DeviceStatusText.Foreground = Brushes.Gray;

            SetModeButtonVisual();
            UpdateCommandPreview();

            AppendLog($"[MODE] -> {mode.ToString().ToUpperInvariant()}");
        }

        private void FastbootMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Fastboot);
        private void OdinMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Odin);
        private void SideloadMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Sideload);
        private void ToolsMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Tools);
        private void BackupMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Backup);

        private void SetModeButtonVisual()
        {
            var active = (Brush)Resources["AccentSoftBrush"];
            var inactive = new SolidColorBrush(Color.FromArgb(0x15, 0x20, 0x33, 0x00));

            FastbootBtn.Background = _mode == FlashMode.Fastboot ? active : inactive;
            OdinBtn.Background = _mode == FlashMode.Odin ? active : inactive;
            SideloadBtn.Background = _mode == FlashMode.Sideload ? active : inactive;
            ToolsBtn.Background = _mode == FlashMode.Tools ? active : inactive;
            BackupBtn.Background = _mode == FlashMode.Backup ? active : inactive;
        }

        private void BuildFastbootRows()
        {
            _fbRows.Clear();
            foreach (var p in new[] { "boot", "recovery", "system", "vendor", "product", "vbmeta", "userdata" })
            {
                _fbRows.Add(new FlashRow { Key = p, Label = p.ToUpperInvariant() });
            }

            foreach (var r in _fbRows)
            {
                r.PropertyChanged += (_, _) => UpdateCommandPreview();
            }
        }

        private void BuildOdinRows()
        {
            _odinRows.Clear();
            foreach (var s in new[] { "BL", "AP", "CP", "CSC", "USERDATA" })
            {
                _odinRows.Add(new FlashRow { Key = s, Label = s });
            }

            foreach (var r in _odinRows)
            {
                r.PropertyChanged += (_, _) => UpdateCommandPreview();
            }
        }

        private async void DetectDevice_Click(object s, RoutedEventArgs e)
        {
            DeviceStatusText.Text = "SCANNING...";
            DeviceStatusText.Foreground = (Brush)Resources["AccentBrush"];

            bool found = _mode switch
            {
                FlashMode.Fastboot => await DetectFastbootAsync(),
                FlashMode.Odin => await DetectOdinDownloadModeAsync(),
                FlashMode.Sideload => await DetectAdbAsync(allowSideload: true),
                FlashMode.Backup => await DetectAdbAsync(allowSideload: false),
                _ => await DetectAdbAsync(allowSideload: false)
            };

            _deviceConnected = found;
            _deviceChecked = true;

            if (found)
            {
                DeviceStatusText.Text = "CONNECTED";
                DeviceStatusText.Foreground = Brushes.LimeGreen;
                AppendLog("[OK] Device detected.");
            }
            else
            {
                DeviceStatusText.Text = "NOT FOUND";
                DeviceStatusText.Foreground = Brushes.Red;

                AppendLog(_mode == FlashMode.Odin
                    ? "[ERR] No download-mode device found. Check Samsung driver / cable / USB port."
                    : "[ERR] No device found. Check cable / drivers.");
            }
        }

        private async Task<bool> DetectFastbootAsync()
        {
            var result = await RunAsync("platform-tools", "fastboot", "devices");
            return SplitLines(result.Out).Any(IsToolDeviceLine);
        }

        private async Task<bool> DetectAdbAsync(bool allowSideload)
        {
            var result = await RunAsync("platform-tools", "adb", "devices");
            var lines = SplitLines(result.Out);

            foreach (var line in lines)
            {
                if (!IsToolDeviceLine(line))
                    continue;

                var state = GetStateFromToolLine(line).ToLowerInvariant();

                if (state == "device" || state == "recovery" || state == "unauthorized")
                    return true;

                if (allowSideload && state == "sideload")
                    return true;
            }

            return false;
        }

        private async Task<bool> DetectOdinDownloadModeAsync()
        {
            for (int i = 0; i < 2; i++)
            {
                var listResult = await RunAsync("odin", "ekoflash", "--list");
                var listText = $"{listResult.Out}\n{listResult.Err}";

                if (LooksLikeOdinDeviceFound(listText))
                    return true;

                if (listResult.Code == 0 && !LooksLikeNoOdinDevice(listText))
                    return true;

                await Task.Delay(180);
            }

            for (int i = 0; i < 2; i++)
            {
                var detectResult = await RunAsync("odin", "ekoflash", "detect");
                var detectText = $"{detectResult.Out}\n{detectResult.Err}";

                if (LooksLikeOdinDeviceFound(detectText))
                    return true;

                if (detectResult.Code == 0 && !LooksLikeNoOdinDevice(detectText))
                    return true;

                await Task.Delay(180);
            }

            return await DetectOdinByPnpUtilAsync();
        }

        private static bool LooksLikeOdinDeviceFound(string text)
        {
            var t = text.ToLowerInvariant();

            if (t.Contains("device detected")) return true;
            if (t.Contains("odin mode")) return true;
            if (t.Contains("connected devices")) return true;

            if (Regex.IsMatch(t, @"vid[_:\s=]*04e8.*pid[_:\s=]*(6601|685d|68c3|6860)", RegexOptions.IgnoreCase))
                return true;

            if (Regex.IsMatch(t, @"04e8[:\s](6601|685d|68c3|6860)", RegexOptions.IgnoreCase))
                return true;

            if (SplitLines(t).Any(IsToolDeviceLine))
                return true;

            return false;
        }

        private static bool LooksLikeNoOdinDevice(string text)
        {
            var t = text.ToLowerInvariant();

            if (t.Contains("no connected devices detected")) return true;
            if (t.Contains("none of the devices are in odin mode")) return true;
            if (t.Contains("failed to detect compatible download-mode device")) return true;
            if (t.Contains("no download-mode device found")) return true;
            if (t.Contains("no device")) return true;

            return false;
        }

        private async Task<bool> DetectOdinByPnpUtilAsync()
        {
            var res = await RunAsync("", "cmd", "/c pnputil /enum-devices /connected");
            var txt = $"{res.Out}\n{res.Err}".ToUpperInvariant();

            if (!txt.Contains("VID_04E8"))
                return false;

            return OdinPids.Any(pid => txt.Contains($"PID_{pid}")) || txt.Contains("VID_04E8");
        }

        private static IEnumerable<string> SplitLines(string text) =>
            text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

        private static bool IsToolDeviceLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            var l = line.Trim();

            if (l.StartsWith("List of devices attached", StringComparison.OrdinalIgnoreCase)) return false;
            if (l.StartsWith("Usage:", StringComparison.OrdinalIgnoreCase)) return false;
            if (l.StartsWith("CLI options", StringComparison.OrdinalIgnoreCase)) return false;
            if (l.StartsWith("Notes:", StringComparison.OrdinalIgnoreCase)) return false;
            if (l.StartsWith("-", StringComparison.OrdinalIgnoreCase)) return false;

            var parts = l.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2;
        }

        private static string GetStateFromToolLine(string line)
        {
            var parts = line.Trim().Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? parts[^1] : string.Empty;
        }

        private void Browse_Click(object s, RoutedEventArgs e)
        {
            if (s is not Button btn)
                return;

            string key = btn.Tag?.ToString() ?? "";

            if (key == "sideload")
            {
                var dlg = new OpenFileDialog { Filter = "ZIP files|*.zip|All files|*.*" };
                if (dlg.ShowDialog() == true)
                    SideloadPathBox.Text = dlg.FileName;
                return;
            }

            var fbRow = _fbRows.FirstOrDefault(r => r.Key == key);
            if (fbRow != null)
            {
                var dlg = new OpenFileDialog { Filter = "Image files|*.img;*.bin|All files|*.*" };
                if (dlg.ShowDialog() == true)
                    fbRow.FilePath = dlg.FileName;
                return;
            }

            var odinRow = _odinRows.FirstOrDefault(r => r.Key == key);
            if (odinRow != null)
            {
                var dlg = new OpenFileDialog { Filter = "TAR/MD5 files|*.tar;*.md5;*.tar.md5|All files|*.*" };
                if (dlg.ShowDialog() == true)
                    odinRow.FilePath = dlg.FileName;
            }
        }

        private async void FlashAll_Click(object s, RoutedEventArgs e)
        {
            if (!_deviceChecked)
            {
                AppendLog("[!] Scan device first.");
                return;
            }

            if (!_deviceConnected)
            {
                AppendLog("[!] Device not connected.");
                return;
            }

            if (_mode == FlashMode.Backup)
            {
                await StartBackupFlowAsync();
                return;
            }

            _cts = new CancellationTokenSource();
            MainProgress.Value = 0;
            ProgressStatusText.Text = "Flashing...";

            try
            {
                bool success = _mode switch
                {
                    FlashMode.Fastboot => await FlashFastboot(_cts.Token),
                    FlashMode.Odin => await FlashOdin(_cts.Token),
                    FlashMode.Sideload => await FlashSideload(_cts.Token),
                    _ => false
                };

                if (success)
                {
                    AppendLog("[OK] All operations finished.");
                    ProgressStatusText.Text = "Done.";
                    MainProgress.Value = 100;
                }
                else
                {
                    AppendLog("[ERR] Flash failed.");
                    ProgressStatusText.Text = "Failed.";
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog("[STOP] Cancelled.");
                ProgressStatusText.Text = "Cancelled.";
            }
        }

        private async Task<bool> FlashFastboot(CancellationToken ct)
        {
            var targets = _fbRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).ToList();

            if (targets.Count == 0)
            {
                AppendLog("[!] No files selected.");
                return false;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var row = targets[i];
                if (!File.Exists(row.FilePath))
                {
                    AppendLog($"[ERR] File not found: {row.FilePath}");
                    return false;
                }

                AppendLog($"[FLASH] {row.Label} <- {row.FilePath}");

                var res = await RunAsync("platform-tools", "fastboot", $"flash {row.Key} \"{row.FilePath}\"", ct);

                if (!string.IsNullOrWhiteSpace(res.Out)) AppendLog(res.Out.Trim());
                if (!string.IsNullOrWhiteSpace(res.Err)) AppendLog($"[ERR] {res.Err.Trim()}");

                if (res.Code != 0)
                {
                    AppendLog($"[ERR] fastboot exit code: {res.Code}");
                    return false;
                }

                MainProgress.Value = (double)(i + 1) / targets.Count * 100;
            }

            return true;
        }

        private async Task<bool> FlashOdin(CancellationToken ct)
        {
            var targets = _odinRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).ToList();

            if (targets.Count == 0)
            {
                AppendLog("[!] No Odin files selected.");
                return false;
            }

            var sb = new StringBuilder();

            foreach (var row in targets)
            {
                if (!File.Exists(row.FilePath))
                {
                    AppendLog($"[ERR] File not found: {row.FilePath}");
                    return false;
                }

                if (!OdinArgMap.TryGetValue(row.Key, out var flag))
                {
                    AppendLog($"[ERR] Unsupported Odin slot: {row.Key}");
                    return false;
                }

                sb.Append(flag).Append(' ').Append('"').Append(row.FilePath).Append("\" ");
            }

            var args = sb.ToString().Trim();
            AppendLog($"[ODIN] ekoflash {args}");

            var res = await RunAsync("odin", "ekoflash", args, ct);

            if (!string.IsNullOrWhiteSpace(res.Out)) AppendLog(res.Out.Trim());
            if (!string.IsNullOrWhiteSpace(res.Err)) AppendLog($"[ERR] {res.Err.Trim()}");

            var combined = $"{res.Out}\n{res.Err}".ToLowerInvariant();

            if (combined.Contains("unknown argument") || combined.Contains("usage:"))
            {
                AppendLog("[ERR] ekoflash rejected arguments.");
                return false;
            }

            if (combined.Contains("no connected devices detected") ||
                combined.Contains("none of the devices are in odin mode") ||
                combined.Contains("not in odin mode"))
            {
                AppendLog("[ERR] Device dropped out of Odin mode.");
                return false;
            }

            if (res.Code != 0)
            {
                AppendLog($"[ERR] ekoflash exit code: {res.Code}");
                return false;
            }

            if (combined.Contains("error"))
            {
                AppendLog("[ERR] ekoflash reported an error.");
                return false;
            }

            MainProgress.Value = 100;
            return true;
        }

        private async Task<bool> FlashSideload(CancellationToken ct)
        {
            string zip = SideloadPathBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(zip))
            {
                AppendLog("[!] No ZIP selected.");
                return false;
            }

            if (!File.Exists(zip))
            {
                AppendLog("[!] File not found.");
                return false;
            }

            AppendLog($"[SIDELOAD] {zip}");

            var res = await RunAsync("platform-tools", "adb", $"sideload \"{zip}\"", ct);

            if (!string.IsNullOrWhiteSpace(res.Out)) AppendLog(res.Out.Trim());
            if (!string.IsNullOrWhiteSpace(res.Err)) AppendLog($"[ERR] {res.Err.Trim()}");

            if (res.Code != 0)
            {
                AppendLog($"[ERR] adb sideload exit code: {res.Code}");
                return false;
            }

            MainProgress.Value = 100;
            return true;
        }

        private async void FlashOne_Click(object s, RoutedEventArgs e)
        {
            if (!_deviceChecked)
            {
                AppendLog("[!] Scan device first.");
                return;
            }

            if (!_deviceConnected)
            {
                AppendLog("[!] Device not connected.");
                return;
            }

            if (s is not Button btn)
                return;

            string key = btn.Tag?.ToString() ?? "";
            var row = _fbRows.FirstOrDefault(r => r.Key == key);

            if (row == null || string.IsNullOrWhiteSpace(row.FilePath))
            {
                AppendLog($"[!] No file for {key}.");
                return;
            }

            if (!File.Exists(row.FilePath))
            {
                AppendLog($"[ERR] File not found: {row.FilePath}");
                return;
            }

            AppendLog($"[FLASH] {row.Label} <- {row.FilePath}");

            var res = await RunAsync("platform-tools", "fastboot", $"flash {row.Key} \"{row.FilePath}\"");

            if (!string.IsNullOrWhiteSpace(res.Out)) AppendLog(res.Out.Trim());
            if (!string.IsNullOrWhiteSpace(res.Err)) AppendLog($"[ERR] {res.Err.Trim()}");

            if (res.Code == 0)
                AppendLog("[OK] Partition flashed.");
            else
                AppendLog($"[ERR] fastboot exit code: {res.Code}");
        }

        private void Cancel_Click(object s, RoutedEventArgs e)
        {
            _cts?.Cancel();
            _backupCts?.Cancel();
            AppendLog("[STOP] Cancelled by user.");
        }

        private async void QuickCmd_Click(object s, RoutedEventArgs e)
        {
            if (s is not Button btn)
                return;

            string cmd = btn.Tag?.ToString() ?? "";

            if (cmd == "zadig")
            {
                string path = ToolsManager.GetExePath("zadig", "zadig");
                if (!File.Exists(path))
                    path = ToolsManager.GetExePath("tools", "zadig");

                if (File.Exists(path))
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                else
                    AppendLog("[!] zadig.exe not found in zadig\\ or tools\\");
                return;
            }

            string[] parts = cmd.Split(new[] { ' ' }, 2);
            string exe = parts[0];
            string args = parts.Length > 1 ? parts[1] : "";

            string dir = exe.Equals("ekoflash", StringComparison.OrdinalIgnoreCase) ? "odin" : "platform-tools";

            AppendLog($"[CMD] {cmd}");

            var res = await RunAsync(dir, exe, args);

            if (!string.IsNullOrWhiteSpace(res.Out)) AppendLog(res.Out.Trim());
            if (!string.IsNullOrWhiteSpace(res.Err)) AppendLog($"[ERR] {res.Err.Trim()}");
        }

        private void BackupSubTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn)
                return;

            _backupTab = btn.Tag?.ToString() ?? "backup";

            if (_backupTab == "backup")
            {
                BackupSubPanel.Visibility = Visibility.Visible;
                RestoreSubPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                BackupSubPanel.Visibility = Visibility.Collapsed;
                RestoreSubPanel.Visibility = Visibility.Visible;
                _ = LoadBackupListAsync();
            }
        }

        private async void RefreshApps_Click(object sender, RoutedEventArgs e)
        {
            await LoadInstalledAppsAsync();
        }

        private async Task LoadInstalledAppsAsync()
        {
            if (!_deviceConnected)
            {
                AppendLog("[!] Scan device first.");
                return;
            }

            AppendLog("[BACKUP] Checking root access...");
            bool rooted = await BackupService.CheckRootAsync(AppendLog);
            if (!rooted)
            {
                AppendLog("[ERR] Root not available. Backup requires root.");
                return;
            }

            AppendLog("[BACKUP] Loading installed apps...");
            var apps = await BackupService.GetInstalledAppsAsync(AppendLog);

            Dispatcher.Invoke(() =>
            {
                AppListBox.ItemsSource = apps;
                AppListHeader.Text = $"INSTALLED APPS ({apps.Count})";
            });

            AppendLog($"[OK] Loaded {apps.Count} app(s).");
        }

        private void SelectAllApps_Click(object sender, RoutedEventArgs e) => AppListBox.SelectAll();
        private void ClearApps_Click(object sender, RoutedEventArgs e) => AppListBox.UnselectAll();

        private async void StartBackup_Click(object sender, RoutedEventArgs e)
        {
            await StartBackupFlowAsync();
        }

        private async Task StartBackupFlowAsync()
        {
            if (!_deviceConnected)
            {
                AppendLog("[!] Scan device first.");
                return;
            }

            var selectedApps = AppListBox.SelectedItems.Cast<AppEntry>().ToList();
            if (selectedApps.Count == 0)
            {
                AppendLog("[!] Select at least one app.");
                return;
            }

            var options = new BackupOptions
            {
                BackupApk = ChkBackupApk.IsChecked == true,
                BackupData = ChkBackupData.IsChecked == true,
                BackupUserDe = ChkBackupUserDe.IsChecked == true,
                BackupObb = ChkBackupObb.IsChecked == true
            };

            _backupCts = new CancellationTokenSource();
            StartBackupBtn.IsEnabled = false;
            BackupProgress.Value = 0;
            BackupProgressText.Visibility = Visibility.Visible;

            int total = selectedApps.Count;
            int done = 0;
            int okCount = 0;

            AppendLog($"[BACKUP] Starting backup for {total} app(s)...");

            foreach (var app in selectedApps)
            {
                if (_backupCts.IsCancellationRequested)
                    break;

                BackupProgressText.Text = $"[{done + 1}/{total}] {app.DisplayName}";
                AppendLog($"[BACKUP] {app.DisplayName}");

                bool ok = await BackupService.BackupAppAsync(app.PackageName, app.DisplayName, options, AppendLog);
                if (ok) okCount++;

                done++;
                BackupProgress.Value = (done * 100.0) / total;
            }

            AppendLog($"[DONE] Backup finished: {okCount}/{total} succeeded.");
            BackupProgressText.Text = $"Done: {okCount}/{total}";
            StartBackupBtn.IsEnabled = true;
            _backupCts = null;
        }

        private void StopBackup_Click(object sender, RoutedEventArgs e)
        {
            _backupCts?.Cancel();
            AppendLog("[STOP] Backup cancelled.");
            StartBackupBtn.IsEnabled = true;
        }

        private async Task LoadBackupListAsync()
        {
            AppendLog("[RESTORE] Loading backup list...");
            var backups = await BackupService.GetBackupsAsync(AppendLog);

            BackupListGrid.ItemsSource = backups;
            BackupRootText.Text = $"Backup root: {BackupService.BackupRoot}";
            AppendLog($"[RESTORE] Found {backups.Count} backup(s).");
        }

        private async void RestoreEntry_Click(object sender, RoutedEventArgs e)
        {
            if (!_deviceConnected)
            {
                AppendLog("[!] Scan device first.");
                return;
            }

            if (sender is not Button btn)
                return;

            string backupPath = btn.Tag?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(backupPath) || !Directory.Exists(backupPath))
            {
                AppendLog("[ERR] Backup path not found.");
                return;
            }

            var confirm = MessageBox.Show(
                $"Restore from:\n{backupPath}\n\nThis may overwrite app data. Continue?",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            btn.IsEnabled = false;
            AppendLog($"[RESTORE] Restoring from: {backupPath}");

            bool ok = await BackupService.RestoreBackupAsync(backupPath, AppendLog);

            if (ok) AppendLog("[OK] Restore completed.");
            else AppendLog("[ERR] Restore failed.");

            btn.IsEnabled = true;
        }

        private void UpdateCommandPreview()
        {
            if (CommandPreviewBox == null)
                return;

            CommandPreviewBox.Text = _mode switch
            {
                FlashMode.Fastboot => BuildFastbootPreview(),
                FlashMode.Odin => BuildOdinPreview(),
                FlashMode.Sideload => $"adb sideload \"{SideloadPathBox?.Text}\"",
                FlashMode.Backup => "adb shell su -c \"pm list packages -3\"",
                _ => "Select a mode above."
            };
        }

        private string BuildFastbootPreview()
        {
            var lines = _fbRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath))
                               .Select(r => $"fastboot flash {r.Key} \"{r.FilePath}\"");
            return lines.Any() ? string.Join("\n", lines) : "fastboot flash";
        }

        private string BuildOdinPreview()
        {
            var parts = _odinRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath))
                                 .Select(r =>
                                 {
                                     var flag = OdinArgMap.TryGetValue(r.Key, out var f) ? f : "?";
                                     return $"{flag} \"{r.FilePath}\"";
                                 });

            return parts.Any() ? "ekoflash " + string.Join(" ", parts) : "ekoflash -b -a -c -s -u ...";
        }

        private void AppendLog(string msg) => Dispatcher.Invoke(() =>
        {
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            LogBox.ScrollToEnd();
            ProgressStatusText.Text = msg;
        });

        private Task<ProcessResult> RunAsync(string dir, string exe, string args, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                var res = new ProcessResult();

                try
                {
                    string path = !string.IsNullOrWhiteSpace(dir) ? ToolsManager.GetExePath(dir, exe) : exe;
                    if (!File.Exists(path)) path = exe;

                    var psi = new ProcessStartInfo(path, args)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var p = Process.Start(psi)!;
                    res.Out = p.StandardOutput.ReadToEnd();
                    res.Err = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    res.Code = p.ExitCode;
                }
                catch (Exception ex)
                {
                    res.Code = -1;
                    res.Err = ex.Message;
                }

                return res;
            }, ct);
        }
    }

    public class FlashRow : INotifyPropertyChanged
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        private string _fp = "";

        public string FilePath
        {
            get => _fp;
            set { _fp = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class ProcessResult
    {
        public int Code { get; set; }
        public string Out { get; set; } = "";
        public string Err { get; set; } = "";
    }
}
