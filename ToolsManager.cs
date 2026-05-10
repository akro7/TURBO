using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace MKVenomTool
{
    public static class ToolsManager
    {
        public static readonly string ToolsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EkoFlashTool",
            "bin");

        private static bool _extracted;
        private static readonly object Sync = new();

        private static readonly Dictionary<string, (string Folder, string FileName)> ResourceMapping = new()
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

        public static void EnsureExtracted()
        {
            if (_extracted)
                return;

            lock (Sync)
            {
                if (_extracted)
                    return;

                var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var resourceNames = asm.GetManifestResourceNames();

                foreach (var entry in ResourceMapping)
                {
                    string resourceKey = entry.Key;
                    var (folder, fileName) = entry.Value;

                    string targetFolder = Path.Combine(ToolsRoot, folder);
                    string targetPath = Path.Combine(targetFolder, fileName);

                    Directory.CreateDirectory(targetFolder);

                    if (File.Exists(targetPath))
                    {
                        try
                        {
                            var fi = new FileInfo(targetPath);
                            if (fi.Length > 0)
                                continue;
                        }
                        catch
                        {
                        }
                    }

                    string? fullResourceName = resourceNames.FirstOrDefault(n =>
                        n.Equals(resourceKey, StringComparison.OrdinalIgnoreCase) ||
                        n.EndsWith("." + resourceKey, StringComparison.OrdinalIgnoreCase));

                    if (fullResourceName == null)
                        continue;

                    ExtractWithRetry(asm, fullResourceName, targetPath);
                }

                _extracted = true;
            }
        }

        private static void ExtractWithRetry(Assembly asm, string resourceName, string targetPath)
        {
            const int maxAttempts = 6;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                string tempPath = targetPath + ".tmp";

                try
                {
                    using (var stream = asm.GetManifestResourceStream(resourceName))
                    {
                        if (stream == null)
                            throw new IOException($"Embedded resource not found: {resourceName}");

                        if (File.Exists(tempPath))
                            File.Delete(tempPath);

                        using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            stream.CopyTo(fs);
                        }
                    }

                    if (File.Exists(targetPath))
                        File.Delete(targetPath);

                    File.Move(tempPath, targetPath);
                    return;
                }
                catch (IOException)
                {
                    if (File.Exists(targetPath))
                        return;

                    if (attempt == maxAttempts)
                        throw;

                    Thread.Sleep(220 * attempt);
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        public static bool ExeExists(string folder, string exeName)
        {
            return File.Exists(GetExePath(folder, exeName));
        }

        public static string GetExePath(string folder, string exeName)
        {
            string cleanName = exeName.Trim();

            if (!cleanName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                !cleanName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                cleanName += ".exe";
            }

            return Path.Combine(ToolsRoot, folder, cleanName);
        }

        public static void ResetTools()
        {
            try
            {
                if (Directory.Exists(ToolsRoot))
                    Directory.Delete(ToolsRoot, true);

                _extracted = false;
            }
            catch
            {
            }
        }
    }
}
