using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - roadmap "WSL2 manager". GUI wrapper γύρω από το ΕΝΣΩΜΑΤΩΜΕΝΟ wsl.exe - λίστα/εκκίνηση/
    // τερματισμός/προεπιλογή/κατάργηση διανομών, ίδιο πνεύμα με το BackupService (wbadmin wrapper):
    // κανένα δικό μας VM/container engine, μόνο GUI πάνω από το επίσημο εργαλείο των Windows.
    // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: το wsl.exe εκτυπώνει UTF-16LE στο redirected stdout (γνωστό quirk,
    // ΟΧΙ UTF-8 σαν τα περισσότερα CLI εργαλεία εδώ) - StandardOutputEncoding=Encoding.Unicode είναι
    // ΑΠΑΡΑΙΤΗΤΟ, διαφορετικά η έξοδος είναι άχρηστα ελληνικά/κινέζικα σκουπίδια χαρακτήρων.
    public static class WslService
    {
        public record WslDistro(string Name, string State, string Version, bool IsDefault);

        public static async Task<bool> IsAvailableAsync()
        {
            var (_, exitCode, started) = await RunAsync("--status");
            return started && exitCode == 0;
        }

        public static async Task<IReadOnlyList<WslDistro>> ListDistrosAsync()
        {
            var (output, _, started) = await RunAsync("-l -v");
            var list = new List<WslDistro>();
            if (!started) return list;

            foreach (var rawLine in output.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.TrimStart().StartsWith("NAME", StringComparison.OrdinalIgnoreCase)) continue;

                var isDefault = line.TrimStart().StartsWith("*");
                var clean = line.TrimStart('*', ' ');
                var parts = Regex.Split(clean.Trim(), @"\s{2,}");
                if (parts.Length < 3) continue;
                list.Add(new WslDistro(parts[0].Trim(), parts[1].Trim(), parts[2].Trim(), isDefault));
            }
            return list;
        }

        // Ανοίγει ΠΡΑΓΜΑΤΙΚΟ interactive τερματικό της διανομής (UseShellExecute=true, χωρίς
        // redirect) - το "start" ενός WSL distro δεν είναι background service call, είναι ζωντανό
        // κέλυφος όπου ο χρήστης θα δουλέψει.
        public static void OpenShell(string name) =>
            Process.Start(new ProcessStartInfo("wsl.exe", $"-d \"{name}\"") { UseShellExecute = true });

        public static Task<bool> SetDefaultAsync(string name) => RunSimpleAsync($"--set-default \"{name}\"");
        public static Task<bool> TerminateAsync(string name) => RunSimpleAsync($"--terminate \"{name}\"");
        public static Task<bool> ShutdownAllAsync() => RunSimpleAsync("--shutdown");
        public static Task<bool> UpdateAsync() => RunSimpleAsync("--update", TimeSpan.FromMinutes(5));

        // ΚΑΤΑΣΤΡΟΦΙΚΟ - διαγράφει ΟΛΑ τα δεδομένα της διανομής (filesystem, εγκατεστημένα πακέτα,
        // κ.λπ.), ΜΗ αναστρέψιμο. Το UI ζητά ρητή επιβεβαίωση (YesNo warning με το όνομα μέσα στο
        // μήνυμα) πριν καλέσει αυτή τη μέθοδο - ίδιο μοτίβο με τις υπόλοιπες μη αναστρέψιμες
        // ενέργειες της εφαρμογής (π.χ. διαγραφή Σημείου Επαναφοράς).
        public static Task<bool> UnregisterAsync(string name) => RunSimpleAsync($"--unregister \"{name}\"", TimeSpan.FromMinutes(2));

        private static async Task<bool> RunSimpleAsync(string args, TimeSpan? timeout = null)
        {
            var (_, exitCode, started) = await RunAsync(args, timeout ?? TimeSpan.FromSeconds(30));
            return started && exitCode == 0;
        }

        private static async Task<(string Output, int ExitCode, bool Started)> RunAsync(string arguments, TimeSpan? timeout = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.Unicode,
            };

            Process process;
            try { process = Process.Start(psi)!; }
            catch (Win32Exception) { return ("", -1, false); }

            using (process)
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                try
                {
                    using var cts = new System.Threading.CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(15));
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(); } catch { }
                    return ("", -1, true);
                }
                var stdout = await stdoutTask;
                return (stdout, process.ExitCode, true);
            }
        }
    }
}
