using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace EkoFlashTool
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _cancelTokenSource;
        private Process _fastbootProcess;
        private DataReceiver _dataReceiver;

        public MainWindow()
        {
            InitializeComponent();
            _dataReceiver = new DataReceiver(Dispatcher);
            LoadSettings();
        }

        private void LoadSettings()
        {
            // تحميل الإعدادات المحفوظة
            if (Properties.Settings.Default.SelectedTabIndex >= 0)
                MainTabControl.SelectedIndex = Properties.Settings.Default.SelectedTabIndex;
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Properties.Settings.Default.SelectedTabIndex = MainTabControl.SelectedIndex;
            Properties.Settings.Default.Save();
        }

        private async void StartProcessButton_Click(object sender, RoutedEventArgs e)
        {
            _cancelTokenSource = new CancellationTokenSource();
            StartProcessButton.IsEnabled = false;
            StopProcessButton.IsEnabled = true;
            OutputDocument.Blocks.Clear();
            try
            {
                await RunEkoFlashAsync(_cancelTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                AppendText("Operation cancelled by user.\n", Colors.Orange);
            }
            catch (Exception ex)
            {
                AppendText($"ERROR: {ex.Message}\n", Colors.Red);
            }
            finally
            {
                StartProcessButton.IsEnabled = true;
                StopProcessButton.IsEnabled = false;
            }
        }

        private void StopProcessButton_Click(object sender, RoutedEventArgs e)
        {
            _cancelTokenSource?.Cancel();
            _fastbootProcess?.Kill();
        }

        private async Task RunEkoFlashAsync(CancellationToken token)
        {
            string ekoFlashPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ekoflash.exe");
            if (!File.Exists(ekoFlashPath))
            {
                throw new FileNotFoundException("ekoflash.exe not found in application directory.");
            }

            var psi = new ProcessStartInfo
            {
                FileName = ekoFlashPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // إضافة الوسائط من واجهة المستخدم
            var args = new List<string>();
            if (OdinCheckBox.IsChecked == true) args.Add("--odin");
            if (PitFileCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(PitFilePathTextBox.Text))
                args.Add($"--pit \"{PitFilePathTextBox.Text}\"");
            if (NoRebootCheckBox.IsChecked == true) args.Add("--no-reboot");
            if (RescanUSB.IsChecked == true) args.Add("--rescan-usb");
            if (!string.IsNullOrWhiteSpace(AdditionalArgsTextBox.Text))
                args.Add(AdditionalArgsTextBox.Text.Trim());
            psi.Arguments = string.Join(" ", args);

            _fastbootProcess = new Process { StartInfo = psi };
            _fastbootProcess.OutputDataReceived += (s, e) => Dispatcher.Invoke(() => AppendText(e.Data + "\n", Colors.LightGreen));
            _fastbootProcess.ErrorDataReceived += (s, e) => Dispatcher.Invoke(() => AppendText(e.Data + "\n", Colors.Red));

            _fastbootProcess.Start();
            _fastbootProcess.BeginOutputReadLine();
            _fastbootProcess.BeginErrorReadLine();

            await _fastbootProcess.WaitForExitAsync(token);
        }

        private void AppendText(string text, Color color)
        {
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(text) { Foreground = new SolidColorBrush(color) });
            OutputDocument.Blocks.Add(paragraph);
            OutputScrollViewer.ScrollToEnd();
        }

        private void BrowsePitButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PIT files (*.pit)|*.pit|All files (*.*)|*.*",
                Title = "Select PIT file"
            };
            if (dlg.ShowDialog() == true)
            {
                PitFilePathTextBox.Text = dlg.FileName;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _fastbootProcess?.Kill();
            _cancelTokenSource?.Cancel();
        }

        // وظيفة مساعدة لكتابة البيانات بشكل غير متزامن إلى الـ TextBox
        private async Task WriteAsync(string message, Color color)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                AppendText(message, color);
            });
        }
    }
}
