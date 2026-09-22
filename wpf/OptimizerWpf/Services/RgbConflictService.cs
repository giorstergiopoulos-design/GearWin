using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - roadmap "RGB conflict detector". ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: δεν υπάρχει ενιαίο δημόσιο API
    // για τον έλεγχο RGB lighting hardware ανά κατασκευαστή (Corsair/Asus/MSI/Gigabyte/κ.λπ. έχουν
    // όλοι κλειστά, ιδιόκτητα SDK) - οπότε αυτό ΔΕΝ είναι ανίχνευση σε επίπεδο υλικού. Αντ' αυτού,
    // ανιχνεύει το ΓΝΩΣΤΟ, καλά τεκμηριωμένο πρόβλημα enthusiast κοινοτήτων: όταν τρέχουν ΤΑΥΤΟΧΡΟΝΑ
    // 2+ προγράμματα ελέγχου RGB διαφορετικών κατασκευαστών (π.χ. Corsair iCUE + ASUS Aura Sync),
    // συχνά "μαλώνουν" για τον έλεγχο του ίδιου ARGB header/συσκευής, προκαλώντας τρεμόπαιγμα ή
    // απρόβλεπτα χρώματα - απλή ανίχνευση διεργασιών εν εξελίξει, όχι ψεύτικη ακρίβεια.
    public static class RgbConflictService
    {
        public record RgbSoftwareInfo(string Vendor, string ProcessName);

        // Γνωστά process names των πιο συνηθισμένων εργαλείων RGB ελέγχου - ένα process name ανά
        // γραμμή (χωρίς ".exe", ίδιο με το Process.ProcessName).
        private static readonly (string Vendor, string[] ProcessNames)[] KnownTools =
        {
            ("Corsair iCUE", new[] { "iCUE" }),
            ("Razer Synapse", new[] { "RazerCentralService", "Razer Synapse Service Process", "RzSynapse" }),
            ("ASUS Aura Sync", new[] { "LightingService", "AsusLightingService", "AuraServiceLauncher" }),
            ("MSI Mystic Light", new[] { "MysticLight", "MysticLight_Ambient" }),
            ("Logitech G HUB", new[] { "lghub", "lghub_agent" }),
            ("SignalRGB", new[] { "SignalRgb" }),
            ("OpenRGB", new[] { "OpenRGB" }),
            ("Gigabyte RGB Fusion", new[] { "RGBFusion", "GCPlatform" }),
            ("SteelSeries GG", new[] { "SteelSeriesGG" }),
            ("Cooler Master MasterPlus+", new[] { "MasterPlus" }),
            ("Thermaltake TT RGB Plus", new[] { "TTRGBPLUS" }),
            ("NZXT CAM", new[] { "NZXT CAM" }),
        };

        public static IReadOnlyList<RgbSoftwareInfo> DetectRunning()
        {
            var running = Process.GetProcesses().Select(p => { try { return p.ProcessName; } catch { return ""; } }).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var found = new List<RgbSoftwareInfo>();
            foreach (var (vendor, names) in KnownTools)
            {
                var match = names.FirstOrDefault(n => running.Contains(n));
                if (match != null) found.Add(new RgbSoftwareInfo(vendor, match));
            }
            return found;
        }
    }
}
