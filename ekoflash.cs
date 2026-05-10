using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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

        private sealed class ThemePalette
        {
            public Color Accent { get; init; }
            public Color AccentSoft { get; init; }
            public Color Border { get; init; }
            public Color MainBg { get; init; }
            public Color Panel { get; init; }
            public Color Card { get; init; }
            public Color Input { get; init; }
            public Color Text { get; init; }
            public Color TextMuted { get; init; }
            public Color ButtonBg { get; init; }
            public Color ButtonHover { get; init; }
            public Color LogBg { get; init; }
            public Color CmdBg { get; init; }
            public Color CmdFg { get; init; }
            public Color FlashText { get; init; }
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

        private bool _isMaterialMode;
        private string _currentClassicTheme = "Blue";

        public MainWindow()
        {
            InitializeComponent();

            InitMaterialDefaults();
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

        private void InitMaterialDefaults()
        {
            MaterialPrimaryBox.Text = "#6750A4";
            MaterialSurfaceBox.Text = "#FFFBFE";
            MaterialBackgroundBox.Text = "#F3EDF7";
            MaterialTextBox.Text = "#1C1B1F";
            MaterialModeCheck.IsChecked = false;
        }

        private static ThemePalette BuildClassicPalette(string name)
        {
            return name switch
            {
                "Blue" => new ThemePalette
                {
                    Accent = Color.FromRgb(0x00, 0xE5, 0xFF),
                    AccentSoft = Color.FromArgb(0x33, 0x00, 0xE5, 0xFF),
                    Border = Color.FromArgb(0x4C, 0x52, 0xBF, 0xFF),
                    MainBg = Color.FromRgb(0x05, 0x0A, 0x14),
                    Panel = Color.FromArgb(0xCC, 0x0D, 0x1B, 0x33),
                    Card = Color.FromRgb(0x10, 0x1B, 0x2F),
                    Input = Color.FromRgb(0x0F, 0x1A, 0x2B),
                    Text = Color.FromRgb(0xEA, 0xF4, 0xFF),
                    TextMuted = Color.FromRgb(0x88, 0xB4, 0xD8),
                    ButtonBg = Color.FromRgb(0x15, 0x20, 0x33),
                    ButtonHover = Color.FromRgb(0x1F, 0x2D, 0x47),
                    LogBg = Color.FromRgb(0x03, 0x07, 0x0F),
                    CmdBg = Color.FromRgb(0x08, 0x10, 0x1E),
                    CmdFg = Color.FromRgb(0x00, 0xFF, 0x95),
                    FlashText = Color.FromRgb(0x05, 0x0A, 0x14)
                },
                "Purple" => new ThemePalette
                {
                    Accent = Color.FromRgb(0xB6, 0x7B, 0xFF),
                    AccentSoft = Color.FromArgb(0x33, 0xB6, 0x7B, 0xFF),
                    Border = Color.FromArgb(0x4C, 0xB1, 0x86, 0xFF),
                    MainBg = Color.FromRgb(0x08, 0x06, 0x13),
                    Panel = Color.FromArgb(0xCC, 0x17, 0x12, 0x3A),
                    Card = Color.FromRgb(0x19, 0x15, 0x3B),
                    Input = Color.FromRgb(0x15, 0x12, 0x34),
                    Text = Color.FromRgb(0xF1, 0xE7, 0xFF),
                    TextMuted = Color.FromRgb(0xB4, 0x9F, 0xD8),
                    ButtonBg = Color.FromRgb(0x23, 0x1C, 0x49),
                    ButtonHover = Color.FromRgb(0x2F, 0x26, 0x5D),
                    LogBg = Color.FromRgb(0x0A, 0x08, 0x16),
                    CmdBg = Color.FromRgb(0x12, 0x0F, 0x26),
                    CmdFg = Color.FromRgb(0xC6, 0xA8, 0xFF),
                    FlashText = Color.FromRgb(0x0A, 0x08, 0x16)
                },
                "Crimson" => new ThemePalette
                {
                    Accent = Color.FromRgb(0xFF, 0x00, 0x55),
                    AccentSoft = Color.FromArgb(0x33, 0xFF, 0x00, 0x55),
                    Border = Color.FromArgb(0x4C, 0xFF, 0x91, 0xC2),
                    MainBg = Color.FromRgb(0x10, 0x05, 0x10),
                    Panel = Color.FromArgb(0xCC, 0x20, 0x0E, 0x24),
                    Card = Color.FromRgb(0x2A, 0x10, 0x2D),
                    Input = Color.FromRgb(0x23, 0x0E, 0x26),
                    Text = Color.FromRgb(0xFF, 0xE8, 0xF1),
                    TextMuted = Color.FromRgb(0xD9, 0x9A, 0xB2),
                    ButtonBg = Color.FromRgb(0x32, 0x12, 0x35),
                    ButtonHover = Color.FromRgb(0x3A, 0x15, 0x3E),
                    LogBg = Color.FromRgb(0x12, 0x06, 0x12),
                    CmdBg = Color.FromRgb(0x1A, 0x0B, 0x1B),
                    CmdFg = Color.FromRgb(0xFF, 0x70, 0xA6),
                    FlashText = Color.FromRgb(0x10, 0x05, 0x10)
                },
                "Emerald" => new ThemePalette
                {
                    Accent = Color.FromRgb(0x00, 0xC8, 0x53),
                    AccentSoft = Color.FromArgb(0x33, 0x00, 0xC8, 0x53),
                    Border = Color.FromArgb(0x4C, 0x66, 0xFF, 0xAA),
                    MainBg = Color.FromRgb(0x05, 0x10, 0x0C),
                    Panel = Color.FromArgb(0xCC, 0x0A, 0x20, 0x16),
                    Card = Color.FromRgb(0x0F, 0x24, 0x1A),
                    Input = Color.FromRgb(0x0E, 0x20, 0x18),
                    Text = Color.FromRgb(0xE9, 0xFF, 0xF2),
                    TextMuted = Color.FromRgb(0x9D, 0xD3, 0xB8),
                    ButtonBg = Color.FromRgb(0x12, 0x2B, 0x1E),
                    ButtonHover = Color.FromRgb(0x16, 0x34, 0x25),
                    LogBg = Color.FromRgb(0x04, 0x0D, 0x08),
                    CmdBg = Color.FromRgb(0x08, 0x16, 0x11),
                    CmdFg = Color.FromRgb(0x66, 0xFF, 0xB5),
                    FlashText = Color.FromRgb(0x04, 0x0D, 0x08)
                },
                "Teal" => new ThemePalette
                {
                    Accent = Color.FromRgb(0x00, 0xBF, 0xA5),
                    AccentSoft = Color.FromArgb(0x33, 0x00, 0xBF, 0xA5),
                    Border = Color.FromArgb(0x4C, 0x8D, 0xFF, 0xF0),
                    MainBg = Color.FromRgb(0x05, 0x0F, 0x12),
                    Panel = Color.FromArgb(0xCC, 0x0A, 0x1D, 0x24),
                    Card = Color.FromRgb(0x0F, 0x23, 0x2B),
                    Input = Color.FromRgb(0x0E, 0x1E, 0x25),
                    Text = Color.FromRgb(0xE7, 0xFE, 0xFB),
                    TextMuted = Color.FromRgb(0x9D, 0xD2, 0xCC),
                    ButtonBg = Color.FromRgb(0x12, 0x2B, 0x35),
                    ButtonHover = Color.FromRgb(0x17, 0x34, 0x3F),
                    LogBg = Color.FromRgb(0x04, 0x0C, 0x0F),
                    CmdBg = Color.FromRgb(0x08, 0x15, 0x1B),
                    CmdFg = Color.FromRgb(0x66, 0xFF, 0xE6),
                    FlashText = Color.FromRgb(0x04, 0x0C, 0x0F)
                },
                "Amber" => new ThemePalette
                {
                    Accent = Color.FromRgb(0xFF, 0xB3, 0x00),
                    AccentSoft = Color.FromArgb(0x33, 0xFF, 0xB3, 0x00),
                    Border = Color.FromArgb(0x4C, 0xFF, 0xD5, 0x4F),
                    MainBg = Color.FromRgb(0x12, 0x0B, 0x03),
                    Panel = Color.FromArgb(0xCC, 0x26, 0x1A, 0x0A),
                    Card = Color.FromRgb(0x31, 0x22, 0x0F),
                    Input = Color.FromRgb(0x2A, 0x1E, 0x0E),
                    Text = Color.FromRgb(0xFF, 0xF4, 0xDC),
                    TextMuted = Color.FromRgb(0xD8, 0xBE, 0x8A),
                    ButtonBg = Color.FromRgb(0x37, 0x28, 0x13),
                    ButtonHover = Color.FromRgb(0x42, 0x31, 0x17),
                    LogBg = Color.FromRgb(0x10, 0x08, 0x03),
                    CmdBg = Color.FromRgb(0x19, 0x11, 0x07),
                    CmdFg = Color.FromRgb(0xFF, 0xDC, 0x7A),
                    FlashText = Color.FromRgb(0x12, 0x0B, 0x03)
                },
                "Rose" => new ThemePalette
                {
                    Accent = Color.FromRgb(0xEC, 0x40, 0x7A),
                    AccentSoft = Color.FromArgb(0x33, 0xEC, 0x40, 0x7A),
                    Border = Color.FromArgb(0x4C, 0xFF, 0x8A, 0xB4),
                    MainBg = Color.FromRgb(0x13, 0x06, 0x0F),
                    Panel = Color.FromArgb(0xCC, 0x24, 0x0F, 0x1C),
                    Card = Color.FromRgb(0x2E, 0x13, 0x25),
                    Input = Color.FromRgb(0x28, 0x11, 0x21),
                    Text = Color.FromRgb(0xFF, 0xE9, 0xF2),
                    TextMuted = Color.FromRgb(0xD8, 0xA2, 0xBC),
                    ButtonBg = Color.FromRgb(0x36, 0x16, 0x2B),
                    ButtonHover = Color.FromRgb(0x42, 0x1A, 0x34),
                    LogBg = Color.FromRgb(0x11, 0x06, 0x0D),
                    CmdBg = Color.FromRgb(0x1A, 0x0A, 0x15),
                    CmdFg = Color.FromRgb(0xFF, 0x9A, 0xC2),
                    FlashText = Color.FromRgb(0x13, 0x06, 0x0F)
                },
                "Indigo" => new ThemePalette
                {
                    Accent = Color.FromRgb(0x5E, 0x35, 0xB1),
                    AccentSoft = Color.FromArgb(0x33, 0x5E, 0x35, 0xB1),
                    Border = Color.FromArgb(0x4C, 0x9C, 0x80, 0xFF),
                    MainBg = Color.FromRgb(0x08, 0x07, 0x15),
                    Panel = Color.FromArgb(0xCC, 0x16, 0x12, 0x32),
                    Card = Color.FromRgb(0x1F, 0x1A, 0x45),
                    Input = Color.FromRgb(0x1A, 0x16, 0x3D),
                    Text = Color.FromRgb(0xEE, 0xEA, 0xFF),
                    TextMuted = Color.FromRgb(0xB1, 0xA8, 0xD8),
                    ButtonBg = Color.FromRgb(0x24, 0x1E, 0x52),
                    ButtonHover = Color.FromRgb(0x2D, 0x26, 0x63),
                    LogBg = Color.FromRgb(0x06, 0x06, 0x12),
                    CmdBg = Color.FromRgb(0x0F, 0x0D, 0x20),
                    CmdFg = Color.FromRgb(0xB6, 0xA2, 0xFF),
                    FlashText = Color.FromRgb(0x08, 0x07, 0x15)
                },
                _ => BuildClassicPalette("Blue")
            };
        }

        private static ThemePalette BuildMaterialPalette(Color primary, Color surface, Color background, Color text)
        {
            byte softA = 0x33;
            return new ThemePalette
            {
                Accent = primary,
                AccentSoft = Color.FromArgb(softA, primary.R, primary.G, primary.B),
                Border = Color.FromArgb(0x55, text.R, text.G, text.B),
                MainBg = background,
                Panel = Color.FromArgb(0xF6, surface.R, surface.G, surface.B),
                Card = Color.FromArgb(0xFF, (byte)Math.Max(0, surface.R - 6), (byte)Math.Max(0, surface.G - 6), (byte)Math.Max(0, surface.B - 6)),
                Input = Color.FromArgb(0xFF, (byte)Math.Max(0, surface.R - 14), (byte)Math.Max(0, surface.G - 14), (byte)Math.Max(0, surface.B - 14)),
                Text = text,
                TextMuted = Color.FromArgb(0xAA, text.R, text.G, text.B),
                ButtonBg = Color.FromArgb(0xFF, (byte)Math.Max(0, surface.R - 18), (byte)Math.Max(0, surface.G - 18), (byte)Math.Max(0, surface.B - 18)),
                ButtonHover = Color.FromArgb(0xFF, (byte)Math.Max(0, surface.R - 28), (byte)Math.Max(0, surface.G - 28), (byte)Math.Max(0, surface.B - 28)),
                LogBg = Color.FromArgb(0xFF, (byte)Math.Max(0, background.R - 8), (byte)Math.Max(0, background.G - 8), (byte)Math.Max(0, background.B - 8)),
                CmdBg = Color.FromArgb(0xFF, (byte)Math.Max(0, background.R - 4), (byte)Math.Max(0, background.G - 4), (byte)Math.Max(0, background.B - 4)),
                CmdFg = primary,
                FlashText = ContrastText(primary)
            };
        }

        private static Color ContrastText(Color bg)
        {
            double luma = (0.299 * bg.R) + (0.587 * bg.G) + (0.114 * bg.B);
            return luma > 150 ? Colors.Black : Colors.White;
        }

        private static bool TryParseHexColor(string input, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var s = input.Trim();
            if (s.StartsWith("#")) s = s[1..];

            if (s.Length != 6) return false;
            if (!int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
                return false;

            color = Color.FromRgb((byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
            return true;
        }

        private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private void ApplyPalette(ThemePalette p)
        {
            SetBrush("AccentBrush", p.Accent);
            SetBrush("AccentSoftBrush", p.AccentSoft);
            SetBrush("BorderBrush", p.Border);
            SetBrush("MainBgBrush", p.MainBg);
            SetBrush("PanelBrush", p.Panel);
            SetBrush("CardBrush", p.Card);
            SetBrush("BgInputBrush", p.Input);
            SetBrush("ForegroundBrush", p.Text);
            SetBrush("TextMutedBrush", p.TextMuted);
            SetBrush("ButtonBgBrush", p.ButtonBg);
            SetBrush("ButtonHoverBrush", p.ButtonHover);
            SetBrush("LogBgBrush", p.LogBg);
            SetBrush("CmdPreviewBgBrush", p.CmdBg);
            SetBrush("CmdPreviewFgBrush", p.CmdFg);
            SetBrush("FlashBtnFgBrush", p.FlashText);

            SetModeButtonVisual();
        }

        private void SetBrush(string key, Color color)
        {
            if (Resources[key] is SolidColorBrush sb)
                sb.Color = color;
            else
                Resources[key] = new SolidColorBrush(color);
        }

        private void Swatch_Click(object s, RoutedEventArgs e)
        {
            if (s is Button b && b.Tag is string name)
            {
                _isMaterialMode = false;
                if (MaterialModeCheck != null) MaterialModeCheck.IsChecked = false;

                ApplyTheme(name);
                AppendLog($"[THEME] Switched to {name}");
            }
        }

        private void ApplyTheme(string theme)
        {
            _currentClassicTheme = theme;
            var p = BuildClassicPalette(theme);
            ApplyPalette(p);
        }

        private void MaterialModeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isMaterialMode = MaterialModeCheck.IsChecked == true;
            if (_isMaterialMode)
                ApplyMaterialFromInputs();
            else
                ApplyTheme(_currentClassicTheme);
        }

        private void ApplyMaterialColors_Click(object sender, RoutedEventArgs e)
        {
            MaterialModeCheck.IsChecked = true;
            _isMaterialMode = true;
            ApplyMaterialFromInputs();
        }

        private void ResetMaterialColors_Click(object sender, RoutedEventArgs e)
        {
            InitMaterialDefaults();
            if (_isMaterialMode) ApplyMaterialFromInputs();
        }

        private void ApplyMaterialFromInputs()
        {
            if (!TryParseHexColor(MaterialPrimaryBox.Text, out var primary) ||
                !TryParseHexColor(MaterialSurfaceBox.Text, out var surface) ||
                !TryParseHexColor(MaterialBackgroundBox.Text, out var background) ||
                !TryParseHexColor(MaterialTextBox.Text, out var text))
            {
                AppendLog("[ERR] Invalid material color format. Use #RRGGBB.");
                return;
            }

            var p = BuildMaterialPalette(primary, surface, background, text);
            ApplyPalette(p);
            AppendLog("[THEME] Material Design palette applied.");
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
                _fbRows.Add(new FlashRow { Key = p, Label = p.ToUpperInvariant() });

            foreach (var r in _fbRows)
                r.PropertyChanged += (_, _) => UpdateCommandPreview();
        }

        private void BuildOdinRows()
        {
            _odinRows.Clear();
            foreach (var s in new[] { "BL", "AP", "CP", "CSC", "USERDATA" })
                _odinRows.Add(new FlashRow { Key = s, Label = s });

            foreach (var r in _odinRows)
                r.PropertyChanged += (_, _) => UpdateCommandPreview();
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
                if (!IsToolDeviceLine(line)) continue;
                var state = GetStateFromToolLine(line).ToLowerInvariant();

                if (state == "device" || state == "recovery" || state == "unauthorized") return true;
                if (allowSideload && state == "sideload") return true;
            }

            return false;
        }

        private async Task<bool> DetectOdinDownloadModeAsync()
        {
            for (int i = 0; i < 2; i++)
            {
                var listResult = await RunAsync("odin", "ekoflash", "--list");
                var listText = $"{listResult.Out}\n{listResult.Err}";

                if (LooksLikeOdinDeviceFound(listText)) return true;
                if (listResult.Code == 0 && !LooksLikeNoOdinDevice(listText)) return true;

                await Task.Delay(180);
            }

            for (int i = 0; i < 2; i++)
            {
                var detectResult = await RunAsync("odin", "ekoflash", "detect");
                var detectText = $"{detectResult.Out}\n{detectResult.Err}";

                if (LooksLikeOdinDeviceFound(detectText)) return true;
                if (detectResult.Code == 0 && !LooksLikeNoOdinDevice(detectText)) return true;

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

            if (Regex.IsMatch(t, @"vid[_:\s=]*04e8.*pid[_:\s=]*(6601|685d|68c3|6860)", RegexOptions.IgnoreCase)) return true;
            if (Regex.IsMatch(t, @"04e8[:\s](6601|685d|68c3|6860)", RegexOptions.IgnoreCase)) return true;
            if (SplitLines(t).Any(IsToolDeviceLine)) return true;

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
            if (!txt.Contains("VID_04E8")) return false;
            return OdinPids.Any(pid => txt.Contains($"PID_{pid}")) || txt.Contains("VID_04E8");
        }

        private static IEnumerable<string> SplitLines(string text) =>
            text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

        private static bool IsToolDeviceLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
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
            if (!_deviceChecked) { AppendLog("[!] Scan device first."); return; }
            if (!_deviceConnected) { AppendLog("[!] Device not connected."); return; }

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
            if (targets.Count == 0) { AppendLog("[!] No files selected."); return false; }

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
            if (targets.Count == 0) { AppendLog("[!] No Odin files selected."); return false; }

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
            if (string.IsNullOrWhiteSpace(zip)) { AppendLog("[!] No ZIP selected."); return false; }
            if (!File.Exists(zip)) { AppendLog("[!] File not found."); return false; }

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
            if (!_deviceChecked) { AppendLog("[!] Scan device first."); return; }
            if (!_deviceConnected) { AppendLog("[!] Device not connected."); return; }
            if (s is not Button btn) return;

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

            if (res.Code == 0) AppendLog("[OK] Partition flashed.");
            else AppendLog($"[ERR] fastboot exit code: {res.Code}");
        }

        private void Cancel_Click(object s, RoutedEventArgs e)
        {
            _cts?.Cancel();
            _backupCts?.Cancel();
            AppendLog("[STOP] Cancelled by user.");
        }

        private async void QuickCmd_Click(object s, RoutedEventArgs e)
        {
            if (s is not Button btn) return;
            string cmd = btn.Tag?.ToString() ?? "";

            if (cmd == "zadig")
            {
                string path = ToolsManager.GetExePath("zadig", "zadig");
                if (!File.Exists(path)) path = ToolsManager.GetExePath("tools", "zadig");

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
            if (sender is not Button btn) return;
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

        private async void RefreshApps_Click(object sender, RoutedEventArgs e) => await LoadInstalledAppsAsync();

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

        private async void StartBackup_Click(object sender, RoutedEventArgs e) => await StartBackupFlowAsync();

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
                if (_backupCts.IsCancellationRequested) break;

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

            if (sender is not Button btn) return;
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

            if (confirm != MessageBoxResult.Yes) return;

            btn.IsEnabled = false;
            AppendLog($"[RESTORE] Restoring from: {backupPath}");

            bool ok = await BackupService.RestoreBackupAsync(backupPath, AppendLog);
            AppendLog(ok ? "[OK] Restore completed." : "[ERR] Restore failed.");

            btn.IsEnabled = true;
        }

        private void UpdateCommandPreview()
        {
            if (CommandPreviewBox == null) return;

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
