using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace MKVenomTool.Theming
{
    public enum AppTheme
    {
        Classic = 0,
        LiquidGlass = 1,
        Material = 2
    }

    public enum ColorMode
    {
        Dark = 0,
        Light = 1
    }

    public enum AccentColor
    {
        Cyan = 0,
        Violet = 1,
        Blue = 2,
        Green = 3,
        Orange = 4,
        Red = 5,
        Pink = 6
    }

    public sealed class ThemeConfig
    {
        public AppTheme Theme { get; set; } = AppTheme.Classic;
        public ColorMode Mode { get; set; } = ColorMode.Dark;
        public AccentColor Accent { get; set; } = AccentColor.Cyan;
    }

    public static class ThemeManager
    {
        private static readonly string ConfigDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TURBO");

        private static readonly string ConfigPath = Path.Combine(ConfigDir, "theme.json");

        public static ThemeConfig Current { get; private set; } = new ThemeConfig();

        public static void Initialize()
        {
            Current = LoadConfig();
            ApplyTheme(Current.Theme, Current.Mode);
            ApplyAccent(Current.Accent);
        }

        public static void ApplyTheme(AppTheme theme, ColorMode mode)
        {
            Current.Theme = theme;
            Current.Mode = mode;

            var app = Application.Current;
            if (app == null) return;

            RemoveThemeDictionaries();

            string source = theme switch
            {
                AppTheme.LiquidGlass => mode == ColorMode.Dark
                    ? "Themes/LiquidGlass.Dark.xaml"
                    : "Themes/LiquidGlass.Light.xaml",

                AppTheme.Material => mode == ColorMode.Dark
                    ? "Themes/Material.Dark.xaml"
                    : "Themes/Material.Light.xaml",

                _ => mode == ColorMode.Dark
                    ? "Themes/Classic.Dark.xaml"
                    : "Themes/Classic.Light.xaml"
            };

            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(source, UriKind.Relative)
            });

            SaveConfig();
        }

        public static void ApplyAccent(AccentColor accent)
        {
            Current.Accent = accent;

            var app = Application.Current;
            if (app == null) return;

            RemoveAccentDictionary();

            string source = accent switch
            {
                AccentColor.Violet => "Themes/Accents/Accent.Violet.xaml",
                AccentColor.Blue => "Themes/Accents/Accent.Blue.xaml",
                AccentColor.Green => "Themes/Accents/Accent.Green.xaml",
                AccentColor.Orange => "Themes/Accents/Accent.Orange.xaml",
                AccentColor.Red => "Themes/Accents/Accent.Red.xaml",
                AccentColor.Pink => "Themes/Accents/Accent.Pink.xaml",
                _ => "Themes/Accents/Accent.Cyan.xaml"
            };

            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(source, UriKind.Relative)
            });

            SaveConfig();
        }

        private static void RemoveThemeDictionaries()
        {
            var merged = Application.Current.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var src = merged[i].Source?.ToString() ?? "";
                if (src.Contains("Classic.") || src.Contains("LiquidGlass.") || src.Contains("Material."))
                {
                    merged.RemoveAt(i);
                }
            }
        }

        private static void RemoveAccentDictionary()
        {
            var merged = Application.Current.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var src = merged[i].Source?.ToString() ?? "";
                if (src.Contains("Themes/Accents/Accent."))
                {
                    merged.RemoveAt(i);
                }
            }
        }

        private static ThemeConfig LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    return new ThemeConfig();

                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<ThemeConfig>(json);
                return cfg ?? new ThemeConfig();
            }
            catch
            {
                return new ThemeConfig();
            }
        }

        private static void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // ignore
            }
        }
    }
}
