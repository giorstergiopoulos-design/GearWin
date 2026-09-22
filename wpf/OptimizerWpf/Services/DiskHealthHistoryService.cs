using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OptimizerWpf.Services
{
    public record DiskHealthSnapshot(string Date, string Drive, string Health, double FreeGb, double TotalGb);

    // ΝΕΟ - roadmap "Τάση υγείας δίσκου (S.M.A.R.T.)" - καταγραφή τιμών ΣΤΟΝ ΧΡΟΝΟ (persisted σε
    // δίσκο, ΟΧΙ μόνο στη μνήμη - αντίθετα με το ProcessHistoryService, εδώ ο σκοπός είναι να φαίνεται
    // επιδείνωση ΜΕΤΑΞΥ διαφορετικών ημερών/εβδομάδων χρήσης, άρα πρέπει να επιβιώνει σε επανεκκίνηση).
    // Ένα στιγμιότυπο ΤΟ ΠΟΛΥ μία φορά την ημέρα ανά δίσκο (dedup by date) - αποφεύγει να γεμίσει το
    // αρχείο με πανομοιότυπες καταχωρήσεις αν ο χρήστης ανοίγει την καρτέλα Υγεία πολλές φορές την ίδια μέρα.
    public static class DiskHealthHistoryService
    {
        private const int MaxEntriesPerDrive = 60; // ~2 μήνες αν καταγράφεται καθημερινά
        private static readonly string StorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf", "DiskHealthHistory.json");

        public static void RecordIfNeeded(IEnumerable<(string Drive, string Health, double FreeGb, double TotalGb)> drives)
        {
            try
            {
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var all = Load();
                var changed = false;
                foreach (var (drive, health, freeGb, totalGb) in drives)
                {
                    if (!all.TryGetValue(drive, out var list)) all[drive] = list = new List<DiskHealthSnapshot>();
                    if (list.Count > 0 && list[^1].Date == today) continue; // ήδη καταγεγραμμένο σήμερα
                    list.Add(new DiskHealthSnapshot(today, drive, health, freeGb, totalGb));
                    if (list.Count > MaxEntriesPerDrive) list.RemoveAt(0);
                    changed = true;
                }
                if (changed) Save(all);
            }
            catch { }
        }

        public static IReadOnlyList<DiskHealthSnapshot> GetHistory(string drive) =>
            Load().TryGetValue(drive, out var list) ? list : Array.Empty<DiskHealthSnapshot>();

        public static IReadOnlyDictionary<string, List<DiskHealthSnapshot>> GetAll() => Load();

        private static Dictionary<string, List<DiskHealthSnapshot>> Load()
        {
            try
            {
                if (File.Exists(StorePath))
                    return JsonSerializer.Deserialize<Dictionary<string, List<DiskHealthSnapshot>>>(File.ReadAllText(StorePath)) ?? new();
            }
            catch { }
            return new();
        }

        private static void Save(Dictionary<string, List<DiskHealthSnapshot>> data)
        {
            try
            {
                var dir = Path.GetDirectoryName(StorePath)!;
                Directory.CreateDirectory(dir);
                File.WriteAllText(StorePath, JsonSerializer.Serialize(data));
            }
            catch { }
        }
    }
}
