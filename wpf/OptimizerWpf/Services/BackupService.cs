using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - roadmap "Backup/imaging". ΕΠΙΛΕΓΜΕΝΟ SCOPE (ρητά επικοινωνήθηκε στον χρήστη πριν την
    // υλοποίηση): GUI wrapper γύρω από το ΕΝΣΩΜΑΤΩΜΕΝΟ wbadmin.exe των Windows (system-image backup),
    // ΟΧΙ μια πλήρης δική μας μηχανή imaging - θα ήταν αναξιόπιστη επανεφεύρεση του τροχού έναντι
    // ενός ήδη δοκιμασμένου εργαλείου του ίδιου του OS. Η εφαρμογή τρέχει ήδη πάντα ως Administrator
    // (requireAdministrator στο app.manifest), οπότε δεν χρειάζεται ξεχωριστό RunAs/elevation hop.
    public static class BackupService
    {
        public static async Task<(bool Success, string Output)> StartSystemImageBackupAsync(string targetDrive)
        {
            // -allCritical: περιλαμβάνει ΟΛΟΥΣ τους κρίσιμους τόμους (συστήματος + EFI/boot) που
            // χρειάζονται για πλήρη ανάκτηση, όχι μόνο το C: - -quiet: καταστέλλει το διαδραστικό
            // "είστε σίγουροι;" prompt (θα κρεμούσε την εφαρμογή, βλ. reference/feedback_avoid_runas_hangs).
            var psi = new ProcessStartInfo("wbadmin.exe", $"start backup -backupTarget:{targetDrive} -allCritical -quiet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc == null) return (false, "");
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return (proc.ExitCode == 0, stdout + stderr);
        }

        public static async Task<IReadOnlyList<string>> GetBackupVersionsAsync()
        {
            var psi = new ProcessStartInfo("wbadmin.exe", "get versions")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc == null) return Array.Empty<string>();
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return output.Replace("\r", "").Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("wbadmin", StringComparison.OrdinalIgnoreCase) &&
                            !l.StartsWith("(C)", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
