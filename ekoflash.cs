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
            if (MaterialPrimaryBox != null) MaterialPrimaryBox.Text = "#6750A4";
            if (MaterialSurfaceBox != null) MaterialSurfaceBox.Text = "#FFFBFE";
            if (MaterialBackgroundBox != null) MaterialBackgroundBox.Text = "#F3EDF7";
            if (MaterialTextBox != null) MaterialTextBox.Text = "#1C1B1F";
            if (MaterialModeCheck != null) MaterialModeCheck.IsChecked = false;
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
            return new ThemePalette
            {
                Accent = primary,
                AccentSoft = Color.FromArgb(0x33, primary.R, primary.G, primary.B),
                Border = Color.FromArgb(0x55, text.R, text.G, text.B),
                MainBg = background,
                Panel = Color.FromArgb(0xF4, surface.R, surface.G, surface.B),
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
            if (!int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value)) return false;

            color = Color.FromRgb((byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
            return true;
        }

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

        // FIX: لا نعدّل brush موجودة (ممكن تكون frozen). نستبدلها دائمًا.
        private void SetBrush(string key, Color color)
        {
            Resources[key] = new SolidColorBrush(color);
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
