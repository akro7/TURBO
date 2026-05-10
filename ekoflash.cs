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
            Tools
        }

        private static readonly string[] OdinPids =
        {
            "6601",
            "685D",
            "68C3"
        };

        private FlashMode _mode = FlashMode.Fastboot;
        private readonly ObservableCollection<FlashRow> _fbRows = new();
        private readonly ObservableCollection<FlashRow> _odinRows = new();

        private bool _deviceConnected;
        private bool _deviceChecked;
        private CancellationTokenSource? _cts;

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

        private void SetModeButtonVisual()
        {
            var active = (Brush)Resources["AccentSoftBrush"];
            var inactive = new SolidColorBrush(Color.FromArgb(0x15, 0x20, 0x33, 0x00));

            FastbootBtn.Background = _mode == FlashMode.Fastboot ? active : inactive;
            OdinBtn.Background = _mode == FlashMode.Odin ? active : inactive;
            SideloadBtn.Background = _mode == FlashMode.Sideload ? active : inactive;
            ToolsBtn.Background = _mode == FlashMode.Tools ? active : inactive;
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

            bool found;

            switch (_mode)
            {
                case FlashMode.Fastboot:
                    found = await DetectFastbootAsync();
                    break;

                case FlashMode.Odin:
                    found = await DetectOdinDownloadModeAsync();
                    break;

                case FlashMode.Sideload:
                    found = await DetectAdbAsync(allowSideload: true);
                    break;

                default:
                    found = await DetectAdbAsync(allowSideload: false);
                    break;
            }

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

                if (_mode == FlashMode.Odin)
                {
                    AppendLog("[ERR] No download-mode device found. Check Samsung driver / cable / USB port.");
                }
                else
                {
                    AppendLog("[ERR] No device found. Check cable / drivers.");
                }
            }
        }

        private async Task<bool> DetectFastbootAsync()
        {
            var result = await RunAsync("platform-tools", "fastboot", "devices");
            var lines = SplitLines(result.Out);

            return lines.Any(l => l.Contains("\tfastboot", StringComparison.OrdinalIgnoreCase));
        }

        private async Task<bool> DetectAdbAsync(bool allowSideload)
        {
            var result = await RunAsync("platform-tools", "adb", "devices");
            var lines = SplitLines(result.Out);

            foreach (var line in lines)
            {
                if (line.Contains("\tdevice", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (allowSideload && line.Contains("\tsideload", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private async Task<bool> DetectOdinDownloadModeAsync()
        {
            // 1) Primary: ekoflash --list
            var listResult = await RunAsync("odin", "ekoflash", "--list");
            var listText = $"{listResult.Out}\n{listResult.Err}";

            if (LooksLikeOdinDeviceFound(listText))
                return true;

            if (LooksLikeNoOdinDevice(listText))
                return false;

            // 2) Secondary: ekoflash detect (for builds that still support it)
            var detectResult = await RunAsync("odin", "ekoflash", "detect");
            var detectText = $"{detectResult.Out}\n{detectResult.Err}";

            if (LooksLikeOdinDeviceFound(detectText))
                return true;

            if (LooksLikeNoOdinDevice(detectText))
                return false;

            // 3) Final fallback: scan Windows connected PnP devices for Samsung Odin VID/PID
            return await DetectOdinByPnpUtilAsync();
        }

        private static bool LooksLikeOdinDeviceFound(string text)
        {
            var t = text.ToLowerInvariant();

            if (t.Contains("device detected")) return true;
            if (t.Contains("detected") && t.Contains("download")) return true;
            if (t.Contains("odin mode")) return true;

            // Matches patterns like:
            // VID_04E8&PID_685D
            // 04e8:685d
            if (Regex.IsMatch(t, @"vid[_:\s=]*04e8.*pid[_:\s=]*(6601|685d|68c3)", RegexOptions.IgnoreCase))
                return true;

            if (Regex.IsMatch(t, @"04e8[:\s](6601|685d|68c3)", RegexOptions.IgnoreCase))
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
            // Uses built-in Windows pnputil without touching your tool paths.
            var res = await RunAsync("", "cmd", "/c pnputil /enum-devices /connected");
            var txt = $"{res.Out}\n{res.Err}".ToUpperInvariant();

            if (!txt.Contains("VID_04E8"))
                return false;

            return OdinPids.Any(pid => txt.Contains($"PID_{pid}"));
        }

        private static IEnumerable<string> SplitLines(string text) =>
            text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

        private void Browse_Click(object s, RoutedEventArgs e)
        {
            if (s is not Button btn) return;

            string key = btn.Tag?.ToString() ?? "";

            if (key == "sideload")
            {
                var dlg = new OpenFileDialog { Filter = "ZIP files|*.zip|All files|*.*" };
                if (dlg.ShowDialog() == true) SideloadPathBox.Text = dlg.FileName;
                return;
            }

            var fbRow = _fbRows.FirstOrDefault(r => r.Key == key);
            if (fbRow != null)
            {
                var dlg = new OpenFileDialog { Filter = "Image files|*.img;*.bin|All files|*.*" };
                if (dlg.ShowDialog() == true) fbRow.FilePath = dlg.FileName;
                return;
            }

            var odinRow = _odinRows.FirstOrDefault(r => r.Key == key);
            if (odinRow != null)
            {
                var dlg = new OpenFileDialog { Filter = "TAR/MD5 files|*.tar;*.md5;*.tar.md5|All files|*.*" };
                if (dlg.ShowDialog() == true) odinRow.FilePath = dlg.FileName;
            }
        }

        private async void FlashAll_Click(object s, RoutedEventArgs e)
        {
            if (!_deviceConnected)
            {
                AppendLog("[!] Scan device first.");
                return;
            }

            _cts = new CancellationTokenSource();
            MainProgress.Value = 0;
            ProgressStatusText.Text = "Flashing...";

            try
            {
                switch (_mode)
                {
                    case FlashMode.Fastboot:
                        await FlashFastboot(_cts.Token);
                        break;
                    case FlashMode.Odin:
                        await FlashOdin(_cts.Token);
                        break;
                    case FlashMode.Sideload:
                        await FlashSideload(_cts.Token);
                        break;
                    default:
                        AppendLog("[!] Switch to Fastboot, Odin, or Sideload mode to flash.");
                        return;
                }

                AppendLog("[OK] All operations finished.");
                ProgressStatusText.Text = "Done.";
                MainProgress.Value = 100;
            }
            catch (OperationCanceledException)
            {
                AppendLog("[STOP] Cancelled.");
                ProgressStatusText.Text = "Cancelled.";
            }
        }

        private async Task FlashFastboot(CancellationToken ct)
        {
            var targets = _fbRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).ToList();

            if (targets.Count == 0)
            {
                AppendLog("[!] No files selected.");
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var row = targets[i];
                AppendLog($"[FLASH] {row.Label} <- {row.FilePath}");

                var res = await RunAsync("platform-tools", "fastboot", $"flash {row.Key} \"{row.FilePath}\"", ct);

                if (!string.IsNullOrWhiteSpace(res.Out)) AppendLog(res.Out.Trim());
                if (!string.IsNullOrWhiteSpace(res.Err)) AppendLog($"[ERR] {res.Err.Trim()}");

                MainProgress.Value = (double)(i + 1) / targets.Count * 100;
            }
        }

        private async Task FlashOdin(CancellationToken ct)
        {
            var targets = _odinRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).ToList();

            if (targets.Count == 0)
            {
                AppendLog("[!] No Odin files selected.");
                return;
            }

            var sb = new StringBuilder();
            foreach (var r in targets)
            {
                sb.Append($"--{r.Key.ToLowerInvariant()} \"{r.FilePath}\" ");
            }

            AppendLog($"[ODIN] ekoflash {sb}");

            var res = await RunAsync("odin", "ekoflash", sb.ToString().Trim(), ct);

            if (!string.IsNullOrWhiteSpace(res.Out)) AppendLog(res.Out.Trim());
            if (!string.IsNullOrWhiteSpace(res.Err)) AppendLog($"[ERR] {res.Err.Trim()}");

            MainProgress.Value = 100;
        }

        private async Task FlashSideload(CancellationToken ct)
        {
            string zip = SideloadPathBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(zip))
            {
                AppendLog("[!] No ZIP selected.");
                return;
            }

            if (!File.Exists(zip))
            {
                AppendLog("[!] File not found.");
                return;
            }

            AppendLog($"[SIDELOAD] {zip}");

            var res = await RunAsync("platform-tools", "adb", $"sideload \"{zip}\"", ct);

            if (!string.IsNullOrWhiteSpace(res.Out)) AppendLog(res.Out.Trim());
            if (!string.IsNullOrWhiteSpace(res.Err)) AppendLog($"[ERR] {res.Err.Trim()}");

            MainProgress.Value = 100;
        }

        private async void FlashOne_Click(object s, RoutedEventArgs e)
        {
            if (!_deviceConnected)
            {
                AppendLog("[!] Scan device first.");
                return;
            }

            if (s is not Button btn) return;

            string key = btn.Tag?.ToString() ?? "";
            var row = _fbRows.FirstOrDefault(r => r.Key == key);

            if (row == null || string.IsNullOrWhiteSpace(row.FilePath))
            {
                AppendLog($"[!] No file for {key}.");
                return;
            }

            AppendLog($"[FLASH] {row.Label} <- {row.FilePath}");

            var res = await RunAsync("platform-tools", "fastboot", $"flash {row.Key} \"{row.FilePath}\"");

            if (!string.IsNullOrWhiteSpace(res.Out)) AppendLog(res.Out.Trim());
            if (!string.IsNullOrWhiteSpace(res.Err)) AppendLog($"[ERR] {res.Err.Trim()}");
        }

        private void Cancel_Click(object s, RoutedEventArgs e)
        {
            _cts?.Cancel();
            AppendLog("[STOP] Cancelled by user.");
        }

        private async void QuickCmd_Click(object s, RoutedEventArgs e)
        {
            if (s is not Button btn) return;

            string cmd = btn.Tag?.ToString() ?? "";

            if (cmd == "zadig")
            {
                // Keep original extracted path mapping (folder is "zadig" in ToolsManager).
                string path = ToolsManager.GetExePath("zadig", "zadig");
                if (File.Exists(path))
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                else
                    AppendLog("[!] zadig.exe not found in zadig\\");
                return;
            }

            string[] parts = cmd.Split(new[] { ' ' }, 2);
            string exe = parts[0];
            string args = parts.Length > 1 ? parts[1] : "";

            AppendLog($"[CMD] {cmd}");

            var res = await RunAsync("platform-tools", exe, args);

            if (!string.IsNullOrWhiteSpace(res.Out)) AppendLog(res.Out.Trim());
            if (!string.IsNullOrWhiteSpace(res.Err)) AppendLog($"[ERR] {res.Err.Trim()}");
        }

        private void UpdateCommandPreview()
        {
            if (CommandPreviewBox == null) return;

            CommandPreviewBox.Text = _mode switch
            {
                FlashMode.Fastboot => BuildFastbootPreview(),
                FlashMode.Odin => BuildOdinPreview(),
                FlashMode.Sideload => $"adb sideload \"{SideloadPathBox?.Text}\"",
                _ => "Select a mode above."
            };
        }

        private string BuildFastbootPreview()
        {
            var lines = _fbRows
                .Where(r => !string.IsNullOrWhiteSpace(r.FilePath))
                .Select(r => $"fastboot flash {r.Key} \"{r.FilePath}\"");

            return lines.Any() ? string.Join("\n", lines) : "fastboot flash";
        }

        private string BuildOdinPreview()
        {
            var parts = _odinRows
                .Where(r => !string.IsNullOrWhiteSpace(r.FilePath))
                .Select(r => $"--{r.Key.ToLowerInvariant()} \"{r.FilePath}\"");

            return parts.Any() ? "ekoflash " + string.Join(" ", parts) : "ekoflash --bl --ap ...";
        }

        private void AppendLog(string msg) =>
            Dispatcher.Invoke(() =>
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
                    string path = ToolsManager.GetExePath(dir, exe);
                    if (!File.Exists(path))
                        path = exe;

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
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
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
            set
            {
                _fp = value;
                OnPropertyChanged();
            }
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
