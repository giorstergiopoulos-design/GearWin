using System.Collections.Generic;
using System.Linq;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - roadmap "Ιστορικό χρήσης πόρων" - απλό, στη μνήμη (ΟΧΙ persisted σε δίσκο - δεν χρειάζεται
    // να επιβιώνει σε επανεκκίνηση, μόνο "στον χρόνο μέσα σε αυτή τη συνεδρία" όπως ζητά το roadmap
    // item) ιστορικό μνήμης ανά όνομα διεργασίας, ενημερώνεται σε κάθε "Ανανέωση" της λίστας
    // Διεργασιών (SystemView) - static ώστε να επιβιώνει σε αλλαγές καρτέλας (κάθε SystemView instance
    // είναι καινούριο, βλ. ήδη καθιερωμένη σύμβαση "νέο view ανά επίσκεψη καρτέλας" αλλού στο project).
    public static class ProcessHistoryService
    {
        private const int MaxSamples = 20;
        private static readonly Dictionary<string, List<double>> History = new();

        public static void RecordSample(string processName, double workingSetMb)
        {
            if (!History.TryGetValue(processName, out var list)) History[processName] = list = new List<double>();
            list.Add(workingSetMb);
            if (list.Count > MaxSamples) list.RemoveAt(0);
        }

        // Unicode block sparkline (▁▂▃▄▅▆▇█) - καμία εξάρτηση σε γραφική βιβλιοθήκη, διαβάζεται
        // απευθείας μέσα σε ένα απλό TextBlock δίπλα στο τρέχον MB της κάθε διεργασίας.
        private static readonly char[] Blocks = { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

        public static string GetSparkline(string processName)
        {
            if (!History.TryGetValue(processName, out var list) || list.Count < 2) return "";
            var min = list.Min(); var max = list.Max();
            if (max - min < 0.01) return new string(Blocks[3], list.Count);
            return new string(list.Select(v => Blocks[System.Math.Clamp((int)((v - min) / (max - min) * (Blocks.Length - 1)), 0, Blocks.Length - 1)]).ToArray());
        }
    }
}
