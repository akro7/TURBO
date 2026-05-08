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
        private bool _deviceConnected, _uiReady;

        public MainWindow()
        {
            InitializeComponent();
            _uiReady = true;

            ApplyTheme("Blue");
            BuildFastbootRows();
            BuildOdinRows();
            SwitchMode(FlashMode.Fastboot);
            ShowTab("cmd");

            // ربط القوائم بالبيانات
            if (RowsList != null) RowsList.ItemsSource = _fbRows;
            if (OdinRowsList != null) OdinRowsList.ItemsSource = _odinRows;
            
            if (FindName("BackupAppsList") is ListView appsList) appsList.ItemsSource = _backupApps;
            if (FindName("BackupSetsList") is ListView setsList) setsList.ItemsSource = _backupSets;

            AppendLog("MK Venom Tool (EkoFlash Engine) Initialized.");
        }

        // --- نظام السمات (Themes) ---
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
            var T = new Dictionary<string, (Color Ac, Color AS)>
            {
                ["Blue"]    = (Color.FromRgb(0x37,0xCF,0xFF), Color.FromArgb(0x7A,0x35,0xCF,0xFF)),
                ["Purple"]  = (Color.FromRgb(0xB6,0x7B,0xFF), Color.FromArgb(0x80,0xA1,0x4D,0xFF)),
                ["Emerald"] = (Color.FromRgb(0x43,0xF2,0xC2), Color.FromArgb(0x7A,0x16,0xC4,0x98)),
                ["Crimson"] = (Color.FromRgb(0xFF,0x6D,0xA8), Color.FromArgb(0x7A,0xFF,0x5B,0x93)),
                ["Gold"]    = (Color.FromRgb(0xFF,0xB8,0x30), Color.FromArgb(0x7A,0xFF,0xA0,0x20)),
            };

            if (T.TryGetValue(theme, out var t))
            {
                Resources["AccentBrush"] = new SolidColorBrush(t.Ac);
                Resources["AccentSoftBrush"] = new SolidColorBrush(t.AS);
            }
            SetModeButtonVisual();
        }

        // --- التحكم في القوائم (Tabs) ---
        private void TabCmd_Click(object s, RoutedEventArgs e) => ShowTab("cmd");
        private void TabOptions_Click(object s, RoutedEventArgs e) => ShowTab("options");

        private void ShowTab(string tab)
        {
            if (TabCmdPanel == null || TabOptionsPanel == null) return;
            TabCmdPanel.Visibility = tab == "cmd" ? Visibility.Visible : Visibility.Collapsed;
            TabOptionsPanel.Visibility = tab == "options" ? Visibility.Visible : Visibility.Collapsed;
        }

        // --- تبديل الأوضاع (Modes) ---
        private void FastbootMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Fastboot);
        private void OdinMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Odin);
        private void SideloadMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Sideload);
        private void ToolsMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Tools);
        private void BackupRestoreMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.BackupRestore);

        private void SwitchMode(FlashMode mode)
        {
            _mode = mode;
            if (!_uiReady) return;

            PanelFastboot.Visibility = mode == FlashMode.Fastboot ? Visibility.Visible : Visibility.Collapsed;
            PanelOdin.Visibility = mode == FlashMode.Odin ? Visibility.Visible : Visibility.Collapsed;
            PanelSideload.Visibility = mode == FlashMode.Sideload ? Visibility.Visible : Visibility.Collapsed;
            PanelTools.Visibility = mode == FlashMode.Tools ? Visibility.Visible : Visibility.Collapsed;
            
            if (FindName("PanelBackupRestore") is Grid bkPanel) 
                bkPanel.Visibility = mode == FlashMode.BackupRestore ? Visibility.Visible : Visibility.Collapsed;

            SetModeButtonVisual();
            UpdateCommandPreview();
            AppendLog($"Switched to {mode} mode.");
        }

        private void SetModeButtonVisual()
        {
            var on = (Brush)Resources["AccentSoftBrush"];
            var off = new SolidColorBrush(Color.FromArgb(0x2D, 0x18, 0x25, 0x3D));
            if (FastbootBtn != null) FastbootBtn.Background = _mode == FlashMode.Fastboot ? on : off;
            if (OdinBtn != null) OdinBtn.Background = _mode == FlashMode.Odin ? on : off;
            if (SideloadBtn != null) SideloadBtn.Background = _mode == FlashMode.Sideload ? on : off;
            if (ToolsBtn != null) ToolsBtn.Background = _mode == FlashMode.Tools ? on : off;
        }

        // --- إنشاء الصفوف (Rows) ---
        private void BuildFastbootRows()
        {
            var parts = new[] { "boot", "recovery", "system", "vendor", "vbmeta", "userdata" };
            foreach (var p in parts) _fbRows.Add(new FlashRow { Key = p, Label = p.ToUpper() });
        }

        private void BuildOdinRows()
        {
            var slots = new[] { "BL", "AP", "CP", "CSC", "USERDATA" };
            foreach (var s in slots) _odinRows.Add(new FlashRow { Key = s, Label = s });
        }

        // --- اختيار الملفات (Browse) ---
        private void FbBrowse_Click(object s, RoutedEventArgs e)
        {
            if (s is Button b && b.Tag is string k)
            {
                var dlg = new OpenFileDialog { Filter = "Image Files|*.img;*.bin|All Files|*.*" };
                if (dlg.ShowDialog() == true) _fbRows.First(r => r.Key == k).FilePath = dlg.FileName;
            }
        }

        private void OdinBrowse_Click(object s, RoutedEventArgs e)
        {
            if (s is Button b && b.Tag is string k)
            {
                var dlg = new OpenFileDialog { Filter = "Odin Files|*.tar;*.md5|All Files|*.*" };
                if (dlg.ShowDialog() == true) _odinRows.First(r => r.Key == k).FilePath = dlg.FileName;
            }
        }

        private void BrowsePit_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PIT Files|*.pit" };
            if (dlg.ShowDialog() == true) { _pitFilePath = dlg.FileName; PitPathBox.Text = _pitFilePath; }
        }

        private void ClearPit_Click(object s, RoutedEventArgs e) { _pitFilePath = ""; PitPathBox.Text = ""; }

        // --- عمليات الفلاش والكشف (Flash & Detect) ---
        private async void DetectDevice_Click(object s, RoutedEventArgs e)
        {
            DeviceStatusText.Text = "Checking...";
            var r = await DetectAsync();
            _deviceConnected = r.ok;
            DeviceStatusText.Text = r.text;
            DeviceStatusText.Foreground = r.ok ? Brushes.LightGreen : Brushes.LightCoral;
            AppendLog(r.log);
        }

        private async Task<(bool ok, string text, string log)> DetectAsync()
        {
            if (_mode == FlashMode.Fastboot)
            {
                var r = await RunAsync("platform-tools", "fastboot", "devices", 5000);
                return r.Out.Contains("fastboot") ? (true, "FASTBOOT CONNECTED", "Device found in fastboot.") : (false, "NO DEVICE", "Fastboot device not found.");
            }
            if (_mode == FlashMode.Odin)
            {
                var r = await RunAsync("ekoflash", "ekoflash", "--detect", 5000);
                return r.Out.Contains("detected") ? (true, "DOWNLOAD MODE", "EkoFlash engine ready.") : (false, "NO DEVICE", "Odin device not found.");
            }
            return (false, "NOT CHECKED", "Unknown mode.");
        }

        private async void StartFlashing_Click(object s, RoutedEventArgs e)
        {
            if (_mode == FlashMode.Fastboot) await FlashFastbootAsync(_fbRows.Where(r => !string.IsNullOrEmpty(r.FilePath)).ToList());
            else if (_mode == FlashMode.Odin) await FlashOdinAsync(_odinRows.Where(r => !string.IsNullOrEmpty(r.FilePath)).ToList());
        }

        private async void FbFlash_Click(object s, RoutedEventArgs e)
        {
            if (s is Button b && b.Tag is string k)
            {
                var row = _fbRows.FirstOrDefault(r => r.Key == k);
                if (row != null && !string.IsNullOrEmpty(row.FilePath)) await FlashFastbootAsync(new List<FlashRow> { row });
            }
        }

        private async void OdinFlashSingle_Click(object s, RoutedEventArgs e)
        {
            if (s is Button b && b.Tag is string k)
            {
                var row = _odinRows.FirstOrDefault(r => r.Key == k);
                if (row != null && !string.IsNullOrEmpty(row.FilePath)) await FlashOdinAsync(new List<FlashRow> { row });
            }
        }

        private async Task FlashFastbootAsync(List<FlashRow> rows)
        {
            foreach (var r in rows)
            {
                AppendLog($"Flashing {r.Key}...");
                await RunAsync("platform-tools", "fastboot", $"flash {r.Key} \"{r.FilePath}\"", 300000);
            }
            if (AutoRebootCheck?.IsChecked == true) await RunAsync("platform-tools", "fastboot", "reboot", 10000);
        }

        private async Task FlashOdinAsync(List<FlashRow> rows)
        {
            foreach (var r in rows)
            {
                AppendLog($"EkoFlash: {r.Key}...");
                string args = $"--flash {r.Key} \"{r.FilePath}\"";
                if (!string.IsNullOrEmpty(_pitFilePath)) args += $" --pit \"{_pitFilePath}\"";
                await RunAsync("ekoflash", "ekoflash", args, 600000);
            }
        }

        // --- أدوات النظام (Tools) ---
        private async void CmdRebootSys_Click(object s, RoutedEventArgs e) => await RunAsync("platform-tools", "adb", "reboot", 10000);
        private async void CmdRebootBl_Click(object s, RoutedEventArgs e) => await RunAsync("platform-tools", "adb", "reboot bootloader", 10000);
        private async void CmdRebootRec_Click(object s, RoutedEventArgs e) => await RunAsync("platform-tools", "adb", "reboot recovery", 10000);
        
        private void LaunchZadig_Click(object s, RoutedEventArgs e) 
        {
            try { Process.Start(new ProcessStartInfo(ToolsManager.GetExePath("zadig", "zadig")) { UseShellExecute = true }); }
            catch (Exception ex) { AppendLog($"Error: {ex.Message}"); }
        }

        // --- المحرك الرئيسي (Engine) ---
        private async Task<(int Code, string Out, string Err)> RunAsync(string dir, string exe, string args, int ms)
        {
            try
            {
                var path = ToolsManager.GetExePath(dir, exe);
                var psi = new ProcessStartInfo(path, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                using var p = Process.Start(psi);
                if (p == null) return (-1, "", "Failed to start");
                var o = await p.StandardOutput.ReadToEndAsync();
                var e = await p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync(new CancellationTokenSource(ms).Token);
                return (p.ExitCode, o, e);
            }
            catch (Exception ex) { return (-1, "", ex.Message); }
        }

        private void AppendLog(string msg)
        {
            if (LogBox == null) return;
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            LogBox.ScrollToEnd();
        }

        private void UpdateCommandPreview()
        {
            if (CommandPreviewBox == null) return;
            CommandPreviewBox.Text = $"Current Mode: {_mode}";
        }

        // --- الروابط المفقودة لـ Sideload و Backup ---
        private void SideloadBrowse_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Zip Files|*.zip" };
            if (dlg.ShowDialog() == true) SideloadPathBox.Text = dlg.FileName;
        }

        private async void LoadApps_Click(object s, RoutedEventArgs e)
        {
            _backupApps.Clear();
            var apps = await BackupService.GetInstalledAppsAsync(AppendLog);
            foreach (var a in apps) _backupApps.Add(a);
        }

        private async void StartBackup_Click(object s, RoutedEventArgs e)
        {
            var sel = _backupApps.Where(a => a.IsSelected).ToList();
            foreach (var app in sel) await BackupService.BackupAppAsync(app.PackageName, app.DisplayName, new BackupOptions(), AppendLog);
        }
    }

    // --- كلاسات البيانات (Models) ---
    public class FlashRow : INotifyPropertyChanged
    {
        private string _fp = "";
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string FilePath { get => _fp; set { _fp = value; OnPropertyChanged(); } }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class BackupAppRow { public bool IsSelected { get; set; } public string PackageName { get; set; } = ""; public string DisplayName { get; set; } = ""; }
    public class BackupSetRow { public string BackupPath { get; set; } = ""; public string PackageName { get; set; } = ""; public string DisplayName { get; set; } = ""; }
}
