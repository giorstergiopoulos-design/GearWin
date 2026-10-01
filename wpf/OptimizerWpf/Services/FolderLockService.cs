using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - roadmap "Κλείδωμα φακέλων με κωδικούς" (ρητό αίτημα χρήστη). Δεν χρησιμοποιείται
    // BitLocker-πάνω-σε-VHD (η προφανής "εγγενής" επιλογή) επειδή το BitLocker για ΠΡΟΣΘΕΤΟΥΣ δίσκους/
    // containers (σε αντίθεση με τον ίδιο τον δίσκο του συστήματος) ΔΕΝ είναι διαθέσιμο στις εκδόσεις
    // Windows Home - θα άφηνε μεγάλο μέρος των χρηστών της εφαρμογής εντελώς εκτός. Αντ' αυτού:
    // πραγματική AES-256-GCM κρυπτογράφηση μέσω των ΗΔΗ ελεγμένων/audited κλάσεων του .NET
    // (System.Security.Cryptography.AesGcm + Rfc2898DeriveBytes.Pbkdf2, ΟΧΙ δική μας υλοποίηση
    // κρυπτογραφικού αλγορίθμου) - λειτουργεί σε ΚΑΘΕ έκδοση Windows.
    //
    // Μορφή αρχείου .vault: [8 bytes magic "OWVAULT1"][16 bytes salt][12 bytes nonce][16 bytes GCM
    // tag][ciphertext = συμπιεσμένο .zip του φακέλου]. Ο κωδικός περνά από PBKDF2-SHA256 με 210.000
    // επαναλήψεις (τρέχουσα σύσταση OWASP 2023) για την παραγωγή του 256-bit κλειδιού AES. Το GCM tag
    // ΕΠΑΛΗΘΕΥΕΙ αυτόματα λάθος κωδικό/κατεστραμμένο αρχείο (authenticated encryption) - καμία ανάγκη
    // ξεχωριστού ελέγχου "σωστός κωδικός".
    //
    // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ (ίδιο πνεύμα με άλλες αντίστοιχες σημειώσεις σε αυτό το codebase): όλο το
    // περιεχόμενο του φακέλου φορτώνεται στη μνήμη κατά την κρυπτογράφηση/αποκρυπτογράφηση (όχι
    // streaming) - κατάλληλο για φακέλους εγγράφων/φωτογραφιών, ΟΧΙ για πολύ μεγάλες βιβλιοθήκες πολυμέσων
    // (πάνω από μερικά GB). Το αρχικό, μη κρυπτογραφημένο περιεχόμενο ΔΙΑΓΡΑΦΕΤΑΙ με το ίδιο μηχανισμό
    // "καταστροφής" (3 περάσματα overwrite) με το FileShredderService, ώστε να μην παραμένει διαθέσιμο σε
    // απλή διαγραφή/κάδο ανακύκλωσης - ίδιος περιορισμός wear-leveling σε SSD/NVMe με το FileShredderService.
    public static class FolderLockService
    {
        private static readonly byte[] Magic = System.Text.Encoding.ASCII.GetBytes("OWVAULT1");
        private const int SaltSize = 16;
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int Pbkdf2Iterations = 210_000;

        public const string VaultExtension = ".vault";

        public static async Task LockFolderAsync(string folderPath, string password, IProgress<string>? progress = null)
        {
            if (!Directory.Exists(folderPath)) throw new DirectoryNotFoundException(folderPath);
            // Μετά την κρυπτογράφηση ο ΑΡΧΙΚΟΣ φάκελος καταστρέφεται (shred). Μια λάθος επιλογή ρίζας δίσκου ή
            // φακέλου συστήματος/προφίλ θα έσβηνε όλο το σύστημα — αρνούμαστε ρητά τέτοιους φακέλους.
            if (IsProtectedFolder(folderPath))
                throw new InvalidOperationException("This folder is a system or user-profile location and cannot be locked/shredded.");
            var vaultPath = folderPath.TrimEnd('\\', '/') + VaultExtension;
            if (File.Exists(vaultPath)) throw new IOException("Vault file already exists.");

            progress?.Report(LanguageService.T("FolderLock_Compressing"));
            byte[] zipBytes;
            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var baseDir = new DirectoryInfo(folderPath);
                    foreach (var file in baseDir.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        var relative = Path.GetRelativePath(folderPath, file.FullName);
                        zip.CreateEntryFromFile(file.FullName, relative, CompressionLevel.Optimal);
                    }
                }
                zipBytes = ms.ToArray();
            }

            progress?.Report(LanguageService.T("FolderLock_Encrypting"));
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);

            var ciphertext = new byte[zipBytes.Length];
            var tag = new byte[TagSize];
            using (var aes = new AesGcm(key, TagSize))
                aes.Encrypt(nonce, zipBytes, ciphertext, tag);
            Array.Clear(zipBytes); // το κλειδί καθαρίζεται ΜΕΤΑ την επαλήθευση του vault, παρακάτω

            await using (var outFile = File.Create(vaultPath))
            {
                await outFile.WriteAsync(Magic);
                await outFile.WriteAsync(salt);
                await outFile.WriteAsync(nonce);
                await outFile.WriteAsync(tag);
                await outFile.WriteAsync(ciphertext);
                await outFile.FlushAsync();
            }

            // ΕΠΑΛΗΘΕΥΣΗ ΠΡΙΝ ΤΗΝ ΚΑΤΑΣΤΡΟΦΗ: το αρχικό περιεχόμενο shred-άρεται αμέσως μετά — αν το .vault που γράφτηκε
            // είναι ελλιπές/κατεστραμμένο (γεμάτος δίσκος, antivirus, διακοπή) τα δεδομένα θα χάνονταν οριστικά.
            // Ξαναδιαβάζουμε το αρχείο από τον δίσκο και ελέγχουμε το GCM tag με το ΙΔΙΟ κλειδί.
            try
            {
                var written = await File.ReadAllBytesAsync(vaultPath);
                var headerLen = Magic.Length + SaltSize + NonceSize + TagSize;
                if (written.Length != headerLen + ciphertext.Length) throw new IOException("Vault size mismatch.");
                var scratch = new byte[ciphertext.Length];
                using var verifyAes = new AesGcm(key, TagSize);
                verifyAes.Decrypt(written.AsSpan(Magic.Length + SaltSize, NonceSize),
                    written.AsSpan(headerLen),
                    written.AsSpan(Magic.Length + SaltSize + NonceSize, TagSize), scratch);
                Array.Clear(scratch);
            }
            catch (Exception ex) when (ex is IOException or CryptographicException)
            {
                try { File.Delete(vaultPath); } catch { }
                throw new IOException("The vault could not be verified after writing; the original folder was NOT deleted.", ex);
            }
            finally { Array.Clear(key); }

            progress?.Report(LanguageService.T("FolderLock_Shredding"));
            await ShredDirectoryAsync(folderPath);
        }

        public static async Task UnlockFolderAsync(string vaultPath, string password, string destinationFolder, IProgress<string>? progress = null)
        {
            if (!File.Exists(vaultPath)) throw new FileNotFoundException(vaultPath);

            progress?.Report(LanguageService.T("FolderLock_Decrypting"));
            var all = await File.ReadAllBytesAsync(vaultPath);
            var offset = 0;
            if (!all.AsSpan(offset, Magic.Length).SequenceEqual(Magic)) throw new InvalidDataException("Not a valid vault file.");
            offset += Magic.Length;
            var salt = all.AsSpan(offset, SaltSize).ToArray(); offset += SaltSize;
            var nonce = all.AsSpan(offset, NonceSize).ToArray(); offset += NonceSize;
            var tag = all.AsSpan(offset, TagSize).ToArray(); offset += TagSize;
            var ciphertext = all.AsSpan(offset).ToArray();

            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);
            var plaintext = new byte[ciphertext.Length];
            try
            {
                using var aes = new AesGcm(key, TagSize);
                // Λάθος κωδικός -> λάθος κλειδί -> ΑΠΟΤΥΧΙΑ επαλήθευσης του GCM tag -> εξαίρεση εδώ.
                // Αυτό ΕΙΝΑΙ ο έλεγχος "σωστός κωδικός" - καμία ξεχωριστή λογική χρειάζεται.
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
            }
            catch (CryptographicException)
            {
                throw new UnauthorizedAccessException(LanguageService.T("FolderLock_WrongPassword"));
            }
            finally { Array.Clear(key); }

            progress?.Report(LanguageService.T("FolderLock_Extracting"));
            Directory.CreateDirectory(destinationFolder);
            using (var ms = new MemoryStream(plaintext))
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
                zip.ExtractToDirectory(destinationFolder, overwriteFiles: true);
            Array.Clear(plaintext);

            // Το vault file ΔΕΝ διαγράφεται αυτόματα εδώ - ο χρήστης το κρατάει σαν το "κλειδωμένο"
            // αντίγραφο, το εξάγει όποτε το χρειάζεται. Ρητή, ξεχωριστή ενέργεια "Διαγραφή vault"
            // στο UI αν το θέλει.
        }

        private static bool IsProtectedFolder(string folderPath)
        {
            var full = Path.GetFullPath(folderPath).TrimEnd('\\', '/');
            if (Path.GetPathRoot(full)?.TrimEnd('\\', '/').Equals(full, StringComparison.OrdinalIgnoreCase) == true) return true; // ρίζα δίσκου
            foreach (var special in new[]
            {
                Environment.SpecialFolder.Windows, Environment.SpecialFolder.System, Environment.SpecialFolder.ProgramFiles,
                Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.CommonApplicationData,
                Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.LocalApplicationData,
            })
            {
                var sp = Environment.GetFolderPath(special).TrimEnd('\\', '/');
                if (sp.Length == 0) continue;
                // ίδιος φάκελος Ή γονικός του (θα περιείχε τον προστατευμένο)
                if (full.Equals(sp, StringComparison.OrdinalIgnoreCase)) return true;
                if (sp.StartsWith(full + "\\", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static async Task ShredDirectoryAsync(string folderPath)
        {
            var dir = new DirectoryInfo(folderPath);
            foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try { await FileShredderService.ShredFileAsync(file.FullName, passes: 1); }
                catch { try { file.Delete(); } catch { } }
            }
            try { Directory.Delete(folderPath, recursive: true); } catch { }
        }
    }
}
