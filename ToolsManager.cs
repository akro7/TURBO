using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MKVenomTool
{
    /// <summary>
    /// TURBO FLASH TOOL - Tools Management Engine
    /// Managed by: AHMED YOUNIS & Mohamed Khaled
    /// </summary>
    public static class ToolsManager
    {
        // تغيير المسار إلى TurboFlashTool ليناسب الهوية الجديدة
        public static readonly string ToolsRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TurboFlashTool", "bin");

        private static bool _extracted;

        // خريطة الموارد: تربط المورد الداخلي بالمسار والمجلد المطلوب
        private static readonly Dictionary<string, (string folder, string fileName)> ResourceMapping = new()
        {
            ["res.pt.adb.exe"] = ("platform-tools", "adb.exe"),
            ["res.pt.fastboot.exe"] = ("platform-tools", "fastboot.exe"),
            ["res.pt.AdbWinApi.dll"] = ("platform-tools", "AdbWinApi.dll"),
            ["res.pt.AdbWinUsbApi.dll"] = ("platform-tools", "AdbWinUsbApi.dll"),
            ["res.ok.ekoflash.exe"] = ("odin", "ekoflash.exe"),
            ["res.ok.libusb.dll"] = ("odin", "libusb-1.0.dll"),
            ["res.zd.zadig.exe"] = ("zadig", "zadig.exe")
        };

        public static string AdbExe => GetExePath("platform-tools", "adb");
        public static string FastbootExe => GetExePath("platform-tools", "fastboot");
        public static string EkoFlashExe => GetExePath("odin", "ekoflash");
        public static string ZadigExe => GetExePath("zadig", "zadig");

        /// <summary>
        /// التأكد من استخراج جميع الأدوات اللازمة للعمل
        /// </summary>
        public static void EnsureExtracted()
        {
            if (_extracted) return;

            try
            {
                var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var resourceNames = asm.GetManifestResourceNames();

                foreach (var entry in ResourceMapping)
                {
                    string resourceKey = entry.Key;
                    var (folder, fileName) = entry.Value;

                    // تحديد المسار الكامل للمجلد والملف
                    string targetFolder = Path.Combine(ToolsRoot, folder);
                    string targetPath = Path.Combine(targetFolder, fileName);

                    // إنشاء المجلد إذا لم يكن موجوداً
                    if (!Directory.Exists(targetFolder))
                        Directory.CreateDirectory(targetFolder);

                    // استخراج الملف فقط إذا كان غير موجود (لتوفير الوقت)
                    if (!File.Exists(targetPath))
                    {
                        var fullResourceName = resourceNames.FirstOrDefault(n =>
                            n.Equals(resourceKey, StringComparison.OrdinalIgnoreCase) ||
                            n.EndsWith("." + resourceKey, StringComparison.OrdinalIgnoreCase));

                        if (fullResourceName != null)
                        {
                            using var stream = asm.GetManifestResourceStream(fullResourceName);
                            if (stream != null)
                            {
                                using var fs = File.Create(targetPath);
                                stream.CopyTo(fs);
                            }
                        }
                    }
                }

                _extracted = true;
            }
            catch (Exception ex)
            {
                // إرسال الخطأ لـ CrashLog (تأكد من وجود هذه الدالة في App.xaml.cs)
                App.CrashLog("ToolsManager.EnsureExtracted", $"Critical failure in extracting core tools: {ex.Message}");
            }
        }

        /// <summary>
        /// فحص وجود الأداة في المجلد المخصص لها
        /// </summary>
        public static bool ExeExists(string folder, string exeName)
        {
            return File.Exists(GetExePath(folder, exeName));
        }

        /// <summary>
        /// جلب المسار الكامل لأي أداة مع معالجة الامتدادات تلقائياً
        /// </summary>
        public static string GetExePath(string folder, string exeName)
        {
            var cleanName = exeName.Trim();
            if (!cleanName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                !cleanName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                cleanName += ".exe";
            }

            return Path.Combine(ToolsRoot, folder, cleanName);
        }

        /// <summary>
        /// مسح جميع الأدوات المستخرجة (في حال الرغبة في التحديث أو الإصلاح)
        /// </summary>
        public static void ResetTools()
        {
            try
            {
                if (Directory.Exists(ToolsRoot))
                    Directory.Delete(ToolsRoot, true);
                _extracted = false;
            }
            catch { /* المجلد قد يكون قيد الاستخدام */ }
        }
    }
}
