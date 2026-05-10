using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace MKVenomTool.Theming
{
    public enum ThemeMode
    {
        Classic = 0,
        LiquidGlass = 1,
        Material = 2
    }

    public enum ThemeAccent
    {
        Cyan = 0,
        Violet = 1,
        Blue = 2,
        Green = 3,
        Orange = 4,
        Red = 5,
        Pink = 6
    }

    public static class ThemeManager
    {
        private const string ThemeMarkerKey = "__THEME_RUNTIME_DICTIONARY__";
        private const string AccentMarkerKey = "__ACCENT_RUNTIME_DICTIONARY__";

        private static readonly string ConfigDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TURBO");

        private static readonly string ConfigFile = Path.Combine(ConfigDir, "theme.cfg");

        public static ThemeMode CurrentTheme { get; private set; } = ThemeMode.Classic;
        public static ThemeAccent CurrentAccent { get; private set; } = ThemeAccent.Cyan;

        public static void Initialize()
        {
            LoadConfig();
            Apply(CurrentTheme, CurrentAccent);
        }

        public static void Apply(ThemeMode mode, ThemeAccent accent)
        {
            CurrentTheme = mode;
            CurrentAccent = accent;

            RemoveRuntimeDictionaries();

            var themeDict = LoadThemeDictionary(mode);
            themeDict[ThemeMarkerKey] = true;

            var accentDict = BuildAccentDictionary(accent);
            accentDict[AccentMarkerKey] = true;

            Application.Current.Resources.MergedDictionaries.Add(themeDict);
            Application.Current.Resources.MergedDictionaries.Add(accentDict);

            SaveConfig();
        }

        private static void RemoveRuntimeDictionaries()
        {
            var merged = Application.Current.Resources.MergedDictionaries;
            var toRemove = merged
                .Where(d => d.Contains(ThemeMarkerKey) || d.Contains(AccentMarkerKey))
                .ToList();

            foreach (var d in toRemove)
            {
                merged.Remove(d);
            }
        }

        private static ResourceDictionary LoadThemeDictionary(ThemeMode mode)
        {
            string file = mode switch
            {
                ThemeMode.LiquidGlass => "Themes/Theme.LiquidGlass.xaml",
                ThemeMode.Material => "Themes/Theme.Material.xaml",
                _ => "Themes/Theme.Classic.xaml"
            };

            return new ResourceDictionary
            {
                Source = new Uri(file, UriKind.Relative)
            };
        }

        private static ResourceDictionary BuildAccentDictionary(ThemeAccent accent)
        {
            var palette = GetAccentPalette(accent);

            var d = new ResourceDictionary();

            d["AccentBrush"] = NewBrush(palette.Primary);
            d["AccentSoftBrush"] = NewBrush(palette.Soft);
            d["NeCyan"] = NewBrush(palette.Primary);
            d["NeCyanSoft"] = NewBrush(palette.Soft);
            d["BorderAccent"] = NewBrush(palette.BorderGlow);
            d["BorderGlow"] = NewBrush(palette.BorderGlow);

            return d;
        }

        private static (Color Primary, Color Soft, Color BorderGlow) GetAccentPalette(ThemeAccent accent)
        {
            return accent switch
            {
                ThemeAccent.Violet => (FromHex("#8B5CF6"), FromHex("#338B5CF6"), FromHex("#558B5CF6")),
                ThemeAccent.Blue => (FromHex("#3B82F6"), FromHex("#333B82F6"), FromHex("#553B82F6")),
                ThemeAccent.Green => (FromHex("#22C55E"), FromHex("#3322C55E"), FromHex("#5522C55E")),
                ThemeAccent.Orange => (FromHex("#F97316"), FromHex("#33F97316"), FromHex("#55F97316")),
                ThemeAccent.Red => (FromHex("#EF4444"), FromHex("#33EF4444"), FromHex("#55EF4444")),
                ThemeAccent.Pink => (FromHex("#EC4899"), FromHex("#33EC4899"), FromHex("#55EC4899")),
                _ => (FromHex("#00E5FF"), FromHex("#1A00E5FF"), FromHex("#5500E5FF"))
            };
        }

        private static SolidColorBrush NewBrush(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private static Color FromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        private static void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                File.WriteAllText(ConfigFile, $"{(int)CurrentTheme}|{(int)CurrentAccent}");
            }
            catch
            {
                // ignore
            }
        }

        private static void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigFile)) return;

                var raw = File.ReadAllText(ConfigFile).Trim();
                var parts = raw.Split('|');
                if (parts.Length != 2) return;

                if (int.TryParse(parts[0], out var themeVal) &&
                    Enum.IsDefined(typeof(ThemeMode), themeVal))
                {
                    CurrentTheme = (ThemeMode)themeVal;
                }

                if (int.TryParse(parts[1], out var accentVal) &&
                    Enum.IsDefined(typeof(ThemeAccent), accentVal))
                {
                    CurrentAccent = (ThemeAccent)accentVal;
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
