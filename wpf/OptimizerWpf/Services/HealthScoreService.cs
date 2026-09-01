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
            var issues = new List<HealthIssue>();
            var score = 100;

            score += CheckSystemDriveSpace(issues);
            score += CheckPendingReboot(issues);
            score += CheckDefender(issues);
            score += CheckStartupCount(issues);
            score += CheckRestorePoints(issues);

            score = Math.Max(0, Math.Min(100, score));
            return new HealthResult(score, issues);
        }

        private static int CheckSystemDriveSpace(List<HealthIssue> issues)
        {
            try
            {
                var sysRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                var drive = new DriveInfo(sysRoot);
                if (!drive.IsReady || drive.TotalSize <= 0) return 0;

                var freePct = Math.Round(100.0 * drive.TotalFreeSpace / drive.TotalSize);
                if (freePct < 10)
                {
                    issues.Add(new HealthIssue("Ο δίσκος συστήματος είναι σχεδόν γεμάτος (λιγότερο από 10% ελεύθερο).", "Storage"));
                    return -25;
                }
                if (freePct < 20)
                {
                    issues.Add(new HealthIssue("Ο ελεύθερος χώρος στον δίσκο συστήματος είναι χαμηλός (λιγότερο από 20%).", "Storage"));
                    return -10;
                }
            }
            catch { /* drive can be unready mid-check - skip, matches the WinForms try/catch-empty pattern */ }
            return 0;
        }

        private static int CheckPendingReboot(List<HealthIssue> issues)
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
                {
                    issues.Add(new HealthIssue("Εκκρεμεί επανεκκίνηση για να ολοκληρωθούν πρόσφατες ενημερώσεις.", "Restart"));
                    return -10;
                }
            }
            catch { }
            return 0;
        }

        private static int CheckDefender(List<HealthIssue> issues)
        {
            try
            {
                var defenderScope = new ManagementScope(@"root\Microsoft\Windows\Defender");
                defenderScope.Connect();
                using var searcher = new ManagementObjectSearcher(defenderScope, new ObjectQuery("SELECT RealTimeProtectionEnabled FROM MSFT_MpComputerStatus"));
                using var results = searcher.Get();
                var status = results.Cast<ManagementBaseObject>().FirstOrDefault();
                if (status == null) return 0;

                var realTimeOn = (bool)(status["RealTimeProtectionEnabled"] ?? true);
                if (realTimeOn) return 0;

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
                    issues.Add(new HealthIssue(
                        $"Η προστασία πραγματικού χρόνου του Windows Defender είναι απενεργοποιημένη επειδή είναι ενεργό άλλο antivirus: {otherAvName} (αναμενόμενη συμπεριφορά Windows - δεν χρειάζεται διόρθωση).",
                        "DefenderOtherAV"));
                    return -5;
                }

                issues.Add(new HealthIssue("Η προστασία πραγματικού χρόνου του Windows Defender είναι απενεργοποιημένη.", "Defender"));
                return -20;
            }
            catch { return 0; }
        }

        private static int CheckStartupCount(List<HealthIssue> issues)
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
                    issues.Add(new HealthIssue($"Πολλές εφαρμογές εκκίνησης ({startupCount}) - επιβραδύνουν την εκκίνηση των Windows.", "Startup"));
                    return -10;
                }
            }
            catch { }
            return 0;
        }

        private static int CheckRestorePoints(List<HealthIssue> issues)
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
                    {
                        issues.Add(new HealthIssue("Δεν υπάρχει πρόσφατο Σημείο Επαναφοράς (πάνω από 30 μέρες).", "RestorePoint"));
                        return -5;
                    }
                }
                else
                {
                    issues.Add(new HealthIssue("Δεν βρέθηκε κανένα Σημείο Επαναφοράς συστήματος.", "RestorePoint"));
                    return -10;
                }
            }
            catch { }
            return 0;
        }
    }
}
