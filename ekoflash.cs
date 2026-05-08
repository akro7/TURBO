using ICSharpCode.SharpZipLib.Tar;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MKVenomTool
{
    public partial class MainWindow : Window
    {
        private enum FlashMode { Fastboot, Odin, Sideload, Tools, BackupRestore }
        private enum OdinBackend { None, Heimdall, OdinDriverOnly }

        private FlashMode _mode = FlashMode.Fastboot;
        private OdinBackend _odinBackend = OdinBackend.None;

        private ObservableCollection<FlashRow> _fbRows = new();
        private ObservableCollection<FlashRow> _odinRows = new();

        private readonly ObservableCollection<BackupAppRow> _backupApps = new();
        private readonly ObservableCollection<BackupSetRow> _backupSets = new();

        private string _pitFilePath = "";
        private bool _deviceChecked, _deviceConnected, _uiReady;

        public MainWindow()
        {
            InitializeComponent();
            _uiReady = true;

            ApplyTheme("Blue");
            BuildFastbootRows();
            BuildOdinRows();
            SwitchMode(FlashMode.Fastboot);
            ShowTab("cmd");

            var appsList = GetBackupAppsList();
            var setsList = GetBackupSetsList();
            if (appsList != null) appsList.ItemsSource = _backupApps;
            if (setsList != null) setsList.ItemsSource = _backupSets;

            AppendLog("MK Venom Tool ready.");
            AppendLog($"platform-tools (adb)      : {(ToolsManager.ExeExists("platform-tools", "adb") ? "✓ ready" : "✗ missing")}");
            AppendLog($"platform-tools (fastboot) : {(ToolsManager.ExeExists("platform-tools", "fastboot") ? "✓ ready" : "✗ missing")}");
            AppendLog($"heimdall                  : {(ToolsManager.ExeExists("heimdall", "heimdall") ? "✓ ready" : "✗ missing")}");
            AppendLog($"zadig                     : {(ToolsManager.ExeExists("zadig", "zadig") ? "✓ ready" : "✗ missing")}");
        }

        // Optional controls (so build passes even if backup UI not in XAML yet)
        private ListView? GetBackupAppsList() =>
            FindName("BackupAppsList") as ListView ?? FindName("AppsListView") as ListView;

        private ListView? GetBackupSetsList() =>
            FindName("BackupSetsList") as ListView ?? FindName("BackupsListView") as ListView;

        private CheckBox? GetBkApkOpt() =>
            FindName("BkOptApk") as CheckBox ?? FindName("BackupApkCheck") as CheckBox;

        private CheckBox? GetBkDataOpt() =>
            FindName("BkOptData") as CheckBox ?? FindName("BackupDataCheck") as CheckBox;

        private CheckBox? GetBkUserDeOpt() =>
            FindName("BkOptUserDe") as CheckBox ?? FindName("BackupUserDeCheck") as CheckBox;

        private CheckBox? GetBkObbOpt() =>
            FindName("BkOptObb") as CheckBox ?? FindName("BackupObbCheck") as CheckBox;

        private Grid? GetBackupPanel() => FindName("PanelBackupRestore") as Grid;
        private Button? GetBackupModeButton() => FindName("BackupRestoreBtn") as Button;

        // Theme
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
                ["Blue"]    =(Color.FromRgb(0x37,0xCF,0xFF),Color.FromArgb(0x7A,0x35,0xCF,0xFF),Color.FromArgb(0x4C,0x52,0xBF,0xFF),Color.FromArgb(0x3A,0x0D,0x1B,0x33),Color.FromArgb(0x5A,0x1C,0xE1,0x7A),Color.FromArgb(0x5A,0xFF,0x4C,0x78)),
                ["Purple"]  =(Color.FromRgb(0xB6,0x7B,0xFF),Color.FromArgb(0x80,0xA1,0x4D,0xFF),Color.FromArgb(0x4C,0xB1,0x86,0xFF),Color.FromArgb(0x3A,0x17,0x12,0x3A),Color.FromArgb(0x5A,0x38,0xD9,0x9E),Color.FromArgb(0x5A,0xFF,0x5E,0x98)),
                ["Emerald"] =(Color.FromRgb(0x43,0xF2,0xC2),Color.FromArgb(0x7A,0x16,0xC4,0x98),Color.FromArgb(0x4C,0x5F,0xE4,0xC2),Color.FromArgb(0x3A,0x0A,0x1C,0x1A),Color.FromArgb(0x5A,0x27,0xE8,0x9D),Color.FromArgb(0x5A,0xFF,0x6C,0x85)),
                ["Crimson"] =(Color.FromRgb(0xFF,0x6D,0xA8),Color.FromArgb(0x7A,0xFF,0x5B,0x93),Color.FromArgb(0x4C,0xFF,0x91,0xC2),Color.FromArgb(0x3A,0x20,0x0E,0x24),Color.FromArgb(0x5A,0x38,0xE0,0x9A),Color.FromArgb(0x5A,0xFF,0x4A,0x72)),
                ["Gold"]    =(Color.FromRgb(0xFF,0xB8,0x30),Color.FromArgb(0x7A,0xFF,0xA0,0x20),Color.FromArgb(0x55,0xFF,0xC0,0x50),Color.FromArgb(0x38,0x1E,0x14,0x05),Color.FromArgb(0x5A,0x1C,0xE1,0x7A),Color.FromArgb(0x5A,0xFF,0x4C,0x78)),
            };
            if (!T.TryGetValue(theme, out var t)) return;

            Resources["AccentBrush"] = new SolidColorBrush(t.Ac);
            Resources["AccentSoftBrush"] = new SolidColorBrush(t.AS);
            Resources["BorderBrush"] = new SolidColorBrush(t.Bo);
            Resources["PanelBrush"] = new SolidColorBrush(t.Pa);
            Resources["SuccessBrush"] = new SolidColorBrush(t.Su);
            Resources["DangerBrush"] = new SolidColorBrush(t.Da);

            var map = new Dictionary<string, Button?>
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

        // Tabs
        private void TabCmd_Click(object s, RoutedEventArgs e) => ShowTab("cmd");
        private void TabOptions_Click(object s, RoutedEventArgs e) => ShowTab("options");

        private void ShowTab(string tab)
        {
            if (TabCmdPanel == null) return;
            TabCmdPanel.Visibility = tab == "cmd" ? Visibility.Visible : Visibility.Collapsed;
            TabOptionsPanel.Visibility = tab == "options" ? Visibility.Visible : Visibility.Collapsed;

            var accent = (Brush)Resources["AccentBrush"];
            var muted = (Brush)Resources["TextMutedBrush"];
            TabCmdBtn.Foreground = tab == "cmd" ? accent : muted;
            TabOptionsBtn.Foreground = tab == "options" ? accent : muted;
            TabCmdBtn.BorderBrush = tab == "cmd" ? accent : Brushes.Transparent;
            TabOptionsBtn.BorderBrush = tab == "options" ? accent : Brushes.Transparent;
        }

        // Modes
        private void FastbootMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Fastboot);
        private void OdinMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Odin);
        private void SideloadMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Sideload);
        private void ToolsMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Tools);
        private void BackupRestoreMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.BackupRestore);

        private void SwitchMode(FlashMode mode)
        {
            _mode = mode;
            _odinBackend = OdinBackend.None;
            _deviceChecked = false;
            _deviceConnected = false;
            SetModeButtonVisual();

            if (_uiReady)
            {
                PanelFastboot.Visibility = mode == FlashMode.Fastboot ? Visibility.Visible : Visibility.Collapsed;
                PanelOdin.Visibility = mode == FlashMode.Odin ? Visibility.Visible : Visibility.Collapsed;
                PanelSideload.Visibility = mode == FlashMode.Sideload ? Visibility.Visible : Visibility.Collapsed;
                PanelTools.Visibility = mode == FlashMode.Tools ? Visibility.Visible : Visibility.Collapsed;

                var backupPanel = GetBackupPanel();
                if (backupPanel != null)
                    backupPanel.Visibility = mode == FlashMode.BackupRestore ? Visibility.Visible : Visibility.Collapsed;

                DeviceStatusText.Text = "Not checked";
                DeviceStatusText.Foreground = (Brush)Resources["WarningBrush"];
            }

            UpdateCommandPreview();
            AppendLog($"Mode → {mode}.");
        }

        private void SetModeButtonVisual()
        {
            var on = (Brush)Resources["AccentSoftBrush"];
            var off = new SolidColorBrush(Color.FromArgb(0x2D, 0x18, 0x25, 0x3D));
            FastbootBtn.Background = _mode == FlashMode.Fastboot ? on : off;
            OdinBtn.Background = _mode == FlashMode.Odin ? on : off;
            SideloadBtn.Background = _mode == FlashMode.Sideload ? on : off;
            ToolsBtn.Background = _mode == FlashMode.Tools ? on : off;

            var backupBtn = GetBackupModeButton();
            if (backupBtn != null)
                backupBtn.Background = _mode == FlashMode.BackupRestore ? on : off;
        }

        private void BuildFastbootRows()
        {
            _fbRows = new ObservableCollection<FlashRow>();
            foreach (var e in new[] { ("boot", "BOOT"), ("recovery", "RECOVERY"), ("system", "SYSTEM"), ("vendor", "VENDOR"), ("product", "PRODUCT"), ("vbmeta", "VBMETA"), ("vendor_boot", "VENDOR_BOOT"), ("userdata", "USERDATA") })
                _fbRows.Add(new FlashRow { Key = e.Item1, Label = e.Item2 });

            foreach (var r in _fbRows)
                r.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FlashRow.FilePath)) UpdateCommandPreview(); };

            RowsList.ItemsSource = _fbRows;
        }

        private void BuildOdinRows()
        {
            _odinRows = new ObservableCollection<FlashRow>();
            foreach (var e in new[] { ("BL", "BL"), ("AP", "AP"), ("CP", "CP"), ("CSC", "CSC"), ("USERDATA", "USERDATA") })
                _odinRows.Add(new FlashRow { Key = e.Item1, Label = e.Item2 });

            foreach (var r in _odinRows)
                r.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FlashRow.FilePath)) UpdateCommandPreview(); };

            OdinRowsList.ItemsSource = _odinRows;
        }

        // Browse
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _fbRows.FirstOrDefault(r => r.Key == key); if (row == null) return;
            var dlg = new OpenFileDialog { Filter = "Flash files (*.img;*.bin;*.zip)|*.img;*.bin;*.zip|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true) { row.FilePath = dlg.FileName; AppendLog($"FB {row.Label}: {row.FilePath}"); }
        }

        private void BrowseOdin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _odinRows.FirstOrDefault(r => r.Key == key); if (row == null) return;
            var dlg = new OpenFileDialog { Filter = "Flash files (*.tar;*.md5;*.img;*.bin)|*.tar;*.md5;*.img;*.bin|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true) { row.FilePath = dlg.FileName; AppendLog($"Odin {row.Label}: {row.FilePath}"); }
        }

        private void BrowsePit_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PIT files (*.pit)|*.pit|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true) { _pitFilePath = dlg.FileName; PitPathBox.Text = _pitFilePath; AppendLog($"PIT: {_pitFilePath}"); UpdateCommandPreview(); }
        }

        private void ClearPit_Click(object s, RoutedEventArgs e)
        {
            _pitFilePath = "";
            PitPathBox.Text = "";
            AppendLog("PIT cleared.");
            UpdateCommandPreview();
        }

        private void BrowseSideload_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true) { SideloadPathBox.Text = dlg.FileName; AppendLog($"Sideload: {dlg.FileName}"); UpdateCommandPreview(); }
        }

        private void BrowseApk_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "APK files (*.apk)|*.apk|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true) { ApkPathBox.Text = dlg.FileName; AppendLog($"APK: {dlg.FileName}"); }
        }

        // Detect
        private async void DetectDevice_Click(object s, RoutedEventArgs e)
        {
            DeviceStatusText.Text = "Checking...";
            DeviceStatusText.Foreground = (Brush)Resources["WarningBrush"];
            var r = await DetectAsync();
            _deviceChecked = true; _deviceConnected = r.ok;
            DeviceStatusText.Text = r.text;
            DeviceStatusText.Foreground = r.ok ? new SolidColorBrush(Color.FromRgb(77, 255, 154)) : new SolidColorBrush(Color.FromRgb(255, 122, 122));
            foreach (var line in r.log.Split('\n')) if (!string.IsNullOrWhiteSpace(line)) AppendLog(line.Trim());
        }

        private async Task<(bool ok, string text, string log)> DetectAsync()
        {
            if (_mode == FlashMode.Fastboot || _mode == FlashMode.Tools)
            {
                var r = await RunAsync("platform-tools", "fastboot", "devices", 12000);
                bool ok = (r.Out + r.Err).ToLower().Contains("fastboot");
                return ok ? (true, "FASTBOOT CONNECTED", "Fastboot device detected.") : (false, "NO FASTBOOT DEVICE", "No fastboot device found.");
            }

            if (_mode == FlashMode.Sideload || _mode == FlashMode.BackupRestore)
            {
                var r = await RunAsync("platform-tools", "adb", "devices", 12000);
                var m = (r.Out + r.Err).ToLower();
                bool ok = m.Contains("\tdevice") || m.Contains("\tsideload");
                return ok ? (true, "ADB CONNECTED", "ADB device detected.") : (false, "NO ADB DEVICE", "No ADB device found.");
            }

            bool hasH = ToolsManager.ExeExists("heimdall", "heimdall");
            if (hasH)
            {
                var r = await RunAsync("heimdall", "heimdall", "detect", 15000);
                var full = (r.Out + r.Err).ToLower();
                if (full.Contains("device detected")) { _odinBackend = OdinBackend.Heimdall; return (true, "DOWNLOAD MODE (HEIMDALL)", "Heimdall: device detected in download mode."); }

                bool driverErr = full.Contains("libusb error") || full.Contains("failed to access") || full.Contains("no device");
                bool samUsb = await SamsungUsbPresentAsync();
                if (samUsb || driverErr)
                {
                    _odinBackend = OdinBackend.OdinDriverOnly;
                    return (true, "DOWNLOAD MODE (ODIN DRIVER)",
                        "Samsung USB found but Heimdall cannot open device (libusb error -5).\nGo to ADB/TOOLS → Launch Zadig → install WinUSB driver → re-detect.");
                }

                return (false, "NO DOWNLOAD DEVICE", "Heimdall: no device found in download mode.");
            }

            bool sam = await SamsungUsbPresentAsync();
            if (sam) { _odinBackend = OdinBackend.OdinDriverOnly; return (true, "DOWNLOAD MODE (ODIN DRIVER)", "Samsung USB detected. Heimdall NOT installed."); }
            return (false, "NO DOWNLOAD DEVICE", "No download-mode device detected.");
        }

        private async Task<bool> SamsungUsbPresentAsync()
        {
            var r = await RunProcessAsync("pnputil.exe", "/enum-devices /connected", 12000);
            return (r.Out + r.Err).ToUpper().Contains("VID_04E8");
        }

        // Flash handlers
        private async void FlashOne_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _fbRows.FirstOrDefault(r => r.Key == key);
            if (row == null || string.IsNullOrWhiteSpace(row.FilePath)) { AppendLog($"No file for {key}."); return; }
            await FlashFastbootAsync(new List<FlashRow> { row });
        }

        private async void FlashOneOdin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _odinRows.FirstOrDefault(r => r.Key == key);
            if (row == null || string.IsNullOrWhiteSpace(row.FilePath)) { AppendLog($"No file for {key}."); return; }
            await FlashOdinAsync(new List<FlashRow> { row });
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
            if (!_deviceChecked) { AppendLog("Detect device first."); return; }
            if (!_deviceConnected) { AppendLog("Device not connected."); return; }

            int total = rows.Count;
            int i = 0;
            foreach (var row in rows)
            {
                i++;
                string args = $"flash {row.Key.ToLower()} \"{row.FilePath}\"";
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
            if (AutoRebootCheck.IsChecked == true)
            {
                await RunAsync("platform-tools", "fastboot", "reboot", 15000);
                AppendLog("Rebooted.");
            }
        }

        private async Task FlashOdinAsync(List<FlashRow> rows)
        {
            if (!_deviceChecked) { AppendLog("Detect device first."); return; }
            if (!_deviceConnected) { AppendLog("Device not connected."); return; }

            if (_odinBackend == OdinBackend.OdinDriverOnly)
            {
                AppendLog("══════════════════════════════════════════");
                AppendLog("  libusb error -5 — WinUSB driver needed");
                AppendLog("══════════════════════════════════════════");
                AppendLog("  1. Go to ADB/TOOLS tab → Launch Zadig");
                AppendLog(            {
                if (kv.Value == null) continue;
                kv.Value.BorderThickness = kv.Key == theme ? new Thickness(3) : new Thickness(1.5);
                kv.Value.Opacity = kv.Key == theme ? 1.0 : 0.6;
            }

            SetModeButtonVisual();
        }

        // Tabs
        private void TabCmd_Click(object s, RoutedEventArgs e) => ShowTab("cmd");
        private void TabOptions_Click(object s, RoutedEventArgs e) => ShowTab("options");

        private void ShowTab(string tab)
        {
            if (TabCmdPanel == null) return;
            TabCmdPanel.Visibility = tab == "cmd" ? Visibility.Visible : Visibility.Collapsed;
            TabOptionsPanel.Visibility = tab == "options" ? Visibility.Visible : Visibility.Collapsed;

            var accent = (Brush)Resources["AccentBrush"];
            var muted = (Brush)Resources["TextMutedBrush"];
            TabCmdBtn.Foreground = tab == "cmd" ? accent : muted;
            TabOptionsBtn.Foreground = tab == "options" ? accent : muted;
            TabCmdBtn.BorderBrush = tab == "cmd" ? accent : Brushes.Transparent;
            TabOptionsBtn.BorderBrush = tab == "options" ? accent : Brushes.Transparent;
        }

        // Modes
        private void FastbootMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Fastboot);
        private void OdinMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Odin);
        private void SideloadMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Sideload);
        private void ToolsMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Tools);
        private void BackupRestoreMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.BackupRestore);

        private void SwitchMode(FlashMode mode)
        {
            _mode = mode;
            _odinBackend = OdinBackend.None;
            _deviceChecked = false;
            _deviceConnected = false;
            SetModeButtonVisual();

            if (_uiReady)
            {
                PanelFastboot.Visibility = mode == FlashMode.Fastboot ? Visibility.Visible : Visibility.Collapsed;
                PanelOdin.Visibility = mode == FlashMode.Odin ? Visibility.Visible : Visibility.Collapsed;
                PanelSideload.Visibility = mode == FlashMode.Sideload ? Visibility.Visible : Visibility.Collapsed;
                PanelTools.Visibility = mode == FlashMode.Tools ? Visibility.Visible : Visibility.Collapsed;

                var backupPanel = GetBackupPanel();
                if (backupPanel != null)
                    backupPanel.Visibility = mode == FlashMode.BackupRestore ? Visibility.Visible : Visibility.Collapsed;

                DeviceStatusText.Text = "Not checked";
                DeviceStatusText.Foreground = (Brush)Resources["WarningBrush"];
            }

            UpdateCommandPreview();
            AppendLog($"Mode → {mode}.");
        }

        private void SetModeButtonVisual()
        {
            var on = (Brush)Resources["AccentSoftBrush"];
            var off = new SolidColorBrush(Color.FromArgb(0x2D, 0x18, 0x25, 0x3D));
            FastbootBtn.Background = _mode == FlashMode.Fastboot ? on : off;
            OdinBtn.Background = _mode == FlashMode.Odin ? on : off;
            SideloadBtn.Background = _mode == FlashMode.Sideload ? on : off;
            ToolsBtn.Background = _mode == FlashMode.Tools ? on : off;

            var backupBtn = GetBackupModeButton();
            if (backupBtn != null)
                backupBtn.Background = _mode == FlashMode.BackupRestore ? on : off;
        }

        private void BuildFastbootRows()
        {
            _fbRows = new ObservableCollection<FlashRow>();
            foreach (var e in new[] { ("boot", "BOOT"), ("recovery", "RECOVERY"), ("system", "SYSTEM"), ("vendor", "VENDOR"), ("product", "PRODUCT"), ("vbmeta", "VBMETA"), ("vendor_boot", "VENDOR_BOOT"), ("userdata", "USERDATA") })
                _fbRows.Add(new FlashRow { Key = e.Item1, Label = e.Item2 });

            foreach (var r in _fbRows)
                r.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FlashRow.FilePath)) UpdateCommandPreview(); };

            RowsList.ItemsSource = _fbRows;
        }

        private void BuildOdinRows()
        {
            _odinRows = new ObservableCollection<FlashRow>();
            foreach (var e in new[] { ("BL", "BL"), ("AP", "AP"), ("CP", "CP"), ("CSC", "CSC"), ("USERDATA", "USERDATA") })
                _odinRows.Add(new FlashRow { Key = e.Item1, Label = e.Item2 });

            foreach (var r in _odinRows)
                r.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FlashRow.FilePath)) UpdateCommandPreview(); };

            OdinRowsList.ItemsSource = _odinRows;
        }

        // Browse
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _fbRows.FirstOrDefault(r => r.Key == key); if (row == null) return;
            var dlg = new OpenFileDialog { Filter = "Flash files (*.img;*.bin;*.zip)|*.img;*.bin;*.zip|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true) { row.FilePath = dlg.FileName; AppendLog($"FB {row.Label}: {row.FilePath}"); }
        }

        private void BrowseOdin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _odinRows.FirstOrDefault(r => r.Key == key); if (row == null) return;
            var dlg = new OpenFileDialog { Filter = "Flash files (*.tar;*.md5;*.img;*.bin)|*.tar;*.md5;*.img;*.bin|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true) { row.FilePath = dlg.FileName; AppendLog($"Odin {row.Label}: {row.FilePath}"); }
        }

        private void BrowsePit_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PIT files (*.pit)|*.pit|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true) { _pitFilePath = dlg.FileName; PitPathBox.Text = _pitFilePath; AppendLog($"PIT: {_pitFilePath}"); UpdateCommandPreview(); }
        }

        private void ClearPit_Click(object s, RoutedEventArgs e)
        {
            _pitFilePath = "";
            PitPathBox.Text = "";
            AppendLog("PIT cleared.");
            UpdateCommandPreview();
        }

        private void BrowseSideload_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true) { SideloadPathBox.Text = dlg.FileName; AppendLog($"Sideload: {dlg.FileName}"); UpdateCommandPreview(); }
        }

        private void BrowseApk_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "APK files (*.apk)|*.apk|All files (*.*)|*.*", CheckFileExists = true };
            if (dlg.ShowDialog() == true) { ApkPathBox.Text = dlg.FileName; AppendLog($"APK: {dlg.FileName}"); }
        }

        // Detect
        private async void DetectDevice_Click(object s, RoutedEventArgs e)
        {
            DeviceStatusText.Text = "Checking...";
            DeviceStatusText.Foreground = (Brush)Resources["WarningBrush"];
            var r = await DetectAsync();
            _deviceChecked = true; _deviceConnected = r.ok;
            DeviceStatusText.Text = r.text;
            DeviceStatusText.Foreground = r.ok ? new SolidColorBrush(Color.FromRgb(77, 255, 154)) : new SolidColorBrush(Color.FromRgb(255, 122, 122));
            foreach (var line in r.log.Split('\n')) if (!string.IsNullOrWhiteSpace(line)) AppendLog(line.Trim());
        }

        private async Task<(bool ok, string text, string log)> DetectAsync()
        {
            if (_mode == FlashMode.Fastboot || _mode == FlashMode.Tools)
            {
                var r = await RunAsync("platform-tools", "fastboot", "devices", 12000);
                bool ok = (r.Out + r.Err).ToLower().Contains("fastboot");
                return ok ? (true, "FASTBOOT CONNECTED", "Fastboot device detected.") : (false, "NO FASTBOOT DEVICE", "No fastboot device found.");
            }

            if (_mode == FlashMode.Sideload || _mode == FlashMode.BackupRestore)
            {
                var r = await RunAsync("platform-tools", "adb", "devices", 12000);
                var m = (r.Out + r.Err).ToLower();
                bool ok = m.Contains("\tdevice") || m.Contains("\tsideload");
                return ok ? (true, "ADB CONNECTED", "ADB device detected.") : (false, "NO ADB DEVICE", "No ADB device found.");
            }

            bool hasH = ToolsManager.ExeExists("heimdall", "heimdall");
            if (hasH)
            {
                var r = await RunAsync("heimdall", "heimdall", "detect", 15000);
                var full = (r.Out + r.Err).ToLower();
                if (full.Contains("device detected")) { _odinBackend = OdinBackend.Heimdall; return (true, "DOWNLOAD MODE (HEIMDALL)", "Heimdall: device detected in download mode."); }

                bool driverErr = full.Contains("libusb error") || full.Contains("failed to access") || full.Contains("no device");
                bool samUsb = await SamsungUsbPresentAsync();
                if (samUsb || driverErr)
                {
                    _odinBackend = OdinBackend.OdinDriverOnly;
                    return (true, "DOWNLOAD MODE (ODIN DRIVER)",
                        "Samsung USB found but Heimdall cannot open device (libusb error -5).\nGo to ADB/TOOLS → Launch Zadig → install WinUSB driver → re-detect.");
                }

                return (false, "NO DOWNLOAD DEVICE", "Heimdall: no device found in download mode.");
            }

            bool sam = await SamsungUsbPresentAsync();
            if (sam) { _odinBackend = OdinBackend.OdinDriverOnly; return (true, "DOWNLOAD MODE (ODIN DRIVER)", "Samsung USB detected. Heimdall NOT installed."); }
            return (false, "NO DOWNLOAD DEVICE", "No download-mode device detected.");
        }

        private async Task<bool> SamsungUsbPresentAsync()
        {
            var r = await RunProcessAsync("pnputil.exe", "/enum-devices /connected", 12000);
            return (r.Out + r.Err).ToUpper().Contains("VID_04E8");
        }

        // Flash handlers
        private async void FlashOne_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _fbRows.FirstOrDefault(r => r.Key == key);
            if (row == null || string.IsNullOrWhiteSpace(row.FilePath)) { AppendLog($"No file for {key}."); return; }
            await FlashFastbootAsync(new List<FlashRow> { row });
        }

        private async void FlashOneOdin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _odinRows.FirstOrDefault(r => r.Key == key);
            if (row == null || string.IsNullOrWhiteSpace(row.FilePath)) { AppendLog($"No file for {key}."); return; }
            await FlashOdinAsync(new List<FlashRow> { row });
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
            if (!_deviceChecked) { AppendLog("Detect device first."); return; }
            if (!_deviceConnected) { AppendLog("Device not connected."); return; }

            int total = rows.Count;
            int i = 0;
            foreach (var row in rows)
            {
                i++;
                string args = $"flash {row.Key.ToLower()} \"{row.FilePath}\"";
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
            if (AutoRebootCheck.IsChecked == true)
            {
                await RunAsync("platform-tools", "fastboot", "reboot", 15000);
                AppendLog("Rebooted.");
            }
        }

        private async Task FlashOdinAsync(List<FlashRow> rows)
        {
            if (!_deviceChecked) { AppendLog("Detect device first."); return; }
            if (!_deviceConnected) { AppendLog("Device not connected."); return; }

            if (_odinBackend == OdinBackend.OdinDriverOnly)
            {
                AppendLog("══════════════════════════════════════════");
                AppendLog("  libusb error -5 — WinUSB driver needed");
                AppendLog("══════════════════════════════════════════");
                AppendLog("  1. Go to ADB/TOOLS tab → Launch Zadig");
                AppendLog(
