using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MKVenomTool
{
    public partial class MainWindow : Window
    {
        private enum FlashMode { Fastboot, Odin, Sideload, Tools, BackupRestore }
        private FlashMode _mode = FlashMode.Fastboot;
        private readonly ObservableCollection<FlashRow> _fbRows = new();
        private readonly ObservableCollection<FlashRow> _odinRows = new();
        private readonly ObservableCollection<BackupAppRow> _backupApps = new();
        private readonly ObservableCollection<BackupSetRow> _backupSets = new();

        private string _pitFilePath = "";
        private bool _deviceChecked;
        private bool _deviceConnected;
        private bool _uiReady;

        public MainWindow()
        {
            InitializeComponent();
            _uiReady = true;
            ApplyTheme("Blue");
            BuildFastbootRows();
            BuildOdinRows();

            // ... (باقي الكود الخاص بـ MainWindow يبقى كما هو دون تغيير،
            //      مع استبدال أي استدعاء لـ `Tuple` قديم بالكود الجديد في الأسفل)
        }

        // ... (جميع الدوال الأخرى تبقى كما هي)

        // ===================================================== //
        // دوال جديدة ومعدلة لحل المشاكل                      //
        // ===================================================== //

        // تعريف الـ ResultType الجديد بـ ValueTuple مع تسمية الحقول
        private async Task<(bool ok, string text, string log)> DetectDeviceStatusAsync()
        {
            if (_mode == FlashMode.Fastboot || _mode == FlashMode.Tools)
            {
                var r = await RunProcessAsync("platform-tools", "fastboot", "devices", 12000);
                bool ok = (r.Out + r.Err).ToLowerInvariant().Contains("fastboot");
                return (ok, ok ? "FASTBOOT CONNECTED" : "NO FASTBOOT DEVICE", ok ? "Fastboot device detected." : "No fastboot device found.");
            }
            if (_mode == FlashMode.Sideload || _mode == FlashMode.BackupRestore)
            {
                var r = await RunProcessAsync("platform-tools", "adb", "devices", 12000);
                var m = (r.Out + r.Err).ToLowerInvariant();
                bool ok = m.Contains("\tdevice") || m.Contains("\tsideload");
                return (ok, ok ? "ADB CONNECTED" : "NO ADB DEVICE", ok ? "ADB device detected." : "No ADB device found.");
            }
            if (!ToolsManager.ExeExists("odin", "ekoflash"))
                return (false, "EKOFLASH MISSING", "ekoflash.exe not found.");

            var od = await RunProcessAsync("odin", "ekoflash", "--list", 15000);
            var full = (od.Out + " " + od.Err).Trim();
            var l = full.ToLowerInvariant();

            if (od.Code == 0 && !l.Contains("no device") && !l.Contains("not found") && !l.Contains("0 device"))
                return (true, "DOWNLOAD MODE READY", string.IsNullOrWhiteSpace(full) ? "Download mode detected." : full);

            var pnp = await RunProcessAsync("pnputil.exe", "/enum-devices /connected", 12000);
            bool samsung = (pnp.Out + pnp.Err).ToUpperInvariant().Contains("VID_04E8");
            return samsung ? (true, "DOWNLOAD MODE READY", "Samsung USB detected (VID_04E8).") : (false, "NO DOWNLOAD DEVICE", "No Samsung download-mode device found.");
        }
        
        // المعدلة
        private async void DetectDevice_Click(object s, RoutedEventArgs e)
        {
            if (DeviceStatusText != null)
            {
                DeviceStatusText.Text = "Checking...";
                DeviceStatusText.Foreground = (Brush)Resources["WarningBrush"];
            }
            var (ok, text, log) = await DetectDeviceStatusAsync();
            _deviceChecked = true;
            _deviceConnected = ok;
            if (DeviceStatusText != null)
            {
                DeviceStatusText.Text = text;
                DeviceStatusText.Foreground = ok ? new SolidColorBrush(Color.FromRgb(77, 255, 154)) : new SolidColorBrush(Color.FromRgb(255, 122, 122));
            }
            AppendLog(log);
        }

        // المعدلة لاستخدام ValueTuple في الإرجاع
        private async Task<(int Code, string Out, string Err)> RunProcessAsync(string fileName, string args, int ms)
        {
            try
            {
                var psi = new ProcessStartInfo { FileName = fileName, Arguments = args, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                using var p = new Process { StartInfo = psi };
                var o = new StringBuilder();
                var er = new StringBuilder();
                p.OutputDataReceived += (_, e) => { if (e.Data != null) o.AppendLine(e.Data); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) er.AppendLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                using var cts = new CancellationTokenSource(ms);
                try { await p.WaitForExitAsync(cts.Token); }
                catch (OperationCanceledException) { try { p.Kill(true); } catch { } return (-1, o.ToString(), $"Timeout {ms / 1000}s"); }
                return (p.ExitCode, o.ToString(), er.ToString());
            }
            catch (Exception ex) { return (-1, "", ex.Message); }
        }
    }

    // تعريفات الكلاسات الأخرى (FlashRow, BackupAppRow, BackupSetRow) تبقى كما هي تماماً
}
