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

                using var diskSearcher = new ManagementObjectSearcher(scope,
                    new ObjectQuery($"ASSOCIATORS OF {{MSFT_Partition.ObjectId='{EscapeObjectId(partition["ObjectId"].ToString()!)}'}} WHERE AssocClass=MSFT_DiskToPartition"));
                var disk = diskSearcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                if (disk == null) return PhysicalDriveKind.Unknown;

                using var physicalDiskSearcher = new ManagementObjectSearcher(scope,
                    new ObjectQuery($"ASSOCIATORS OF {{MSFT_Disk.ObjectId='{EscapeObjectId(disk["ObjectId"].ToString()!)}'}} WHERE AssocClass=MSFT_PhysicalDiskToDisk"));
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

        private static string EscapeObjectId(string objectId) => objectId.Replace(@"\", @"\\").Replace("\"", "\\\"");
    }
}
