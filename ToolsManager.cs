using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MKVenomTool
{
    public static class ToolsManager
    {
        public static readonly string ToolsRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EkoFlashTool", "bin");

        private static bool _extracted;

        private static readonly Dictionary<string, string> ResourceToFile = new()
        {
            ["res.pt.adb.exe"] = "adb.exe",
            ["res.pt.fastboot.exe"] = "fastboot.exe",
            ["res.pt.AdbWinApi.dll"] = "AdbWinApi.dll",
            ["res.pt.AdbWinUsbApi.dll"] = "AdbWinUsbApi.dll",
            ["res.ok.ekoflash.exe"] = "ekoflash.exe",
            ["res.ok.libusb.dll"] = "libusb-1.0.dll",
            ["res.zd.zadig.exe"] = "zadig.exe"
        };

        public static string AdbExe => GetExePath("platform-tools", "adb");
        public static string FastbootExe => GetExePath("platform-tools", "fastboot");
        public static string EkoFlashExe => GetExePath("odin", "ekoflash");
        public static string ZadigExe => GetExePath("zadig", "zadig");

        public static void EnsureExtracted()
        {
            if (_extracted) return;

            try
            {
                Directory.CreateDirectory(ToolsRoot);

                var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var all = asm.GetManifestResourceNames();

                foreach (var kv in ResourceToFile)
                {
                    var logical = all.FirstOrDefault(n =>
                        string.Equals(n, kv.Key, StringComparison.OrdinalIgnoreCase) ||
                        n.EndsWith("." + kv.Key, StringComparison.OrdinalIgnoreCase));

                    if (logical == null) continue;

                    using var stream = asm.GetManifestResourceStream(logical);
                    if (stream == null) continue;

                    var dst = Path.Combine(ToolsRoot, kv.Value);
                    using var fs = File.Create(dst);
                    stream.CopyTo(fs);
                }

                _extracted = true;
            }
            catch (Exception ex)
            {
                App.CrashLog("ToolsManager.EnsureExtracted", ex.ToString());
            }
        }

        public static bool ExeExists(string folder, string exe) => File.Exists(GetExePath(folder, exe));

        public static string GetExePath(string folder, string exe)
        {
            var e = exe.Trim();
            if (!e.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                !e.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                e += ".exe";

            return Path.Combine(ToolsRoot, e);
        }
    }
}
