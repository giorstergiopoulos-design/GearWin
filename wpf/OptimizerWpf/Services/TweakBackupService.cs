using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OptimizerWpf.Services
{
    // Κοινή "backup αρχικής τιμής" υποδομή - port του Get/Save-TweakBackupData του Optimizer.ps1,
    // που χρησιμοποιείται από πολλά διαφορετικά σημεία (Υπηρεσίες Συστήματος, αργότερα Επιπλέον
    // Ρυθμίσεις) ώστε "επαναφορά" να σημαίνει "γύρνα στην ΠΡΑΓΜΑΤΙΚΗ προηγούμενη τιμή του χρήστη",
    // ΟΧΙ κάποια hardcoded προεπιλογή. Αποθηκεύει μόνο την ΠΡΩΤΗ φορά που φαίνεται ένα key - επόμενες
    // κλήσεις BackupIfNeeded για το ίδιο key δεν αντικαθιστούν ήδη αποθηκευμένη τιμή (ίδια λογική με
    // το ps1 original - διαφορετικά η "αρχική" τιμή θα χανόταν μετά από toggle-off/toggle-on/toggle-off).
    public static class TweakBackupService
    {
        private static readonly string StorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OptimizerWpf", "TweakBackup.json");

        private static Dictionary<string, string>? _cache;

        private static Dictionary<string, string> Load()
        {
            if (_cache != null) return _cache;
            try
            {
                if (File.Exists(StorePath))
                {
                    _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(StorePath)) ?? new();
                    return _cache;
                }
            }
            catch { }
            _cache = new Dictionary<string, string>();
            return _cache;
        }

        private static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(StorePath)!;
                Directory.CreateDirectory(dir);
                File.WriteAllText(StorePath, JsonSerializer.Serialize(_cache));
            }
            catch { }
        }

        public static void BackupIfNeeded(string key, string currentValue)
        {
            var store = Load();
            if (store.ContainsKey(key)) return;
            store[key] = currentValue;
            Save();
        }

        public static string? GetBackup(string key) => Load().TryGetValue(key, out var v) ? v : null;
    }
}
