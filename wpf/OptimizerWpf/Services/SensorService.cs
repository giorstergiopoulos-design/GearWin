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

        // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε: "ψάξε όλο το νετ για λύση για τις θερμοκρασίες" όταν είναι ενεργό
        // το VBS/Memory Integrity) - το LibreHardwareMonitorLib χρειάζεται τον ίδιο μπλοκαρισμένο
        // τύπο πρόσβασης (ring-0 driver) ανεξαρτήτως κατασκευαστή GPU, άρα αποτυγχάνει το ίδιο και για
        // τις 3 κάρτες. Η NVIDIA όμως εκθέτει την ΕΠΙΣΗΜΗ, υπογεγραμμένη βιβλιοθήκη NVML
        // (nvml.dll - εγκαθίσταται ΗΔΗ μαζί με τον driver της, καμία επιπλέον εξάρτηση) που ΔΕΝ
        // χρειάζεται τον μπλοκαρισμένο τρόπο πρόσβασης - λειτουργεί ΚΑΙ με ενεργό VBS/HVCI (ίδια
        // τεχνική με ανεξάρτητα HVCI-safe εργαλεία, π.χ. WRCX/SidebarMonitor). ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ:
        // καμία αντίστοιχη απλή, δωρεάν-για-ενσωμάτωση λύση δεν βρέθηκε για AMD/Intel - το AMD Ryzen
        // Master Monitoring SDK (CPU) και το ADLX (AMD GPU) υπάρχουν αλλά απαιτούν bundling ενός
        // μεγάλου, ξεχωριστού SDK κατασκευαστή + native (C++) bridge - πολύ μεγαλύτερη αλλαγή, εκτός
        // πεδίου εδώ. Δοκιμάζεται ΠΡΩΤΑ το ήδη υπάρχον LibreHardwareMonitorLib (πιάνει AMD/Intel όταν
        // το VBS είναι ανενεργό), μετά το NVML ως fallback ΜΟΝΟ για NVIDIA.
        // ΕΝΗΜΕΡΩΣΗ (χρήστης ζήτησε να συνεχιστεί η έρευνα): βρέθηκε ΚΑΙ για AMD μια αντίστοιχη λύση
        // χωρίς bundling - το atiadlxx.dll (AMD Display Library, το ΠΑΛΙΟ "ADL"/Overdrive API, ΟΧΙ το
        // νεότερο ADLX που πράγματι θα χρειαζόταν C++ bridge) εγκαθίσταται ΗΔΗ με ΚΑΘΕ driver AMD GPU
        // στο System32 - flat, τεκμηριωμένο C API, P/Invoke-able απευθείας όπως το NVML. Το Ryzen
        // Master Monitoring SDK (CPU AMD) ΠΑΡΑΜΕΝΕΙ εκτός πεδίου - αυτό ΔΕΝ εγκαθίσταται αυτόματα με
        // κανέναν οδηγό, χρειάζεται πραγματικά ξεχωριστό bundling/εγκατάσταση.
        public static int? GetGpuTemperatureCelsius() =>
            GetTemperature(HardwareType.GpuNvidia, GpuCoreNames) ??
            GetTemperature(HardwareType.GpuAmd, GpuCoreNames) ??
            GetTemperature(HardwareType.GpuIntel, GpuCoreNames) ??
            GetNvidiaTemperatureViaNvml() ??
            GetAmdTemperatureViaAdl();

        private static int? GetAmdTemperatureViaAdl()
        {
            try
            {
                if (Adl.ADL_Main_Control_Create(AdlAlloc, 1) != 0) return null;
                try
                {
                    if (Adl.ADL_Adapter_NumberOfAdapters_Get(out var count) != 0 || count <= 0) return null;
                    for (var i = 0; i < count; i++)
                    {
                        var temp = new AdlTemperature { iSize = System.Runtime.InteropServices.Marshal.SizeOf<AdlTemperature>() };
                        if (Adl.ADL_Overdrive5_Temperature_Get(i, 0, ref temp) == 0 && temp.iTemperature > 0)
                            return temp.iTemperature / 1000; // millidegrees -> whole °C
                    }
                    return null;
                }
                finally
                {
                    Adl.ADL_Main_Control_Destroy();
                }
            }
            catch (DllNotFoundException)
            {
                return null; // Δεν υπάρχει κάρτα/driver AMD εγκατεστημένο - καμία επίδραση σε NVIDIA/Intel μηχανήματα.
            }
            catch
            {
                return null;
            }
        }

        // ADL_MAIN_MALLOC_CALLBACK - το ADL απαιτεί ένα callback για δέσμευση μνήμης εσωτερικά·
        // χρησιμοποιεί το ίδιο το .NET marshaling (AllocHGlobal) αντί για native malloc.
        private delegate IntPtr AdlMainMallocCallback(int size);
        private static readonly AdlMainMallocCallback AdlAlloc = size => System.Runtime.InteropServices.Marshal.AllocHGlobal(size);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct AdlTemperature { public int iSize; public int iTemperature; }

        // Ελάχιστο P/Invoke wrapper για το atiadlxx.dll (AMD Display Library) - δημόσιο, τεκμηριωμένο
        // API, εγκαθίσταται μαζί με κάθε driver AMD GPU (Catalyst/Adrenalin).
        private static class Adl
        {
            [System.Runtime.InteropServices.DllImport("atiadlxx.dll")]
            public static extern int ADL_Main_Control_Create(AdlMainMallocCallback callback, int enumConnectedAdapters);

            [System.Runtime.InteropServices.DllImport("atiadlxx.dll")]
            public static extern int ADL_Main_Control_Destroy();

            [System.Runtime.InteropServices.DllImport("atiadlxx.dll")]
            public static extern int ADL_Adapter_NumberOfAdapters_Get(out int numAdapters);

            [System.Runtime.InteropServices.DllImport("atiadlxx.dll")]
            public static extern int ADL_Overdrive5_Temperature_Get(int adapterIndex, int thermalControllerIndex, ref AdlTemperature temperature);
        }

        private static int? GetNvidiaTemperatureViaNvml()
        {
            try
            {
                if (Nvml.nvmlInit_v2() != 0) return null;
                try
                {
                    if (Nvml.nvmlDeviceGetHandleByIndex_v2(0, out var device) != 0) return null;
                    if (Nvml.nvmlDeviceGetTemperature(device, 0 /* NVML_TEMPERATURE_GPU */, out var temp) != 0) return null;
                    return (int)temp;
                }
                finally
                {
                    Nvml.nvmlShutdown();
                }
            }
            catch (DllNotFoundException)
            {
                return null; // Δεν υπάρχει κάρτα NVIDIA / driver εγκατεστημένο - καμία επίδραση σε AMD/Intel μηχανήματα.
            }
            catch
            {
                return null;
            }
        }

        // Ελάχιστο P/Invoke wrapper για το nvml.dll (NVIDIA Management Library) - δημόσια,
        // τεκμηριωμένη επίσημη βιβλιοθήκη, εγκαθίσταται μαζί με κάθε πρόσφατο NVIDIA driver.
        private static class Nvml
        {
            [System.Runtime.InteropServices.DllImport("nvml.dll")]
            public static extern int nvmlInit_v2();

            [System.Runtime.InteropServices.DllImport("nvml.dll")]
            public static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

            [System.Runtime.InteropServices.DllImport("nvml.dll")]
            public static extern int nvmlDeviceGetTemperature(IntPtr device, int sensorType, out uint temp);

            [System.Runtime.InteropServices.DllImport("nvml.dll")]
            public static extern int nvmlShutdown();
        }

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
        // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε: "θέλω λύση για τη θερμοκρασία της CPU οπωσδήποτε") - επιβεβαιώθηκε
        // ΖΩΝΤΑΝΑ σε αυτό το μηχάνημα (elevated test, IsHvciActive=True): η CPU temp επέστρεφε 0°C
        // αντί για null/"—". Αιτία: με μπλοκαρισμένη MSR πρόσβαση (VBS/HVCI) το LibreHardwareMonitorLib
        // εξακολουθεί να δημιουργεί sensor objects με "ονόματα-ταίριασμα" (π.χ. "Core Max") αλλά ΧΩΡΙΣ
        // πραγματική τιμή από το hardware - Value.HasValue=true, Value=0 (αντί για null). Το 0°C είναι
        // ΠΟΤΕ ρεαλιστικό για CPU/GPU/δίσκο υπό λειτουργία (ημιαγωγοί σε τάση αυτοθερμαίνονται πάντα
        // αισθητά πάνω από 0°C) - φιλτράρεται ως "καμία πραγματική τιμή" εδώ, αντί να εμφανίζεται ένα
        // παραπλανητικό ψεύτικο 0°C στον χρήστη.
        private static int? PickTemperature(IHardware hw, string[]? preferredNames)
        {
            var sensors = hw.Sensors.Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue && s.Value.Value > 1).ToList();
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
