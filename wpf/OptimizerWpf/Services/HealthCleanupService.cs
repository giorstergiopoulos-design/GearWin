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
    public record RegistryBackupEntry(string FileName, string FullPath, DateTime CreatedAt, long SizeBytes);

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

            // ΝΕΟ - ρητό αίτημα χρήστη ("ενσωμάτωσε τον καθαρισμό registry" ως λειτουργία): 2 ακόμα
            // ΑΣΦΑΛΕΙΣ κατηγορίες (ίδιο αυστηρό κριτήριο με τα παραπάνω - flag ΜΟΝΟ όταν το exe-path
            // που δείχνει η καταχώρηση δεν υπάρχει πια στον δίσκο, ΠΟΤΕ heuristic/μαντεψιά).
            // App Paths: HKLM/HKCU ...\App Paths\<exe> - η (Default) τιμή είναι η πλήρης διαδρομή που
            // χρησιμοποιεί ο Windows Explorer/Run dialog για να εντοπίσει το exe με βάση μόνο το όνομά
            // του· μένει πίσω σαν "ορφανή" καταχώρηση μετά από απεγκατάσταση που δεν καθάρισε το κλειδί.
            foreach (var (hive, subPath) in new[]
                     {
                         (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths"),
                         (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths"),
                     })
            {
                using var appPathsKey = hive.OpenSubKey(subPath);
                if (appPathsKey == null) continue;
                foreach (var name in appPathsKey.GetSubKeyNames())
                {
                    using var sub = appPathsKey.OpenSubKey(name);
                    var exePath = sub?.GetValue(null) as string;
                    if (string.IsNullOrWhiteSpace(exePath) || IsProtected(exePath, protectedPaths) || Exists(exePath)) continue;
                    findings.Add(new RegistryFinding("OrphanedAppPath", exePath, $"{HiveName(hive)}\\{subPath}\\{name}", null));
                }
            }

            // Καταχωρήσεις αυτόματης εκκίνησης (Run) που δείχνουν σε exe που πια δεν υπάρχει - το ίδιο
            // "σκουπίδι μητρώου" που αφήνει πίσω μια απεγκατάσταση/μετακίνηση προγράμματος, ΔΕΝ αγγίζει
            // καμία τρέχουσα λειτουργική καταχώρηση εκκίνησης (ίδιο κριτήριο Exists() με τα παραπάνω).
            foreach (var (hive, subPath) in new[]
                     {
                         (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
                         (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
                     })
            {
                using var runKey = hive.OpenSubKey(subPath);
                if (runKey == null) continue;
                foreach (var valueName in runKey.GetValueNames())
                {
                    var command = runKey.GetValue(valueName) as string;
                    if (string.IsNullOrWhiteSpace(command)) continue;
                    var exePath = GetExePath(command);
                    if (exePath == null || IsProtected(exePath, protectedPaths) || Exists(exePath)) continue;
                    findings.Add(new RegistryFinding("OrphanedStartupEntry", valueName, $"{HiveName(hive)}\\{subPath}", valueName));
                }
            }

            // ΝΕΟ - roadmap "Registry Cleaner - περισσότερες ασφαλείς κατηγορίες": Shared DLLs
            // (HKLM ...\SharedDLLs) - κάθε τιμή είναι η πλήρης διαδρομή ενός DLL που καταμετράει
            // αναφορές (reference count) πολλαπλών εγκατεστημένων προγραμμάτων· ένα leftover μετά από
            // ατελή απεγκατάσταση δείχνει σε DLL που πια δεν υπάρχει - ίδιο αυστηρό κριτήριο Exists()
            // με τις υπόλοιπες κατηγορίες.
            using (var sharedDlls = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs"))
            {
                if (sharedDlls != null)
                {
                    foreach (var valueName in sharedDlls.GetValueNames())
                    {
                        if (string.IsNullOrWhiteSpace(valueName) || IsProtected(valueName, protectedPaths) || Exists(valueName)) continue;
                        findings.Add(new RegistryFinding("OrphanedSharedDll", valueName,
                            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs", valueName));
                    }
                }
            }

            return findings;
        }

        // Αυτόματο .reg backup ΠΡΙΝ από κάθε διαγραφή (μέσω reg.exe export), ίδιο με το ps1 original.
        public static Task<bool> DeleteFindingsAsync(IReadOnlyList<RegistryFinding> findings, string backupPath) =>
            Task.Run(() => DeleteFindings(findings, backupPath));

        // ΝΕΟ - roadmap "Backup registry - καμία διεπαφή επαναφοράς": τα .reg backups (βλ. DeleteFindings
        // παραπάνω) υπήρχαν ήδη στο δίσκο αλλά καμία οθόνη δεν επέτρεπε να τα δεις/επαναφέρεις μέσα από
        // την εφαρμογή - μόνο χειροκίνητο διπλό-κλικ έξω από αυτήν. Καθαρή ανάγνωση/επαναφορά, καμία
        // αλλαγή στο ΠΩΣ παίρνονται τα backups.
        public static string RegistryBackupsDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf", "RegistryBackups");

        public static Task<IReadOnlyList<RegistryBackupEntry>> ListRegistryBackupsAsync() => Task.Run(() =>
        {
            if (!Directory.Exists(RegistryBackupsDir)) return (IReadOnlyList<RegistryBackupEntry>)Array.Empty<RegistryBackupEntry>();
            return (IReadOnlyList<RegistryBackupEntry>)Directory.EnumerateFiles(RegistryBackupsDir, "*.reg")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .Select(f => new RegistryBackupEntry(f.Name, f.FullName, f.LastWriteTime, f.Length))
                .ToList();
        });

        // reg.exe import εφαρμόζει το backup ΩΣ ΕΧΕΙ (επαναφέρει τις καταχωρήσεις όπως ήταν πριν τη
        // διαγραφή) - exit code 0 = επιτυχία, ίδιο μοτίβο ελέγχου με RunAndWait αλλού στο αρχείο.
        public static Task<bool> RestoreRegistryBackupAsync(string filePath) =>
            Task.Run(() => RunAndWait("reg.exe", $"import \"{filePath}\"") == 0);

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
                // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): έλειπε εντελώς το Vivaldi - το ps1 original
                // το ανιχνεύει (ίδιο μοτίβο με Chrome/Edge/Brave, ίδια Chromium-based δομή προφίλ).
                ("Vivaldi", "Vivaldi", Path.Combine(localAppData, "Vivaldi", "User Data"), "Cache"),
                ("Firefox", "Mozilla Firefox", Path.Combine(appData, "Mozilla", "Firefox", "Profiles"), "cache2"),
            };

            var result = new List<BrowserCacheEntry>();
            foreach (var c in candidates)
            {
                if (!Directory.Exists(c.BaseDir)) continue;
                // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): το Firefox χωρίζει τα δεδομένα προφίλ - το
                // ΙΔΙΟ το προφίλ (bookmarks/prefs) είναι στο Roaming (%APPDATA%), αλλά το ΙΔΙΟ το cache2
                // είναι ΠΑΝΤΑ στο Local (%LOCALAPPDATA%) με το ΙΔΙΟ όνομα υποφακέλου προφίλ - πριν, ο
                // κώδικας έψαχνε cache2 ΜΕΣΑ στο Roaming path (όπου δεν υπάρχει ποτέ), άρα ο καθαρισμός
                // "πετύχαινε" σιωπηλά χωρίς να διαγράφει ποτέ τίποτα πραγματικό.
                var paths = c.Name == "Firefox"
                    ? SafeEnumerateDirectories(c.BaseDir).Select(p => Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles", Path.GetFileName(p), c.CachePattern)).ToList()
                    : new List<string> { c.BaseDir }; // Chrome/Edge/Brave/Vivaldi: κάθε προφίλ υποφάκελος μέσα εδώ, το caller σαρώνει *\Cache
                result.Add(new BrowserCacheEntry(c.Name, c.Label, paths));
            }
            return result;
        }

        // ΝΕΟ - ρητό αίτημα χρήστη: το μέγεθος cache περιηγητών να συνυπολογίζεται στον Πλήρη Έλεγχο
        // Υγείας (HealthCheckWindow's στάδιο "Καθαρισμός Χώρου"), ΧΩΡΙΣ να αγγίξει καθόλου την ήδη
        // υπάρχουσα, ξεχωριστή ανά-περιηγητή λίστα/κουμπιά στην καρτέλα Υγεία & Συντήρηση (ρητή
        // οδηγία χρήστη: "άσε τα as they are") - μόνο ανάγνωση μεγέθους εδώ, επαναχρησιμοποιεί το ίδιο
        // DetectBrowsers() ώστε να μετρά ΑΚΡΙΒΩΣ τους ίδιους φακέλους που θα καθάριζε το υπάρχον κουμπί.
        public static Task<long> ScanBrowserCacheSizeAsync() => Task.Run(() =>
        {
            long total = 0;
            foreach (var browser in DetectBrowsers())
            {
                foreach (var basePath in browser.Paths)
                {
                    if (browser.Name is "Chrome" or "Edge" or "Brave" or "Vivaldi")
                    {
                        foreach (var profileDir in SafeEnumerateDirectories(basePath))
                            foreach (var cacheSub in new[] { "Cache", "Code Cache", "GPUCache" })
                                total += GetDirectorySize(Path.Combine(profileDir, cacheSub));
                    }
                    else
                    {
                        total += GetDirectorySize(basePath);
                    }
                }
            }
            return total;
        });

        private static long GetDirectorySize(string dir)
        {
            if (!Directory.Exists(dir)) return 0;
            long size = 0;
            foreach (var f in SafeEnumerateFilesTopDown(dir))
            {
                try { size += new FileInfo(f).Length; } catch { }
            }
            foreach (var d in SafeEnumerateDirectories(dir)) size += GetDirectorySize(d);
            return size;
        }

        public static Task ClearBrowserCacheAsync(BrowserCacheEntry browser) => Task.Run(() =>
        {
            foreach (var basePath in browser.Paths)
            {
                if (browser.Name is "Chrome" or "Edge" or "Brave" or "Vivaldi")
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

        // ===== Εργαλεία Υγείας & Συντήρησης Συστήματος (ρητό αίτημα χρήστη: "τα εργαλεία υγείας
        // συντήρησης συστήματος δεν φαίνονται πουθενά" - ολόκληρη αυτή η ενότητα, port του ps1's
        // Add-HealthToolButton (~13302-13400), έλειπε εντελώς από το WPF port). Σε αντίθεση με το
        // ps1 original (ανοίγει ελαχιστοποιημένο console παράθυρο PowerShell, Invoke-DiagnosticCommand
        // ~63), εδώ η έξοδος καταγράφεται και εμφανίζεται σε MessageBox μέσα στην ίδια την εφαρμογή -
        // πιο καθαρό WPF-native αποτέλεσμα, καμία λειτουργική διαφορά.

        public static Task<(bool Success, string Output)> RunSfcScanAsync() => RunCommandCaptureAsync("sfc.exe", "/scannow");
        public static Task<(bool Success, string Output)> RunDismCheckHealthAsync() => RunCommandCaptureAsync("Dism.exe", "/Online /Cleanup-Image /CheckHealth");
        public static Task<(bool Success, string Output)> RunDismRestoreHealthAsync() => RunCommandCaptureAsync("Dism.exe", "/Online /Cleanup-Image /RestoreHealth");
        public static Task<(bool Success, string Output)> RunChkdskAsync() => RunCommandCaptureAsync("chkdsk.exe", "C: /f", stdin: "Y\r\n");
        public static Task<(bool Success, string Output)> RunWinSxsCleanupAsync() => RunCommandCaptureAsync("Dism.exe", "/online /Cleanup-Image /StartComponentCleanup");

        // ΝΕΟ - roadmap "Διαγνωστική αναφορά με ένα κλικ" - συμπίεση βασικών logs/πληροφοριών σε ένα
        // .zip, έτοιμο να μοιραστεί όταν κάποιος ζητά βοήθεια. Μαζεύει systeminfo (ίδια πηγή με το ήδη
        // υπάρχον AdvancedToolsService.CreateSystemReport) + οι τελευταίες 50 καταχωρήσεις Application/
        // System Event Log (Error/Warning μόνο - όχι θόρυβος) + το ίδιο το in-app Ιστορικό Ενεργειών.
        public static async Task<string?> GenerateDiagnosticsZipAsync()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"OptimizerWpf_Diag_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var sysInfoPath = Path.Combine(tempDir, "systeminfo.txt");
                using (var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c systeminfo > \"{sysInfoPath}\"") { UseShellExecute = false, CreateNoWindow = true }))
                    if (p != null) await p.WaitForExitAsync();

                File.WriteAllText(Path.Combine(tempDir, "action_log.txt"), string.Join("\n", ActionLogService.Entries));

                var eventLogText = await Task.Run(() => CaptureRecentEventErrors());
                File.WriteAllText(Path.Combine(tempDir, "recent_event_errors.txt"), eventLogText);

                var zipPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    $"OptimizerWpf_Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, zipPath);
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{zipPath}\"") { UseShellExecute = true });
                return zipPath;
            }
            catch { return null; }
            finally { try { Directory.Delete(tempDir, true); } catch { } }
        }

        private static string CaptureRecentEventErrors()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var logName in new[] { "Application", "System" })
            {
                try
                {
                    var session = new System.Diagnostics.Eventing.Reader.EventLogQuery(logName, System.Diagnostics.Eventing.Reader.PathType.LogName,
                        "*[System[(Level=1 or Level=2)]]");
                    using var reader = new System.Diagnostics.Eventing.Reader.EventLogReader(session);
                    sb.AppendLine($"=== {logName} (τελευταία 25 Error/Warning) ===");
                    var count = 0;
                    for (var e = reader.ReadEvent(); e != null && count < 25; e = reader.ReadEvent())
                    {
                        sb.AppendLine($"[{e.TimeCreated:yyyy-MM-dd HH:mm}] {e.LevelDisplayName} - {e.ProviderName}: {e.FormatDescription()?.Split('\n').FirstOrDefault()}");
                        count++;
                    }
                    sb.AppendLine();
                }
                catch (Exception ex) { sb.AppendLine($"=== {logName}: {ex.Message} ===\n"); }
            }
            return sb.ToString();
        }

        // ΝΕΟ - roadmap "Αναφορά μπαταρίας" - powercfg /batteryreport είναι το επίσημο εργαλείο των
        // Windows (υγεία μπαταρίας, χωρητικότητα σχεδίασης έναντι πραγματικής) - η εφαρμογή απλώς
        // παράγει την αναφορά και την ανοίγει στον προεπιλεγμένο browser (ίδιο πνεύμα με το ήδη
        // υπάρχον AdvancedToolsService.CreateSystemReport). Σε desktop χωρίς μπαταρία το powercfg
        // επιστρέφει μη-μηδενικό κωδικό εξόδου - honest failure, καμία ψεύτικη αναφορά.
        public static async Task<bool> GenerateBatteryReportAsync()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OptimizerWpf_BatteryReport.html");
            var exit = await Task.Run(() => RunAndWait("powercfg.exe", $"/batteryreport /output \"{path}\""));
            if (exit != 0 || !System.IO.File.Exists(path)) return false;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }

        public static Task<bool> OptimizeSsdRetrimAsync() => Task.Run(() =>
            RunAndWait("powershell.exe", "-NoProfile -Command \"Optimize-Volume -DriveLetter C -ReTrim -ErrorAction SilentlyContinue\"") == 0);

        // Port του ps1's inline broken-shortcut scan (~13374) - COM WScript.Shell δεν έχει καθαρό .NET
        // ισοδύναμο χωρίς πρόσθετη COM αναφορά, οπότε τρέχει μέσω σύντομου PowerShell script (ίδιο
        // μοτίβο με το DriverService.cs's RunPowerShellScriptAsync).
        public static async Task<int> FixBrokenShortcutsAsync()
        {
            const string script = @"
$shellCom = New-Object -ComObject WScript.Shell
$searchPaths = @(""$env:USERPROFILE\Desktop"", ""$env:APPDATA\Microsoft\Windows\Start Menu"") | Where-Object { Test-Path $_ }
$brokenCount = 0
Get-ChildItem -Path $searchPaths -Recurse -Include *.lnk -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $target = $shellCom.CreateShortcut($_.FullName).TargetPath
        if ($target -and -not (Test-Path $target)) {
            Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
            $brokenCount++
        }
    } catch {}
}
Write-Output $brokenCount
";
            var output = await RunPowerShellScriptAsync(script);
            return int.TryParse(output.Trim(), out var count) ? count : 0;
        }

        // Απευθείας ανάγνωση μητρώου (ΟΧΙ shell-out σε PowerShell, πιο απλό/γρήγορο για μια απλή τιμή) -
        // port του ps1's .NET Framework version check (~13398).
        public static string GetDotNetFrameworkVersionInfo()
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
            var release = key?.GetValue("Release") as int?;
            return release switch
            {
                >= 528040 => $"{LanguageService.T("HealthSvc_DotNetInstalledPrefix")}{release}{LanguageService.T("HealthSvc_DotNet48Plus")}",
                >= 461808 => $"{LanguageService.T("HealthSvc_DotNetInstalledPrefix")}{release}{LanguageService.T("HealthSvc_DotNet472")}",
                not null => $"{LanguageService.T("HealthSvc_DotNetInstalledPrefix")}{release}{LanguageService.T("HealthSvc_DotNetOlder")}",
                null => LanguageService.T("HealthSvc_DotNetNotFound"),
            };
        }

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

        // Χρησιμοποιείται από τα εργαλεία υγείας (SFC/DISM/CHKDSK/WinSxS) - μπορεί να πάρει αρκετά
        // λεπτά (ειδικά DISM RestoreHealth), γι' αυτό Task.Run + καμία timeout εδώ (ο καλών code-behind
        // δείχνει StatusService.SetBusy όσο περιμένει).
        private static Task<(bool Success, string Output)> RunCommandCaptureAsync(string fileName, string arguments, string? stdin = null) =>
            Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo(fileName, arguments)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardInput = stdin != null,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                    };
                    using var process = Process.Start(psi);
                    if (process == null) return (false, LanguageService.T("HealthSvc_ProcessStartFailed"));
                    if (stdin != null)
                    {
                        process.StandardInput.Write(stdin);
                        process.StandardInput.Close();
                    }
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return (process.ExitCode == 0, output);
                }
                catch (Exception ex)
                {
                    return (false, $"{LanguageService.T("HealthSvc_ErrorPrefix")}{ex.Message}");
                }
            });

        private static async Task<string> RunPowerShellScriptAsync(string script)
        {
            var scriptFile = Path.Combine(Path.GetTempPath(), $"OptimizerWpfHealth_{Guid.NewGuid():N}.ps1");
            File.WriteAllText(scriptFile, script);
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptFile}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                };
                using var process = Process.Start(psi);
                if (process == null) return "";
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                return output;
            }
            finally
            {
                try { File.Delete(scriptFile); } catch { }
            }
        }
    }
}
