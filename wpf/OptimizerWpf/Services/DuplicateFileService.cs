using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public record DuplicateGroup(long SizeBytes, IReadOnlyList<string> Paths);

    // ΝΕΟ - roadmap "Εύρεση διπλότυπων αρχείων" - ο χρήστης επιλέγει ρητά έναν φάκελο (ΠΟΤΕ
    // ολόκληρος δίσκος by default - το σάρωμα hash είναι σχετικά αργό, και το scope πρέπει να είναι
    // κάτι που ο χρήστης πραγματικά θέλει να ελέγξει). Στάδιο 1 (γρήγορο): ομαδοποίηση κατά ΑΚΡΙΒΕΣ
    // μέγεθος αρχείου - αρχεία με διαφορετικό μέγεθος ΔΕΝ μπορούν να είναι πανομοιότυπα, μηδενικό
    // κόστος υπολογισμού hash για την τεράστια πλειοψηφία που δεν έχει κανένα ταίρι. Στάδιο 2 (ακριβές):
    // MD5 hash ΜΟΝΟ μέσα σε κάθε ομάδα ίδιου μεγέθους (καμία ανάγκη κρυπτογραφικά ασφαλούς hash εδώ -
    // ανίχνευση διπλότυπων, όχι ασφάλεια) - αρχεία με το ΙΔΙΟ hash ΚΑΙ το ΙΔΙΟ μέγεθος θεωρούνται
    // διπλότυπα.
    public static class DuplicateFileService
    {
        private const long MaxFileSizeBytes = 2L * 1024 * 1024 * 1024; // 2GB - αποφυγή εξαιρετικά αργού hashing σε τεράστια αρχεία (π.χ. ISO/VM images)

        public static Task<IReadOnlyList<DuplicateGroup>> ScanAsync(string rootFolder, IProgress<int>? progress = null) =>
            Task.Run(() => Scan(rootFolder, progress));

        private static IReadOnlyList<DuplicateGroup> Scan(string rootFolder, IProgress<int>? progress)
        {
            var bySize = new Dictionary<long, List<string>>();
            var scanned = 0;
            foreach (var file in SafeEnumerateFiles(rootFolder))
            {
                long size;
                try { size = new FileInfo(file).Length; } catch { continue; }
                if (size <= 0 || size > MaxFileSizeBytes) continue;

                if (!bySize.TryGetValue(size, out var list)) { list = new List<string>(); bySize[size] = list; }
                list.Add(file);

                scanned++;
                if (scanned % 200 == 0) progress?.Report(scanned);
            }

            // Μόνο αρχεία που έχουν τουλάχιστον ένα άλλο αρχείο ΙΔΙΟΥ μεγέθους χρειάζονται πραγματικά
            // hash - η τεράστια πλειοψηφία αποκλείεται ήδη παραπάνω με μηδενικό κόστος.
            var candidates = bySize.Values.Where(list => list.Count > 1).SelectMany(list => list).ToList();

            // ΝΕΟ - βελτίωση απόδοσης: παράλληλο MD5 hashing αντί για σειριακό ανά ομάδα μεγέθους - το
            // hashing είναι το πιο ακριβό βήμα της σάρωσης και επωφελείται σημαντικά από πολλαπλούς
            // πυρήνες, ειδικά σε φακέλους με πολλά υποψήφια αρχεία.
            var hashes = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
            Parallel.ForEach(candidates, path =>
            {
                var hash = TryComputeHash(path);
                if (hash != null) hashes[path] = hash;
            });

            var groups = new List<DuplicateGroup>();
            foreach (var (size, paths) in bySize)
            {
                if (paths.Count < 2) continue;

                var byHash = new Dictionary<string, List<string>>();
                foreach (var path in paths)
                {
                    if (!hashes.TryGetValue(path, out var hash)) continue;
                    if (!byHash.TryGetValue(hash, out var list)) { list = new List<string>(); byHash[hash] = list; }
                    list.Add(path);
                }

                foreach (var (_, matchedPaths) in byHash)
                {
                    if (matchedPaths.Count > 1) groups.Add(new DuplicateGroup(size, matchedPaths));
                }
            }

            return groups.OrderByDescending(g => g.SizeBytes * (g.Paths.Count - 1)).ToList();
        }

        private static string? TryComputeHash(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var md5 = MD5.Create();
                return Convert.ToHexString(md5.ComputeHash(stream));
            }
            catch { return null; }
        }

        private static IEnumerable<string> SafeEnumerateFiles(string root)
        {
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var dir = stack.Pop();
                string[] files;
                try { files = Directory.GetFiles(dir); } catch { files = Array.Empty<string>(); }
                foreach (var f in files) yield return f;

                string[] subdirs;
                try { subdirs = Directory.GetDirectories(dir); } catch { subdirs = Array.Empty<string>(); }
                foreach (var d in subdirs) stack.Push(d);
            }
        }

        // Επιστρέφει πόσα αρχεία διαγράφηκαν με επιτυχία - ο caller αφαιρεί ΜΟΝΟ αυτά από τη λίστα
        // (ένα μεμονωμένο κλειδωμένο/απαγορευμένο αρχείο δεν σταματά τα υπόλοιπα).
        public static Task<int> DeleteAsync(IReadOnlyList<string> paths) => Task.Run(() =>
        {
            var deleted = 0;
            foreach (var path in paths)
            {
                try { File.Delete(path); deleted++; } catch { }
            }
            return deleted;
        });
    }
}
