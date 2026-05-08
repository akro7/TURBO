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
        private bool _uiReady;

        public MainWindow()
        {
            InitializeComponent();
            _uiReady = true;

            BuildFastbootRows();
            BuildOdinRows();

            RowsList.ItemsSource = _fbRows;
            OdinRowsList.ItemsSource = _odinRows;

            if (FindName("BackupAppsList") is ListView appsList) appsList.ItemsSource = _backupApps;
            if (FindName("BackupSetsList") is ListView setsList) setsList.ItemsSource = _backupSets;

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
                ["Gold"]    =(Color.FromRgb(0xFF,0xB8,0x30),Color.FromArgb(0x7A,0xFF,0xA0,0x20),Color.FromArgb(0x55,0xFF,0xC0,0x50),Color.FromArgb(0x38,0x1E,0x14,0x05),Color.FromArgb(0x5A,0x1C,0E1,0x7A),Color.FromArgb(0x5A,0xFF,0x4C,0x78)),
            };
            if (!T.TryGetValue(theme, out var t)) return;

            Resources["Accent"] = new SolidColorBrush(t.Ac);
            Resources["Accent2"] = new SolidColorBrush(t.AS);
            Resources["PanelBorder"] = new SolidColorBrush(t.Bo);
            Resources["PanelBg"] = new SolidColorBrush(t.Pa);
        }

        private void TabCmd_Click(object s, RoutedEventArgs e) => ShowTab("cmd");
        private void TabOptions_Click(object s, RoutedEventArgs e) => ShowTab("options");

        private void ShowTab(string tab)
        {
            TabCmdPanel.Visibility = tab == "cmd" ? Visibility.Visible : Visibility.Collapsed;
            TabOptionsPanel.Visibility = tab == "options" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void FastbootMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Fastboot);
        private void OdinMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Odin);
        private void SideloadMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Sideload);
        private void ToolsMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.Tools);
        private void BackupRestoreMode_Click(object s, RoutedEventArgs e) => SwitchMode(FlashMode.BackupRestore);

        private void SwitchMode(FlashMode mode)
        {
            _mode = mode;

            PanelFastboot.Visibility = mode == FlashMode.Fastboot ? Visibility.Visible : Visibility.Collapsed;
            PanelOdin.Visibility = mode == FlashMode.Odin ? Visibility.Visible : Visibility.Collapsed;
            PanelSideload.Visibility = mode == FlashMode.Sideload ? Visibility.Visible : Visibility.Collapsed;
            PanelTools.Visibility = mode == FlashMode.Tools ? Visibility.Visible : Visibility.Collapsed;

            if (FindName("PanelBackupRestore") is Grid bk)
                bk.Visibility = mode == FlashMode.BackupRestore ? Visibility.Visible : Visibility.Collapsed;

            DeviceStatusText.Text = "Not checked";
            DeviceStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 201, 75));

            AppendLog($"Mode -> {mode}");
            UpdateCommandPreview();
        }

        private void BuildFastbootRows()
        {
            _fbRows.Clear();
            foreach (var p in new[] { "boot", "recovery", "system", "vendor", "product", "vbmeta", "vendor_boot", "userdata" })
                _fbRows.Add(new FlashRow { Key = p, Label = p.ToUpperInvariant() });
        }

        private void BuildOdinRows()
        {
            _odinRows.Clear();
            foreach (var s in new[] { "BL", "AP", "CP", "CSC", "USERDATA" })
                _odinRows.Add(new FlashRow { Key = s, Label = s });
        }

        // XAML handlers
        private void FbBrowse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _fbRows.FirstOrDefault(r => r.Key == key);
            if (row == null) return;

            var dlg = new OpenFileDialog { Filter = "Image Files|*.img;*.bin|All Files|*.*" };
            if (dlg.ShowDialog() == true)
            {
                row.FilePath = dlg.FileName;
                UpdateCommandPreview();
            }
        }

        private void OdinBrowse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var row = _odinRows.FirstOrDefault(r => r.Key == key);
            if (row == null) return;

            var dlg = new OpenFileDialog { Filter = "Odin Files|*.tar;*.md5;*.img;*.bin|All Files|*.*" };
            if (dlg.ShowDialog() == true)
            {
                row.FilePath = dlg.FileName;
                UpdateCommandPreview();
            }
        }

        private void BrowsePit_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PIT files (*.pit)|*.pit|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                _pitFilePath = dlg.FileName;
                PitPathBox.Text = _pitFilePath;
                UpdateCommandPreview();
            }
        }

        private void ClearPit_Click(object s, RoutedEventArgs e)
        {
            _pitFilePath = "";
            PitPathBox.Text = "";
            UpdateCommandPreview();
        }

        private void SideloadBrowse_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                SideloadPathBox.Text = dlg.FileName;
                UpdateCommandPreview();
            }
        }

        private async void DetectDevice_Click(object s, RoutedEventArgs e)
        {
            DeviceStatusText.Text = "Checking...";
            var result = await RunProcessAsync(ToolsManager.AdbExe, "devices", 15000);

            var hasDevice = result.Out
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(l => l.EndsWith("\tdevice", StringComparison.OrdinalIgnoreCase));

            DeviceStatusText.Text = hasDevice ? "Connected" : "Not found";
            DeviceStatusText.Foreground = hasDevice
                ? new SolidColorBrush(Color.FromRgb(77, 255, 154))
                : new SolidColorBrush(Color.FromRgb(255, 122, 122));

            AppendLog("adb devices");
            if (!string.IsNullOrWhiteSpace(result.Out)) AppendLog(result.Out.Trim());
            if (!string.IsNullOrWhiteSpace(result.Err)) AppendLog(result.Err.Trim());
        }

        private async void StartFlashing_Click(object s, RoutedEventArgs e)
        {
            if (_mode == FlashMode.Fastboot)
            {
                var sel = _fbRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).ToList();
                if (sel.Count == 0) { AppendLog("No files selected."); return; }
                await FlashFastbootAsync(sel);
                return;
            }

            if (_mode == FlashMode.Odin)
            {
                var sel = _odinRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).ToList();
                if (sel.Count == 0) { AppendLog("No files selected."); return; }
                await StartOdinAsync(sel);
                return;
            }

            if (_mode == FlashMode.Sideload)
            {
                await StartSideloadInternal();
                return;
            }

            AppendLog("This mode has no main flash action.");
        }

        private async Task FlashFastbootAsync(List<FlashRow> rows)
        {
            foreach (var row in rows)
            {
                var args = $"flash {row.Key.ToLowerInvariant()} \"{row.FilePath}\"";
                AppendLog($"> fastboot {args}");

                var r = await RunProcessAsync(ToolsManager.FastbootExe, args, 30 * 60 * 1000);
                if (r.Code != 0)
                {
                    AppendLog($"[{row.Label}] FAILED ({r.Code})");
                    if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.Trim());
                    if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.Trim());
                    return;
                }

                AppendLog($"[{row.Label}] done.");
            }

            AppendLog("Fastboot flashing complete.");
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

            if (AutoRebootChk?.IsChecked != true)
                sb.Append(" --no-reboot");

            return sb.ToString().Trim();
        }

        private async Task StartOdinAsync(List<FlashRow> rows)
        {
            string exe = ToolsManager.GetExePath("odin", "ekoflash");
            if (!File.Exists(exe))
            {
                AppendLog($"ERROR: ekoflash.exe not found at: {exe}");
                return;
            }

            var args = BuildOdinCommandArgs(rows);
            if (string.IsNullOrWhiteSpace(args))
            {
                AppendLog("No Odin files selected.");
                return;
            }

            if (RepartitionChk?.IsChecked == true)
                AppendLog("Note: Re-Partition option ignored (not supported by current ekoflash CLI).");
            if (FResetTimeChk?.IsChecked == true)
                AppendLog("Note: F. Reset Time option ignored (not supported by current ekoflash CLI).");

            AppendLog($"Running: \"{exe}\" {args}");
            var r = await RunProcessAsync(exe, args, 30 * 60 * 1000);

            if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.Trim());
            if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.Trim());

            AppendLog(r.Code == 0 ? "Odin operation completed successfully." : $"Odin operation failed with code: {r.Code}");
        }

        private async Task StartSideloadInternal()
        {
            var path = SideloadPathBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(path))
            {
                AppendLog("Select an OTA ZIP first.");
                return;
            }

            AppendLog($"> adb sideload \"{path}\"");
            var r = await RunProcessAsync(ToolsManager.AdbExe, $"sideload \"{path}\"", 30 * 60 * 1000);
            if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.Trim());
            if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.Trim());
            AppendLog(r.Code == 0 ? "Sideload complete ✓" : "Sideload FAILED.");
        }

        // Tools panel handlers in XAML
        private async void CmdRebootSys_Click(object sender, RoutedEventArgs e)
        {
            var r = await RunProcessAsync(ToolsManager.AdbExe, "reboot", 15000);
            if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.Trim());
            if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.Trim());
        }

        private async void CmdRebootBl_Click(object sender, RoutedEventArgs e)
        {
            var r = await RunProcessAsync(ToolsManager.AdbExe, "reboot bootloader", 15000);
            if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.Trim());
            if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.Trim());
        }

        private async void CmdRebootRec_Click(object sender, RoutedEventArgs e)
        {
            var r = await RunProcessAsync(ToolsManager.AdbExe, "reboot recovery", 15000);
            if (!string.IsNullOrWhiteSpace(r.Out)) AppendLog(r.Out.Trim());
            if (!string.IsNullOrWhiteSpace(r.Err)) AppendLog(r.Err.Trim());
        }

        private void LaunchZadig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var exe = ToolsManager.GetExePath("zadig", "zadig");
                if (!File.Exists(exe))
                {
                    AppendLog("zadig.exe not found.");
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true
                });

                AppendLog("zadig launched.");
            }
            catch (Exception ex)
            {
                AppendLog($"Launch failed: {ex.Message}");
            }
        }

        // Backup handlers (if backup panel exists)
        private async void LoadApps_Click(object sender, RoutedEventArgs e)
        {
            _backupApps.Clear();
            var apps = await BackupService.GetInstalledAppsAsync(AppendLog);
            foreach (var a in apps)
                _backupApps.Add(new BackupAppRow { PackageName = a.PackageName, DisplayName = a.DisplayName });
            AppendLog($"Apps loaded: {_backupApps.Count}");
        }

        private void StartBackup_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Backup start requested.");
        }

        private void UpdateCommandPreview()
        {
            var lines = new List<string>();

            if (_mode == FlashMode.Fastboot)
            {
                lines.AddRange(_fbRows
                    .Where(r => !string.IsNullOrWhiteSpace(r.FilePath))
                    .Select(r => $"fastboot flash {r.Key.ToLowerInvariant()} \"{r.FilePath}\""));
            }
            else if (_mode == FlashMode.Odin)
            {
                var selected = _odinRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).ToList();
                var args = BuildOdinCommandArgs(selected);
                lines.Add(string.IsNullOrWhiteSpace(args) ? "No command queued." : $"ekoflash {args}");
            }
            else if (_mode == FlashMode.Sideload)
            {
                if (!string.IsNullOrWhiteSpace(SideloadPathBox.Text))
                    lines.Add($"adb sideload \"{SideloadPathBox.Text}\"");
                else
                    lines.Add("No command queued.");
            }
            else
            {
                lines.Add("No command queued.");
            }

            CommandPreviewBox.Text = string.Join(Environment.NewLine, lines);
        }

        private void AppendLog(string msg)
        {
            if (!_uiReady || LogBox == null) return;
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            LogBox.ScrollToEnd();
        }

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

                p.OutputDataReceived += (_, e) => { if (e.Data != null) o.AppendLine(e.Data); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) er.AppendLine(e.Data); };

                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                using var cts = new CancellationTokenSource(ms);
                try
                {
                    await p.WaitForExitAsync(cts.Token);
                }
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
        private void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
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
