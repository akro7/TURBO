using System.Windows;
using System.Windows.Controls;
using MKVenomTool.Theming;

namespace MKVenomTool.UI
{
    public partial class ThemeSettingsWindow : Window
    {
        public ThemeSettingsWindow()
        {
            InitializeComponent();

            ThemeCombo.SelectedIndex = (int)ThemeManager.CurrentTheme;
            AccentCombo.SelectedIndex = (int)ThemeManager.CurrentAccent;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            var mode = (ThemeMode)ThemeCombo.SelectedIndex;
            var accent = (ThemeAccent)AccentCombo.SelectedIndex;

            ThemeManager.Apply(mode, accent);
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
