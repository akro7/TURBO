using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace MKVenomTool
{
    public static class ToolsManager
    {
        public static readonly string ToolsRoot =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MKVenomTool", "bin");

        private static bool _extracted;

        private static readonly Dictionary<string, string> _map = new()
        {
            ["MKVenomTool.Resources.adb.exe"]            = "adb.exe",
            ["MKVenomTool.Resources.fastboot.exe"]       = "fastboot.exe",
            ["MKVenomTool.Resources.AdbWinApi.dll"]      = "AdbWinApi.dll",
            ["MKVenomTool.Resources.AdbWinUsbApi.dll"]   = "AdbWinUsbApi.dll",
            ["MKVenomTool.Resources.libusb-1.0.dll"]     = "libusb-1.0.dll",
            ["MKVenomTool.Resources.zadig.exe"]          = "zadig.exe"
        };

        public static void EnsureExtracted()
        {
            if (_extracted) return;
            
            try
            {
                if (!Directory.Exists(ToolsRoot))
                {
                    Directory.CreateDirectory(ToolsRoot);
                }

                var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

                foreach (var item in _map)
                {
                    string logicalName = item.Key;
                    string fileName = item.Value;

                    using var stream = asm.GetManifestResourceStream(logicalName);
                    if (stream == null) continue;

                    string dest = Path.Combine(ToolsRoot, fileName);

                    if (File.Exists(dest) && new FileInfo(dest).Length == stream.Length) 
                        continue;

                    using var fs = File.Create(dest);
                    stream.CopyTo(fs);
                }

                _extracted = true;
            }
            catch (Exception ex)
            {
                App.CrashLog("ToolsManager Extraction", ex.Message);
            }
        }

        public static string GetToolPath(string fileName)
        {
            string path = Path.Combine(ToolsRoot, fileName);
            return File.Exists(path) ? path : fileName;
        }

        public static bool IsReady() => _extracted;
    }
}
