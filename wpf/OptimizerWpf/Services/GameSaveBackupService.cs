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
            long total = 0;
            foreach (var f in EnumerateFilesNoReparse(dir)) { try { total += new FileInfo(f).Length; } catch { } }
            return total;
        }

        // Ίδια λογική με την αναδρομική απαρίθμηση, αλλά ΔΕΝ διασχίζει φακέλους-junction/symlink (κύκλοι → ατέρμονο
        // zip / διαρροή αρχείων εκτός των δύο φακέλων αποθήκευσης). Αρχεία-placeholder (OneDrive) περιλαμβάνονται.
        private static IEnumerable<string> EnumerateFilesNoReparse(string root)
        {
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var dir = stack.Pop();
                string[] files = Array.Empty<string>(), dirs = Array.Empty<string>();
                try { files = Directory.GetFiles(dir); dirs = Directory.GetDirectories(dir); } catch { }
                foreach (var f in files) yield return f;
                foreach (var d in dirs)
                {
                    try { if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) continue; } catch { continue; }
                    stack.Push(d);
                }
            }
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
                    foreach (var file in EnumerateFilesNoReparse(folder))
                    {
                        if (string.Equals(file, destinationZipPath, StringComparison.OrdinalIgnoreCase)) continue; // ποτέ το ίδιο το zip
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
