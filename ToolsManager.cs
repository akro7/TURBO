using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace MKVenomTool
{
    public static class ToolsManager
    {
        public static readonly string ToolsRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EkoFlashTool", "bin");

        private static bool _extracted;

        private static readonly string[] _files =
        {
            "ekoflash.exe",
            "adb.exe",
            "fastboot.exe",
            "AdbWinApi.dll",
            "AdbWinUsbApi.dll",
            "libusb-1.0.dll",
            "zadig.exe"
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
                var allResources = asm.GetManifestResourceNames();

                foreach (var fileName in _files)
                {
                    var logicalName = FindResourceName(allResources, fileName);
                    if (logicalName == null)
                    {
                        App.CrashLog("ToolsManager", $"Resource not found for file: {fileName}");
                        continue;
                    }

                    using var stream = asm.GetManifestResourceStream(logicalName);
                    if (stream == null)
                    {
                        App.CrashLog("ToolsManager", $"Resource stream is null: {logicalName}");
                        continue;
                    }

                    string dest = Path.Combine(ToolsRoot, fileName);

                    if (File.Exists(dest))
                    {
                        var srcMd5 = StreamMd5(stream);
                        stream.Seek(0, SeekOrigin.Begin);
                        var dstMd5 = FileMd5(dest);
                        if (string.Equals(srcMd5, dstMd5, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    using var fs = File.Create(dest);
                    stream.CopyTo(fs);
                }

                _extracted = true;
            }
            catch (Exception ex)
            {
                App.CrashLog("ToolsManager.EnsureExtracted", ex.ToString());
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
            foreach (var file in _files)
            {
                if (!File.Exists(Path.Combine(ToolsRoot, file)))
                    missing.Add(file);
            }
            return missing.Count == 0;
        }

        public static string GetToolPath(string fileName)
        {
            var path = Path.Combine(ToolsRoot, fileName);
            return File.Exists(path) ? path : fileName;
        }

        public static bool ExeExists(string folder, string exe)
        {
            return File.Exists(GetExePath(folder, exe));
        }

        public static string GetExePath(string folder, string exe)
        {
            string e = (exe ?? "").Trim();
            if (!e.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                !e.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                e += ".exe";
            }

            return Path.Combine(ToolsRoot, e);
        }

        public static bool IsReady() => _extracted && Verify(out _);

        private static string? FindResourceName(IEnumerable<string> names, string fileName)
        {
            return names.FirstOrDefault(n =>
                n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(n, fileName, StringComparison.OrdinalIgnoreCase));
        }

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
