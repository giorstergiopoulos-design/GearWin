using System;
using System.Collections.Generic;
using System.Linq;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - roadmap "Ιστορικό/trend γράφημα για το Health Score" - ίδιο μοτίβο με το ήδη υπάρχον
    // ProcessHistoryService (απλό, στη μνήμη, ΟΧΙ persisted σε δίσκο - μόνο "στον χρόνο μέσα σε αυτή
    // τη συνεδρία"): static ώστε να επιβιώνει σε αλλαγές καρτέλας (κάθε HomeView instance είναι
    // καινούριο, ίδια ήδη καθιερωμένη σύμβαση "νέο view ανά επίσκεψη καρτέλας" αλλού στο project).
    public static class HealthScoreHistoryService
    {
        private const int MaxSamples = 20;
        private static readonly List<int> History = new();

        public static void RecordSample(int score)
        {
            History.Add(score);
            if (History.Count > MaxSamples) History.RemoveAt(0);
        }

        // Unicode block sparkline (▁▂▃▄▅▆▇█) - ίδια τεχνική με το ProcessHistoryService.GetSparkline,
        // καμία εξάρτηση σε γραφική βιβλιοθήκη, διαβάζεται απευθείας μέσα σε ένα απλό TextBlock.
        private static readonly char[] Blocks = { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

        public static string GetSparkline()
        {
            if (History.Count < 2) return "";
            var min = History.Min(); var max = History.Max();
            if (max == min) return new string(Blocks[3], History.Count);
            return new string(History.Select(v => Blocks[Math.Clamp((int)((double)(v - min) / (max - min) * (Blocks.Length - 1)), 0, Blocks.Length - 1)]).ToArray());
        }
    }
}
