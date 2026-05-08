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

        // ── Resource map ─────────────────────────────────────────────
        // Key   = Logical resource name (AssemblyName.Folder.FileName)
        // Value = Output file name on disk
        private static readonly Dictionary<string, string> _map = new()
        {
            ["EkoFlashTool.Resources.ekoflash.exe"]      = "ekoflash.exe",
            ["EkoFlashTool.Resources.adb.exe"]           = "adb.exe",
            ["EkoFlashTool.Resources.fastboot.exe"]      = "fastboot.exe",
            ["EkoFlashTool.Resources.AdbWinApi.dll"]     = "AdbWinApi.dll",
            ["EkoFlashTool.Resources.AdbWinUsbApi.dll"]  = "AdbWinUsbApi.dll",
            ["EkoFlashTool.Resources.libusb-1.0.dll"]    = "libusb-1.0.dll",
            ["EkoFlashTool.Resources.zadig.exe"]         = "zadig.exe"
        };

        // ── Convenience path properties ───────────────────────────────
        public static string EkoFlashExe  => GetToolPath("ekoflash.exe");
        public static string AdbExe       => GetToolPath("adb.exe");
        public static string FastbootExe  => GetToolPath("fastboot.exe");
        public static string ZadigExe     => GetToolPath("zadig.exe");

        // ── Extract all embedded resources to ToolsRoot ───────────────
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

                    // ── Skip if file already extracted and identical ──
                    if (File.Exists(dest) && FileMd5(dest) == StreamMd5(stream))
                    {
                        stream.Seek(0, SeekOrigin.Begin); // reset after MD5 read
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

        // ── Force re-extraction (call after update) ───────────────────
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

        // ── Verify all tools are present on disk ──────────────────────
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

        // ── Get full path for a tool, fallback to filename ────────────
        public static string GetToolPath(string fileName)
        {
            string path = Path.Combine(ToolsRoot, fileName);
            return File.Exists(path) ? path : fileName;
        }

        public static bool IsReady() => _extracted && Verify(out _);

        // ── MD5 helpers ───────────────────────────────────────────────
        private static string FileMd5(string path)
        {
            using var md5 = MD5.Create();
            using var fs  = File.OpenRead(path);
            return BitConverter.ToString(md5.ComputeHash(fs));
        }

        private static string StreamMd5(Stream stream)
        {
            using var md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(stream));
        }
    }
}
