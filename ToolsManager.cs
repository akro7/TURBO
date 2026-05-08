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
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EkoFlashTool", "bin");

        private static bool _extracted;

        // Embedded resources -> extracted file path (relative to ToolsRoot)
        private static readonly Dictionary<string, string> _resourceMap = new()
        {
            ["res.pt.adb.exe"]          = Path.Combine("platform-tools", "adb.exe"),
            ["res.pt.fastboot.exe"]     = Path.Combine("platform-tools", "fastboot.exe"),
            ["res.pt.AdbWinApi.dll"]    = Path.Combine("platform-tools", "AdbWinApi.dll"),
            ["res.pt.AdbWinUsbApi.dll"] = Path.Combine("platform-tools", "AdbWinUsbApi.dll"),

            ["res.ok.ekoflash.exe"]     = Path.Combine("odin", "ekoflash.exe"),
            ["res.ok.libusb.dll"]       = Path.Combine("odin", "libusb-1.0.dll"),

            ["res.zd.zadig.exe"]        = Path.Combine("zadig", "zadig.exe")
        };

        public static void EnsureExtracted()
        {
            if (_extracted) return;

            try
            {
                Directory.CreateDirectory(ToolsRoot);
                var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

                foreach (var kv in _resourceMap)
                {
                    var logicalName = kv.Key;
                    var relPath = kv.Value;
                    var dst = Path.Combine(ToolsRoot, relPath);

                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

                    using var stream = asm.GetManifestResourceStream(logicalName);
                    if (stream == null)
                    {
                        App.CrashLog("ToolsManager", $"Resource not found: {logicalName}");
                        continue;
                    }

                    if (File.Exists(dst))
                    {
                        var srcMd5 = StreamMd5(stream);
                        stream.Seek(0, SeekOrigin.Begin);
                        var dstMd5 = FileMd5(dst);
                        if (string.Equals(srcMd5, dstMd5, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

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

        public static void Reset()
        {
            _extracted = false;
            try
            {
                if (Directory.Exists(ToolsRoot))
                    Directory.Delete(ToolsRoot, true);
            }
            catch (Exception ex)
            {
                App.CrashLog("ToolsManager.Reset", ex.Message);
            }
        }

        public static bool ExeExists(string folder, string exe)
        {
            var p = GetExePath(folder, exe);
            return File.Exists(p);
        }

        public static string GetExePath(string folder, string exe)
        {
            var f = (folder ?? "").Trim().ToLowerInvariant();
            var e = (exe ?? "").Trim().ToLowerInvariant();

            if (!e.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                !e.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                e += ".exe";
            }

            return f switch
            {
                "platform-tools" => Path.Combine(ToolsRoot, "platform-tools", e),
                "odin"           => Path.Combine(ToolsRoot, "odin", e),
                "zadig"          => Path.Combine(ToolsRoot, "zadig", e),
                _                => Path.Combine(ToolsRoot, e)
            };
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
