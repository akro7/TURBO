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

        private string _pitFilePath = string.Empty;
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
            AppendLog($"platform-tools (adb)      : {(File.Exists(ToolsManager.AdbExe) ? "ready" : "missing")}");
            AppendLog($"platform-tools (fastboot) : {(File.Exists(ToolsManager.FastbootExe) ? "ready" : "missing")}");
            AppendLog($"odin engine (ekoflash)    : {(File.Exists(ToolsManager.EkoFlashExe) ? "ready" : "missing")}");
            AppendLog($"zadig                     : {(File.Exists(ToolsManager.ZadigExe) ? "ready" : "missing")}");
            UpdateCommandPreview();
        }

        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string theme)
                ApplyTheme(theme);
        }

        private void ApplyTheme(string theme)
        {
            var colors = new Dictionary<string, (Color ac, Color acSoft)>
            {
                ["Blue"] = (Color.FromRgb(0x37, 0xCF, 0xFF), Color.FromRgb(0x30, 0x5A, 0x89)),
                ["Purple"] = (Color.FromRgb(0xB6, 0x7B, 0xFF), Color.FromRgb(0x5E, 0x46, 0x86)),
                ["Emerald"] = (Color.FromRgb(0x43, 0xF2, 0xC2), Color.FromRgb(0x1F, 0x6A, 0x59)),
                ["Crimson"] = (Color.FromRgb(0xFF, 0x6D, 0xA8), Color.FromRgb(0x7D, 0x2D, 0x4E)),
                ["Gold"] = (Color.FromRgb(0xFF, 0xB8, 0x30), Color.FromRgb(0x7A, 0x56, 0x1A))
            };

            if (!colors.TryGetValue(theme, out var c)) return;
            Resources["Accent"] = new SolidColorBrush(c.ac);
            Resources["Accent2"] = new SolidColorBrush(c.acSoft);
        }

        private void TabCmd_Click(object sender, RoutedEventArgs e) => ShowTab("cmd");
        private void TabOptions_Click(object sender, RoutedEventArgs e) => ShowTab("options");

        private void ShowTab(string tab)
        {
            if (TabCmdPanel == null || TabOptionsPanel == null) return;
            TabCmdPanel.Visibility = tab == "cmd" ? Visibility.Visible : Visibility.Collapsed;
            TabOptionsPanel.Visibility = tab == "options" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void FastbootMode_Click(object sender, RoutedEventArgs e) => SwitchMode(FlashMode.Fastboot);
        private void OdinMode_Click(object sender, RoutedEventArgs e) => SwitchMode(FlashMode.Odin);
        private void SideloadMode_Click(object sender, RoutedEventArgs e) => SwitchMode(FlashMode.Sideload);
        private void ToolsMode_Click(object sender, RoutedEventArgs e) => SwitchMode(FlashMode.Tools);
        private void BackupRestoreMode_Click(object sender, RoutedEventArgs e) => SwitchMode(FlashMode.BackupRestore);

        private void SwitchMode(FlashMode mode)
        {
            _mode = mode;
            if (!_uiReady) return;

            PanelFastboot.Visibility = mode == FlashMode.Fastboot ? Visibility.Visible : Visibility.Collapsed;
            PanelOdin.Visibility = mode == FlashMode.Odin ? Visibility.Visible : Visibility.Collapsed;
            PanelSideload.Visibility = mode == FlashMode.Sideload ? Visibility.Visible : Visibility.Collapsed;
            PanelTools.Visibility = mode == FlashMode.Tools ? Visibility.Visible : Visibility.Collapsed;

            if (FindName("PanelBackupRestore") is Grid backupPanel)
                backupPanel.Visibility = mode == FlashMode.BackupRestore ? Visibility.Visible : Visibility.Collapsed;

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
            foreach (var slot in new[] { "BL", "AP", "CP", "CSC", "USERDATA" })
                _odinRows.Add(new FlashRow { Key = slot, Label = slot });
        }

        private void FbBrowse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var dlg = new OpenFileDialog { Filter = "Image Files|*.img;*.bin|All Files|*.*" };
            if (dlg.ShowDialog() != true) return;

            var row = _fbRows.FirstOrDefault(r => r.Key == key);
            if (row != null) row.FilePath = dlg.FileName;
            UpdateCommandPreview();
        }

        private void OdinBrowse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string key) return;
            var dlg = new OpenFileDialog { Filter = "Odin Files|*.tar;*.md5;*.img;*.bin|All Files|*.*" };
            if (dlg.ShowDialog() != true) return;

            var row = _odinRows.FirstOrDefault(r => r.Key == key);
            if (row != null) row.FilePath = dlg.FileName;
            UpdateCommandPreview();
        }

        private void BrowsePit_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PIT Files|*.pit|All Files|*.*" };
            if (dlg.ShowDialog() != true) return;

            _pitFilePath = dlg.FileName;
            PitPathBox.Text = _pitFilePath;
            UpdateCommandPreview();
        }

        private void ClearPit_Click(object sender, RoutedEventArgs e)
        {
            _pitFilePath = string.Empty;
            PitPathBox.Text = string.Empty;
            UpdateCommandPreview();
        }

        private void SideloadBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "ZIP Files|*.zip|All Files|*.*" };
            if (dlg.ShowDialog() != true) return;

            SideloadPathBox.Text = dlg.FileName;
            UpdateCommandPreview();
        }

        private async void DetectDevice_Click(object sender, RoutedEventArgs e)
        {
            DeviceStatusText.Text = "Checking...";
            var result = await RunProcessAsync(ToolsManager.AdbExe, "devices", 15000);

            var hasDevice = result.Out
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(l => l.EndsWith("\tdevice", StringComparison.OrdinalIgnoreCase));

            DeviceStatusText.Text = hasDevice ? "Connected" : "Not found";

            AppendLog("adb devices");
            if (!string.IsNullOrWhiteSpace(result.Out)) AppendLog(result.Out.Trim());
            if (!string.IsNullOrWhiteSpace(result.Err)) AppendLog(result.Err.Trim());
        }

        private async void StartFlashing_Click(object sender, RoutedEventArgs e)
        {
            switch (_mode)
            {
                case FlashMode.Odin:
                    await StartOdinAsync();
                    break;
                case FlashMode.Fastboot:
                    AppendLog("Fastboot operation requested.");
                    break;
                case FlashMode.Sideload:
                    AppendLog("Sideload operation requested.");
                    break;
                case FlashMode.Tools:
                    AppendLog("Tools action requested.");
                    break;
                case FlashMode.BackupRestore:
                    AppendLog("Backup/Restore action requested.");
                    break;
            }
        }

        private async Task StartOdinAsync()
        {
            var exe = ToolsManager.EkoFlashExe;
            if (!File.Exists(exe))
            {
                AppendLog($"ERROR: ekoflash.exe not found at: {exe}");
                return;
            }

            var args = BuildOdinCommandArgs();
            if (string.IsNullOrWhiteSpace(args))
            {
                AppendLog("No Odin files selected.");
                return;
            }

            AppendLog($"Running: \"{exe}\" {args}");
            var result = await RunProcessAsync(exe, args, 30 * 60 * 1000);

            if (!string.IsNullOrWhiteSpace(result.Out)) AppendLog(result.Out.Trim());
            if (!string.IsNullOrWhiteSpace(result.Err)) AppendLog(result.Err.Trim());

            AppendLog(result.Code == 0
                ? "Odin operation completed successfully."
                : $"Odin operation failed with code: {result.Code}");
        }

        private string BuildOdinCommandArgs()
        {
            var sb = new StringBuilder();

            foreach (var row in _odinRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)))
            {
                switch (row.Key)
                {
                    case "BL":
                        sb.Append($" --bl \"{row.FilePath}\"");
                        break;
                    case "AP":
                        sb.Append($" --ap \"{row.FilePath}\"");
                        break;
                    case "CP":
                        sb.Append($" --cp \"{row.FilePath}\"");
                        break;
                    case "CSC":
                        sb.Append($" --csc \"{row.FilePath}\"");
                        break;
                    case "USERDATA":
                        sb.Append($" --userdata \"{row.FilePath}\"");
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(_pitFilePath))
                sb.Append($" --pit \"{_pitFilePath}\"");

            if (RepartitionChk.IsChecked == true) sb.Append(" --repartition");
            if (AutoRebootChk.IsChecked == true) sb.Append(" --auto-reboot");
            if (FResetTimeChk.IsChecked == true) sb.Append(" --f-reset-time");

            return sb.ToString().Trim();
        }

        private void UpdateCommandPreview()
        {
            if (CommandPreviewBox == null) return;

            switch (_mode)
            {
                case FlashMode.Odin:
                    CommandPreviewBox.Text = $"\"{ToolsManager.EkoFlashExe}\" {BuildOdinCommandArgs()}";
                    break;
                case FlashMode.Fastboot:
                    CommandPreviewBox.Text = $"\"{ToolsManager.FastbootExe}\" flash <partition> <image>";
                    break;
                case FlashMode.Sideload:
                    CommandPreviewBox.Text = $"\"{ToolsManager.AdbExe}\" sideload \"{SideloadPathBox?.Text}\"";
                    break;
                case FlashMode.Tools:
                    CommandPreviewBox.Text = "Utility commands (reboot / zadig / adb).";
                    break;
                default:
                    CommandPreviewBox.Text = "Backup and restore operations.";
                    break;
            }
        }

        private async void CmdRebootSys_Click(object sender, RoutedEventArgs e) => await RunAndLogAsync(ToolsManager.AdbExe, "reboot");
        private async void CmdRebootBl_Click(object sender, RoutedEventArgs e) => await RunAndLogAsync(ToolsManager.AdbExe, "reboot bootloader");
        private async void CmdRebootRec_Click(object sender, RoutedEventArgs e) => await RunAndLogAsync(ToolsManager.AdbExe, "reboot recovery");

        private void LaunchZadig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(ToolsManager.ZadigExe))
                {
                    AppendLog("zadig.exe not found.");
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = ToolsManager.ZadigExe,
                    UseShellExecute = true
                });

                AppendLog("zadig.exe launched.");
            }
            catch (Exception ex)
            {
                AppendLog($"Failed to launch zadig: {ex.Message}");
            }
        }

        private async void LoadApps_Click(object sender, RoutedEventArgs e)
        {
            _backupApps.Clear();
            AppendLog("Loading installed apps...");
            var apps = await BackupService.GetInstalledAppsAsync(AppendLog);
            foreach (var app in apps)
                _backupApps.Add(new BackupAppRow { PackageName = app.PackageName, DisplayName = app.DisplayName });

            AppendLog($"Loaded {apps.Count} apps.");
        }

        private void StartBackup_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Backup start requested.");
        }

        private async Task RunAndLogAsync(string fileName, string args)
        {
            AppendLog($"Run: \"{fileName}\" {args}");
            var result = await RunProcessAsync(fileName, args, 120000);

            if (!string.IsNullOrWhiteSpace(result.Out)) AppendLog(result.Out.Trim());
            if (!string.IsNullOrWhiteSpace(result.Err)) AppendLog(result.Err.Trim());

            AppendLog($"Exit code: {result.Code}");
        }

        private static async Task<(int Code, string Out, string Err)> RunProcessAsync(string fileName, string args, int timeoutMs)
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
                var output = new StringBuilder();
                var error = new StringBuilder();

                p.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };

                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                var waitTask = p.WaitForExitAsync();
                var delayTask = Task.Delay(timeoutMs);
                var completed = await Task.WhenAny(waitTask, delayTask);

                if (completed == delayTask)
                {
                    try { p.Kill(true); } catch { }
                    return (-1, output.ToString(), $"Timeout after {timeoutMs / 1000}s");
                }

                return (p.ExitCode, output.ToString(), error.ToString());
            }
            catch (Exception ex)
            {
                return (-1, string.Empty, ex.Message);
            }
        }

        private void AppendLog(string message)
        {
            if (LogBox == null) return;
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogBox.ScrollToEnd();
        }
    }

    public class FlashRow : INotifyPropertyChanged
    {
        private string _filePath = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;

        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class BackupAppRow
    {
        public bool IsSelected { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class BackupSetRow
    {
        public string BackupPath { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
