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
    /// <summary>
    /// TURBO FLASH TOOL - Developed by AHMED YOUNIS & Mohamed Khaled
    /// </summary>
    public partial class MainWindow : Window
    {
        private enum FlashMode { Fastboot, Odin, Sideload, Tools, BackupRestore }
        private FlashMode _mode = FlashMode.Fastboot;

        private readonly ObservableCollection<FlashRow> _fbRows = new();
        private readonly ObservableCollection<FlashRow> _odinRows = new();
        
        private string _pitFilePath = "";
        private bool _deviceChecked;
        private bool _deviceConnected;
        private bool _uiReady;
        private CancellationTokenSource? _cts;

        public MainWindow()
        {
            InitializeComponent();
            _uiReady = true;

            ApplyTheme("Blue");
            
            BuildFastbootRows();
            BuildOdinRows();
            
            if (RowsList != null) RowsList.ItemsSource = _fbRows;
            if (OdinRowsList != null) OdinRowsList.ItemsSource = _odinRows;

            SwitchMode(FlashMode.Fastboot);
            ShowTab("cmd");

            AppendLog("===============================================");
            AppendLog("   TURBO FLASH TOOL v1.0 READY");
            AppendLog("   Developed by: AHMED YOUNIS & Mohamed Khaled");
            AppendLog("===============================================");
            CheckRequirements();
            UpdateCommandPreview();
        }

        private void CheckRequirements()
        {
            AppendLog($"[SYSTEM] ADB Engine: {(ToolsManager.ExeExists("platform-tools", "adb") ? "✔ OK" : "✘ MISSING")}");
            AppendLog($"[SYSTEM] Fastboot Engine: {(ToolsManager.ExeExists("platform-tools", "fastboot") ? "✔ OK" : "✘ MISSING")}");
            AppendLog($"[SYSTEM] Turbo Engine (ekoflash): {(ToolsManager.ExeExists("odin", "ekoflash") ? "✔ OK" : "✘ MISSING")}");
        }

        #region UI & Themes
        private void Swatch_Click(object s, RoutedEventArgs e)
        {
            if (s is Button b && b.Tag is string n)
            {
                ApplyTheme(n);
                AppendLog($"[THEME] Switched to {n} Style");
            }
        }

        private void ApplyTheme(string theme)
        {
            var colors = new Dictionary<string, (Color Ac, Color AS, Color Bo, Color Pa)>
            {
                ["Blue"]    = (Color.FromRgb(0x00, 0xE5, 0xFF), Color.FromArgb(0x33, 0x00, 0xE5, 0xFF), Color.FromArgb(0x4C, 0x52, 0xBF, 0xFF), Color.FromArgb(0xCC, 0x0D, 0x1B, 0x33)),
                ["Purple"]  = (Color.FromRgb(0xB6, 0x7B, 0xFF), Color.FromArgb(0x33, 0xB6, 0x7B, 0xFF), Color.FromArgb(0x4C, 0xB1, 0x86, 0xFF), Color.FromArgb(0xCC, 0x17, 0x12, 0x3A)),
                ["Crimson"] = (Color.FromRgb(0xFF, 0x00, 0x55), Color.FromArgb(0x33, 0xFF, 0x00, 0x55), Color.FromArgb(0x4C, 0xFF, 0x91, 0xC2), Color.FromArgb(0xCC, 0x20, 0x0E, 0x24))
            };

            if (!colors.TryGetValue(theme, out var c)) return;

            Resources["AccentBrush"] = new SolidColorBrush(c.Ac);
            Resources["AccentSoftBrush"] = new SolidColorBrush(c.AS);
            Resources["BorderBrush"] = new SolidColorBrush(c.Bo);
            Resources["PanelBrush"] = new SolidColorBrush(c.Pa);
            
            SetModeButtonVisual();
        }

        private void ShowTab(string tab)
        {
            if (TabCmdPanel == null || TabOptionsPanel == null) return;
            TabCmdPanel.Visibility = tab == "cmd" ? Visibility.Visible : Visibility.Collapsed;
            TabOptionsPanel.Visibility = tab == "options" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TabCmd_Click(object s, RoutedEventArgs e) => ShowTab("cmd");
        private void TabOptions_Click(object s, RoutedEventArgs e) => ShowTab("options");
        #endregion

        #region Navigation & Modes
        private void SwitchMode(FlashMode mode)
        {
            _mode = mode;
            _deviceChecked = false;

            if (PanelFastboot != null) PanelFastboot.Visibility = mode == FlashMode.Fastboot ? Visibility.Visible : Visibility.Collapsed;
            if (PanelOdin != null) PanelOdin.Visibility = mode == FlashMode.Odin ? Visibility.Visible : Visibility.Collapsed;
            if (PanelSideload != null) PanelSideload.Visibility = mode == FlashMode.Sideload ? Visibility.Visible : Visibility.Collapsed;
            if (PanelTools != null) PanelTools.Visibility = mode == FlashMode.Tools ? Visibility.Visible : Visibility.Collapsed;

            if (DeviceStatusText != null)
            {
                DeviceStatusText.Text = "NOT CHECKED";
                DeviceStatusText.Foreground = Brushes.Gray;
            }

            SetModeButtonVisual();
            UpdateCommandPreview();
            AppendLog($"[MODE] Switched to {mode.ToString().ToUpper()}");
        }

        private void FastbootMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Fastboot);
        private void OdinMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Odin);
        private void SideloadMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Sideload);
        private void ToolsMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Tools);

        private void SetModeButtonVisual()
        {
            var activeBrush = (Brush)Resources["AccentSoftBrush"];
            var inactiveBrush = new SolidColorBrush(Color.FromArgb(0x15, 0x20, 0x33, 0x00));

            if (FastbootBtn != null) FastbootBtn.Background = _mode == FlashMode.Fastboot ? activeBrush : inactiveBrush;
            if (OdinBtn != null) OdinBtn.Background = _mode == FlashMode.Odin ? activeBrush : inactiveBrush;
            if (SideloadBtn != null) SideloadBtn.Background = _mode == FlashMode.Sideload ? activeBrush : inactiveBrush;
            if (ToolsBtn != null) ToolsBtn.Background = _mode == FlashMode.Tools ? activeBrush : inactiveBrush;
        }
        #endregion

        #region Core Logic (Flash & Execution)
        private void BuildFastbootRows()
        {
            _fbRows.Clear();
            string[] partitions = { "boot", "recovery", "system", "vendor", "product", "vbmeta", "userdata" };
            foreach (var p in partitions)
                _fbRows.Add(new FlashRow { Key = p, Label = p.ToUpper() });
            
            foreach (var r in _fbRows)
                r.PropertyChanged += (s, e) => UpdateCommandPreview();
        }

        private void BuildOdinRows()
        {
            _odinRows.Clear();
            string[] slots = { "BL", "AP", "CP", "CSC", "USERDATA" };
            foreach (var s in slots)
                _odinRows.Add(new FlashRow { Key = s, Label = s });

            foreach (var r in _odinRows)
                r.PropertyChanged += (s, e) => UpdateCommandPreview();
        }

        private async void DetectDevice_Click(object s, RoutedEventArgs e)
        {
            DeviceStatusText.Text = "SCANNING...";
            DeviceStatusText.Foreground = (Brush)Resources["AccentBrush"];

            string exe = _mode == FlashMode.Fastboot ? "fastboot" : "adb";
            string args = _mode == FlashMode.Fastboot ? "devices" : "devices";
            
            var result = await RunAsync("platform-tools", exe, args);
            bool found = result.Out.Split('\n').Any(l => l.Contains("\tdevice") || l.Contains("\tfastboot"));

            _deviceConnected = found;
            _deviceChecked = true;

            if (found)
            {
                DeviceStatusText.Text = "CONNECTED";
                DeviceStatusText.Foreground = Brushes.LimeGreen;
                AppendLog("[SUCCESS] Device detected successfully.");
            }
            else
            {
                DeviceStatusText.Text = "NOT FOUND";
                DeviceStatusText.Foreground = Brushes.Red;
                AppendLog("[ERROR] No device found. Check cables/drivers.");
            }
        }

        private async void FlashAll_Click(object s, RoutedEventArgs e)
        {
            if (!_deviceConnected) { AppendLog("[!] Please detect device first."); return; }

            _cts = new CancellationTokenSource();
            MainProgress.Value = 0;
            AppendLog("[PROCESS] Starting Turbo Flash sequence...");

            if (_mode == FlashMode.Fastboot)
            {
                var targetRows = _fbRows.Where(r => !string.IsNullOrEmpty(r.FilePath)).ToList();
                for (int i = 0; i < targetRows.Count; i++)
                {
                    var row = targetRows[i];
                    AppendLog($"[FLASHING] Sending {row.Label}...");
                    await RunAsync("platform-tools", "fastboot", $"flash {row.Key} \"{row.FilePath}\"");
                    MainProgress.Value = ((double)(i + 1) / targetRows.Count) * 100;
                }
            }
            else if (_mode == FlashMode.Odin)
            {
                AppendLog("[PROCESS] Invoking Turbo Odin Engine (ekoflash)...");
                string args = "";
                foreach(var r in _odinRows.Where(x => !string.IsNullOrEmpty(x.FilePath)))
                    args += $"--{r.Key.ToLower()} \"{r.FilePath}\" ";
                
                await RunAsync("odin", "ekoflash", args);
                MainProgress.Value = 100;
            }

            AppendLog("[COMPLETED] All operations finished.");
            MainProgress.Value = 100;
        }

        private void Cancel_Click(object s, RoutedEventArgs e)
        {
            _cts?.Cancel();
            AppendLog("[STOP] Operation cancelled by user.");
        }
        #endregion

        #region Helpers
        private void AppendLog(string msg)
        {
            Dispatcher.Invoke(() => {
                LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                LogBox.ScrollToEnd();
            });
        }

        private void UpdateCommandPreview()
        {
            if (CommandPreviewBox == null) return;
            if (_mode == FlashMode.Fastboot)
                CommandPreviewBox.Text = "fastboot flash <partition> <file_path>";
            else if (_mode == FlashMode.Odin)
                CommandPreviewBox.Text = "ekoflash --bl <file> --ap <file> ...";
            else
                CommandPreviewBox.Text = "Ready for input...";
        }

        private async Task<ProcessResult> RunAsync(string dir, string exe, string args)
        {
            return await Task.Run(() => {
                var res = new ProcessResult();
                try {
                    string path = ToolsManager.GetExePath(dir, exe);
                    if (!File.Exists(path)) path = exe;

                    var psi = new ProcessStartInfo(path, args) {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    var p = Process.Start(psi);
                    res.Out = p!.StandardOutput.ReadToEnd();
                    res.Err = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    res.Code = p.ExitCode;
                } catch (Exception ex) { res.Err = ex.Message; }
                return res;
            });
        }

        private void Browse_Click(object s, RoutedEventArgs e) {
            var btn = s as Button;
            var row = _fbRows.FirstOrDefault(x => x.Key == btn?.Tag?.ToString());
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == true && row != null) row.FilePath = dlg.FileName;
        }

        private void FlashOne_Click(object s, RoutedEventArgs e) => FlashAll_Click(s, e);
        
        private void QuickCmd_Click(object s, RoutedEventArgs e) {
            if (s is Button b && b.Tag is string cmd) {
                string[] parts = cmd.Split(new[] { ' ' }, 2);
                RunAsync("platform-tools", parts[0], parts.Length > 1 ? parts[1] : "");
                AppendLog($"[CMD] Executing: {cmd}");
            }
        }
        #endregion
    }

    #region Helper Classes
    // ✅ تم حذف ToolsManager المكرر — الكلاس موجود في ToolsManager.cs
    public class FlashRow : INotifyPropertyChanged {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        private string _filePath = "";
        public string FilePath { 
            get => _filePath; 
            set { _filePath = value; OnPropertyChanged(); } 
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ProcessResult {
        public int Code { get; set; }
        public string Out { get; set; } = "";
        public string Err { get; set; } = "";
    }
    #endregion
}
