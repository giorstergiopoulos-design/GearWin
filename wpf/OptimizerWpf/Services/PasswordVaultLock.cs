using System;
using System.Security.Cryptography;
using System.Text;

namespace OptimizerWpf.Services
{
    // 6.9.0 - κλείδωμα του διαχειριστή κωδικών browser με κωδικό πρόσβασης του χρήστη. Αποθηκεύεται ΜΟΝΟ salted PBKDF2-SHA256 hash (όχι ο κωδικός)
    // στις ρυθμίσεις· ο έλεγχος γίνεται με σταθερό χρόνο. Προστατεύει το άνοιγμα του παραθύρου/εξαγωγής από άλλον χρήστη του ίδιου λογαριασμού που
    // βλέπει την οθόνη - ΔΕΝ κρυπτογραφεί τους κωδικούς του browser (αυτοί παραμένουν στον browser).
    public static class PasswordVaultLock
    {
        public const int MinLength = 6;
        public const int Iterations = 200_000;

        public static bool IsSet => !string.IsNullOrEmpty(AppSettingsService.Current.PwdVaultHash);

        public static string NewSalt() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        public static string Hash(string password, string saltBase64) =>
            Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Convert.FromBase64String(saltBase64), Iterations, HashAlgorithmName.SHA256, 32));

        public static bool VerifyAgainst(string password, string saltBase64, string hashBase64)
        {
            try { return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(Hash(password, saltBase64)), Convert.FromBase64String(hashBase64)); }
            catch (FormatException) { return false; }
        }

        public static bool Verify(string password) =>
            IsSet && VerifyAgainst(password, AppSettingsService.Current.PwdVaultSalt ?? "", AppSettingsService.Current.PwdVaultHash ?? "");

        public static bool TrySet(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < MinLength) return false;
            var salt = NewSalt();
            AppSettingsService.Current.PwdVaultSalt = salt;
            AppSettingsService.Current.PwdVaultHash = Hash(password, salt);
            AppSettingsService.Save();
            return true;
        }

        public static void Clear()
        {
            AppSettingsService.Current.PwdVaultSalt = null;
            AppSettingsService.Current.PwdVaultHash = null;
            AppSettingsService.Save();
        }

        // Εξαγωγή σε απλό κείμενο (αίτημα χρήστη). Καθαρή συνάρτηση.
        public static string FormatExport(System.Collections.Generic.IEnumerable<PasswordVaultService.SavedCredential> items, DateTime now)
        {
            var sb = new StringBuilder();
            sb.AppendLine("GearWin - saved browser passwords export - " + now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine("WARNING: this file is plain text. Anyone who can read it can read the passwords. Delete it when you no longer need it.");
            sb.AppendLine(new string('-', 60));
            foreach (var c in items)
            {
                sb.AppendLine("Browser:  " + c.Browser);
                sb.AppendLine("Site:     " + c.Origin);
                sb.AppendLine("User:     " + c.Username);
                sb.AppendLine("Password: " + c.Password);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
