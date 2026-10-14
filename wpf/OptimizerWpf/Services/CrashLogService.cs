using System;
using System.IO;
using System.Linq;

namespace OptimizerWpf.Services
{
    // Έλεγχος ορθής λειτουργίας: ΜΟΝΟ το DispatcherUnhandledException (UI νήμα) ήταν καταχωρημένο. Εξαίρεση σε νήμα
    // παρασκηνίου (Task.Run, timer, thread pool) τερμάτιζε την εφαρμογή ΣΙΩΠΗΛΑ χωρίς κανένα ίχνος, κάτι που
    // έκανε αδύνατη τη διάγνωση "η εφαρμογή έκλεισε μόνη της". Τώρα καταγράφονται (με όριο μεγέθους) στο
    // %LocalAppData%\OptimizerWpf\crash.log. Η καταγραφή δεν αλλάζει τη συμπεριφορά (δεν "καταπίνει" crash).
    public static class CrashLogService
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf", "crash.log");
        private static readonly object Lock = new();

        public static void Install()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Write(e.IsTerminating ? "FATAL (background thread)" : "Unhandled (non-fatal)", e.ExceptionObject as Exception);
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
                Write("Unobserved task exception", e.Exception);
        }

        public static void Write(string kind, Exception? ex)
        {
            try
            {
                lock (Lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                    if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 256 * 1024)
                    {
                        var all = File.ReadAllLines(LogPath);
                        File.WriteAllLines(LogPath, all.Skip(all.Length / 2));
                    }
                    File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kind}: {ex}{Environment.NewLine}");
                }
            }
            catch { /* η καταγραφή δεν πρέπει ποτέ να προκαλέσει νέο σφάλμα */ }
        }
    }
}
