using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // Πρόταση χρήστη: "αντίγραφο ασφαλείας αποθηκεύσεων παιχνιδιών". ΔΕΝ υπάρχει ενιαίος, αξιόπιστος
    // τρόπος να μαντέψουμε τη θέση αποθήκευσης ΚΑΘΕ παιχνιδιού (ποικίλει ανά εκδότη/μηχανή, πολλά
    // είναι αποκλειστικά cloud-save) - το να ΠΡΟΣΠΟΙΗΘΟΥΜΕ πληρότητα θα ήταν ψευδής διαβεβαίωση.
    // Αντί γι' αυτό, καλύπτει μόνο τις ΔΥΟ πραγματικά τεκμηριωμένες, γενικές συμβάσεις των Windows
    // που πολλά (όχι όλα) παιχνίδια χρησιμοποιούν: τον Known Folder "Saved Games" και
    // Documents\My Games. Το UI λέει ρητά ποια καλύπτει, όχι "όλα τα παιχνίδια".
    public static class GameSaveBackupService
    {
        private static readonly Guid SavedGamesFolderId = new("4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4");

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);

        private static string? SavedGamesFolder()
        {
            try
            {
                if (SHGetKnownFolderPath(SavedGamesFolderId, 0, IntPtr.Zero, out var ptr) != 0) return null;
                var path = Marshal.PtrToStringUni(ptr);
                Marshal.FreeCoTaskMem(ptr);
                return path;
            }
            catch { return null; }
        }

        public static IEnumerable<string> KnownSaveFolders()
        {
            if (SavedGamesFolder() is { } saved) yield return saved;
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games");
        }

        public static long EstimateSize() => KnownSaveFolders().Where(Directory.Exists).Sum(DirSize);

        private static long DirSize(string dir)
        {
            try { return new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
            catch { return 0; }
        }

        public static Task<bool> BackupAsync(string destinationZipPath) => Task.Run(() =>
        {
            try
            {
                if (File.Exists(destinationZipPath)) File.Delete(destinationZipPath);
                using var zip = ZipFile.Open(destinationZipPath, ZipArchiveMode.Create);
                foreach (var folder in KnownSaveFolders().Where(Directory.Exists))
                {
                    var root = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
                    foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                    {
                        try { zip.CreateEntryFromFile(file, Path.Combine(root, Path.GetRelativePath(folder, file))); }
                        catch { /* ένα κλειδωμένο/απρόσιτο αρχείο δεν ακυρώνει όλο το backup */ }
                    }
                }
                return true;
            }
            catch { return false; }
        });
    }
}
