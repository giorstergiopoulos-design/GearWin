using System;
using System.IO;
using System.Linq;
using System.Management;

namespace OptimizerWpf.Services
{
    public enum PhysicalDriveKind { Hdd, Ssd, Nvme, Usb, Unknown }

    // Detects whether a drive letter is backed by an HDD, SSD, or removable/USB media - the user
    // asked for the disk tile's icon to change based on this. Removable media (USB flash drives,
    // SD cards) is detected the cheap, reliable way via System.IO.DriveInfo.DriveType. For fixed
    // drives, distinguishing HDD vs SSD needs the modern Storage Management WMI provider
    // (MSFT_PhysicalDisk.MediaType in root\Microsoft\Windows\Storage) - the older Win32_DiskDrive
    // class doesn't reliably expose this. Correlates DriveLetter -> MSFT_Partition -> MSFT_Disk ->
    // MSFT_PhysicalDisk via the storage subsystem's association classes.
    public static class DriveTypeService
    {
        // Fallback όταν το WMI δεν καταφέρνει να προσδιορίσει τον τύπο (ρητό αίτημα χρήστη: "από την
        // ονομασία των δίσκων μπορείς να καταλάβεις τι είδος είναι") - εκτίμηση βάσει γνωστών μοτίβων
        // ονομασίας μοντέλου δίσκου. ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: αυτό είναι ΕΚΤΙΜΗΣΗ, όχι σίγουρη ανίχνευση
        // (π.χ. το "WD Blue" καλύπτει ΚΑΙ HDD ΚΑΙ SSD/NVMe ανάλογα με το μοντέλο - το "SN" πρόθεμα
        // διακρίνει τα NVMe μοντέλα της σειράς) - χρησιμοποιείται ΜΟΝΟ όταν η πραγματική ανίχνευση
        // μέσω WMI αποτύχει εντελώς.
        public static PhysicalDriveKind GuessFromModelName(string? model)
        {
            if (string.IsNullOrWhiteSpace(model)) return PhysicalDriveKind.Unknown;
            var m = model.ToUpperInvariant();

            // Ρητό αίτημα χρήστη ("αντίστοιχα εικονίδια... για nvme") - ξεχωριστό κλάδο από το γενικό
            // SSD, με τα πιο σαφή NVMe-specific μοτίβα ονόματος πρώτα (μεγαλύτερη σιγουριά).
            if (m.Contains("NVME") || System.Text.RegularExpressions.Regex.IsMatch(m, @"\bSN\d{3}\b") ||
                m.Contains("EVO") || m.Contains("970") || m.Contains("980") || m.Contains("990") ||
                m.Contains("CRUCIAL P") || m.Contains("KINGSTON NV") || m.Contains("KINGSTON A2000"))
                return PhysicalDriveKind.Nvme;

            if (m.Contains("SSD") || m.Contains("KINGSTON KC"))
                return PhysicalDriveKind.Ssd;

            if (m.Contains("BARRACUDA") || m.Contains("IRONWOLF") || m.Contains("SKYHAWK") ||
                System.Text.RegularExpressions.Regex.IsMatch(m, @"\bWD\d{2,}E[A-Z]{3}\b") || m.Contains("TOSHIBA DT") || m.Contains("TOSHIBA P3"))
                return PhysicalDriveKind.Hdd;

            return PhysicalDriveKind.Unknown;
        }

        public static PhysicalDriveKind Detect(string driveLetter)
        {
            try
            {
                var root = driveLetter.TrimEnd('\\') + "\\";
                var info = new DriveInfo(root);
                if (info.DriveType == DriveType.Removable) return PhysicalDriveKind.Usb;
                if (info.DriveType != DriveType.Fixed) return PhysicalDriveKind.Unknown;
            }
            catch { return PhysicalDriveKind.Unknown; }

            var mediaAndBus = GetMediaTypeAndBusType(driveLetter);
            if (mediaAndBus == null) return PhysicalDriveKind.Unknown;
            var (mediaType, busType) = mediaAndBus.Value;

            // MediaType: 0=Unspecified, 3=HDD, 4=SSD, 5=SCM (treated as SSD-like).
            if (mediaType == 3) return PhysicalDriveKind.Hdd;
            if (mediaType != 4 && mediaType != 5) return PhysicalDriveKind.Unknown;

            // Ρητό αίτημα χρήστη ("αντίστοιχα εικονίδια... για nvme") - BusType 17 = NVMe.
            return busType == 17 ? PhysicalDriveKind.Nvme : PhysicalDriveKind.Ssd;
        }

        // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε: "βρες μηχανισμό να 'ναι η εφαρμογή σίγουρη" - επιβεβαιώθηκε
        // ΖΩΝΤΑΝΑ, με βήμα-προς-βήμα διαγνωστικό test, ότι η αλυσίδα MSFT_Partition -> MSFT_Disk ->
        // MSFT_PhysicalDisk (μέσω GetRelated, το προηγούμενο fix) φτάνει ΣΩΣΤΑ μέχρι το MSFT_Disk
        // (count=1), αλλά το ΤΕΛΕΥΤΑΙΟ hop (MSFT_PhysicalDiskToDisk) επιστρέφει ΠΑΝΤΑ 0 αποτελέσματα
        // σε αυτό το μηχάνημα - περιορισμός/quirk του συγκεκριμένου Storage WMI provider, όχι κάτι
        // διορθώσιμο με ακόμα-πιο-προσεκτικό escaping. Η αξιόπιστη εναλλακτική (επίσης επιβεβαιωμένη
        // ζωντανά): η ΚΛΑΣΙΚΗ αλυσίδα Win32_LogicalDisk -> Win32_DiskPartition -> Win32_DiskDrive
        // (απλά string DeviceIDs, ΚΑΝΕΝΑ πρόβλημα escaping) δίνει το Index του φυσικού δίσκου, το
        // οποίο ταυτίζεται ΠΑΝΤΑ με το MSFT_PhysicalDisk.DeviceId του ΙΔΙΟΥ δίσκου (επιβεβαιώθηκε:
        // Win32_DiskDrive Index=2 "Samsung SSD 970..." == MSFT_PhysicalDisk DeviceId=2 ίδιο μοντέλο).
        // Ερώτημα ΑΠΕΥΘΕΙΑΣ στο MSFT_PhysicalDisk (χωρίς ASSOCIATORS/GetRelated καθόλου) αντί για την
        // εύθραυστη αλυσίδα συσχέτισης.
        private static (int MediaType, int BusType)? GetMediaTypeAndBusType(string driveLetter)
        {
            try
            {
                var letter = driveLetter.TrimEnd('\\');
                using var partSearcher = new ManagementObjectSearcher(
                    new ObjectQuery($"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{letter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition"));
                var partition = partSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (partition == null) return null;

                using var diskSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                var disk = diskSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (disk?["Index"] == null) return null;
                var diskIndex = disk["Index"].ToString();

                using var physSearcher = new ManagementObjectSearcher(new ManagementScope(@"root\Microsoft\Windows\Storage"),
                    new ObjectQuery($"SELECT MediaType, BusType FROM MSFT_PhysicalDisk WHERE DeviceId='{diskIndex}'"));
                var physicalDisk = physSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (physicalDisk == null) return null;

                return (Convert.ToInt32(physicalDisk["MediaType"]), Convert.ToInt32(physicalDisk["BusType"]));
            }
            catch
            {
                // Storage WMI provider can be unavailable (older Windows, restricted permissions) -
                // degrade to null/Unknown rather than guessing or crashing the tab.
                return null;
            }
        }

        // ΝΕΟ v3.3.0 - human-readable bus type για το "Η Συσκευή Μου" section (MyDeviceService) -
        // ίδιος μηχανισμός με το Detect() παραπάνω, αλλά επιστρέφει το πραγματικό BusType (SATA/SAS/
        // USB/κ.λπ.), όχι απλά το 4-τιμών PhysicalDriveKind enum.
        public static string? GetBusTypeName(string driveLetter)
        {
            var result = GetMediaTypeAndBusType(driveLetter);
            if (result == null) return null;
            // BusType enum (MSFT_PhysicalDisk.BusType, Storage Management API): 1=SCSI, 3=ATA,
            // 7=USB, 8=RAID, 9=iSCSI, 10=SAS, 11=SATA, 17=NVMe.
            return result.Value.BusType switch
            {
                1 => "SCSI", 3 => "ATA", 7 => "USB", 8 => "RAID", 9 => "iSCSI", 10 => "SAS", 11 => "SATA", 17 => "NVMe",
                _ => null,
            };
        }

        // Μάρκα/περιγραφή δίσκου (ρητό αίτημα χρήστη: "δεν αναφέρεις... την περιγραφή του δίσκου")
        // - χρησιμοποιεί το ΚΛΑΣΙΚΟ Win32_DiskDrive.Model (π.χ. "Samsung SSD 970 EVO Plus 1TB") μέσω
        // της παλιάς associator αλυσίδας Win32_LogicalDisk -> Win32_DiskPartition -> Win32_DiskDrive
        // - ΔΕΝ χρησιμοποιεί τη νεότερη root\Microsoft\Windows\Storage (όπως το Detect() παραπάνω)
        // επειδή το Win32_DiskDrive.Model είναι πιο καθολικά διαθέσιμο/σταθερό σε παλαιότερα Windows.
        public static string? GetDiskModel(string driveLetter)
        {
            try
            {
                var deviceId = driveLetter.TrimEnd('\\');
                using var partitionSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{deviceId}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");
                var partition = partitionSearcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                if (partition == null) return null;

                using var diskSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                var disk = diskSearcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                var model = disk?["Model"]?.ToString();
                return string.IsNullOrWhiteSpace(model) ? null : model.Trim();
            }
            catch
            {
                return null;
            }
        }

        // ΝΕΟ - roadmap "Τάση υγείας δίσκου (S.M.A.R.T.)" - επιβεβαιώθηκε κατά την ανάπτυξη ότι το πεδίο
        // Health του StorageDriveInfo ήταν HARDCODED στο "Healthy" (ΠΟΤΕ δεν άλλαζε τιμή, ανεξάρτητα
        // από την πραγματική κατάσταση του δίσκου) - πραγματικό bug, όχι απλά λείπον χαρακτηριστικό.
        // Το MSStorageDriver_FailurePredictStatus (root\WMI) είναι ο καθιερωμένος πάροχος SMART
        // predictive-failure στα Windows - ΙΔΙΑ γνωστή αναξιοπιστία με τους αισθητήρες θερμοκρασίας
        // αλλού σε αυτό το project (ειδικά σε NVMe): αν η κλάση/το instance δεν βρεθεί, επιστρέφει null
        // (η εφαρμογή δείχνει "Άγνωστη" - ΠΟΤΕ ψευδές "Healthy" όταν δεν μπορεί πραγματικά να το
        // επιβεβαιώσει). Η αντιστοίχιση δίσκου->InstanceName γίνεται μέσω του PNPDeviceID (ίδιο μοτίβο
        // αλυσίδας associator με το GetDiskModel παραπάνω, +1 βήμα ως το Win32_PnPEntity).
        public static bool? GetSmartHealthy(string driveLetter)
        {
            try
            {
                var deviceId = driveLetter.TrimEnd('\\');
                using var partitionSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{deviceId}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");
                var partition = partitionSearcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                if (partition == null) return null;

                using var diskSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                var disk = diskSearcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                var pnpDeviceId = disk?["PNPDeviceID"]?.ToString();
                if (string.IsNullOrEmpty(pnpDeviceId)) return GetSmartHealthyViaPhysicalDisk(driveLetter);

                // Win32_DiskDrive.PNPDeviceID όπως "SCSI\DISK&VEN_...\4&...&0" - το InstanceName στο
                // MSStorageDriver_FailurePredictStatus έχει τη μορφή "SCSI\Disk&Ven...\4&...&0_0" (ίδιο
                // πρόθεμα, πεζά/κεφαλαία ανεξάρτητα - StartsWith με OrdinalIgnoreCase).
                var scope = new ManagementScope(@"root\WMI");
                scope.Connect();
                using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM MSStorageDriver_FailurePredictStatus"));
                foreach (ManagementBaseObject item in searcher.Get())
                {
                    var instanceName = item["InstanceName"]?.ToString() ?? "";
                    if (instanceName.StartsWith(pnpDeviceId, StringComparison.OrdinalIgnoreCase))
                        return !(bool)item["PredictFailure"];
                }
                // Κανένα instance δεν ταίριαξε - συνηθισμένο σε NVMe, βλ. fallback παρακάτω.
                return GetSmartHealthyViaPhysicalDisk(driveLetter);
            }
            catch
            {
                return GetSmartHealthyViaPhysicalDisk(driveLetter);
            }
        }

        // ΝΕΟ (roadmap "S.M.A.R.T./NVMe τεχνική αναβάθμιση") - το MSStorageDriver_FailurePredictStatus
        // παραπάνω είναι ο ΠΑΛΙΟΤΕΡΟΣ πάροχος SMART (root\WMI) και συχνά δεν καλύπτει καθόλου NVMe
        // δίσκους σε αυτό το μηχάνημα (καμία αντιστοιχία InstanceName -> επιστρέφει null, δείχνει
        // "Άγνωστη" στο UI). Το MSFT_PhysicalDisk.HealthStatus (root\Microsoft\Windows\Storage) είναι
        // το ΙΔΙΟ storage subsystem WMI provider που χρησιμοποιεί ήδη το GetMediaTypeAndBusType
        // παραπάνω (επιβεβαιωμένο ζωντανά ότι λειτουργεί αξιόπιστα σε αυτό το μηχάνημα μέσω του
        // diskIndex) και καλύπτει SATA+NVMe ενιαία, οπότε χρησιμοποιείται ως fallback όταν ο παλιός
        // πάροχος δεν βρίσκει αντιστοιχία - όχι αντικατάσταση, γιατί ο παλιός πάροχος παραμένει
        // ακριβέστερος όταν διαθέσιμος (πραγματικό predictive-failure flag, όχι συνοπτικό health enum).
        private static bool? GetSmartHealthyViaPhysicalDisk(string driveLetter)
        {
            try
            {
                var letter = driveLetter.TrimEnd('\\');
                using var partSearcher = new ManagementObjectSearcher(
                    new ObjectQuery($"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{letter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition"));
                var partition = partSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (partition == null) return null;

                using var diskSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                var disk = diskSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (disk?["Index"] == null) return null;
                var diskIndex = disk["Index"].ToString();

                using var physSearcher = new ManagementObjectSearcher(new ManagementScope(@"root\Microsoft\Windows\Storage"),
                    new ObjectQuery($"SELECT HealthStatus FROM MSFT_PhysicalDisk WHERE DeviceId='{diskIndex}'"));
                var physicalDisk = physSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (physicalDisk?["HealthStatus"] == null) return null;

                // MSFT_PhysicalDisk.HealthStatus: 0=Healthy, 1=Warning, 2=Unhealthy, 5=Unknown.
                var status = Convert.ToInt32(physicalDisk["HealthStatus"]);
                return status switch { 0 => true, 5 => (bool?)null, _ => false };
            }
            catch
            {
                return null;
            }
        }
    }
}
