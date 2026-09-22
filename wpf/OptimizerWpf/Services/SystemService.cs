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

        // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): void, χωρίς try/catch - ένα κλειδωμένο/απαγορευμένο
        // HKLM κλειδί (π.χ. χωρίς αρκετά δικαιώματα σε ασυνήθιστη διαμόρφωση) θα πετούσε αδιαχείριστη
        // εξαίρεση. Επιστρέφει τώρα bool ώστε το UI να μπορεί να ενημερώσει/επαναφέρει τον διακόπτη.
        public static bool SetStartupItemEnabled(StartupItem item, bool enabled)
        {
            try
            {
                var hive = item.Location == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
                using var key = hive.OpenSubKey(RunKeyPath, writable: true);
                if (key == null) return false;
                if (enabled)
                {
                    var disabledName = DisabledPrefix + item.Name;
                    var value = key.GetValue(disabledName) as string;
                    if (value == null) return false;
                    key.SetValue(item.Name, value);
                    key.DeleteValue(disabledName, throwOnMissingValue: false);
                }
                else
                {
                    var value = key.GetValue(item.Name) as string;
                    if (value == null) return false;
                    key.SetValue(DisabledPrefix + item.Name, value);
                    key.DeleteValue(item.Name, throwOnMissingValue: false);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ΝΕΟ - roadmap "Εκτίμηση χρόνου εκκίνησης" - "Πόσο καθυστερεί κάθε εφαρμογή εκκίνησης την
        // είσοδο στα Windows, όχι μόνο on/off". ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ (ίδιο πνεύμα με το Boot Timeline/
        // S.M.A.R.T. αλλού στην εφαρμογή): καμία επίσημη Windows API εκθέτει το per-app "Startup
        // impact" που δείχνει η Διαχείριση Εργασιών - υπολογίζεται εδώ μια ΠΑΡΑΤΗΡΟΥΜΕΝΗ εκτίμηση από
        // την ΤΡΕΧΟΥΣΑ συνεδρία: πόσο μετά την εκκίνηση του explorer.exe (proxy για "μπήκα στα
        // Windows") ξεκίνησε η αντίστοιχη διεργασία - ΟΧΙ εγγυημένη τιμή για την ΕΠΟΜΕΝΗ εκκίνηση,
        // μόνο ό,τι πραγματικά καταγράφηκε τώρα. null όταν η διεργασία δεν βρέθηκε τρέχουσα.
        public static Dictionary<string, TimeSpan?> GetStartupDelayEstimates(IReadOnlyList<StartupItem> items)
        {
            var result = new Dictionary<string, TimeSpan?>();
            DateTime? explorerStart;
            try { explorerStart = Process.GetProcessesByName("explorer").FirstOrDefault()?.StartTime; }
            catch { explorerStart = null; }

            foreach (var item in items)
            {
                result[item.Name] = null;
                if (explorerStart == null) continue;

                var exeName = ExtractExeName(item.Command);
                if (exeName == null) continue;

                try
                {
                    var proc = Process.GetProcessesByName(exeName).FirstOrDefault();
                    if (proc == null) continue;
                    var delta = proc.StartTime - explorerStart.Value;
                    if (delta > TimeSpan.Zero && delta < TimeSpan.FromMinutes(5)) result[item.Name] = delta;
                }
                catch { /* access-denied σε process ιδιοκτησίας άλλου χρήστη, ή η διεργασία μόλις τερμάτισε */ }
            }
            return result;
        }

        private static string? ExtractExeName(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            var trimmed = command.Trim();
            string path;
            if (trimmed.StartsWith("\""))
            {
                var end = trimmed.IndexOf('"', 1);
                path = end > 0 ? trimmed[1..end] : trimmed.TrimStart('"');
            }
            else
            {
                path = trimmed.Split(' ')[0];
            }
            try { return Path.GetFileNameWithoutExtension(path); }
            catch { return null; }
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

        // ===== "Λειτουργία Ύπνου" Παρασκηνίου (v3.2.0) =====
        // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε μετά από web research: "sleep mode αναστολή background apps για
        // άμεση ελευθέρωση RAM, στυλ AVG TuneUp"). ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: ΔΕΝ αναστέλλει/παγώνει
        // πραγματικά τις διεργασίες (SuspendThread ανά νήμα είναι ρίσκο - μπορεί να "παγώσει" μόνιμα
        // ένα app αν κάτι πάει στραβά όσο είναι σε αναστολή, ή να σπάσει IPC/handles που κρατάει
        // ανοιχτά). Αντ' αυτού καλεί το EmptyWorkingSet (psapi.dll) - ΕΠΙΣΗΜΗ, τεκμηριωμένη Win32 API
        // που λέει στα Windows να σελιδοποιήσει (page out) τη ΜΗ ενεργά χρησιμοποιούμενη μνήμη μιας
        // διεργασίας· η ίδια η διεργασία ΣΥΝΕΧΙΖΕΙ να τρέχει κανονικά, ανεπηρέαστη - απλά η ήδη
        // αδρανής μνήμη της ελευθερώνεται στο σύστημα (θα την ξαναπάρει αν τη χρειαστεί). Ίδια τεχνική
        // με αυτή που χρησιμοποιούν εργαλεία σαν το AVG TuneUp/Wise Memory Optimizer "under the hood".
        public static Task<(int ProcessCount, double FreedMb)> FreeBackgroundMemoryAsync() => Task.Run(() =>
        {
            var currentPid = Environment.ProcessId;
            var count = 0;
            long freedBytes = 0;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == currentPid || CriticalProcessNames.Contains(p.ProcessName)) continue;
                    var before = SafeWorkingSet(p);
                    if (before <= 0) continue;
                    if (!NativeMethods.EmptyWorkingSet(p.Handle)) continue;
                    p.Refresh();
                    var after = SafeWorkingSet(p);
                    if (after < before) { freedBytes += before - after; count++; }
                }
                catch { /* διεργασία σε άλλο επίπεδο δικαιωμάτων/ήδη τερματισμένη - αγνοείται, συνεχίζει με τις υπόλοιπες */ }
            }
            return (count, Math.Round(freedBytes / 1024.0 / 1024.0, 1));
        });

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("psapi.dll", SetLastError = true)]
            public static extern bool EmptyWorkingSet(IntPtr hProcess);
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

        public static Task<IReadOnlyList<(string Path, double SizeMb, DateTime LastWriteTime)>> FindLargeFilesAsync() => FindLargeFilesInAsync(PersonalFolders());

        // ΔΙΟΡΘΩΣΗ (v3.2.0, χρήστης ζήτησε μετά από web research: "cloud cleanup για Google Drive/
        // Dropbox, ίδιο πνεύμα με το OneDrive") - το OneDrive's "ελευθέρωση χώρου" λειτουργεί μέσω
        // attrib.exe +U (μετατροπή σε cloud-only placeholder), μηχανισμός ΑΠΟΚΛΕΙΣΤΙΚΑ του OneDrive/
        // NTFS (Files On-Demand) - το Google Drive και το Dropbox ΔΕΝ εκθέτουν το ίδιο απλό
        // μηχανισμό (κάθε ένα έχει δικό του, διαφορετικό "smart sync"). Αντί να προσποιηθούμε κάτι
        // που δεν υπάρχει, επαναχρησιμοποιούνται τα ΗΔΗ υπάρχοντα εργαλεία εύρεσης διπλότυπων/μεγάλων
        // αρχείων, με πεδίο σάρωσης τον τοπικό φάκελο συγχρονισμού του καθενός - ο χρήστης βλέπει τι
        // πιάνει χώρο και το διαγράφει ο ίδιος (η διαγραφή συγχρονίζεται φυσιολογικά στο cloud μετά).
        private static Task<IReadOnlyList<(string Path, double SizeMb, DateTime LastWriteTime)>> FindDuplicatesInAsync(IEnumerable<string> roots) => Task.Run(() =>
        {
            var files = roots.SelectMany(SafeEnumerateFiles)
                .Select(f => new FileInfo(f))
                .Where(fi => { try { return fi.Length is > 0 and < 500L * 1024 * 1024; } catch { return false; } })
                .ToList();

            var byLength = files.GroupBy(f => f.Length).Where(g => g.Count() > 1);
            var results = new List<(string, double, DateTime)>();
            foreach (var group in byLength)
            {
                var byHash = group.GroupBy(f => ComputeHash(f.FullName)).Where(g => g.Key != null && g.Count() > 1);
                foreach (var hashGroup in byHash)
                {
                    foreach (var f in hashGroup) results.Add((f.FullName, Math.Round(f.Length / 1024.0 / 1024.0, 2), f.LastWriteTime));
                }
            }
            return (IReadOnlyList<(string, double, DateTime)>)results;
        });

        private static Task<IReadOnlyList<(string Path, double SizeMb, DateTime LastWriteTime)>> FindLargeFilesInAsync(IEnumerable<string> roots) => Task.Run(() =>
        {
            var results = roots.SelectMany(SafeEnumerateFiles)
                .Select(f => { try { return new FileInfo(f); } catch { return null; } })
                .Where(fi => fi != null)
                .OrderByDescending(fi => fi!.Length)
                .Take(50)
                .Select(fi => (fi!.FullName, Math.Round(fi.Length / 1024.0 / 1024.0, 2), fi.LastWriteTime))
                .ToList();
            return (IReadOnlyList<(string, double, DateTime)>)results;
        });

        // Επίσημα τεκμηριωμένος τρόπος εντοπισμού του τοπικού φακέλου Dropbox - το ίδιο το Dropbox
        // γράφει info.json με το πραγματικό μονοπάτι (μπορεί να έχει μετακινηθεί από τον χρήστη).
        public static string? FindDropboxFolder()
        {
            try
            {
                foreach (var infoPath in new[]
                         {
                             Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dropbox", "info.json"),
                             Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dropbox", "info.json"),
                         })
                {
                    if (!File.Exists(infoPath)) continue;
                    using var doc = JsonDocument.Parse(File.ReadAllText(infoPath));
                    foreach (var account in doc.RootElement.EnumerateObject()) // "personal" / "business"
                    {
                        if (account.Value.TryGetProperty("path", out var pathEl))
                        {
                            var path = pathEl.GetString();
                            if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) return path;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: το Google Drive for Desktop δεν εκθέτει κάποιο αντίστοιχο επίσημο
        // "info.json" - ελέγχονται οι δύο πιο συνηθισμένες περιπτώσεις: ο κλασικός φάκελος (παλιό
        // "Backup and Sync") ή προσαρτημένος τόμος με ετικέτα "Google Drive" (τρέχουσα προεπιλογή).
        // Αν ο χρήστης έχει αλλάξει τοποθεσία/γράμμα δίσκου, ενδέχεται να μη βρεθεί - honest fallback.
        public static string? FindGoogleDriveFolder()
        {
            try
            {
                var classic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Google Drive");
                if (Directory.Exists(classic)) return classic;
                foreach (var drive in DriveInfo.GetDrives())
                {
                    try { if (drive.IsReady && drive.VolumeLabel.Contains("Google Drive", StringComparison.OrdinalIgnoreCase)) return drive.RootDirectory.FullName; }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        public static Task<IReadOnlyList<(string Path, double SizeMb, DateTime LastWriteTime)>> FindDuplicatesInFolderAsync(string root) => FindDuplicatesInAsync(new[] { root });
        public static Task<IReadOnlyList<(string Path, double SizeMb, DateTime LastWriteTime)>> FindLargeFilesInFolderAsync(string root) => FindLargeFilesInAsync(new[] { root });

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

        // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε κατά τη δοκιμή του "AI"-στυλ scoring σε πραγματικό
        // σύστημα): Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories) είναι ΤΕΜΠΕΛΗΣ
        // (lazy) - ένα UnauthorizedAccessException από απαγορευμένο/junction υποφάκελο (π.χ. "Η
        // μουσική μου" όταν είναι reparse point με περιορισμένα δικαιώματα) πετάγεται ΜΕΣΑ στην
        // απαρίθμηση, ΟΧΙ στην αρχική κλήση - το try/catch εδώ δεν το έπιανε καθόλου, με αποτέλεσμα
        // αδιαχείριστη εξαίρεση να κατεβάζει τον γενικό DispatcherUnhandledException handler ενώ η
        // Εύρεση Διπλότυπων/Μεγάλων Αρχείων έτρεχε. ΙΔΙΟ μοτίβο διόρθωσης με το QuickCleanService's
        // DirSize αλλού στην εφαρμογή - αναδρομικός, ΑΝΑ-ΦΑΚΕΛΟ προστατευμένος περίπατος αντί για
        // αυτό, ώστε ένας απρόσβατος υποφάκελος να παραλείπεται αντί να ρίχνει όλη τη σάρωση.
        private static IEnumerable<string> SafeEnumerateFiles(string root)
        {
            var results = new List<string>();
            SafeEnumerateFilesInto(root, results);
            return results;
        }

        private static void SafeEnumerateFilesInto(string dir, List<string> results)
        {
            string[] files;
            try { files = Directory.GetFiles(dir); } catch { return; }
            results.AddRange(files);

            string[] subdirs;
            try { subdirs = Directory.GetDirectories(dir); } catch { return; }
            foreach (var sub in subdirs) SafeEnumerateFilesInto(sub, results);
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

        // Property (όχι readonly field) ώστε να ξαναχτίζεται με την τρέχουσα γλώσσα κάθε φορά που
        // ανοίγει το SystemView (ρητό αίτημα χρήστη: "μετάφρασε τα όλα").
        public static IReadOnlyDictionary<string, string> SafeServicesToOptimize => new Dictionary<string, string>
        {
            ["DiagTrack"] = LanguageService.T("Svc_DiagTrack"),
            ["dmwappushservice"] = "WAP Push Message Routing",
            ["MapsBroker"] = "Downloaded Maps Manager",
            ["lfsvc"] = LanguageService.T("Svc_Lfsvc"),
            ["WbioSrvc"] = LanguageService.T("Svc_Wbio"),
            ["WMPNetworkSvc"] = LanguageService.T("Svc_Wmp"),
            ["PcaSvc"] = LanguageService.T("Svc_Pca"),
            ["SessionEnv"] = LanguageService.T("Svc_SessionEnv"),
            ["TermService"] = LanguageService.T("Svc_Term"),
            ["RemoteRegistry"] = LanguageService.T("Svc_RemoteReg"),
            ["TapiSrv"] = LanguageService.T("Svc_Tapi"),
            ["TabletInputService"] = LanguageService.T("Svc_Tablet"),
            ["SNMPTrap"] = "SNMP Trap",
            ["WebClient"] = "WebClient",
            ["WerSvc"] = LanguageService.T("Svc_Wer"),
            ["Wecsvc"] = LanguageService.T("Svc_Wecsvc"),
            ["SDRSVC"] = LanguageService.T("Svc_Sdrsvc"),
            ["fdPHost"] = LanguageService.T("Svc_Fdphost"),
            ["FDResPub"] = LanguageService.T("Svc_Fdrespub"),
            ["upnphost"] = LanguageService.T("Svc_Upnphost"),
            ["SSDPSRV"] = LanguageService.T("Svc_Ssdp"),
            ["SysMain"] = "SysMain (Superfetch)",
            ["TrkWks"] = LanguageService.T("Svc_Trkwks"),
            ["iphlpsvc"] = LanguageService.T("Svc_Iphlp"),
            ["MSiSCSI"] = LanguageService.T("Svc_Msiscsi"),
            ["WSearch"] = LanguageService.T("Svc_Wsearch"),
            ["WinRM"] = LanguageService.T("Svc_Winrm"),
            ["XblAuthManager"] = LanguageService.T("Svc_XblAuth"),
            ["XblGameSave"] = LanguageService.T("Svc_XblSave"),
            ["XboxNetApiSvc"] = LanguageService.T("Svc_XboxNet"),
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
                if (drive.AvailableFreeSpace < 1024L * 1024 * 1024) return new BenchmarkResult(0, 0, LanguageService.T("SysSvc_InsufficientSpace"));

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
