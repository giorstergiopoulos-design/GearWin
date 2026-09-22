using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OptimizerWpf.Services
{
    // Μετακινήθηκε εδώ από TweaksView.xaml.cs (ήταν private nested class) ώστε να μπορεί να
    // ξαναχρησιμοποιηθεί ΚΑΙ από το ήδη υπάρχον αρχείο-βασισμένο Export/Import ΚΑΙ από το νέο
    // NamedProfileService παρακάτω - ίδιο ακριβώς schema, καμία αλλαγή στη μορφή του JSON (πλήρης
    // συμβατότητα με ήδη εξαγμένα αρχεία προφίλ).
    public class FullProfileData
    {
        public Dictionary<string, bool> Tweaks { get; set; } = new();
        public string? ThemeName { get; set; }
        public bool? IsDarkMode { get; set; }
        public string? Language { get; set; }
        public bool? SidebarEnabled { get; set; }
        public string? SidebarPosition { get; set; }
        public string? MenuMode { get; set; }
        public List<string>? PinnedTweakKeys { get; set; }
    }

    // ΝΕΟ - roadmap "Ονομασμένα προφίλ πολλαπλών χρηστών/μηχανημάτων" - επεκτείνει το ήδη υπάρχον
    // αρχείο-βασισμένο Export/Import Profile (SaveFileDialog/OpenFileDialog, ένα αρχείο τη φορά) με
    // μια δεύτερη, γρηγορότερη επιλογή: ονομασμένα προφίλ αποθηκευμένα σε ΈΝΑ σταθερό φάκελο
    // (%LocalAppData%\OptimizerWpf\Profiles\), ώστε ο χρήστης να μπορεί να αποθηκεύσει π.χ. "Γραφείο"
    // vs "Gaming" και να εναλλάσσεται μεταξύ τους από μια απλή λίστα, χωρίς να χρειάζεται να θυμάται
    // πού αποθήκευσε κάθε αρχείο .json. ΙΔΙΟ FullProfileData/JSON schema με το ήδη υπάρχον
    // αρχείο-βασισμένο export - τα δύο μονοπάτια είναι πλήρως συμβατά μεταξύ τους.
    public static class NamedProfileService
    {
        private static readonly string ProfilesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf", "Profiles");

        public static IReadOnlyList<string> ListProfiles()
        {
            try
            {
                if (!Directory.Exists(ProfilesDir)) return Array.Empty<string>();
                return Directory.GetFiles(ProfilesDir, "*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Select(n => n!)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch { return Array.Empty<string>(); }
        }

        public static bool Save(string name, FullProfileData profile)
        {
            try
            {
                Directory.CreateDirectory(ProfilesDir);
                File.WriteAllText(PathFor(name), JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
                return true;
            }
            catch { return false; }
        }

        public static FullProfileData? Load(string name)
        {
            try
            {
                var path = PathFor(name);
                return File.Exists(path) ? JsonSerializer.Deserialize<FullProfileData>(File.ReadAllText(path)) : null;
            }
            catch { return null; }
        }

        public static bool Delete(string name)
        {
            try
            {
                var path = PathFor(name);
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch { return false; }
        }

        private static string PathFor(string name) => Path.Combine(ProfilesDir, SafeFileName(name) + ".json");

        private static string SafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }
}
