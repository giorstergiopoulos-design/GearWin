using System;
using System.Linq;
using LibreHardwareMonitor.Hardware;

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

        public static int? GetCpuTemperatureCelsius() => GetTemperature(HardwareType.Cpu, preferredNameContains: "Package");

        public static int? GetGpuTemperatureCelsius() =>
            GetTemperature(HardwareType.GpuNvidia, null) ??
            GetTemperature(HardwareType.GpuAmd, null) ??
            GetTemperature(HardwareType.GpuIntel, null);

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

                var sensor = match.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Value.HasValue);
                return sensor?.Value.HasValue == true ? (int)Math.Round(sensor.Value!.Value) : null;
            }
            catch
            {
                return null;
            }
        }

        private static int? GetTemperature(HardwareType type, string? preferredNameContains)
        {
            var computer = GetComputer();
            if (computer == null) return null;
            try
            {
                RefreshAll(computer);
                var hw = computer.Hardware.FirstOrDefault(h => h.HardwareType == type);
                if (hw == null) return null;

                var sensors = hw.Sensors.Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue).ToList();
                var preferred = preferredNameContains != null
                    ? sensors.FirstOrDefault(s => s.Name.Contains(preferredNameContains, StringComparison.OrdinalIgnoreCase))
                    : null;
                var sensor = preferred ?? sensors.FirstOrDefault();
                return sensor?.Value.HasValue == true ? (int)Math.Round(sensor.Value!.Value) : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
