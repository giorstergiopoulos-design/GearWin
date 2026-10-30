using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public enum SdioAction { Download, Integrate }

    // 6.9.1 - "Λήψη" και "Ενσωμάτωση" μέσω του SDI (Snappy Driver Installer Origin) που εγκαθίσταται από το winget. ΜΟΝΟ με ρητή ενέργεια του χρήστη.
    // Χρησιμοποιεί τις εντολές script του SDI (init, checkupdates, get ..., select ..., enableinstall on, install) όπως στο επίσημο example-script
    // (gitverse.ru/sdi-tool/SDI .../scripts/example-script.txt). Λήψη = ευρετήρια + ενημερώσεις των driver packs που υπάρχουν ήδη· Ενσωμάτωση = επιλογή
    // "missing better" και εγκατάσταση (το SDI κατεβάζει μόνο τα packs που χρειάζονται), ΠΑΝΤΑ μετά από επιβεβαίωση και σημείο επαναφοράς.
    // ΜΗ ΕΠΑΛΗΘΕΥΜΕΝΟ σε πραγματικό υπολογιστή: το σύνολο εντολών προέρχεται από την τεκμηρίωση, δεν εκτελέστηκε εδώ.
    public static class SdioService
    {
        public static string? FindSdio()
        {
            var links = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links", "sdio.exe");
            if (File.Exists(links)) return links;
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                try { var c = Path.Combine(dir.Trim('"'), "sdio.exe"); if (File.Exists(c)) return c; } catch { }
            }
            return null;
        }

        // Καθαρή συνάρτηση (δοκιμάζεται χωρίς SDIO).
        public static string BuildScript(SdioAction action)
        {
            var lines = new List<string> { "verbose 384", "logging on", "init", "checkupdates", "onerror goto :end" };
            if (action == SdioAction.Download)
            {
                lines.AddRange(new[] { "get indexes", "onerror goto :end", "get driverpacks updates", "onerror goto :end" });
            }
            else
            {
                lines.AddRange(new[] { "select missing better", "enableinstall on", "install", "onerror goto :end" });
            }
            lines.AddRange(new[] { ":end", "end" });
            return string.Join("\r\n", lines) + "\r\n";
        }

        public static async Task<(bool Started, int ExitCode, string LogDir)> RunAsync(SdioAction action, CancellationToken ct = default)
        {
            var exe = FindSdio();
            var logDir = Path.Combine(Environment.GetEnvironmentVariable("GEARWIN_DATA_DIR") is { Length: > 0 } c ? c : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf"), "sdio-logs", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            if (exe == null) return (false, -1, logDir);
            Directory.CreateDirectory(logDir);
            // το script τρέχει από εξυψωμένο SDIO: μένει σε φάκελο εγγράψιμο μόνο από διαχειριστή (SecureStorage)
            SecureStorage.SecureDirectory(Path.Combine(SecureStorage.RootDir, "Sdio"));
            var scriptPath = Path.Combine(SecureStorage.RootDir, "Sdio", action + ".txt");
            File.WriteAllText(scriptPath, BuildScript(action));
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in new[] { "-nogui", "-autoclose", "-nostamp", "-script:" + scriptPath, "-output_dir:" + logDir, "-log_dir:" + logDir }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p == null) return (false, -1, logDir);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(action == SdioAction.Download ? 90 : 120));
            try { await p.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException) { try { p.Kill(true); } catch { } return (true, -2, logDir); }
            return (true, p.ExitCode, logDir);
        }
    }
}
