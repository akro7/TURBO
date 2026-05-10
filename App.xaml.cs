using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MKVenomTool
{
    public partial class App : Application
    {
        private static readonly string ConfigDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TurboFlashTool");

        private static readonly string ConfigPath = Path.Combine(ConfigDir, "theme.json");

        public ThemeConfig CurrentThemeConfig { get; private set; } = new ThemeConfig();

        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
                CrashLog("Core Engine Error", ex.ExceptionObject?.ToString());

            DispatcherUnhandledException += (s, ex) =>
            {
                CrashLog("UI System Error", ex.Exception?.ToString());
                ex.Handled = true;
            };

            base.OnStartup(e);

            try
            {
                ToolsManager.EnsureExtracted();
            }
            catch (Exception ex)
            {
                CrashLog("Tools Initialization", ex.ToString());
            }

            CurrentThemeConfig = LoadThemeConfig();
            ApplyTheme(CurrentThemeConfig);

            var win = new MainWindow();

            try
            {
                Uri iconUri = new Uri("pack://application:,,,/icon.ico", UriKind.RelativeOrAbsolute);
                var sri = GetResourceStream(iconUri);
                if (sri != null)
                    win.Icon = BitmapFrame.Create(sri.Stream);
            }
            catch
            {
                // ignore icon failure
            }

            win.Show();
        }

        public void ApplyTheme(ThemeConfig cfg)
        {
            CurrentThemeConfig = cfg;

            var mode = cfg.ColorMode;
            var theme = cfg.Theme;
            var accent = cfg.Accent;

            // Base palettes
            Color bgRoot;
            Color bgPanel;
            Color bgCard;
            Color bgInput;
            Color bgHover;
            Color fgBright;
            Color fgMid;
            Color fgDim;
            Color panel;
            Color border;

            if (theme == "LiquidGlass")
            {
                if (mode == "Light")
                {
                    bgRoot = FromHex("#DDE8FF");
                    bgPanel = FromHex("#B3FFFFFF");
                    bgCard = FromHex("#99FFFFFF");
                    bgInput = FromHex("#CCFFFFFF");
                    bgHover = FromHex("#E6FFFFFF");
                    fgBright = FromHex("#0F172A");
                    fgMid = FromHex("#334155");
                    fgDim = FromHex("#64748B");
                    panel = FromHex("#99FFFFFF");
                    border = FromHex("#CCFFFFFF");
                }
                else
                {
                    bgRoot = FromHex("#0B1020");
                    bgPanel = FromHex("#40FFFFFF");
                    bgCard = FromHex("#2FFFFFFF");
                    bgInput = FromHex("#55FFFFFF");
                    bgHover = FromHex("#70FFFFFF");
                    fgBright = FromHex("#F9FAFB");
                    fgMid = FromHex("#E5E7EB");
                    fgDim = FromHex("#9CA3AF");
                    panel = FromHex("#55FFFFFF");
                    border = FromHex("#66FFFFFF");
                }
            }
            else if (theme == "Material")
            {
                if (mode == "Light")
                {
                    bgRoot = FromHex("#F6F7FB");
                    bgPanel = FromHex("#FFFFFF");
                    bgCard = FromHex("#FAFAFA");
                    bgInput = FromHex("#FFFFFF");
                    bgHover = FromHex("#F1F5F9");
                    fgBright = FromHex("#0F172A");
                    fgMid = FromHex("#334155");
                    fgDim = FromHex("#94A3B8");
                    panel = FromHex("#FFFFFF");
                    border = FromHex("#E4E4E7");
                }
                else
                {
                    bgRoot = FromHex("#121212");
                    bgPanel = FromHex("#1E1E1E");
                    bgCard = FromHex("#232428");
                    bgInput = FromHex("#2A2D33");
                    bgHover = FromHex("#31353D");
                    fgBright = FromHex("#F5F5F5");
                    fgMid = FromHex("#D4D4D8");
                    fgDim = FromHex("#71717A");
                    panel = FromHex("#1E1E1E");
                    border = FromHex("#2A2A2A");
                }
            }
            else
            {
                if (mode == "Light")
                {
                    bgRoot = FromHex("#EEF4FF");
                    bgPanel = FromHex("#FFFFFF");
                    bgCard = FromHex("#F6FAFF");
                    bgInput = FromHex("#FFFFFF");
                    bgHover = FromHex("#EAF2FF");
                    fgBright = FromHex("#0F172A");
                    fgMid = FromHex("#334155");
                    fgDim = FromHex("#94A3B8");
                    panel = FromHex("#F9FCFF");
                    border = FromHex("#C7D9F7");
                }
                else
                {
                    bgRoot = FromHex("#05070D");
                    bgPanel = FromHex("#0D1626");
                    bgCard = FromHex("#101B2F");
                    bgInput = FromHex("#0F1A2B");
                    bgHover = FromHex("#1F2D47");
                    fgBright = FromHex("#EAF4FF");
                    fgMid = FromHex("#88B4D8");
                    fgDim = FromHex("#2A3F5F");
                    panel = FromHex("#CC0D1B33");
                    border = FromHex("#3352BFFF");
                }
            }

            // Accent palette
            Color ac = accent switch
            {
                "Violet" => FromHex("#8B5CF6"),
                "Blue" => FromHex("#3B82F6"),
                "Green" => FromHex("#22C55E"),
                "Orange" => FromHex("#F97316"),
                "Red" => FromHex("#EF4444"),
                "Pink" => FromHex("#EC4899"),
                _ => FromHex("#00E5FF")
            };

            Color acSoft = Color.FromArgb(0x33, ac.R, ac.G, ac.B);

            Resources["MainBgBrush"] = Brush(bgRoot);
            Resources["PanelBrush"] = Brush(panel);
            Resources["BorderBrush"] = Brush(border);
            Resources["AccentBrush"] = Brush(ac);
            Resources["AccentSoftBrush"] = Brush(acSoft);

            Resources["BgRoot"] = Brush(bgRoot);
            Resources["BgPanel"] = Brush(bgPanel);
            Resources["BgCard"] = Brush(bgCard);
            Resources["BgInput"] = Brush(bgInput);
            Resources["BgHover"] = Brush(bgHover);
            Resources["FgBright"] = Brush(fgBright);
            Resources["FgMid"] = Brush(fgMid);
            Resources["FgDim"] = Brush(fgDim);

            SaveThemeConfig(cfg);
        }

        public static void CrashLog(string src, string? msg)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string logFile = Path.Combine(desktopPath, "EkoFlash_Crash.log");

                string formattedMsg =
                    $"[CRASH LOG - {DateTime.Now:yyyy-MM-dd HH:mm:ss}]{Environment.NewLine}" +
                    $"SOURCE  : {src}{Environment.NewLine}" +
                    $"DETAILS : {msg}{Environment.NewLine}" +
                    $"{new string('=', 60)}{Environment.NewLine}";

                File.AppendAllText(logFile, formattedMsg);

                MessageBox.Show(
                    $"EKO FLASH TOOL - Critical error in: {src}{Environment.NewLine}{Environment.NewLine}" +
                    $"Check 'EkoFlash_Crash.log' on your Desktop for details.",
                    "EkoFlash Tool | System Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch
            {
                // ignore
            }
        }

        private static SolidColorBrush Brush(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private static Color FromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        private static ThemeConfig LoadThemeConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    return new ThemeConfig();

                var raw = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<ThemeConfig>(raw);
                return cfg ?? new ThemeConfig();
            }
            catch
            {
                return new ThemeConfig();
            }
        }

        private static void SaveThemeConfig(ThemeConfig cfg)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var raw = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, raw);
            }
            catch
            {
                // ignore
            }
        }
    }

    public class ThemeConfig
    {
        public string Theme { get; set; } = "Classic";
        public string ColorMode { get; set; } = "Dark";
        public string Accent { get; set; } = "Cyan";
    }
}
