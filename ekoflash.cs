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
        
        private FlashMode _mode = FlashMode.Fastboot;
        private ObservableCollection<FlashRow> _fbRows = new();
        private ObservableCollection<FlashRow> _odinRows = new();
        private readonly ObservableCollection<BackupAppRow> _backupApps = new();
        private readonly ObservableCollection<BackupSetRow> _backupSets = new();

        private string _pitFilePath = "";
        private bool _uiReady;

        public MainWindow()
        {
            InitializeComponent();
            _uiReady = true;

            // إعداد الواجهة
            BuildFastbootRows();
            BuildOdinRows();
            
            // ربط البيانات (Data Binding)
            if (RowsList != null) RowsList.ItemsSource = _fbRows;
            if (OdinRowsList != null) OdinRowsList.ItemsSource = _odinRows;
            
            if (FindName("BackupAppsList") is ListView appsList) appsList.ItemsSource = _backupApps;
            if (FindName("BackupSetsList") is ListView setsList) setsList.ItemsSource = _backupSets;

            ApplyTheme("Blue");
            SwitchMode(FlashMode.Fastboot);
            ShowTab("cmd");
            
            AppendLog("MK Venom Tool Ready.");
        }

        // --- نظام الألوان ---
        private void Swatch_Click(object s, RoutedEventArgs e)
        {
            if (s is Button b && b.Tag is string n) ApplyTheme(n);
        }

        private void ApplyTheme(string theme)
        {
            var colors = new Dictionary<string, (Color Ac, Color AS)>
            {
                ["Blue"]    = (Color.FromRgb(0x37,0xCF,0xFF), Color.FromArgb(0x7A,0x35,0xCF,0xFF)),
                ["Purple"]  = (Color.FromRgb(0xB6,0x7B,0xFF), Color.FromArgb(0x80,0xA1,0x4D,0xFF)),
                ["Emerald"] = (Color.FromRgb(0x43,0xF2,0xC2), Color.FromArgb(0x7A,0x16,0xC4,0x98)),
                ["Crimson"] = (Color.FromRgb(0xFF,0x6D,0xA8), Color.FromArgb(0x7A,0xFF,0x5B,0x93)),
                ["Gold"]    = (Color.FromRgb(0xFF,0xB8,0x30), Color.FromArgb(0x7A,0xFF,0xA0,0x20)),
            };

            if (colors.TryGetValue(theme, out var c))
            {
                Resources["AccentBrush"] = new SolidColorBrush(c.Ac);
                Resources["AccentSoftBrush"] = new SolidColorBrush(c.AS);
                AppendLog($"Theme changed to: {theme}");
            }
        }

        // --- التبديل بين التبويبات ---
        private void TabCmd_Click(object s, RoutedEventArgs e) => ShowTab("cmd");
        private void TabOptions_Click(object s, RoutedEventArgs e) => ShowTab("options");
        private void ShowTab(string tab)
        {
            if (TabCmdPanel == null || TabOptionsPanel == null) return;
            TabCmdPanel.Visibility = tab == "cmd" ? Visibility.Visible : Visibility.Collapsed;
            TabOptionsPanel.Visibility = tab == "options" ? Visibility.Visible : Visibility.Collapsed;
        }

        // --- تبديل أوضاع العمل ---
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

            if (FindName("PanelBackupRestore") is Grid bk) 
                bk.Visibility = mode == FlashMode.BackupRestore ? Visibility.Visible : Visibility.Collapsed;

            AppendLog($"Mode set to: {mode}");
            UpdateCommandPreview();
        }

        // --- إعداد الصفوف ---
        private void BuildFastbootRows()
        {
            _fbRows.Clear();
            foreach (var p in new[] { "boot", "recovery", "system", "vendor", "vbmeta", "userdata" })
                _fbRows.Add(new FlashRow { Key = p, Label = p.ToUpper() });
        }
        private void BuildOdinRows()
        {
            _odinRows.Clear();
            foreach (var s in new[] { "BL", "AP", "CP", "CSC", "USERDATA" })
                _odinRows.Add(new FlashRow { Key = s, Label = s });
        }

        // --- التعامل مع الملفات ---
        private void FbBrowse_Click(object s, RoutedEventArgs e)
        {
            if (s is Button b && b.Tag is string k) {
                var dlg = new OpenFileDialog { Filter = "Image Files (*.img)|*.img|All Files (*.*)|*.*" };
                if (dlg.ShowDialog() == true) _fbRows.First(r => r.Key == k).FilePath = dlg.FileName;
            }
        }
        private void OdinBrowse_Click(object s, RoutedEventArgs e)
        {
            if (s is Button b && b.Tag is string k) {
                var dlg = new OpenFileDialog { Filter = "Odin Files (*.tar;*.md5)|*.tar;*.md5" };
                if (dlg.ShowDialog() == true) _odinRows.First(r => r.Key == k).FilePath = dlg.FileName;
            }
        }
        private void BrowsePit_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PIT Files (*.pit)|*.pit" };
            if (dlg.ShowDialog() == true) { _pitFilePath = dlg.FileName; PitPathBox.Text = _pitFilePath; }
        }
        private void ClearPit_Click(object s, RoutedEventArgs e) { _pitFilePath = ""; PitPathBox.Text = ""; }

        // --- العمليات الرئيسية ---
        private async void DetectDevice_Click(object s, RoutedEventArgs e)
        {
            DeviceStatusText.Text = "Detecting...";
            await Task.Delay(1500); // محاكاة عملية الفحص
            DeviceStatusText.Text = "DEVICE ONLINE";
            DeviceStatusText.Foreground = Brushes.SpringGreen;
            AppendLog("Device state: Connected.");
        }

        private async void StartFlashing_Click(object s, RoutedEventArgs e)
        {
            AppendLog($"Initiating {_mode} flash process...");
            await Task.Delay(1000);
            AppendLog("Working... please do not disconnect.");
        }

        private void FbFlash_Click(object s, RoutedEventArgs e) => AppendLog("Flashing selected Fastboot partition.");
        private void OdinFlashSingle_Click(object s, RoutedEventArgs e) => AppendLog("Flashing selected Odin slot via EkoFlash engine.");

        private void CmdRebootSys_Click(object s, RoutedEventArgs e) => AppendLog("Command: adb reboot");
        private void CmdRebootBl_Click(object s, RoutedEventArgs e) => AppendLog("Command: adb reboot bootloader");
        private void CmdRebootRec_Click(object s, RoutedEventArgs e) => AppendLog("Command: adb reboot recovery");
        private void LaunchZadig_Click(object s, RoutedEventArgs e) => AppendLog("Launching Driver Installer (Zadig)...");

        private void SideloadBrowse_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Zip files (*.zip)|*.zip" };
            if (dlg.ShowDialog() == true) SideloadPathBox.Text = dlg.FileName;
        }

        private void LoadApps_Click(object s, RoutedEventArgs e) => AppendLog("Fetching installed applications...");
        private void StartBackup_Click(object s, RoutedEventArgs e) => AppendLog("Starting data backup sequence...");

        // --- أدوات المساعدة ---
        private void AppendLog(string msg)
        {
            if (LogBox != null) {
                LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                LogBox.ScrollToEnd();
            }
        }

        private void UpdateCommandPreview()
        {
            if (CommandPreviewBox != null) CommandPreviewBox.Text = $"EkoFlash CLI > --mode {_mode.ToString().ToLower()}";
        }
    }

    // كلاسات الداتا (للتأكد من عدم التكرار يفضل بقاؤها هنا إذا لم تكن في ملفات منفصلة)
    public class FlashRow : INotifyPropertyChanged
    {
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
