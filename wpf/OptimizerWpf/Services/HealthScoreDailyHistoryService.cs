using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OptimizerWpf.Services
{
    public record HealthScoreDailySnapshot(string Date, int Score, int IssueCount);

    // ΝΕΟ - roadmap ιδέα #7 (ρητό αίτημα χρήστη: "κάνε τα 4-7" - "Εξαγωγή Health Check report σε PDF
    // ιστορικά... αρχείο τάσης στον χρόνο, ίδιο πνεύμα με το S.M.A.R.T. trend") - ίδιο ΑΚΡΙΒΩΣ μοτίβο
    // persistence/dedup-ανά-ημέρα με το DiskHealthHistoryService (βλ. εκεί) - ΜΙΑ καταγραφή το πολύ
    // ανά ημέρα, κρατά τις τελευταίες MaxEntries.
    //
    // ΔΙΑΦΟΡΕΤΙΚΟ (ρητά, ξεχωριστό αρχείο) από το ήδη υπάρχον HealthScoreHistoryService: εκείνο είναι
    // ΜΟΝΟ στη μνήμη (ΟΧΙ persisted, μηδενίζεται σε κάθε επανεκκίνηση) και τροφοδοτεί το sparkline
    // ▁▂▃▄▅▆▇█ της Αρχικής - διαφορετικός σκοπός (ζωντανή αίσθηση "μέσα σε αυτή τη συνεδρία") από αυτό
    // εδώ (persisted, ΜΙΑ φορά τη μέρα, για μακροπρόθεσμη τάση στο εξαγόμενο PDF). Ο πρώτος γύρος
    // αυτής της υλοποίησης χρησιμοποίησε κατά λάθος το ΙΔΙΟ όνομα κλάσης και αντικατέστησε το
    // υπάρχον αρχείο (εντοπίστηκε από compile error CS0117, επαναφέρθηκε μέσω git checkout).
    public static class HealthScoreDailyHistoryService
    {
        private const int MaxEntries = 60; // ~2 μήνες αν καταγράφεται καθημερινά
        private static readonly string StorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf", "HealthScoreDailyHistory.json");

        public static void RecordIfNeeded(int score, int issueCount)
        {
            try
            {
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var list = Load();
                if (list.Count > 0 && list[^1].Date == today) return; // ήδη καταγεγραμμένο σήμερα
                list.Add(new HealthScoreDailySnapshot(today, score, issueCount));
                if (list.Count > MaxEntries) list.RemoveAt(0);
                Save(list);
            }
            catch { }
        }

        public static IReadOnlyList<HealthScoreDailySnapshot> GetHistory() => Load();

        private static List<HealthScoreDailySnapshot> Load()
        {
            try
            {
                if (File.Exists(StorePath))
                    return JsonSerializer.Deserialize<List<HealthScoreDailySnapshot>>(File.ReadAllText(StorePath)) ?? new();
            }
            catch { }
            return new();
        }

        private static void Save(List<HealthScoreDailySnapshot> data)
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
