using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    public record StartupItem(string Name, string Command, string Location, bool IsDisabled);
    public record ProcessRow(int Pid, string Name, double WorkingSetMb, bool IsCritical);
    public record FileGroup(string Label, IReadOnlyList<(string Path, double SizeMb)> Files);
    public record RestorePointInfo(int SequenceNumber, string Description, DateTime CreationTime);
    public record ServiceRow(string ServiceName, string DisplayName, string Description, string StartMode);
    public record BenchmarkResult(double WriteMbps, double ReadMbps, string? Error);

    // Port του Optimizer.ps1's Σύστημα καρτέλα (~15622-16518): Εκκίνηση/Διεργασίες/Αποθηκευτικός
    // Χώρος/Σημεία Επαναφοράς/Υπηρεσίες/Δοκιμή Ταχύτητας Δίσκου. Βλ. HANDOFF.md §0.4ιβ.
    public static class SystemService
    {
        // ===== Εφαρμογές Εκκίνησης - ΠΑΝΤΑ αναστρέψιμο (rename τιμής, ΠΟΤΕ διαγραφή δεδομένων) =====

        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string DisabledPrefix = "OptimizerDisabled_";

        public static Task<IReadOnlyList<StartupItem>> LoadStartupItemsAsync() => Task.Run(LoadStartupItems);

        private static IReadOnlyList<StartupItem> LoadStartupItems()
        {
            var items = new List<StartupItem>();
            foreach (var (hive, label) in new[] { (Registry.CurrentUser, "HKCU"), (Registry.LocalMachine, "HKLM") })
            {
                using var key = hive.OpenSubKey(RunKeyPath);
                if (key == null) continue;
                foreach (var valueName in key.GetValueNames())
                {
                    var command = key.GetValue(valueName) as string ?? "";
                    if (valueName.StartsWith(DisabledPrefix, StringComparison.Ordinal))
                    {
                        var realName = valueName[DisabledPrefix.Length..];
                        if (items.Any(i => i.Name == realName)) continue;
                        items.Add(new StartupItem(realName, command, label, true));
                    }
                    else
                    {
                        items.Add(new StartupItem(valueName, command, label, false));
                    }
                }
            }
            return items;
        }

        public static void SetStartupItemEnabled(StartupItem item, bool enabled)
        {
            var hive = item.Location == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
            using var key = hive.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;
            if (enabled)
            {
                var disabledName = DisabledPrefix + item.Name;
                var value = key.GetValue(disabledName) as string;
                if (value == null) return;
                key.SetValue(item.Name, value);
                key.DeleteValue(disabledName, throwOnMissingValue: false);
            }
            else
            {
                var value = key.GetValue(item.Name) as string;
                if (value == null) return;
                key.SetValue(DisabledPrefix + item.Name, value);
                key.DeleteValue(item.Name, throwOnMissingValue: false);
            }
        }

        // ===== Διεργασίες Συστήματος =====

        private static readonly HashSet<string> CriticalProcessNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Idle", "Registry", "smss", "csrss", "wininit", "services", "lsass", "winlogon",
            "explorer", "dwm", "fontdrvhost", "sihost", "WUDFHost", "Memory Compression",
        };

        public static Task<IReadOnlyList<ProcessRow>> LoadProcessesAsync() => Task.Run(() =>
        {
            var rows = new List<ProcessRow>();
            foreach (var p in Process.GetProcesses().OrderByDescending(p => SafeWorkingSet(p)).Take(25))
            {
                rows.Add(new ProcessRow(p.Id, p.ProcessName, Math.Round(SafeWorkingSet(p) / 1024.0 / 1024.0, 1), CriticalProcessNames.Contains(p.ProcessName)));
            }
            return (IReadOnlyList<ProcessRow>)rows;
        });

        private static long SafeWorkingSet(Process p) { try { return p.WorkingSet64; } catch { return 0; } }

        public static bool KillProcess(int pid)
        {
            try { Process.GetProcessById(pid).Kill(); return true; }
            catch { return false; }
        }

        // ===== Αποθηκευτικός Χώρος - διπλότυπα/μεγάλα αρχεία, ΜΟΝΟ προσωπικοί φάκελοι =====

        private static IEnumerable<string> PersonalFolders()
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var f in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                         Path.Combine(profile, "Downloads"),
                         Path.Combine(profile, "Desktop"),
                         Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                         Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                     })
            {
                if (Directory.Exists(f)) yield return f;
            }
        }

        public static Task<IReadOnlyList<(string Path, double SizeMb)>> FindDuplicatesAsync() => Task.Run(() =>
        {
            var files = PersonalFolders().SelectMany(SafeEnumerateFiles)
                .Select(f => new FileInfo(f))
                .Where(fi => { try { return fi.Length is > 0 and < 500L * 1024 * 1024; } catch { return false; } })
                .ToList();

            var byLength = files.GroupBy(f => f.Length).Where(g => g.Count() > 1);
            var results = new List<(string, double)>();
            foreach (var group in byLength)
            {
                var byHash = group.GroupBy(f => ComputeHash(f.FullName)).Where(g => g.Key != null && g.Count() > 1);
                foreach (var hashGroup in byHash)
                {
                    foreach (var f in hashGroup) results.Add((f.FullName, Math.Round(f.Length / 1024.0 / 1024.0, 2)));
                }
            }
            return (IReadOnlyList<(string, double)>)results;
        });

        public static Task<IReadOnlyList<(string Path, double SizeMb)>> FindLargeFilesAsync() => Task.Run(() =>
        {
            var results = PersonalFolders().SelectMany(SafeEnumerateFiles)
                .Select(f => { try { return new FileInfo(f); } catch { return null; } })
                .Where(fi => fi != null)
                .OrderByDescending(fi => fi!.Length)
                .Take(50)
                .Select(fi => (fi!.FullName, Math.Round(fi.Length / 1024.0 / 1024.0, 2)))
                .ToList();
            return (IReadOnlyList<(string, double)>)results;
        });

        private static string? ComputeHash(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var sha = System.Security.Cryptography.SHA256.Create();
                return Convert.ToBase64String(sha.ComputeHash(stream));
            }
            catch { return null; }
        }

        private static IEnumerable<string> SafeEnumerateFiles(string root)
        {
            try { return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories); }
            catch { return Array.Empty<string>(); }
        }

        // Κάδος Ανακύκλωσης, ΠΟΤΕ μόνιμη διαγραφή.
        public static bool SendToRecycleBin(string path)
        {
            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                return true;
            }
            catch { return false; }
        }

        public static Task<bool> OneDriveFreeUpSpaceAsync() => Task.Run(() =>
        {
            var oneDrive = Environment.GetEnvironmentVariable("OneDrive");
            if (string.IsNullOrEmpty(oneDrive) || !Directory.Exists(oneDrive)) return false;
            try
            {
                using var process = Process.Start(new ProcessStartInfo("attrib.exe", $"+U -P /s /d \"{oneDrive}\\*.*\"")
                { UseShellExecute = false, CreateNoWindow = true });
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch { return false; }
        });

        // ===== Σημεία Επαναφοράς Συστήματος =====
        // Checkpoint-Computer/Get-ComputerRestorePoint/Restore-Computer δεν έχουν άμεσο .NET API
        // equivalent χωρίς WMI method-signature ρίσκο - shell out σε σύντομο PowerShell script, ίδιο
        // μοτίβο με το DriverService/WingetService.

        public static async Task<IReadOnlyList<RestorePointInfo>> ListRestorePointsAsync()
        {
            const string script = "Get-ComputerRestorePoint | Sort-Object SequenceNumber -Descending | " +
                                   "Select-Object SequenceNumber, Description, @{N='CreationTime';E={$_.ConvertToDateTime($_.CreationTime).ToString('o')}} | " +
                                   "ConvertTo-Json -Compress";
            var output = await RunPowerShellAsync(script);
            if (string.IsNullOrWhiteSpace(output)) return Array.Empty<RestorePointInfo>();
            try
            {
                var root = JsonDocument.Parse(output).RootElement;
                var elements = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : new[] { root }.AsEnumerable();
                return elements.Select(e => new RestorePointInfo(
                    e.GetProperty("SequenceNumber").GetInt32(),
                    e.TryGetProperty("Description", out var d) ? d.GetString() ?? "" : "",
                    DateTime.TryParse(e.GetProperty("CreationTime").GetString(), out var dt) ? dt : DateTime.MinValue)).ToList();
            }
            catch (JsonException) { return Array.Empty<RestorePointInfo>(); }
        }

        public static Task<bool> CreateRestorePointAsync() =>
            RunPowerShellForSuccessAsync($"Checkpoint-Computer -Description \"Windows Optimizer - {DateTime.Now:yyyy-MM-dd HH:mm}\" -RestorePointType MODIFY_SETTINGS");

        // ΣΚΟΠΙΜΑ ΔΕΝ υλοποιείται delete-by-sequence - τεκμηριωμένος περιορισμός στο ps1 original: το
        // WMI SystemRestore δεν έχει Delete method, το vssadmin έχει ασύμβατο ID scheme (ρίσκο λάθος
        // διαγραφής). Η επιλογή "Άνοιγμα Ρυθμίσεων Προστασίας" (SystemPropertiesProtection.exe) είναι
        // η ίδια "διέξοδος" που χρησιμοποιεί και το ps1 original - βλ. SystemView.xaml.cs.
        public static Task<bool> RestoreToPointAsync(int sequenceNumber) =>
            RunPowerShellForSuccessAsync($"Restore-Computer -RestorePoint {sequenceNumber}");

        // ===== Βελτιστοποίηση Υπηρεσιών - curated λίστα, μόνο υπηρεσίες που ήταν ήδη Automatic =====

        public static readonly IReadOnlyDictionary<string, string> SafeServicesToOptimize = new Dictionary<string, string>
        {
            ["DiagTrack"] = "Διαγνωστικά & Τηλεμετρία",
            ["dmwappushservice"] = "WAP Push Message Routing",
            ["MapsBroker"] = "Downloaded Maps Manager",
            ["lfsvc"] = "Υπηρεσία Τοποθεσίας Γεωγραφίας",
            ["WbioSrvc"] = "Βιομετρική Υπηρεσία (μην αγγίζετε αν χρησιμοποιείτε βιομετρικά)",
            ["WMPNetworkSvc"] = "Κοινή Χρήση Δικτύου Windows Media Player",
            ["PcaSvc"] = "Βοηθός Συμβατότητας Προγράμματος",
            ["SessionEnv"] = "Ρύθμιση Περιβάλλοντος Υπηρεσιών Απομακρυσμένης Επιφάνειας Εργασίας (μην αγγίζετε αν χρησιμοποιείτε RDP)",
            ["TermService"] = "Υπηρεσίες Απομακρυσμένης Επιφάνειας Εργασίας (μην αγγίζετε αν χρησιμοποιείτε RDP)",
            ["RemoteRegistry"] = "Απομακρυσμένο Μητρώο",
            ["TapiSrv"] = "Τηλεφωνία",
            ["TabletInputService"] = "Υπηρεσία Εισαγωγής Tablet (μην αγγίζετε αν έχετε οθόνη αφής)",
            ["SNMPTrap"] = "SNMP Trap",
            ["WebClient"] = "WebClient",
            ["WerSvc"] = "Αναφορά Σφαλμάτων Windows",
            ["Wecsvc"] = "Συλλέκτης Συμβάντων Windows",
            ["SDRSVC"] = "Αντίγραφα Ασφαλείας Windows",
            ["fdPHost"] = "Ανακάλυψη Συσκευών Function Discovery",
            ["FDResPub"] = "Δημοσίευση Πόρων Function Discovery",
            ["upnphost"] = "Οικοδεσπότης Συσκευών UPnP",
            ["SSDPSRV"] = "Ανακάλυψη SSDP",
            ["SysMain"] = "SysMain (Superfetch)",
            ["TrkWks"] = "Πελάτης Παρακολούθησης Κατανεμημένων Συνδέσμων",
            ["iphlpsvc"] = "Βοηθός IP",
            ["MSiSCSI"] = "Πρόγραμμα Εκκίνησης Microsoft iSCSI",
            ["WSearch"] = "Αναζήτηση Windows (μην αγγίζετε αν χρησιμοποιείτε συχνά την αναζήτηση)",
            ["WinRM"] = "Απομακρυσμένη Διαχείριση Windows",
            ["XblAuthManager"] = "Xbox Live Auth Manager (μην αγγίζετε αν χρησιμοποιείτε Xbox/Game Pass)",
            ["XblGameSave"] = "Xbox Live Game Save (μην αγγίζετε αν χρησιμοποιείτε Xbox/Game Pass)",
            ["XboxNetApiSvc"] = "Xbox Live Networking Service (μην αγγίζετε αν χρησιμοποιείτε Xbox/Game Pass)",
        };

        public static Task<IReadOnlyList<ServiceRow>> LoadServicesAsync() => Task.Run(() =>
        {
            var rows = new List<ServiceRow>();
            foreach (var (name, description) in SafeServicesToOptimize)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher($"SELECT DisplayName, StartMode FROM Win32_Service WHERE Name='{name}'");
                    var svc = searcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                    if (svc == null) continue;
                    rows.Add(new ServiceRow(name, svc["DisplayName"]?.ToString() ?? name, description, svc["StartMode"]?.ToString() ?? ""));
                }
                catch { }
            }
            return (IReadOnlyList<ServiceRow>)rows;
        });

        // Μόνο υπηρεσίες που ήταν ΗΔΗ Automatic αγγίζονται ποτέ - backup μέσω μοιρασμένου TweakBackupService.
        public static Task<bool> SetServiceManualAsync(string serviceName) => Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Service WHERE Name='{serviceName}'");
                var svc = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (svc == null) return false;
                TweakBackupService.BackupIfNeeded($"SVC_{serviceName}", svc["StartMode"]?.ToString() ?? "Auto");
                var result = svc.InvokeMethod("ChangeStartMode", new object[] { "Manual" });
                return Convert.ToInt32(result) == 0;
            }
            catch { return false; }
        });

        public static Task<bool> RestoreServiceStartModeAsync(string serviceName) => Task.Run(() =>
        {
            try
            {
                var original = TweakBackupService.GetBackup($"SVC_{serviceName}") ?? "Auto";
                using var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Service WHERE Name='{serviceName}'");
                var svc = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (svc == null) return false;
                var result = svc.InvokeMethod("ChangeStartMode", new object[] { original });
                return Convert.ToInt32(result) == 0;
            }
            catch { return false; }
        });

        // ===== Δοκιμή Ταχύτητας Δίσκου =====

        public static Task<BenchmarkResult> RunBenchmarkAsync(string driveLetter) => Task.Run(() =>
        {
            var testFile = Path.Combine(driveLetter.TrimEnd('\\') + "\\", "OptimizerWpfBenchTest_TEMP.bin");
            try
            {
                var drive = new DriveInfo(driveLetter.TrimEnd('\\') + "\\");
                if (drive.AvailableFreeSpace < 1024L * 1024 * 1024) return new BenchmarkResult(0, 0, "Ανεπαρκής ελεύθερος χώρος (χρειάζεται τουλάχιστον 1GB).");

                const int size = 256 * 1024 * 1024;
                var buffer = new byte[size];
                new Random().NextBytes(buffer);

                var sw = Stopwatch.StartNew();
                using (var fs = new FileStream(testFile, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024, FileOptions.WriteThrough))
                {
                    fs.Write(buffer, 0, buffer.Length);
                    fs.Flush(true);
                }
                sw.Stop();
                var writeMbps = size / 1024.0 / 1024.0 / sw.Elapsed.TotalSeconds;

                sw.Restart();
                using (var fs = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.None, 4 * 1024 * 1024, FileOptions.SequentialScan))
                {
                    var readBuf = new byte[4 * 1024 * 1024];
                    while (fs.Read(readBuf, 0, readBuf.Length) > 0) { }
                }
                sw.Stop();
                var readMbps = size / 1024.0 / 1024.0 / sw.Elapsed.TotalSeconds;

                return new BenchmarkResult(Math.Round(writeMbps, 1), Math.Round(readMbps, 1), null);
            }
            catch (Exception ex)
            {
                return new BenchmarkResult(0, 0, ex.Message);
            }
            finally
            {
                try { File.Delete(testFile); } catch { }
            }
        });

        // ===== κοινά PowerShell helpers =====

        private static async Task<string> RunPowerShellAsync(string command)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
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

        private static async Task<bool> RunPowerShellForSuccessAsync(string command)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(psi);
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
    }
}
