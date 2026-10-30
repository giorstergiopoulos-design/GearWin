using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // Πρόταση "χρονοδιάγραμμα ενημερώσεων": εγκατάσταση ενημερώσεων ΕΦΑΡΜΟΓΩΝ (winget) αυτόματα τη νύχτα μέσω εργασίας Task Scheduler
    // (ΟΧΙ timer μέσα στην εφαρμογή - δεν χρειάζεται να τρέχει). ΣΚΟΠΙΜΑ ΔΕΝ περιλαμβάνει Windows Update/οδηγούς (μπορεί να απαιτήσουν
    // επανεκκίνηση ή να είναι ριψοκίνδυνα χωρίς επίβλεψη) ούτε Microsoft Store apps (αυτοματισμός UI χρειάζεται ενεργή οθόνη). Ο χρήστης
    // ενεργοποιεί/απενεργοποιεί ρητά από την καρτέλα Βελτιστοποίηση· το αποτέλεσμα γράφεται στο auto-updates.log.
    public static class AutoUpdateService
    {
        public const string TaskName = "OptimizerWpf_NightlyUpdates";

        private static string LogPath => Path.Combine(
            Environment.GetEnvironmentVariable("GEARWIN_DATA_DIR") is { Length: > 0 } c
                ? c : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf"),
            "auto-updates.log");

        public static async Task<bool> SetEnabledAsync(bool enabled)
        {
            if (!enabled) { await TaskSchedulerService.DeleteAsync(TaskName); return true; }
            var exePath = Environment.ProcessPath ?? "";
            if (string.IsNullOrEmpty(exePath)) return false;
            // Η εργασία τρέχει με HighestAvailable χωρίς UAC - ίδια προϋπόθεση ασφαλείας με την εκκίνηση με τα Windows (βλ. SecureStorage).
            if (!SecureStorage.IsDirectoryAdminOnlyWritable(Path.GetDirectoryName(exePath))) return false;
            var xml = TaskSchedulerService.BuildDailyTaskXml(exePath, "--auto-updates", TaskSchedulerService.CurrentUserId(), "GearWin - nightly app updates");
            return await TaskSchedulerService.CreateAsync(TaskName, xml);
        }

        // Καλείται ΜΟΝΟ από το App.xaml.cs με --auto-updates (headless).
        public static async Task RunAsync()
        {
            try
            {
                var (started, exitCode, output) = await WingetService.UpgradeAllSilentAsync();
                var tail = output.Length > 4000 ? output[^4000..] : output;
                Append($"=== {DateTime.Now:yyyy-MM-dd HH:mm} winget started={started} exit={exitCode} ({WingetService.DescribeExitCode(exitCode) ?? "-"}){Environment.NewLine}{tail}");
            }
            catch (Exception ex) { Append($"=== {DateTime.Now:yyyy-MM-dd HH:mm} error: {ex.Message}"); }

            // 6.9.1: προαιρετικά και σημαντικές ενημερώσεις Windows - σειριακά, ποτέ επανεκκίνηση (ο χρήστης ενημερώνεται από τα Windows)
            if (AppSettingsService.Current.NightlyWindowsUpdates)
            {
                try
                {
                    var scan = await WindowsUpdateService.ScanDetailedAsync();
                    if (!scan.Success) { Append($"=== {DateTime.Now:yyyy-MM-dd HH:mm} windows update scan failed: {scan.Error}"); return; }
                    foreach (var u in scan.Updates.Where(x => !x.IsOptional))
                    {
                        var (ok, reboot, error) = await WindowsUpdateService.InstallAsync(u.RowId);
                        Append($"=== {DateTime.Now:yyyy-MM-dd HH:mm} windows update '{u.Title}' ok={ok} restartNeeded={reboot}{(ok ? "" : " error=" + error)}");
                    }
                }
                catch (Exception ex) { Append($"=== {DateTime.Now:yyyy-MM-dd HH:mm} windows update error: {ex.Message}"); }
            }
        }

        private static void Append(string text)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 256 * 1024) File.Delete(LogPath);
                File.AppendAllText(LogPath, text + Environment.NewLine);
            }
            catch { }
        }
    }
}
