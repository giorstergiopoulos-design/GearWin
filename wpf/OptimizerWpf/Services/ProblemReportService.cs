using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // Πρόταση "αναφορά προβλήματος μέσα από την εφαρμογή": ένα zip με ό,τι χρειάζεται για διάγνωση - έκδοση/Windows, τα αρχεία καταγραφής
    // (crash.log, update-check.log) και τις ρυθμίσεις. ΔΕΝ περιλαμβάνει κωδικούς, ιστορικό clipboard, αποθηκευμένους κωδικούς browser ή
    // οποιοδήποτε άλλο προσωπικό αρχείο. ΔΕΝ στέλνεται πουθενά αυτόματα - αποθηκεύεται στην Επιφάνεια Εργασίας και ο χρήστης
    // αποφασίζει αν και σε ποιον θα το δώσει.
    public static class ProblemReportService
    {
        private static readonly string DataDir = Environment.GetEnvironmentVariable("GEARWIN_DATA_DIR") is { Length: > 0 } c
            ? c : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf");

        // Αρχεία δεδομένων που ΕΠΙΤΡΕΠΕΤΑΙ να μπουν στο zip (whitelist - ό,τι δεν αναφέρεται εδώ δεν συμπεριλαμβάνεται ποτέ).
        public static readonly string[] AllowedFiles = { "crash.log", "update-check.log", "AppSettings.json" };

        public static string BuildInfoText(string appVersion) =>
            $"GearWin {appVersion}{Environment.NewLine}" +
            $"Windows: {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")}){Environment.NewLine}" +
            $".NET: {Environment.Version}{Environment.NewLine}" +
            $"Language: {LanguageService.Current}{Environment.NewLine}" +
            $"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}";

        public static Task<string> CreateAsync(string destinationZip, string appVersion, string? dataDir = null) => Task.Run(() =>
        {
            var dir = dataDir ?? DataDir;
            if (File.Exists(destinationZip)) File.Delete(destinationZip);
            using var zip = ZipFile.Open(destinationZip, ZipArchiveMode.Create);
            var info = zip.CreateEntry("info.txt");
            using (var w = new StreamWriter(info.Open(), new UTF8Encoding(false))) w.Write(BuildInfoText(appVersion));
            foreach (var name in AllowedFiles)
            {
                var path = Path.Combine(dir, name);
                if (!File.Exists(path)) continue;
                try
                {
                    // FileShare.ReadWrite: το αρχείο μπορεί να είναι ανοιχτό για γράψιμο από την εφαρμογή την ίδια στιγμή.
                    using var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var entry = zip.CreateEntry(name);
                    using var dst = entry.Open();
                    src.CopyTo(dst);
                }
                catch { /* ένα μη αναγνώσιμο αρχείο δεν ακυρώνει την αναφορά */ }
            }
            return destinationZip;
        });
    }
}
