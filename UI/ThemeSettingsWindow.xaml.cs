using System;
using System.Windows;
using MKVenomTool.Theming;

namespace MKVenomTool.UI
{
    public partial class ThemeSettingsWindow : Window
    {
        public ThemeSettingsWindow()
        {
            InitializeComponent();

            ThemeCombo.ItemsSource = Enum.GetValues(typeof(AppTheme));
            ModeCombo.ItemsSource = Enum.GetValues(typeof(ColorMode));
            AccentCombo.ItemsSource = Enum.GetValues(typeof(AccentColor));

            ThemeCombo.SelectedItem = ThemeManager.Current.Theme;
            ModeCombo.SelectedItem = ThemeManager.Current.Mode;
            AccentCombo.SelectedItem = ThemeManager.Current.Accent;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (ThemeCombo.SelectedItem is not AppTheme theme) return;
            if (ModeCombo.SelectedItem is not ColorMode mode) return;
            if (AccentCombo.SelectedItem is not AccentColor accent) return;

            ThemeManager.ApplyTheme(theme, mode);
            ThemeManager.ApplyAccent(accent);

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
