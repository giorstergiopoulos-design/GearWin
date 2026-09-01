using System;
using System.Linq;
using System.Management;

namespace OptimizerWpf.Services
{
    // Θερμοκρασίες υλικού (ρητό αίτημα χρήστη) - ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: τα Windows ΔΕΝ εκθέτουν καμία
    // καθολική, εγγυημένη πηγή θερμοκρασίας χωρίς εξάρτηση σε λογισμικό/SDK συγκεκριμένου
    // κατασκευαστή (LibreHardwareMonitor-style προσεγγίσεις διαβάζουν απευθείας από hardware
    // registers, εκτός εμβέλειας εδώ). Και οι δύο μέθοδοι παρακάτω επιστρέφουν null όταν δεν είναι
    // διαθέσιμες σε αυτό το συγκεκριμένο σύστημα - το UI πρέπει να δείχνει "—" (όχι ψευδή τιμή) σε
    // αυτή την περίπτωση.
    public static class HardwareSensorService
    {
        // Δίσκος: MSFT_PhysicalDisk.GetStorageReliabilityCounter() (root\Microsoft\Windows\Storage) -
        // η ΙΔΙΑ επίσημη μέθοδος πίσω από το PowerShell cmdlet Get-StorageReliabilityCounter,
        // εκθέτει SMART-like μετρήσεις αξιοπιστίας ΣΥΜΠΕΡΙΛΑΜΒΑΝΟΜΕΝΗΣ θερμοκρασίας σε Κελσίου - ΔΕΝ
        // υποστηρίζεται από κάθε δίσκο/controller (π.χ. πολλοί εξωτερικοί/USB δίσκοι δεν την
        // εκθέτουν καθόλου, επιστρέφουν 0).
        public static int? GetDiskTemperatureCelsius(string driveLetter)
        {
            try
            {
                var letter = driveLetter.TrimEnd('\\').TrimEnd(':');
                var scope = new ManagementScope(@"root\Microsoft\Windows\Storage");
                scope.Connect();

                using var partitionSearcher = new ManagementObjectSearcher(scope,
                    new ObjectQuery($"SELECT ObjectId FROM MSFT_Partition WHERE DriveLetter='{letter}'"));
                var partition = partitionSearcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                if (partition == null) return null;

                using var diskSearcher = new ManagementObjectSearcher(scope,
                    new ObjectQuery($"ASSOCIATORS OF {{MSFT_Partition.ObjectId='{Escape(partition["ObjectId"].ToString()!)}'}} WHERE AssocClass=MSFT_DiskToPartition"));
                var disk = diskSearcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                if (disk == null) return null;

                using var physicalDiskSearcher = new ManagementObjectSearcher(scope,
                    new ObjectQuery($"ASSOCIATORS OF {{MSFT_Disk.ObjectId='{Escape(disk["ObjectId"].ToString()!)}'}} WHERE AssocClass=MSFT_PhysicalDiskToDisk"));
                var physicalDisk = physicalDiskSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (physicalDisk == null) return null;

                using var reliability = physicalDisk.InvokeMethod("GetStorageReliabilityCounter", null, null);
                var temp = reliability?["Temperature"];
                if (temp == null) return null;
                var celsius = Convert.ToInt32(temp);
                return celsius > 0 ? celsius : null;
            }
            catch
            {
                return null;
            }
        }

        // CPU: MSAcpi_ThermalZoneTemperature (root\WMI) - πολύ ασυνεπής υποστήριξη σε desktop
        // hardware (πολλά motherboards/BIOS δεν γεμίζουν καθόλου την ACPI thermal zone, ειδικά σε
        // επιτραπέζιους υπολογιστές· πιο αξιόπιστο σε laptops). Τιμή σε δέκατα Kelvin -> Κελσίου.
        public static int? GetCpuTemperatureCelsius()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var tenthsKelvin = Convert.ToDouble(obj["CurrentTemperature"]);
                    var celsius = tenthsKelvin / 10.0 - 273.15;
                    if (celsius is > 0 and < 150) return (int)Math.Round(celsius);
                }
            }
            catch
            {
                // Namespace/class απλά δεν υπάρχει σε πολλά συστήματα - όχι σφάλμα προς αναφορά.
            }
            return null;
        }

        private static string Escape(string objectId) => objectId.Replace(@"\", @"\\").Replace("\"", "\\\"");
    }
}
