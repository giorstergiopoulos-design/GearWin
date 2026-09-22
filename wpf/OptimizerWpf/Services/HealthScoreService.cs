using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    public record HealthIssue(string Title, string FixType);
    public record HealthResult(int Score, IReadOnlyList<HealthIssue> Issues);

    // Direct C# port of Get-SystemHealthScore from Optimizer.ps1 (line ~17852) - same checks, same
    // score deductions, same v2.8.2 fix (PendingFileRenameOperations removed from the reboot check -
    // see the comment on PendingReboot below for why).
    public static class HealthScoreService
    {
        public static HealthResult Compute()
        {
            var checks = new Func<(int Delta, HealthIssue? Issue)>[]
            {
                CheckSystemDriveSpace,
                CheckPendingReboot,
                CheckDefender,
                CheckStartupCount,
                CheckRestorePoints,
            };

            // ΝΕΟ - βελτίωση απόδοσης: οι 5 έλεγχοι είναι πλήρως ανεξάρτητοι μεταξύ τους - κανένας δεν
            // διαβάζει το αποτέλεσμα κάποιου άλλου. Δύο από αυτούς (Defender, Restore Points) κάνουν
            // WMI ερωτήματα που μπορούν να πάρουν αισθητό χρόνο· τρέχοντάς τους παράλληλα αντί για
            // σειριακά, ο συνολικός χρόνος καθορίζεται από τον ΠΙΟ αργό έλεγχο αντί από το άθροισμα
            // όλων. AsOrdered() διατηρεί την ίδια σειρά ευρημάτων με πριν (σειριακά) για προβλέψιμη
            // εμφάνιση, χωρίς επιπλέον κόστος αφού είναι ήδη μόνο 5 στοιχεία.
            var results = checks.AsParallel().AsOrdered().Select(check => check()).ToList();

            var score = 100 + results.Sum(r => r.Delta);
            var issues = results.Where(r => r.Issue != null).Select(r => r.Issue!).ToList();

            score = Math.Max(0, Math.Min(100, score));
            return new HealthResult(score, issues);
        }

        private static (int Delta, HealthIssue? Issue) CheckSystemDriveSpace()
        {
            try
            {
                var sysRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                var drive = new DriveInfo(sysRoot);
                if (!drive.IsReady || drive.TotalSize <= 0) return (0, null);

                var freePct = Math.Round(100.0 * drive.TotalFreeSpace / drive.TotalSize);
                if (freePct < 10)
                    return (-25, new HealthIssue(LanguageService.T("HealthScore_DiskAlmostFull"), "Storage"));
                if (freePct < 20)
                    return (-10, new HealthIssue(LanguageService.T("HealthScore_DiskLowSpace"), "Storage"));
            }
            catch { /* drive can be unready mid-check - skip, matches the WinForms try/catch-empty pattern */ }
            return (0, null);
        }

        private static (int Delta, HealthIssue? Issue) CheckPendingReboot()
        {
            // v2.8.2 root-cause fix carried over from Optimizer.ps1: PendingFileRenameOperations was
            // live-verified to be a generic delayed-file-cleanup mechanism used by dozens of unrelated
            // installers (Office Click-to-Run, Xbox/GamingServices, etc.), NOT a reliable "Windows
            // Update reboot pending" signal - it produced a real false positive on the dev machine
            // used to test the original fix. Only the two servicing-stack-specific keys are checked.
            try
            {
                var cbsPending = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") != null;
                var wuPending = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") != null;

                if (cbsPending || wuPending)
                    return (-10, new HealthIssue(LanguageService.T("HealthScore_RebootPending"), "Restart"));
            }
            catch { }
            return (0, null);
        }

        private static (int Delta, HealthIssue? Issue) CheckDefender()
        {
            try
            {
                var defenderScope = new ManagementScope(@"root\Microsoft\Windows\Defender");
                defenderScope.Connect();
                using var searcher = new ManagementObjectSearcher(defenderScope, new ObjectQuery("SELECT RealTimeProtectionEnabled FROM MSFT_MpComputerStatus"));
                using var results = searcher.Get();
                var status = results.Cast<ManagementBaseObject>().FirstOrDefault();
                if (status == null) return (0, null);

                var realTimeOn = (bool)(status["RealTimeProtectionEnabled"] ?? true);
                if (realTimeOn) return (0, null);

                // Real-time protection is off - check whether another active AV explains it (Windows
                // Security Center only allows one active real-time AV at a time by design).
                string? otherAvName = null;
                try
                {
                    var scScope = new ManagementScope(@"root\SecurityCenter2");
                    scScope.Connect();
                    using var scSearcher = new ManagementObjectSearcher(scScope, new ObjectQuery("SELECT displayName, productState FROM AntiVirusProduct"));
                    foreach (ManagementBaseObject av in scSearcher.Get())
                    {
                        var name = av["displayName"]?.ToString() ?? "";
                        if (name.Contains("Windows Defender", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Microsoft Defender", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var productState = Convert.ToInt32(av["productState"]);
                        var stateHex = productState.ToString("X6");
                        var middleByte = stateHex.Substring(2, 2);
                        if (middleByte is "10" or "11") { otherAvName = name; break; }
                    }
                }
                catch { }

                if (otherAvName != null)
                {
                    return (-5, new HealthIssue(
                        $"{LanguageService.T("HealthScore_DefenderOtherAvPrefix")}{otherAvName}{LanguageService.T("HealthScore_DefenderOtherAvSuffix")}",
                        "DefenderOtherAV"));
                }

                return (-20, new HealthIssue(LanguageService.T("HealthScore_DefenderOff"), "Defender"));
            }
            catch { return (0, null); }
        }

        private static (int Delta, HealthIssue? Issue) CheckStartupCount()
        {
            try
            {
                var startupCount = 0;
                foreach (var (hive, path) in new[]
                         {
                             (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
                             (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
                         })
                {
                    using var key = hive.OpenSubKey(path);
                    if (key != null) startupCount += key.GetValueNames().Length;
                }

                if (startupCount > 15)
                {
                    return (-10, new HealthIssue($"{LanguageService.T("HealthScore_TooManyStartupPrefix")}{startupCount}{LanguageService.T("HealthScore_TooManyStartupSuffix")}", "Startup"));
                }
            }
            catch { }
            return (0, null);
        }

        private static (int Delta, HealthIssue? Issue) CheckRestorePoints()
        {
            try
            {
                var scope = new ManagementScope(@"root\default");
                scope.Connect();
                using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT CreationTime FROM SystemRestore"));
                using var results = searcher.Get();
                var creationTimes = results.Cast<ManagementBaseObject>()
                    .Select(rp => ManagementDateTimeConverter.ToDateTime(rp["CreationTime"].ToString()))
                    .ToList();

                if (creationTimes.Count > 0)
                {
                    var lastRp = creationTimes.Max();
                    if ((DateTime.Now - lastRp).TotalDays > 30)
                        return (-5, new HealthIssue(LanguageService.T("HealthScore_NoRecentRestorePoint"), "RestorePoint"));
                }
                else
                {
                    return (-10, new HealthIssue(LanguageService.T("HealthScore_NoRestorePointFound"), "RestorePoint"));
                }
            }
            catch { }
            return (0, null);
        }
    }
}
