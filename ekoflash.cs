// 🔥 نفس الـ using بتاعك + إضافة واحدة
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO; // ✅ NEW
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

        // ================= NEW =================
        string currentTheme = "Blue";

        void SaveTheme(string theme)
        {
            try { File.WriteAllText("theme.cfg", theme); } catch { }
        }

        string LoadTheme()
        {
            try
            {
                if (File.Exists("theme.cfg"))
                    return File.ReadAllText("theme.cfg");
            }
            catch { }
            return "Blue";
        }
        // =======================================

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

        public MainWindow()
        {
            InitializeComponent();

            // 🔥 بدل ApplyTheme("Blue")
            ApplyTheme(LoadTheme());

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

        // ----------------------------- Theme -----------------------------
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
            currentTheme = theme;

            // 🔥 NEW THEMES
            if (theme == "Liquid")
            {
                Resources["AccentBrush"] = new SolidColorBrush(Colors.White);
                Resources["AccentSoftBrush"] = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
                Resources["BorderBrush"] = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                Resources["PanelBrush"] = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));

                SetModeButtonVisual();
                SaveTheme(theme);
                return;
            }

            if (theme == "Material")
            {
                Resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(103, 80, 164));
                Resources["AccentSoftBrush"] = new SolidColorBrush(Color.FromArgb(80, 103, 80, 164));
                Resources["BorderBrush"] = new SolidColorBrush(Color.FromArgb(80, 60, 60, 60));
                Resources["PanelBrush"] = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30));

                SetModeButtonVisual();
                SaveTheme(theme);
                return;
            }

            // 🔥 ORIGINAL (زي ما هو)
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
            SaveTheme(theme);
        }
    }
}
