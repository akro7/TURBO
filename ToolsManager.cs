using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace MKVenomTool
{
    public static class ToolsManager
    {
        public static readonly string ToolsRoot =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EkoFlashTool", "bin");

        private static bool _extracted;

        // Embedded resources map
        private static readonly Dictionary<string, string> _map = new()
        {
            ["EkoFlashTool.Resources.ekoflash.exe"]     = "ekoflash.exe",
            ["EkoFlashTool.Resources.adb.exe"]          = "adb.exe",
            ["EkoFlashTool.Resources.fastboot.exe"]     = "fastboot.exe",
            ["EkoFlashTool.Resources.AdbWinApi.dll"]    = "AdbWinApi.dll",
            ["EkoFlashTool.Resources.AdbWinUsbApi.dll"] = "AdbWinUsbApi.dll",
            ["EkoFlashTool.Resources.libusb-1.0.dll"]   = "libusb-1.0.dll",
            ["EkoFlashTool.Resources.zadig.exe"]        = "zadig.exe"
        };

        public static string EkoFlashExe => GetToolPath("ekoflash.exe");
        public static string AdbExe => GetToolPath("adb.exe");
        public static string FastbootExe => GetToolPath("fastboot.exe");
        public static string ZadigExe => GetToolPath("zadig.exe");

        public static void EnsureExtracted()
        {
            if (_extracted) return;

            try
            {
                Directory.CreateDirectory(ToolsRoot);

                var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

                foreach (var (logicalName, fileName) in _map)
                {
                    using var stream = asm.GetManifestResourceStream(logicalName);
                    if (stream == null)
                    {
                        App.CrashLog("ToolsManager", $"Resource not found: {logicalName}");
                        continue;
                    }

                    string dest = Path.Combine(ToolsRoot, fileName);

                    if (File.Exists(dest) && FileMd5(dest) == StreamMd5(stream))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        continue;
                    }

                    stream.Seek(0, SeekOrigin.Begin);
                    using var fs = File.Create(dest);
                    stream.CopyTo(fs);
                }

                _extracted = true;
            }
            catch (Exception ex)
            {
                App.CrashLog("ToolsManager.EnsureExtracted", ex.Message);
            }
        }

        public static void Reset()
        {
            _extracted = false;
            try
            {
                if (Directory.Exists(ToolsRoot))
                    Directory.Delete(ToolsRoot, recursive: true);
            }
            catch (Exception ex)
            {
                App.CrashLog("ToolsManager.Reset", ex.Message);
            }
        }

        public static bool Verify(out List<string> missing)
        {
            missing = new List<string>();
            foreach (var fileName in _map.Values)
            {
                string path = Path.Combine(ToolsRoot, fileName);
                if (!File.Exists(path))
                    missing.Add(fileName);
            }

            return missing.Count == 0;
        }

        public static string GetToolPath(string fileName)
        {
            string path = Path.Combine(ToolsRoot, fileName);
            return File.Exists(path) ? path : fileName;
        }

        // Compatibility bridge for old callers (BackupService uses this)
        public static string GetExePath(string folder, string exe)
        {
            var n = (exe ?? string.Empty).Trim().ToLowerInvariant();

            return n switch
            {
                "adb" or "adb.exe" => AdbExe,
                "fastboot" or "fastboot.exe" => FastbootExe,
                "ekoflash" or "ekoflash.exe" => EkoFlashExe,
                "zadig" or "zadig.exe" => ZadigExe,
                _ => GetToolPath(exe)
            };
        }

        public static bool IsReady() => _extracted && Verify(out _);

        private static string FileMd5(string path)
        {
            using var md5 = MD5.Create();
            using var fs = File.OpenRead(path);
            return BitConverter.ToString(md5.ComputeHash(fs));
        }

        private static string StreamMd5(Stream stream)
        {
            using var md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(stream));
        }
    }
}
