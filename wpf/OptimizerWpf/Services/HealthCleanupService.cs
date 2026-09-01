using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    public record RegistryFinding(string Category, string Display, string RegPath, string? ValueName);
    public record BrowserCacheEntry(string Name, string Label, IReadOnlyList<string> Paths);
    public record WinREStatus(bool Success, bool Enabled);

    // Port του Optimizer.ps1's Υγεία & Συντήρηση καρτέλα (~13300-13792): Registry Cleaner (ΣΚΟΠΙΜΑ
    // στενός/ασφαλής - μόνο 2 κατηγορίες), Καθαρισμός Cache Περιηγητών, WinRE.
    public static class HealthCleanupService
    {
        // Ίδιες προστατευμένες διαδρομές με το ps1 original - ΠΟΤΕ δεν προτείνονται προς διαγραφή.
        private static readonly string[] ProtectedFolders =
        {
            "System32", "SysWOW64", "Sysnative", "WinSxS", "servicing", "SystemApps", "Microsoft.NET", "Fonts",
        };

        public static Task<IReadOnlyList<RegistryFinding>> ScanRegistryAsync() => Task.Run(ScanRegistry);

        private static IReadOnlyList<RegistryFinding> ScanRegistry()
        {
            var findings = new List<RegistryFinding>();
            var systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
            var protectedPaths = ProtectedFolders.Select(f => Path.Combine(systemRoot, f)).ToArray();

            foreach (var (hive, subPath) in new[]
                     {
                         (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                         (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                         (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                     })
            {
                using var uninstallKey = hive.OpenSubKey(subPath);
                if (uninstallKey == null) continue;
                foreach (var name in uninstallKey.GetSubKeyNames())
                {
                    using var sub = uninstallKey.OpenSubKey(name);
                    if (sub == null) continue;
                    var displayName = sub.GetValue("DisplayName") as string;
                    var uninstallString = sub.GetValue("UninstallString") as string;
                    if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(uninstallString)) continue;
                    if (sub.GetValue("SystemComponent") != null || sub.GetValue("WindowsInstaller") != null || sub.GetValue("NoRemove") != null) continue;

                    var exePath = GetExePath(uninstallString);
                    if (exePath == null || IsProtected(exePath, protectedPaths) || Exists(exePath)) continue;

                    var fullRegPath = $"{HiveName(hive)}\\{subPath}\\{name}";
                    findings.Add(new RegistryFinding("MissingUninstaller", displayName, fullRegPath, null));
                }
            }

            const string muiKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache";
            using var mui = Registry.CurrentUser.OpenSubKey(muiKey);
            if (mui != null)
            {
                foreach (var valueName in mui.GetValueNames())
                {
                    if (valueName.StartsWith("PS", StringComparison.Ordinal)) continue;
                    var cleanPath = System.Text.RegularExpressions.Regex.Replace(valueName, @"\.(FriendlyAppName|ApplicationCompany)$", "");
                    if (!System.Text.RegularExpressions.Regex.IsMatch(cleanPath, @"^[a-zA-Z]:\\")) continue;
                    if (Exists(cleanPath)) continue;
                    findings.Add(new RegistryFinding("ObsoleteMuiCache", cleanPath, $"HKCU\\{muiKey}", valueName));
                }
            }

            return findings;
        }

        // Αυτόματο .reg backup ΠΡΙΝ από κάθε διαγραφή (μέσω reg.exe export), ίδιο με το ps1 original.
        public static Task<bool> DeleteFindingsAsync(IReadOnlyList<RegistryFinding> findings, string backupPath) =>
            Task.Run(() => DeleteFindings(findings, backupPath));

        private static bool DeleteFindings(IReadOnlyList<RegistryFinding> findings, string backupPath)
        {
            try
            {
                foreach (var finding in findings)
                {
                    var exportArgs = $"export \"{finding.RegPath}\" \"{Path.Combine(backupPath, SafeFileName(finding.Display) + ".reg")}\" /y";
                    RunAndWait("reg.exe", exportArgs);
                }

                foreach (var finding in findings)
                {
                    var (hive, subKey) = SplitRegPath(finding.RegPath);
                    if (hive == null) continue;
                    if (finding.ValueName != null)
                    {
                        using var key = hive.OpenSubKey(subKey, writable: true);
                        key?.DeleteValue(finding.ValueName, throwOnMissingValue: false);
                    }
                    else
                    {
                        var parent = Path.GetDirectoryName(subKey)?.Replace('/', '\\') ?? "";
                        var leaf = Path.GetFileName(subKey);
                        using var parentKey = hive.OpenSubKey(parent, writable: true);
                        parentKey?.DeleteSubKeyTree(leaf, throwOnMissingSubKey: false);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Μόνο browsers που πραγματικά ανιχνεύτηκαν στο σύστημα (ο φάκελος User Data/Profiles τους
        // υπάρχει) εμφανίζονται - ίδια συμπεριφορά με το ps1's δυναμικό $browserCacheCatalog.
        public static IReadOnlyList<BrowserCacheEntry> DetectBrowsers()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var candidates = new (string Name, string Label, string BaseDir, string CachePattern)[]
            {
                ("Chrome", "Google Chrome", Path.Combine(localAppData, "Google", "Chrome", "User Data"), "Cache"),
                ("Edge", "Microsoft Edge", Path.Combine(localAppData, "Microsoft", "Edge", "User Data"), "Cache"),
                ("Brave", "Brave", Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data"), "Cache"),
                ("Opera", "Opera", Path.Combine(appData, "Opera Software", "Opera Stable"), "Cache"),
                ("Firefox", "Mozilla Firefox", Path.Combine(appData, "Mozilla", "Firefox", "Profiles"), "cache2"),
            };

            var result = new List<BrowserCacheEntry>();
            foreach (var c in candidates)
            {
                if (!Directory.Exists(c.BaseDir)) continue;
                var paths = c.Name == "Firefox"
                    ? SafeEnumerateDirectories(c.BaseDir).Select(p => Path.Combine(p, c.CachePattern)).ToList()
                    : new List<string> { c.BaseDir }; // Chrome/Edge/Brave: κάθε προφίλ υποφάκελος μέσα εδώ, το caller σαρώνει *\Cache
                result.Add(new BrowserCacheEntry(c.Name, c.Label, paths));
            }
            return result;
        }

        public static Task ClearBrowserCacheAsync(BrowserCacheEntry browser) => Task.Run(() =>
        {
            foreach (var basePath in browser.Paths)
            {
                if (browser.Name is "Chrome" or "Edge" or "Brave")
                {
                    // Κάθε προφίλ (Default, Profile 1, ...) έχει δικό του Cache/Code Cache/GPUCache υποφάκελο.
                    foreach (var profileDir in SafeEnumerateDirectories(basePath))
                    {
                        foreach (var cacheSub in new[] { "Cache", "Code Cache", "GPUCache" })
                        {
                            DeleteDirectoryContents(Path.Combine(profileDir, cacheSub));
                        }
                    }
                }
                else
                {
                    DeleteDirectoryContents(basePath);
                }
            }
        });

        public static Task<WinREStatus> GetWinREStatusAsync() => Task.Run(() =>
        {
            try
            {
                var output = RunAndCapture("reagentc.exe", "/info");
                var statusLine = output.Split('\n').FirstOrDefault(l => l.Contains("Windows RE status"));
                var enabled = statusLine != null && statusLine.Contains("Enabled") && !statusLine.Contains("Disabled");
                return new WinREStatus(true, enabled);
            }
            catch
            {
                return new WinREStatus(false, false);
            }
        });

        public static Task<bool> EnableWinREAsync() => Task.Run(() => RunAndWait("reagentc.exe", "/enable") == 0);

        // ===== helpers =====

        private static string? GetExePath(string uninstallString)
        {
            var s = uninstallString.Trim();
            if (s.StartsWith('"'))
            {
                var endQuote = s.IndexOf('"', 1);
                if (endQuote > 0) return s[1..endQuote];
            }
            return s.Split(' ', 2)[0];
        }

        private static bool IsProtected(string path, string[] protectedPaths) =>
            protectedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        private static bool Exists(string path)
        {
            try { return File.Exists(Environment.ExpandEnvironmentVariables(path)) || Directory.Exists(Environment.ExpandEnvironmentVariables(path)); }
            catch { return false; }
        }

        private static string HiveName(RegistryKey hive) => hive == Registry.LocalMachine ? "HKLM" : "HKCU";

        private static (RegistryKey? Hive, string SubKey) SplitRegPath(string fullPath)
        {
            if (fullPath.StartsWith("HKLM\\")) return (Registry.LocalMachine, fullPath["HKLM\\".Length..]);
            if (fullPath.StartsWith("HKCU\\")) return (Registry.CurrentUser, fullPath["HKCU\\".Length..]);
            return (null, "");
        }

        private static string SafeFileName(string name) =>
            string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

        private static IEnumerable<string> SafeEnumerateDirectories(string root)
        {
            try { return Directory.EnumerateDirectories(root); } catch { return Array.Empty<string>(); }
        }

        private static void DeleteDirectoryContents(string dir)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in SafeEnumerateFilesTopDown(dir))
            {
                try { File.Delete(f); } catch { }
            }
            foreach (var d in SafeEnumerateDirectories(dir))
            {
                try { Directory.Delete(d, true); } catch { }
            }
        }

        private static IEnumerable<string> SafeEnumerateFilesTopDown(string dir)
        {
            try { return Directory.EnumerateFiles(dir); } catch { return Array.Empty<string>(); }
        }

        private static int RunAndWait(string fileName, string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false, CreateNoWindow = true });
            process?.WaitForExit();
            return process?.ExitCode ?? -1;
        }

        private static string RunAndCapture(string fileName, string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            });
            var output = process?.StandardOutput.ReadToEnd() ?? "";
            process?.WaitForExit();
            return output;
        }
    }
}
