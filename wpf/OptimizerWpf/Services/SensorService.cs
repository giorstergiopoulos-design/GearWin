using System;
using System.Linq;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    // Θερμοκρασίες CPU/GPU/Δίσκου μέσω LibreHardwareMonitorLib (ρητό αίτημα χρήστη μετά από
    // διαδικτυακή έρευνα - βλ. HANDOFF.md): οι προηγούμενες προσπάθειες μέσω καθαρού WMI
    // (MSFT_PhysicalDisk.GetStorageReliabilityCounter/MSAcpi_ThermalZoneTemperature) επιβεβαιώθηκαν
    // ζωντανά ότι δεν επιστρέφουν τίποτα σε συνηθισμένο σύγχρονο hardware (π.χ. NVMe δίσκους), και
    // ΔΕΝ υπάρχει καμία WMI κλάση για GPU θερμοκρασία χωρίς vendor SDK (NVIDIA NVML/AMD ADL). Το
    // LibreHardwareMonitorLib διαβάζει αισθητήρες απευθείας (ίδια τεχνική με το ίδιο το Task Manager/
    // HWiNFO/κ.λπ.) - χρειάζεται Administrator, το οποίο η εφαρμογή ήδη απαιτεί (app.manifest).
    //
    // Ο `Computer` είναι "βαρύ" αντικείμενο (φορτώνει kernel driver πρόσβαση σε αισθητήρες στο
    // Open()) - κρατιέται ως ΕΝΑ, process-wide singleton, ΠΟΤΕ per-query. Update() σε κάθε κλήση
    // είναι σχετικά φθηνό μετά το αρχικό Open() - καλείται από τον καλούντα (HomeView) όχι πιο συχνά
    // από 1 φορά ανά μερικά δευτερόλεπτα, αφού οι θερμοκρασίες δεν αλλάζουν αισθητά μέσα σε 1s.
    public static class SensorService
    {
        private static Computer? _computer;
        private static bool _openFailed;

        private static Computer? GetComputer()
        {
            if (_openFailed) return null;
            if (_computer != null) return _computer;
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsStorageEnabled = true,
                    IsMemoryEnabled = true,
                };
                _computer.Open();
                return _computer;
            }
            catch
            {
                // Ο kernel driver του LibreHardwareMonitor μπορεί να αποτύχει να φορτώσει (π.χ.
                // πολιτική antivirus/Group Policy που μπλοκάρει drivers, ή απουσία elevation σε
                // ασυνήθιστη περίπτωση) - υποβαθμίζεται σε "καμία θερμοκρασία διαθέσιμη" αντί να
                // ρίξει την εφαρμογή.
                _openFailed = true;
                return null;
            }
        }

        private static void RefreshAll(Computer computer)
        {
            foreach (var hw in computer.Hardware)
            {
                hw.Update();
                foreach (var sub in hw.SubHardware) sub.Update();
            }
        }

        // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "η θερμοκρασία... φαίνεται η ίδια σε CPU/GPU"): "Package" από
        // μόνο του είναι Intel-στυλ ονομασία - στο AMD ο αντίστοιχος αισθητήρας λέγεται "Tctl/Tdie"
        // ή "Core (Tctl/Tdie)", ΔΕΝ ταίριαζε ποτέ σε AMD σύστημα, οπότε έπεφτε σε
        // `sensors.FirstOrDefault()` (ΟΠΟΙΟΣΔΗΠΟΤΕ αισθητήρας θερμοκρασίας βρεθεί πρώτος, μπορεί να
        // είναι ασήμαντος - π.χ. VRM/SoC αντί για τον πυρήνα). Τώρα δοκιμάζει πολλαπλά γνωστά
        // ονόματα (Intel ΚΑΙ AMD) με τη σειρά.
        private static readonly string[] CpuPackageNames = { "Package", "Tctl", "Tdie", "CPU Die", "Core Average", "Core Max" };
        private static readonly string[] GpuCoreNames = { "GPU Core", "Core", "Hot Spot", "Junction" };

        // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "CPU temp δεν φαίνεται, η Gigabyte έχει εφαρμογή που τη μετράει") -
        // επιβεβαιώθηκε ζωντανά ότι το Memory Integrity/VBS/HVCI είναι ενεργό σε αυτό το μηχάνημα
        // (SecurityServicesRunning=2, ίδιο registry key με το ήδη υπάρχον Gaming Mode toggle). Το VBS
        // μπλοκάρει ΑΚΡΙΒΩΣ την απευθείας MSR/IO port πρόσβαση που χρειάζεται το LibreHardwareMonitorLib
        // (ο κύριος, τεκμηριωμένος λόγος που εργαλεία σαν αυτό αποτυγχάνουν σιωπηλά σε πολλά σύγχρονα
        // Windows 11 συστήματα με VBS - ανεξάρτητα από elevation). Vendor εργαλεία (Gigabyte SIV/Control
        // Center κ.λπ.) συχνά έχουν το ΔΙΚΟ ΤΟΥΣ signed driver με διαφορετικό, εγκεκριμένο μονοπάτι.
        public static bool IsHvciActive()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");
                return Convert.ToInt32(key?.GetValue("Enabled") ?? 0) == 1;
            }
            catch { return false; }
        }

        public static int? GetCpuTemperatureCelsius() => GetTemperature(HardwareType.Cpu, CpuPackageNames);

        public static int? GetGpuTemperatureCelsius() =>
            GetTemperature(HardwareType.GpuNvidia, GpuCoreNames) ??
            GetTemperature(HardwareType.GpuAmd, GpuCoreNames) ??
            GetTemperature(HardwareType.GpuIntel, GpuCoreNames);

        // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "στη RAM δεν φαίνεται τπτ") - καμία τυπική/καθολικά προσβάσιμη
        // πηγή θερμοκρασίας RAM υπάρχει σε commodity hardware (μόνο συγκεκριμένα SPD hub chips σε
        // RGB RAM kits) - ΑΥΤΟ παραμένει αλήθεια, αλλά το tile δεν είχε καν ΓΡΑΜΜΗ θερμοκρασίας
        // (ούτε καν "—"), ασυνεπές με τα άλλα 3 tiles. Τώρα δοκιμάζεται ΚΙ αυτό μέσω
        // HardwareType.Memory (σπάνια θα βρει κάτι, αλλά αν το hardware το υποστηρίζει θα
        // εμφανιστεί) - αλλιώς δείχνει τίμια "—" σαν τα υπόλοιπα, όχι πια τίποτα.
        public static int? GetRamTemperatureCelsius() => GetTemperature(HardwareType.Memory, null);

        // Δεν υπάρχει άμεση αντιστοίχιση "γράμμα δίσκου -> LibreHardwareMonitor storage hardware" -
        // ταιριάζει με βάση το ήδη γνωστό μοντέλο δίσκου (DriveTypeService.GetDiskModel), ίδιο μοτίβο
        // με το πώς ο ίδιος ο Task Manager/CrystalDiskInfo συσχετίζουν οθόνη <-> φυσικό δίσκο (μέσω
        // ονόματος μοντέλου, όχι γράμματος - τα Windows δεν εκθέτουν άμεσα drive-letter -> sensor
        // αντιστοίχιση σε κανένα επίπεδο). Αν δεν βρεθεί ταίριασμα μοντέλου (π.χ. άγνωστο μοντέλο),
        // επιστρέφει τη θερμοκρασία του ΠΡΩΤΟΥ δίσκου που βρέθηκε - σωστό στη συνηθέστερη περίπτωση
        // (ένας μόνο φυσικός δίσκος στο σύστημα).
        public static int? GetDiskTemperatureCelsius(string? modelHint)
        {
            var computer = GetComputer();
            if (computer == null) return null;
            try
            {
                RefreshAll(computer);
                var storageHw = computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage).ToList();
                if (storageHw.Count == 0) return null;

                var match = !string.IsNullOrWhiteSpace(modelHint)
                    ? storageHw.FirstOrDefault(h => h.Name.Contains(modelHint, StringComparison.OrdinalIgnoreCase))
                    : null;
                match ??= storageHw[0];

                return PickTemperature(match, null);
            }
            catch
            {
                return null;
            }
        }

        private static int? GetTemperature(HardwareType type, string[]? preferredNames)
        {
            var computer = GetComputer();
            if (computer == null) return null;
            try
            {
                RefreshAll(computer);
                var hw = computer.Hardware.FirstOrDefault(h => h.HardwareType == type);
                if (hw == null) return null;
                return PickTemperature(hw, preferredNames);
            }
            catch
            {
                return null;
            }
        }

        // Δοκιμάζει κάθε προτιμώμενο όνομα ΜΕ ΤΗ ΣΕΙΡΑ (πρώτο match κερδίζει) πριν καταλήξει σε
        // "οποιοσδήποτε αισθητήρας θερμοκρασίας βρεθεί πρώτος" - αποφεύγει να πιάσει τυχαία έναν
        // ασήμαντο αισθητήρα (π.χ. VRM/SoC) όταν το προτιμώμενο όνομα δεν ταιριάζει στη
        // συγκεκριμένη ονοματολογία του κατασκευαστή (Intel vs AMD κ.λπ.).
        private static int? PickTemperature(IHardware hw, string[]? preferredNames)
        {
            var sensors = hw.Sensors.Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue).ToList();
            if (sensors.Count == 0) return null;

            if (preferredNames != null)
            {
                foreach (var name in preferredNames)
                {
                    var match = sensors.FirstOrDefault(s => s.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
                    if (match != null) return (int)Math.Round(match.Value!.Value);
                }
            }

            return (int)Math.Round(sensors[0].Value!.Value);
        }
    }
}
