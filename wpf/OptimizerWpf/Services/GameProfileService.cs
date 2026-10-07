using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OptimizerWpf.Services
{
    public class GameProfileData
    {
        public GpuPreference? GpuPreference { get; set; }
        public bool? DisableFullscreenOptimizations { get; set; }
        // Κλειδί = SimpleTweak.Id (π.χ. "GameMode","Hags","WindowedOpt","VrrOptimize") - βλ.
        // GamingTweaksService.GlobalTweaks(). Μόνο τα tweaks που ο χρήστης είχε ανοιχτά/κλειστά τη
        // στιγμή της αποθήκευσης αποθηκεύονται εδώ· η Εφαρμογή Προφίλ αλλάζει ΜΟΝΟ αυτά.
        public Dictionary<string, bool> GlobalTweaks { get; set; } = new();
    }

    // REQ-590-06 (ήδη στο backlog, PROJECT_STATE.md) - "προφίλ tweaks ανά παιχνίδι". Η προτίμηση GPU
    // και το FSO είναι ΗΔΗ ανά-exe μέσω των δικών τους registry κλειδιών (GamingTweaksService) - αυτό
    // που λείπει είναι τα 4 ΚΑΘΟΛΙΚΑ (μηχανής, όχι ανά-exe) gaming tweaks: π.χ. "θέλω VRR off στο
    // παιχνίδι Χ αλλά on αλλού". Αποθηκεύει ρητά, χωρίς παρακολούθηση διαδικασιών/αυτόματη εφαρμογή
    // στην εκκίνηση/έξοδο του παιχνιδιού (μη επαληθεύσιμο σε αυτό το περιβάλλον) - ο χρήστης πατά
    // "Αποθήκευση" και "Εφαρμογή" ρητά, ίδιο μοτίβο επαλήθευσης με το ήδη υπάρχον NamedProfileService.
    public static class GameProfileService
    {
        private static readonly string ProfilesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf", "GameProfiles");

        public static bool HasProfile(string exePath) => File.Exists(PathFor(exePath));

        public static bool Save(string exePath, GameProfileData data)
        {
            try
            {
                Directory.CreateDirectory(ProfilesDir);
                File.WriteAllText(PathFor(exePath), JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
                return true;
            }
            catch { return false; }
        }

        public static GameProfileData? Load(string exePath)
        {
            try
            {
                var path = PathFor(exePath);
                return File.Exists(path) ? JsonSerializer.Deserialize<GameProfileData>(File.ReadAllText(path)) : null;
            }
            catch { return null; }
        }

        private static string PathFor(string exePath) => Path.Combine(ProfilesDir, SafeFileName(exePath) + ".json");

        public static string SafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }
}
