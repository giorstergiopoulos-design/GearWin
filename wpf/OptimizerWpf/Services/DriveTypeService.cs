using System;
using System.IO;
using System.Linq;
using System.Management;

namespace OptimizerWpf.Services
{
    public enum PhysicalDriveKind { Hdd, Ssd, Usb, Unknown }

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

            if (m.Contains("NVME") || m.Contains("SSD") || System.Text.RegularExpressions.Regex.IsMatch(m, @"\bSN\d{3}\b") ||
                m.Contains("EVO") || m.Contains("970") || m.Contains("980") || m.Contains("990") ||
                m.Contains("CRUCIAL P") || m.Contains("KINGSTON NV") || m.Contains("KINGSTON KC") || m.Contains("KINGSTON A2000"))
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

            try
            {
                var letter = driveLetter.TrimEnd('\\').TrimEnd(':');
                var scope = new ManagementScope(@"root\Microsoft\Windows\Storage");
                scope.Connect();

                using var partitionSearcher = new ManagementObjectSearcher(scope,
                    new ObjectQuery($"SELECT ObjectId FROM MSFT_Partition WHERE DriveLetter='{letter}'"));
                var partition = partitionSearcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                if (partition == null) return PhysicalDriveKind.Unknown;

                // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "όλα τα εικονίδια δίσκου δείχνουν ερωτηματικό") - το
                // MSFT_Partition.ObjectId περιέχει ΗΔΗ ενσωματωμένα διπλά εισαγωγικά μέσα στην ίδια
                // του τη μορφή (π.χ. `WSP_Partition.ObjectId="{guid}:PR:..."`). Το EscapeObjectId
                // κάνει escape ΑΚΡΙΒΩΣ αυτά τα εσωτερικά " σε \" (σωστό για ενσωμάτωση μέσα σε
                // ΔΙΠΛΑ εισαγωγικά) - αλλά το query τα τύλιγε σε ΜΟΝΑ εισαγωγικά, αναντιστοιχία που
                // έκανε το WQL parser να αποτυγχάνει με "Invalid property" σε ΚΑΘΕ δίσκο, όχι
                // περιστασιακά - επιβεβαιώθηκε ζωντανά αναπαράγοντας το ίδιο query.
                using var diskSearcher = new ManagementObjectSearcher(scope,
                    new ObjectQuery($"ASSOCIATORS OF {{MSFT_Partition.ObjectId=\"{EscapeObjectId(partition["ObjectId"].ToString()!)}\"}} WHERE AssocClass=MSFT_DiskToPartition"));
                var disk = diskSearcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                if (disk == null) return PhysicalDriveKind.Unknown;

                using var physicalDiskSearcher = new ManagementObjectSearcher(scope,
                    new ObjectQuery($"ASSOCIATORS OF {{MSFT_Disk.ObjectId=\"{EscapeObjectId(disk["ObjectId"].ToString()!)}\"}} WHERE AssocClass=MSFT_PhysicalDiskToDisk"));
                var physicalDisk = physicalDiskSearcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                if (physicalDisk == null) return PhysicalDriveKind.Unknown;

                // MediaType: 0=Unspecified, 3=HDD, 4=SSD, 5=SCM (treated as SSD-like).
                var mediaType = Convert.ToInt32(physicalDisk["MediaType"]);
                return mediaType switch
                {
                    3 => PhysicalDriveKind.Hdd,
                    4 or 5 => PhysicalDriveKind.Ssd,
                    _ => PhysicalDriveKind.Unknown,
                };
            }
            catch
            {
                // Storage WMI provider can be unavailable (older Windows, restricted permissions) -
                // degrade to Unknown rather than guessing or crashing the tab.
                return PhysicalDriveKind.Unknown;
            }
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

        private static string EscapeObjectId(string objectId) => objectId.Replace(@"\", @"\\").Replace("\"", "\\\"");
    }
}
