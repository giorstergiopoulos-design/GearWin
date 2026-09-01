using System.Diagnostics;
using System.Linq;
using System.Management;

namespace OptimizerWpf.Services
{
    // GPU (ρητό αίτημα χρήστη: 4ο πλακίδιο απόδοσης με θερμοκρασία + ποσοστό χρήσης). ΣΗΜΕΙΩΣΗ
    // ΕΙΛΙΚΡΙΝΕΙΑΣ: τα Windows ΔΕΝ εκθέτουν καμία καθολική πηγή θερμοκρασίας GPU χωρίς εξάρτηση σε
    // vendor SDK (NVIDIA NVML/AMD ADL) - εκτός εμβέλειας εδώ, η θερμοκρασία GPU θα δείχνει πάντα "—"
    // στο tile, ΟΧΙ ψευδή τιμή. Το ΠΟΣΟΣΤΟ ΧΡΗΣΗΣ όμως ΕΙΝΑΙ αξιόπιστα διαθέσιμο μέσω της
    // ενσωματωμένης κατηγορίας μετρητών "GPU Engine" (Windows 10+, καμία vendor εξάρτηση) - η ΙΔΙΑ
    // πηγή δεδομένων που δείχνει το tab Απόδοση > GPU του Task Manager.
    public static class GpuInfoService
    {
        public static string GetName()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                var name = searcher.Get().Cast<ManagementBaseObject>().FirstOrDefault()?["Name"]?.ToString();
                return string.IsNullOrWhiteSpace(name) ? "GPU" : name.Trim();
            }
            catch
            {
                return "GPU";
            }
        }

        // Ένας μετρητής ανά "engtype_3D" instance (ίδιο instance filtering με το Task Manager) - οι
        // μετρητές πρέπει να παραμείνουν ζωντανοί ανάμεσα σε κλήσεις (το NextValue() της πρώτης
        // κλήσης πάντα επιστρέφει 0 - ίδιος περιορισμός με τον υπάρχοντα μετρητή CPU). Ο καλών
        // (HomeView) κρατάει το αποτέλεσμα και καλεί Sample() σε κάθε tick του υπάρχοντος 1s timer.
        public static PerformanceCounter[]? CreateUsageCounters()
        {
            try
            {
                if (!PerformanceCounterCategory.Exists("GPU Engine")) return null;
                var category = new PerformanceCounterCategory("GPU Engine");
                var instances = category.GetInstanceNames().Where(n => n.Contains("engtype_3D")).ToArray();
                if (instances.Length == 0) return null;

                var counters = instances
                    .Select(n =>
                    {
                        try
                        {
                            var c = new PerformanceCounter("GPU Engine", "Utilization Percentage", n, true);
                            c.NextValue(); // πρώτη κλήση πάντα 0 - προθέρμανση
                            return c;
                        }
                        catch { return null; }
                    })
                    .Where(c => c != null)
                    .Cast<PerformanceCounter>()
                    .ToArray();
                return counters.Length > 0 ? counters : null;
            }
            catch
            {
                return null;
            }
        }

        public static int SampleUsagePercent(PerformanceCounter[] counters)
        {
            double sum = 0;
            foreach (var c in counters)
            {
                try { sum += c.NextValue(); } catch { /* instance can disappear mid-run (process exited) */ }
            }
            return (int)System.Math.Round(System.Math.Min(100, sum));
        }
    }
}
