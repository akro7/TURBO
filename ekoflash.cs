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
        private enum FlashMode
        {
            Fastboot,
            Odin,
            Sideload,
            Tools
        }

        private static readonly Dictionary<string, string> OdinArgMap =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["BL"] = "-b",
                ["AP"] = "-a",
                ["CP"] = "-c",
                ["CSC"] = "-s",
                ["USERDATA"] = "-u"
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

            InitThemeControls();
            BuildFastbootRows();
            BuildOdinRows();

            RowsList.ItemsSource = _fbRows;
            OdinRowsList.ItemsSource = _odinRows;

            SwitchMode(FlashMode.Fastboot);
            ShowTab("cmd");
            CheckRequirements();
            UpdateCommandPreview();

            AppendLog("===============================================");
            AppendLog(" TURBO FLASH TOOL READY");
            AppendLog("===============================================");
        }

        private void InitThemeControls()
        {
            var app = (App)Application.Current;
            var cfg = app.CurrentThemeConfig;

            ThemeCombo.SelectedIndex = cfg.Theme switch
            {
                "LiquidGlass" => 1,
                "Material" => 2,
                _ => 0
            };

            ModeCombo.SelectedIndex = cfg.ColorMode == "Light" ? 1 : 0;

            AccentCombo.SelectedIndex = cfg.Accent switch
            {
                "Violet" => 1,
                "Blue" => 2,
                "Green" => 3,
                "Orange" => 4,
                "Red" => 5,
                "Pink" => 6,
                _ => 0
            };

            SetModeButtonVisual();
        }

        private void ApplyTheme_Click(object sender, RoutedEventArgs e)
        {
            string theme = ((ComboBoxItem)ThemeCombo.SelectedItem).Content.ToString() ?? "Classic";
            string mode = ((ComboBoxItem)ModeCombo.SelectedItem).Content.ToString() ?? "Dark";
            string accent = ((ComboBoxItem)AccentCombo.SelectedItem).Content.ToString() ?? "Cyan";

            var app = (App)Application.Current;
            app.ApplyTheme(new ThemeConfig
            {
                Theme = theme,
                ColorMode = mode,
                Accent = accent
            });

            SetModeButtonVisual();
            AppendLog($"[THEME] {theme} / {mode} / {accent}");
        }

        private void CheckRequirements()
        {
            AppendLog($"[SYSTEM] ADB : {Chk("platform-tools", "adb")}");
            AppendLog($"[SYSTEM] Fastboot : {Chk("platform-tools", "fastboot")}");
            AppendLog($"[SYSTEM] ekoflash : {Chk("odin", "ekoflash")}");
        }

        private static string Chk(string dir, string exe) =>
            ToolsManager.ExeExists(dir, exe) ? "OK" : "MISSING";

        private void ShowTab(string tab)
        {
            TabCmdPanel.Visibility = tab == "cmd" ? Visibility.Visible : Visibility.Collapsed;
            TabOptionsPanel.Visibility = tab == "options" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TabCmd_Click(object sender, RoutedEventArgs e) => ShowTab("cmd");
        private void TabOptions_Click(object sender, RoutedEventArgs e) => ShowTab("options");

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

            AppendLog($"[MODE] {mode.ToString().ToUpperInvariant()}");
        }

        private void FastbootMode_Click(object sender, RoutedEventArgs e) => SwitchMode(FlashMode.Fastboot);
        private void OdinMode_Click(object sender, RoutedEventArgs e) => SwitchMode(FlashMode.Odin);
        private void SideloadMode_Click(object sender, RoutedEventArgs e) => SwitchMode(FlashMode.Sideload);
        private void ToolsMode_Click(object sender, RoutedEventArgs e) => SwitchMode(FlashMode.Tools);

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
                var row = new FlashRow { Key = p, Label = p.ToUpperInvariant() };
                row.PropertyChanged += (_, _) => UpdateCommandPreview();
                _fbRows.Add(row);
            }
        }

        private void BuildOdinRows()
        {
            _odinRows.Clear();
            foreach (var s in new[] { "BL", "AP", "CP", "CSC", "USERDATA" })
            {
                var row = new FlashRow { Key = s, Label = s };
                row.PropertyChanged += (_, _) => UpdateCommandPreview();
                _odinRows.Add(row);
            }
        }

        private async void DetectDevice_Click(object sender, RoutedEventArgs e)
        {
            DeviceStatusText.Text = "SCANNING...";
            DeviceStatusText.Foreground = (Brush)Resources["AccentBrush"];

            bool found = _mode switch
            {
                FlashMode.Fastboot => await DetectFastbootAsync(),
                FlashMode.Odin => await DetectOdinAsync(),
                FlashMode.Sideload => await DetectAdbAsync(true),
                _ => await DetectAdbAsync(false)
            };

            _deviceConnected = found;
            _deviceChecked = true;

            if (found)
            {
                DeviceStatusText.Text = "CONNECTED";
                DeviceStatusText.Foreground = Brushes.LimeGreen;
                AppendLog("[OK] Device detected");
            }
            else
            {
                DeviceStatusText.Text = "NOT FOUND";
                DeviceStatusText.Foreground = Brushes.Red;
                AppendLog("[ERR] No device found");
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

        private async Task<bool> DetectOdinAsync()
        {
            var res = await RunAsync("", "cmd", "/c pnputil /enum-devices /connected");
            var txt = $"{res.Out}\n{res.Err}".ToUpperInvariant();
            return txt.Contains("VID_04E8");
        }

        private static IEnumerable<string> SplitLines(string text) =>
            text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

        private static bool IsToolDeviceLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            var l = line.Trim();
            if (l.StartsWith("List of devices attached", StringComparison.OrdinalIgnoreCase))
                return false;

            var parts = l.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2;
        }

        private static string GetStateFromToolLine(string line)
        {
            var parts = line.Trim().Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? parts[^1] : string.Empty;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn)
                return;

            var key = btn.Tag?.ToString() ?? "";

            if (key == "sideload")
            {
                var dlg = new OpenFileDialog { Filter = "ZIP files|*.zip|All files|*.*" };
                if (dlg.ShowDialog() == true)
                    SideloadPathBox.Text = dlg.FileName;

                UpdateCommandPreview();
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

        private async void FlashAll_Click(object sender, RoutedEventArgs e)
        {
            if (!_deviceChecked)
            {
                AppendLog("[!] Scan device first");
                return;
            }

            if (!_deviceConnected)
            {
                AppendLog("[!] Device not connected");
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
                    MainProgress.Value = 100;
                    ProgressStatusText.Text = "Done";
                    AppendLog("[OK] Completed");
                }
                else
                {
                    ProgressStatusText.Text = "Failed";
                    AppendLog("[ERR] Failed");
                }
            }
            catch (OperationCanceledException)
            {
                ProgressStatusText.Text = "Canceled";
                AppendLog("[STOP] Canceled");
            }
        }

        private async Task<bool> FlashFastboot(CancellationToken ct)
        {
            var targets = _fbRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).ToList();
            if (targets.Count == 0)
            {
                AppendLog("[!] No files selected");
                return false;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var row = targets[i];
                if (!File.Exists(row.FilePath))
                {
                    AppendLog($"[ERR] File missing: {row.FilePath}");
                    return false;
                }

                AppendLog($"[FLASH] {row.Key} -> {row.FilePath}");
                var res = await RunAsync("platform-tools", "fastboot", $"flash {row.Key} \"{row.FilePath}\"", ct);

                if (res.Code != 0)
                {
                    AppendLog($"[ERR] fastboot failed on {row.Key}");
                    if (!string.IsNullOrWhiteSpace(res.Err))
                        AppendLog(res.Err.Trim());
                    return false;
                }

                MainProgress.Value = (i + 1) * 100.0 / targets.Count;
            }

            if (AutoRebootCheck.IsChecked == true)
            {
                await RunAsync("platform-tools", "fastboot", "reboot", ct);
            }

            return true;
        }

        private async Task<bool> FlashOdin(CancellationToken ct)
        {
            var targets = _odinRows.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).ToList();
            if (targets.Count == 0)
            {
                AppendLog("[!] No Odin files selected");
                return false;
            }

            var sb = new StringBuilder();
            foreach (var row in targets)
            {
                if (!File.Exists(row.FilePath))
                {
                    AppendLog($"[ERR] File missing: {row.FilePath}");
                    return false;
                }

                if (!OdinArgMap.TryGetValue(row.Key, out var flag))
                {
                    AppendLog($"[ERR] Invalid Odin slot: {row.Key}");
                    return false;
                }

                sb.Append(flag).Append(' ').Append('"').Append(row.FilePath).Append("\" ");
            }

            var args = sb.ToString().Trim();
            AppendLog($"[ODIN] ekoflash {args}");

            var res = await RunAsync("odin", "ekoflash", args, ct);
            if (!string.IsNullOrWhiteSpace(res.Out))
                AppendLog(res.Out.Trim());

            if (!string.IsNullOrWhiteSpace(res.Err))
                AppendLog(res.Err.Trim());

            return res.Code == 0;
        }

        private async Task<bool> FlashSideload(CancellationToken ct)
        {
            var zip = SideloadPathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(zip))
            {
                AppendLog("[!] No ZIP selected");
                return false;
            }

            if (!File.Exists(zip))
            {
                AppendLog("[ERR] ZIP not found");
                return false;
            }

            var res = await RunAsync("platform-tools", "adb", $"sideload \"{zip}\"", ct);
            if (!string.IsNullOrWhiteSpace(res.Out))
                AppendLog(res.Out.Trim());

            if (!string.IsNullOrWhiteSpace(res.Err))
                AppendLog(res.Err.Trim());

            return res.Code == 0;
        }

        private async void FlashOne_Click(object sender, RoutedEventArgs e)
        {
            if (!_deviceChecked || !_deviceConnected)
            {
                AppendLog("[!] Scan and connect device first");
                return;
            }

            if (sender is not Button btn)
                return;

            var key = btn.Tag?.ToString() ?? "";
            var row = _fbRows.FirstOrDefault(r => r.Key == key);
            if (row == null || string.IsNullOrWhiteSpace(row.FilePath))
            {
                AppendLog($"[!] No file for {key}");
                return;
            }

            if (!File.Exists(row.FilePath))
            {
                AppendLog($"[ERR] File missing: {row.FilePath}");
                return;
            }

            AppendLog($"[FLASH ONE] {row.Key}");
            var res = await RunAsync("platform-tools", "fastboot", $"flash {row.Key} \"{row.FilePath}\"");

            if (!string.IsNullOrWhiteSpace(res.Out))
                AppendLog(res.Out.Trim());

            if (!string.IsNullOrWhiteSpace(res.Err))
                AppendLog(res.Err.Trim());
        }

        private async void QuickCmd_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string cmd)
                return;

            string exe;
            string args;
            string dir;

            var parts = cmd.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return;

            exe = parts[0];
            args = parts.Length > 1 ? parts[1] : "";
            dir = exe.Equals("fastboot", StringComparison.OrdinalIgnoreCase) ? "platform-tools" : "platform-tools";

            AppendLog($"[CMD] {cmd}");
            var res = await RunAsync(dir, exe, args);

            if (!string.IsNullOrWhiteSpace(res.Out))
                AppendLog(res.Out.Trim());

            if (!string.IsNullOrWhiteSpace(res.Err))
                AppendLog(res.Err.Trim());
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            AppendLog("[STOP] Cancel requested");
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
                _ => "Select a command from Tools"
            };
        }

        private string BuildFastbootPreview()
        {
            var lines = _fbRows
                .Where(r => !string.IsNullOrWhiteSpace(r.FilePath))
                .Select(r => $"fastboot flash {r.Key} \"{r.FilePath}\"");

            return lines.Any() ? string.Join(Environment.NewLine, lines) : "fastboot flash";
        }

        private string BuildOdinPreview()
        {
            var parts = _odinRows
                .Where(r => !string.IsNullOrWhiteSpace(r.FilePath))
                .Select(r =>
                {
                    var flag = OdinArgMap.TryGetValue(r.Key, out var f) ? f : "?";
                    return $"{flag} \"{r.FilePath}\"";
                });

            return parts.Any() ? "ekoflash " + string.Join(" ", parts) : "ekoflash -b -a -c -s -u ...";
        }

        private void AppendLog(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
                LogBox.ScrollToEnd();
                ProgressStatusText.Text = msg;
            });
        }

        private Task<ProcessResult> RunAsync(string dir, string exe, string args, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                var result = new ProcessResult();

                try
                {
                    string path = !string.IsNullOrWhiteSpace(dir) ? ToolsManager.GetExePath(dir, exe) : exe;
                    if (!File.Exists(path))
                        path = exe;

                    var psi = new ProcessStartInfo(path, args)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var p = Process.Start(psi);
                    if (p == null)
                    {
                        result.Code = -1;
                        result.Err = "Process failed to start";
                        return result;
                    }

                    result.Out = p.StandardOutput.ReadToEnd();
                    result.Err = p.StandardError.ReadToEnd();
                    p.WaitForExit();

                    result.Code = p.ExitCode;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result.Code = -1;
                    result.Err = ex.Message;
                }

                return result;
            }, ct);
        }
    }

    public class FlashRow : INotifyPropertyChanged
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";

        private string _filePath = "";
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

        protected void OnPropertyChanged([CallerMemberName] string? member = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(member));
        }
    }

    public class ProcessResult
    {
        public int Code { get; set; }
        public string Out { get; set; } = "";
        public string Err { get; set; } = "";
    }
}
