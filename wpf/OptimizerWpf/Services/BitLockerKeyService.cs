using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public record BitLockerDriveInfo(string MountPoint, string ProtectionStatus, IReadOnlyList<string> RecoveryKeys);

    // ΝΕΟ - roadmap "διαχείριση κλειδιών ανάκτησης BitLocker" (από έρευνα ανταγωνιστικών εργαλείων) -
    // καθαρά ΕΝΗΜΕΡΩΤΙΚΟ, καμία αλλαγή σε ρύθμιση (η ίδια η ενεργοποίηση/απενεργοποίηση BitLocker
    // παραμένει μέσω Tweaks_OpenEncryptionSettings -> επίσημες Ρυθμίσεις Windows, ρητά ΟΧΙ εδώ).
    // Χρησιμοποιεί το ενσωματωμένο manage-bde.exe (ΟΧΙ το PowerShell BitLocker module, το οποίο
    // απαιτεί το προαιρετικό "RSAT-Feature-Tools-BitLocker" σε κάποιες εκδόσεις server/LTSC - το
    // manage-bde είναι πάντα διαθέσιμο σε κάθε έκδοση Windows που υποστηρίζει καθόλου BitLocker).
    public static class BitLockerKeyService
    {
        public static async Task<IReadOnlyList<BitLockerDriveInfo>> GetDrivesAsync()
        {
            var results = new List<BitLockerDriveInfo>();
            foreach (var drive in System.IO.DriveInfo.GetDrives())
            {
                if (drive.DriveType != System.IO.DriveType.Fixed || !drive.IsReady) continue;
                var mount = drive.Name.TrimEnd('\\');
                var output = await RunManageBdeAsync($"-status {mount}");
                if (string.IsNullOrWhiteSpace(output)) continue;
                if (!output.Contains("BitLocker")) continue;

                var status = output.Contains("Protection On") ? "On" : output.Contains("Protection Off") ? "Off" : "Unknown";
                var keys = new List<string>();
                if (status == "On")
                {
                    var keyOutput = await RunManageBdeAsync($"-protectors -get {mount}");
                    foreach (var line in keyOutput.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        // Recovery password format: 8 groups of 6 digits separated by dashes.
                        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d{6}(-\d{6}){7}$"))
                            keys.Add(trimmed);
                    }
                }
                results.Add(new BitLockerDriveInfo(mount, status, keys));
            }
            return results;
        }

        private static async Task<string> RunManageBdeAsync(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("manage-bde.exe", args)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(psi);
                if (process == null) return "";
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                return output;
            }
            catch { return ""; }
        }
    }
}
