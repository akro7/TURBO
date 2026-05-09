using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EkoFlashTool
{
    public partial class App : Application
    {
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

            // - Error 72 Fix: MainWindow وليس EkoFlashWindow
            var win = new MainWindow();

            try
            {
                Uri iconUri = new Uri("pack://application:,,,/icon.ico", UriKind.RelativeOrAbsolute);
                var sri = GetResourceStream(iconUri);
                if (sri != null)
                    win.Icon = BitmapFrame.Create(sri.Stream);
            }
            catch { }

            win.Show();
        }

        public static void CrashLog(string src, string? msg)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string logFile     = Path.Combine(desktopPath, "EkoFlash_Crash.log");

                string entry = $"[CRASH LOG - {DateTime.Now:yyyy-MM-dd HH:mm:ss}]{Environment.NewLine}" +
                               $"SOURCE  : {src}{Environment.NewLine}"                                   +
                               $"DETAILS : {msg}{Environment.NewLine}"                                   +
                               $"{new string('═', 60)}{Environment.NewLine}";

                File.AppendAllText(logFile, entry);

                MessageBox.Show(
                    $"⚡ EKO FLASH TOOL — Critical Error in: {src}{Environment.NewLine}{Environment.NewLine}" +
                    $"Check 'EkoFlash_Crash.log' on your Desktop for details.",
                    "EkoFlash Tool | System Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch { }
        }
    }
}
