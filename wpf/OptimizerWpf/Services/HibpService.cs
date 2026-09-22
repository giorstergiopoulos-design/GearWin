using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - roadmap "έλεγχος παραβιασμένων κωδικών" (από έρευνα ανταγωνιστικών εργαλείων, βλ.
    // Have I Been Pwned Pwned Passwords API). Χρησιμοποιεί το επίσημο k-anonymity πρωτόκολλο - ο
    // ΠΛΗΡΗΣ κωδικός ΔΕΝ φεύγει ΠΟΤΕ από τον υπολογιστή: υπολογίζεται τοπικά το SHA-1 hash του, και
    // στέλνονται ΜΟΝΟ τα πρώτα 5 hex ψηφία του (π.χ. "5BAA6" για "password") μέσω HTTPS - ο server
    // επιστρέφει όλα τα γνωστά hash suffixes που ταιριάζουν με αυτό το πρόθεμα (συνήθως εκατοντάδες),
    // και η αντιστοίχιση με το ΠΛΗΡΕΣ hash γίνεται τοπικά. Ίδιο ακριβώς πρωτόκολλο με το επίσημο site
    // haveibeenpwned.com/Passwords και με ό,τι χρησιμοποιούν 1Password/Bitwarden για τον ίδιο σκοπό -
    // κανένα API key απαιτείται για αυτό το endpoint. Ο κωδικός που πληκτρολογεί ο χρήστης ΔΕΝ
    // αποθηκεύεται/καταγράφεται πουθενά από την εφαρμογή.
    public static class HibpService
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

        public static async Task<int> CheckPasswordAsync(string password)
        {
            using var sha1 = SHA1.Create();
            var hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(password));
            var hash = Convert.ToHexString(hashBytes); // uppercase hex, no dashes
            var prefix = hash[..5];
            var suffix = hash[5..];

            var response = await Http.GetAsync($"https://api.pwnedpasswords.com/range/{prefix}");
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();

            foreach (var line in body.Split('\n'))
            {
                var parts = line.Trim().Split(':');
                if (parts.Length != 2) continue;
                if (string.Equals(parts[0], suffix, StringComparison.OrdinalIgnoreCase))
                    return int.TryParse(parts[1], out var count) ? count : -1;
            }
            return 0;
        }
    }
}
