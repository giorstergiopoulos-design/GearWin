using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - roadmap "Ασφαλής καταστροφέας αρχείων (file shredder)" - μόνιμη διαγραφή με επικάλυψη
    // αντί για απλό File.Delete, στάνταρ χαρακτηριστικό στο Glary Utilities/CCleaner Pro.
    // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: σε δίσκους SSD/NVMe η επικάλυψη ΔΕΝ είναι εγγυημένη λόγω wear-leveling
    // (ο controller μπορεί να γράψει τα νέα δεδομένα σε διαφορετικό φυσικό block, αφήνοντας τα παλιά
    // δεδομένα ανέπαφα μέχρι το TRIM/garbage collection) - γνωστός, τεκμηριωμένος περιορισμός κάθε
    // λογισμικού shredder σε SSD, ΟΧΙ κάτι που μπορούμε να αποφύγουμε. Το UI το αναφέρει ρητά.
    public static class FileShredderService
    {
        public static Task<bool> ShredFileAsync(string path, int passes = 3) => Task.Run(() =>
        {
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) return false;
                var length = fi.Length;

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    for (var pass = 0; pass < passes; pass++)
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        long remaining = length;
                        while (remaining > 0)
                        {
                            var chunk = (int)Math.Min(buffer.Length, remaining);
                            RandomNumberGenerator.Fill(buffer.AsSpan(0, chunk));
                            stream.Write(buffer, 0, chunk);
                            remaining -= chunk;
                        }
                        stream.Flush(flushToDisk: true);
                    }
                }

                // Μετονομασία σε τυχαίο όνομα πριν τη διαγραφή - αποκρύπτει το αρχικό όνομα αρχείου
                // από το MFT/journal, στάνταρ πρακτική shredder εργαλείων.
                var dir = Path.GetDirectoryName(path)!;
                var randomPath = Path.Combine(dir, Guid.NewGuid().ToString("N"));
                File.Move(path, randomPath);
                File.Delete(randomPath);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }
}
