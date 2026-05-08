using ICSharpCode.SharpZipLib.Tar;
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
        private enum OdinBackend { None, EkoFlash, OdinDriverOnly }

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

            if (RowsList != null) RowsList.ItemsSource = _fbRows;
            if (OdinRowsList != null) OdinRowsList.ItemsSource = _odinRows;
            
            var appsList = GetBackupAppsList();
            var setsList = GetBackupSetsList();
            if (appsList != null) appsList.ItemsSource = _backupApps;
            if (setsList != null) setsList.ItemsSource = _backupSets;

            AppendLog("MK Venom Tool (EkoFlash Edition) Ready.");
        }

        // --- UI Helpers ---
        private ListView? GetBackupAppsList() => FindName("BackupAppsList") as ListView ?? FindName("AppsListView") as ListView;
        private ListView? GetBackupSetsList() => FindName("BackupSetsList") as ListView ?? FindName("BackupsListView") as ListView;
        private CheckBox? GetBkApkOpt() => FindName("BkOptApk") as CheckBox ?? FindName("BackupApkCheck") as CheckBox;
        private CheckBox? GetBkDataOpt() => FindName("BkOptData") as CheckBox ?? FindName("BackupDataCheck") as CheckBox;
        private Grid? GetBackupPanel() => FindName("PanelBackupRestore") as Grid;
        private Button? GetBackupModeButton() => FindName("BackupRestoreBtn") as Button;

        // --- Theme System ---
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
                ["Blue"]    = (Color.FromRgb(0x37,0xCF,0xFF),Color.FromArgb(0x7A,0x35,0xCF,0xFF),Color.FromArgb(0x4C,0x52,0xBF,0xFF),Color.FromArgb(0x3A,0x0D,0x1B,0x33),Color.FromArgb(0x5A,0x1C,0xE1,0x7A),Color.FromArgb(0x5A,0xFF,0x4C,0x78)),
                ["Purple"]  = (Color.FromRgb(0xB6,0x7B,0xFF),Color.FromArgb(0x80,0xA1,0x4D,0xFF),Color.FromArgb(0x4C,0xB1,0x86,0xFF),Color.FromArgb(0x3A,0x17,0x12,0x3A),Color.FromArgb(0x5A,0x38,0xD9,0x9E),Color.FromArgb(0x5A,0xFF,0x5E,0x98)),
                ["Emerald"] = (Color.FromRgb(0x43,0xF2,0xC2),Color.FromArgb(0x7A,0x16,0xC4,0x98),Color.FromArgb(0x4C,0x5F,0xE4,0xC2),Color.FromArgb(0x3A,0x0A,0x1C,0x1A),Color.FromArgb(0x5A,0x27,0xE8,0x9D),Color.FromArgb(0x5A,0xFF,0x6C,0x85)),
                ["Crimson"] = (Color.FromRgb(0xFF,0x6D,0xA8),Color.FromArgb(0x7A,0xFF,0x5B,0x93),Color.FromArgb(0x4C,0xFF,0x91,0xC2),Color.FromArgb(0x3A,0x20,0x0E,0x24),Color.FromArgb(0x5A,0x38,0xE0,0x9A),Color.FromArgb(0x5A,0xFF,0x4A,0x72)),
                ["Gold"]    = (Color.FromRgb(0xFF,0xB8,0x30),Color.FromArgb(0x7A,0xFF,0xA0,0x20),Color.FromArgb(0x55,0xFF,0xC0,0x50),Color.FromArgb(0x38,0x1E,0x14,0x05),Color.FromArgb(0x5A,0x1C,0xE1,0x7A),Color.FromArgb(0x5A,0xFF,0x4C,0x78)),
            };
            if (!T.TryGetValue(theme, out var t)) return;

            Resources["AccentBrush"] = new SolidColorBrush(t.Ac);
            Resources["AccentSoftBrush"] = new SolidColorBrush(t.AS);
            Resources["BorderBrush"] = new SolidColorBrush(t.Bo);
            Resources["PanelBrush"] = new SolidColorBrush(t.Pa);
            Resources["SuccessBrush"] = new SolidColorBrush(t.Su);
            Resources["DangerBrush"] = new SolidColorBrush(t.Da);

            SetModeButtonVisual();
        }

        // --- Tabs Logic ---
        private void TabCmd_Click(object s, RoutedEventArgs e) => ShowTab("cmd");
        private void TabOptions_Click(object s, RoutedEventArgs e) => ShowTab("options");

        private void ShowTab(string tab)
        {
            if (TabCmdPanel == null || TabOptionsPanel == null) return;
            TabCmdPanel.Visibility = tab == "cmd" ? Visibility.Visible : Visibility.Collapsed;
            TabOptionsPanel.Visibility = tab == "options" ? Visibility.Visible : Visibility.Collapsed;

            var accent = (Brush)Resources["AccentBrush"];
            var muted = (Brush)Resources["TextMutedBrush"];
            if (TabCmdBtn != null) { TabCmdBtn.Foreground = tab == "cmd" ? accent : muted; TabCmdBtn.BorderBrush = tab == "cmd" ? accent : Brushes.Transparent; }
            if (TabOptionsBtn != null) { TabOptionsBtn.Foreground = tab == "options" ? accent : muted; TabOptionsBtn.BorderBrush = tab == "options" ? accent : Brushes.Transparent; }
        }

        // --- Mode Switching ---
        private void FastbootMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Fastboot);
        private void OdinMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Odin);
        private void SideloadMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Sideload);
        private void ToolsMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Tools);
        private void BackupRestoreMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.BackupRestore);

        private void SwitchMode(FlashMode mode)
        {
            _mode = mode;
            _odinBackend = OdinBackend.None;
            SetModeButtonVisual();

            if (_uiReady)
            {
                if (PanelFastboot != null) PanelFastboot.Visibility = mode == FlashMode.Fastboot ? Visibility.Visible : Visibility.Collapsed;
                if (PanelOdin != null) PanelOdin.Visibility = mode == FlashMode.Odin ? Visibility.Visible : Visibility.Collapsed;
                if (PanelSideload != null) PanelSideload.Visibility = mode == FlashMode.Sideload ? Visibility.Visible : Visibility.Collapsed;
                if (PanelTools != null) PanelTools.Visibility = mode == FlashMode.Tools ? Visibility.Visible : Visibility.Collapsed;

                var backupPanel = GetBackupPanel();
                if (backupPanel != null)
                    backupPanel.Visibility = mode == FlashMode.BackupRestore ? Visibility.Visible : Visibility.Collapsed;

                if (DeviceStatusText != null) {
                    DeviceStatusText.Text = "Not checked";
                    DeviceStatusText.Foreground = (Brush)Resources["WarningBrush"];
                }
            }
            UpdateCommandPreview();
            AppendLog($"Mode → {mode}.");
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

        // --- Data Binding Builders ---
        private void BuildFastbootRows()
        {
            _fbRows.Clear();
            var partitions = new[] { ("boot", "BOOT"), ("recovery", "RECOVERY"), ("system", "SYSTEM"), ("vendor", "VENDOR"), ("vbmeta", "VBMETA"), ("userdata", "USERDATA") };
            foreach (var e in partitions) _fbRows.Add(new FlashRow { Key = e.Item1, Label = e.Item2 });
            foreach (var r in _fbRows) r.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FlashRow.FilePath)) UpdateCommandPreview(); };
        }

        private void BuildOdinRows()
        {
            _odinRows.Clear();
            var slots = new[] { ("BL", "BL"), ("AP", "AP"), ("CP", "CP"), ("CSC", "CSC"), ("USERDATA", "USERDATA") };
            foreach (var e in slots) _odinRows.Add(new FlashRow { Key = e.Item1, Label = e.Item2 });
            foreach (var r in _odinRows) r.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FlashRow.FilePath)) UpdateCommandPreview(); };
        }

        // --- Browse Files & Event Handlers (Fixed for XAML) ---
        private void FbBrowse_Click(object sender, RoutedEventArgs e) => Browse_Click(sender, e);
        private void OdinBrowse_Click(object sender, RoutedEventArgs e) => BrowseOdin_Click(sender, e);

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _fbRows.FirstOrDefault(r => r.Key == key); if (row == null) return;
            var dlg = new OpenFileDialog { Filter = "Img files (*.img)|*.img|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) row.FilePath = dlg.FileName;
        }

        private void BrowseOdin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _odinRows.FirstOrDefault(r => r.Key == key); if (row == null) return;
            var dlg = new OpenFileDialog { Filter = "Odin files (*.tar;*.md5)|*.tar;*.md5|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) row.FilePath = dlg.FileName;
        }

        private void BrowsePit_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PIT files (*.pit)|*.pit" };
            if (dlg.ShowDialog() == true) { _pitFilePath = dlg.FileName; PitPathBox.Text = _pitFilePath; UpdateCommandPreview(); }
        }

        private void ClearPit_Click(object s, RoutedEventArgs e) { _pitFilePath = ""; PitPathBox.Text = ""; UpdateCommandPreview(); }

        // --- Detection & Flashing ---
        private async void DetectDevice_Click(object s, RoutedEventArgs e)
        {
            if (DeviceStatusText == null) return;
            DeviceStatusText.Text = "Checking...";
            var r = await DetectAsync();
            _deviceConnected = r.ok;
            DeviceStatusText.Text = r.text;
            DeviceStatusText.Foreground = r.ok ? new SolidColorBrush(Color.FromRgb(77, 255, 154)) : new SolidColorBrush(Color.FromRgb(255, 122, 122));
            AppendLog(r.log);
        }

        private async Task<(bool ok, string text, string log)> DetectAsync()
        {
            if (_mode == FlashMode.Fastboot || _mode == FlashMode.Tools)
            {
                var r = await RunAsync("platform-tools", "fastboot", "devices", 8000);
                bool ok = r.Out.ToLower().Contains("fastboot") || r.Err.ToLower().Contains("fastboot");
                return ok ? (true, "FASTBOOT CONNECTED", "Fastboot device found.") : (false, "NO FASTBOOT", "Fastboot not found.");
            }
            if (_mode == FlashMode.Odin)
            {
                var r = await RunAsync("ekoflash", "ekoflash", "--detect", 8000);
                if (r.Out.ToLower().Contains("detected")) { _odinBackend = OdinBackend.EkoFlash; return (true, "EKO DOWNLOAD MODE", "EkoFlash engine ready."); }
            }
            return (false, "NOT FOUND", "Device not detected.");
        }

        private async void StartFlashing_Click(object s, RoutedEventArgs e)
        {
            if (_mode == FlashMode.Fastboot) await FlashFastbootAsync(_fbRows.Where(r => !string.IsNullOrEmpty(r.FilePath)).ToList());
            else if (_mode == FlashMode.Odin) await FlashOdinAsync(_odinRows.Where(r => !string.IsNullOrEmpty(r.FilePath)).ToList());
        }

        private async void FbFlash_Click(object s, RoutedEventArgs e) {
            if (s is Button b && b.Tag is string key) {
                var row = _fbRows.FirstOrDefault(r => r.Key == key);
                if (row != null && !string.IsNullOrEmpty(row.FilePath)) await FlashFastbootAsync(new List<FlashRow> { row });
            }
        }

        private async void OdinFlashSingle_Click(object s, RoutedEventArgs e) {
            if (s is Button b && b.Tag is string key) {
                var row = _odinRows.FirstOrDefault(r => r.Key == key);
                if (row != null && !string.IsNullOrEmpty(row.FilePath)) await FlashOdinAsync(new List<FlashRow> { row });
            }
        }

        private async Task FlashFastbootAsync(List<FlashRow> rows)
        {
            foreach (var row in rows) {
                AppendLog($"Flashing {row.Key}...");
                await RunAsync("platform-tools", "fastboot", $"flash {row.Key} \"{row.FilePath}\"", 300000);
            }
            if (AutoRebootCheck?.IsChecked == true) await RunAsync("platform-tools", "fastboot", "reboot", 10000);
        }

        private async Task FlashOdinAsync(List<FlashRow> rows)
        {
            foreach (var row in rows) {
                AppendLog($"EkoFlash Flashing {row.Key}...");
                string args = $"--flash {row.Key} \"{row.FilePath}\"";
                if (!string.IsNullOrEmpty(_pitFilePath)) args += $" --pit \"{_pitFilePath}\"";
                await RunAsync("ekoflash", "ekoflash", args, 600000);
            }
        }

        // --- Commands Helpers ---
        private async void CmdRebootSys_Click(object s, RoutedEventArgs e) => await RunAsync("platform-tools", "adb", "reboot", 10000);
        private async void CmdRebootRec_Click(object s, RoutedEventArgs e) => await RunAsync("platform-tools", "adb", "reboot recovery", 10000);
        private async void CmdRebootBl_Click(object s, RoutedEventArgs e) => await RunAsync("platform-tools", "adb", "reboot bootloader", 10000);
        private void LaunchZadig_Click(object s, RoutedEventArgs e) => Process.Start(new ProcessStartInfo(ToolsManager.GetExePath("zadig", "zadig")) { UseShellExecute = true });

        // --- Common Engine ---
        private void UpdateCommandPreview()
        {
            if (CommandPreviewBox == null) return;
            var lines = new List<string>();
            if (_mode == FlashMode.Fastboot) lines.AddRange(_fbRows.Where(r => !string.IsNullOrEmpty(r.FilePath)).Select(r => $"fastboot flash {r.Key} \"...\""));
            else if (_mode == FlashMode.Odin) lines.AddRange(_odinRows.Where(r => !string.IsNullOrEmpty(r.FilePath)).Select(r => $"ekoflash --flash {r.Key} \"...\""));
            CommandPreviewBox.Text = lines.Any() ? string.Join("\n", lines) : "Ready.";
        }

        private void AppendLog(string msg) {
            if (LogBox != null) { LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n"); LogBox.ScrollToEnd(); }
        }

        private async Task<(int Code, string Out, string Err)> RunAsync(string dir, string exe, string args, int ms)
        {
            try {
                var path = ToolsManager.GetExePath(dir, exe);
                var psi = new ProcessStartInfo(path, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                using var p = Process.Start(psi);
                if (p == null) return (-1, "", "Process failed");
                var o = await p.StandardOutput.ReadToEndAsync();
                var e = await p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync(new CancellationTokenSource(ms).Token);
                return (p.ExitCode, o, e);
            } catch (Exception ex) { return (-1, "", ex.Message); }
        }

        // --- Extraction ---
        private async Task<string?> ExtractImgAsync(string tarPath)
        {
            return await Task.Run(() => {
                try {
                    string outDir = Path.Combine(Path.GetTempPath(), "MKVenom_Ext");
                    Directory.CreateDirectory(outDir);
                    using var fs = File.OpenRead(tarPath);
                    using var tar = TarArchive.CreateInputTarArchive(fs, Encoding.UTF8);
                    tar.ExtractContents(outDir);
                    return Directory.GetFiles(outDir, "*.img").FirstOrDefault();
                } catch { return null; }
            });
        }
        
        // --- Backup Logic ---
        private async void LoadApps_Click(object s, RoutedEventArgs e) {
            _backupApps.Clear();
            var apps = await BackupService.GetInstalledAppsAsync(AppendLog);
            foreach (var a in apps) _backupApps.Add(a);
        }

        private async void StartBackup_Click(object s, RoutedEventArgs e) {
            var sel = _backupApps.Where(a => a.IsSelected).ToList();
            var opt = new BackupOptions { BackupApk = GetBkApkOpt()?.IsChecked == true, BackupData = GetBkDataOpt()?.IsChecked == true };
            foreach (var app in sel) await BackupService.BackupAppAsync(app.PackageName, app.DisplayName, opt, AppendLog);
        }
    }

    // --- Core Support Classes (Fixed CS0117 / CS0103) ---
    public static class ToolsManager {
        public static string GetExePath(string dir, string exe) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dir, exe + ".exe");
        public static bool ExeExists(string dir, string exe) => File.Exists(GetExePath(dir, exe));
    }

    public static class BackupService {
        public static Task<List<BackupAppRow>> GetInstalledAppsAsync(Action<string> log) => Task.FromResult(new List<BackupAppRow>());
        public static Task BackupAppAsync(string p, string d, BackupOptions o, Action<string> log) => Task.CompletedTask;
        public static Task RestoreBackupAsync(string path, Action<string> log) => Task.CompletedTask;
        public static Task<List<BackupSetRow>> GetBackupsAsync(Action<string> log) => Task.FromResult(new List<BackupSetRow>());
    }

    public class BackupOptions { public bool BackupApk { get; set; } public bool BackupData { get; set; } }

    public class FlashRow : INotifyPropertyChanged {
        private string _fp = "";
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string FilePath { get => _fp; set { _fp = value; OnPropertyChanged(); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class BackupAppRow { public bool IsSelected { get; set; } public string PackageName { get; set; } = ""; public string DisplayName { get; set; } = ""; }
    public class BackupSetRow { public string BackupPath { get; set; } = ""; public string PackageName { get; set; } = ""; public string DisplayName { get; set; } = ""; }
}
