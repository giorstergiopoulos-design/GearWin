using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    // ΝΕΟ v3.3.0 (χρήστης έδειξε screenshots από το "My Device" tab του WMT, ζήτησε: "να
    // συμπεριλάβεις όλες τις πληροφορίες όπως το WMT, όχι τόσο μικρό πλαίσιο") - πλήρες, πολυκάρτο
    // section στην κορυφή της καρτέλας Σύστημα (Motherboard/Storage/OS/Network/CPU/Battery/RAM/GPU),
    // ΚΑΘΑΡΑ πληροφοριακό (χωρίς κουμπιά ενεργειών, ρητά ζητήθηκε) - βλ. SystemView.xaml.
    public record MotherboardInfo(
        string? SystemManufacturer, string? SystemModel, string? BoardManufacturer, string? BoardModel,
        string? Uuid, string? BiosVendor, string? BiosVersion, string? BiosDate, string? SmBiosVersion,
        bool SecureBootEnabled, int MemorySlotsUsed, int MemorySlotsTotal, string? TpmStatus, IReadOnlyList<string> Chipset);

    public record StorageDriveInfo(
        string Letter, string Label, string Model, string Health, string BusType, string FileSystem,
        string PartitionStyle, string Serial, string OperationalStatus, double FreeGb, double TotalGb);

    public record OsInfo(
        string? Caption, string? Version, string? Build, string? Edition, string? Architecture,
        string? ComputerName, string? Workgroup, int UsersTotal, int UsersEnabled, int LocalAdmins,
        DateTime? InstallDate, DateTime? LastBoot, string? LatestHotfix, string? Activation,
        bool SecureBoot, string? BitLocker, string? DefenderStatus, bool PendingReboot, string? Locale);

    public record NetworkAdapterInfo(
        string Name, string Description, bool Connected, long SpeedBps, string? Ipv4, string? Gateway,
        string? Dns, string? Mac, long BytesReceived, long BytesSent);

    public record CpuInfo(
        string? Name, string? Vendor, string? Socket, int Cores, int Threads,
        double BaseClockMhz, double MaxClockMhz, string? L2Cache, string? L3Cache, bool VirtualizationFirmware);

    public record BatteryInfo(bool Present, int? ChargePercent, string? Status, string? PowerPlan);

    public record MemoryModuleInfo(string Location, double CapacityGb, string? Type, double SpeedMhz, string? Manufacturer);
    public record RamInfo(double TotalGb, IReadOnlyList<MemoryModuleInfo> Modules);

    public record GpuFullInfo(string Name, string? Vendor, double? VramGb, string? DriverVersion, string? DriverDate, string? Status, string? Resolution);

    public record DeviceSummary(
        MotherboardInfo Motherboard, IReadOnlyList<StorageDriveInfo> Drives, OsInfo Os,
        IReadOnlyList<NetworkAdapterInfo> NetworkAdapters, CpuInfo Cpu, BatteryInfo Battery,
        RamInfo Ram, IReadOnlyList<GpuFullInfo> Gpus);

    public static class MyDeviceService
    {
        // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "να μην ξανασκαναρονται καθε φορα... για τις πληροφοριες
        // συστηματος") - το SystemView δημιουργείται εξ αρχής σε κάθε επίσκεψη της καρτέλας «Σύστημα»
        // (βλ. MainWindow.ShowTabContent), οπότε χωρίς πλήρη cache ΟΛΗ η σειρά κλήσεων WMI (Μητρική/
        // Δίσκοι/OS/Δίκτυο/CPU/Μπαταρία/RAM/GPU) ξανάτρεχε από την αρχή κάθε φορά - ακόμα και τα
        // «φρέσκα» πεδία (ελεύθερος χώρος δίσκου, σύνδεση δικτύου, φόρτιση μπαταρίας) είναι απλώς
        // ενημερωτικά εδώ (καμία ρύθμιση/ενέργεια σε αυτή την καρτέλα - βλ. Help_Sys_MyDevice), οπότε
        // δεν αξίζει το κόστος μιας πλήρους επανασάρωσης WMI σε κάθε εναλλαγή καρτέλας. Η cache ζει
        // όσο τρέχει η εφαρμογή· επανεκκίνηση την αδειάζει φυσικά.
        private static DeviceSummary? _cachedSummary;

        // ΝΕΟ - ρητό αίτημα χρήστη: "βρες πως η εφαρμογη θα ξέρει αν κάτι άλλαξε" - η cache δεν έχει
        // τρόπο να ανιχνεύσει αλλαγές υλικού που έγιναν ΕΞΩ από την εφαρμογή (π.χ. σύνδεση νέου
        // δίσκου) όσο τρέχει το ίδιο session, οπότε προσφέρεται ρητό χειροκίνητο "Ανανέωση" κουμπί στο
        // SystemView (βλ. BtnRefreshDevice_Click) αντί για σιωπηλά stale δεδομένα χωρίς καμία διέξοδο.
        public static void ClearCache() => _cachedSummary = null;

        public static Task<DeviceSummary> GetSummaryAsync() => Task.Run(() =>
        {
            if (_cachedSummary != null) return _cachedSummary;
            var mb = GetMotherboard();
            var drives = GetDrives();
            var os = GetOs();
            var net = GetNetworkAdapters();
            var cpu = GetCpu();
            var battery = GetBattery();
            var ram = GetRam();
            var gpus = GetGpus();
            _cachedSummary = new DeviceSummary(mb, drives, os, net, cpu, battery, ram, gpus);
            return _cachedSummary;
        });

        private static MotherboardInfo GetMotherboard()
        {
            string? sysM = null, sysMod = null, boardM = null, boardMod = null, uuid = null;
            string? biosVendor = null, biosVer = null, biosDate = null, smbios = null;
            bool secureBoot = false;
            int slotsUsed = 0, slotsTotal = 0;
            string? tpm = null;
            var chipset = new List<string>();

            try
            {
                using var s = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
                var cs = s.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                sysM = cs?["Manufacturer"]?.ToString()?.Trim();
                sysMod = cs?["Model"]?.ToString()?.Trim();
            }
            catch { }
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
                var b = s.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                boardM = b?["Manufacturer"]?.ToString()?.Trim();
                boardMod = b?["Product"]?.ToString()?.Trim();
            }
            catch { }
            try
            {
                using var s = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
                uuid = s.Get().Cast<ManagementBaseObject>().FirstOrDefault()?["UUID"]?.ToString();
            }
            catch { }
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate, SMBIOSMajorVersion, SMBIOSMinorVersion FROM Win32_BIOS");
                var bios = s.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                biosVendor = bios?["Manufacturer"]?.ToString()?.Trim();
                biosVer = bios?["SMBIOSBIOSVersion"]?.ToString()?.Trim();
                if (bios?["ReleaseDate"] is string wmiDate && wmiDate.Length >= 8)
                    biosDate = $"{wmiDate.Substring(0, 4)}-{wmiDate.Substring(4, 2)}-{wmiDate.Substring(6, 2)}";
                var maj = bios?["SMBIOSMajorVersion"]; var min = bios?["SMBIOSMinorVersion"];
                if (maj != null && min != null) smbios = $"{maj}.{min}";
            }
            catch { }
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
                secureBoot = Convert.ToInt32(key?.GetValue("UEFISecureBootEnabled") ?? 0) == 1;
            }
            catch { }
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
                slotsUsed = s.Get().Count;
                using var s2 = new ManagementObjectSearcher("SELECT MemoryDevices FROM Win32_PhysicalMemoryArray");
                var arr = s2.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                slotsTotal = arr?["MemoryDevices"] is { } md ? Convert.ToInt32(md) : slotsUsed;
            }
            catch { }
            try
            {
                using var s = new ManagementObjectSearcher(@"root\CIMV2\Security\MicrosoftTpm", "SELECT IsEnabled_InitialValue, SpecVersion FROM Win32_Tpm");
                var t = s.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                if (t != null)
                {
                    var enabled = t["IsEnabled_InitialValue"] is bool en && en;
                    tpm = enabled ? $"Enabled ({t["SpecVersion"]})" : "Present, Disabled";
                }
                else tpm = "Not Detected";
            }
            catch { tpm = "Not Detected"; }
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE PNPClass='SCSIAdapter' OR (PNPClass='System' AND (Name LIKE '%PCI Express Root%' OR Name LIKE '%Chipset%' OR Name LIKE '%SMBUS%' OR Name LIKE '%GPIO%'))");
                foreach (var d in s.Get().Cast<ManagementBaseObject>())
                {
                    var name = d["Name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name) && chipset.Count < 8) chipset.Add(name);
                }
            }
            catch { }

            return new MotherboardInfo(sysM, sysMod, boardM, boardMod, uuid, biosVendor, biosVer, biosDate, smbios, secureBoot, slotsUsed, slotsTotal, tpm, chipset);
        }

        private static IReadOnlyList<StorageDriveInfo> GetDrives()
        {
            var result = new List<StorageDriveInfo>();
            foreach (var drive in System.IO.DriveInfo.GetDrives().Where(d => d.IsReady && (d.DriveType == System.IO.DriveType.Fixed || d.DriveType == System.IO.DriveType.Removable)))
            {
                try
                {
                    var letter = drive.Name.TrimEnd('\\').TrimEnd(':');
                    var model = DriveTypeService.GetDiskModel(drive.Name) ?? "?";
                    // ΝΕΟ v3.3.0 - το πραγματικό BusType (SATA/USB/NVMe/...) από τη Storage Management
                    // WMI API απευθείας, όχι το χονδρικό 4-τιμών PhysicalDriveKind enum που
                    // χρησιμοποιείται αλλού στην εφαρμογή για το εικονίδιο του πλακιδίου.
                    var busType = DriveTypeService.GetBusTypeName(drive.Name) ?? "?";
                    // ΔΙΟΡΘΩΣΗ (roadmap "Τάση υγείας δίσκου") - το Health ήταν hardcoded "Healthy",
                    // ΠΟΤΕ δεν άλλαζε - τώρα πραγματικό SMART predictive-failure query (root\WMI), με
                    // ρητή "Άγνωστη" τιμή όταν δεν μπορεί να επιβεβαιωθεί (π.χ. συχνά σε NVMe) αντί για
                    // ψευδές "Healthy".
                    var smartHealthy = DriveTypeService.GetSmartHealthy(drive.Name);
                    var health = smartHealthy switch { true => "Healthy", false => "Warning", null => "Unknown" };
                    result.Add(new StorageDriveInfo(
                        letter, string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "(No label)" : drive.VolumeLabel,
                        model, health, busType, drive.DriveFormat, "?", "?", "Online",
                        Math.Round(drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0, 1),
                        Math.Round(drive.TotalSize / 1024.0 / 1024.0 / 1024.0, 1)));
                }
                catch { }
            }
            return result;
        }

        private static OsInfo GetOs()
        {
            string? caption = null, version = null, build = null, arch = null, computerName = null, workgroup = null, hotfix = null, activation = null, locale = null;
            DateTime? installDate = null, lastBoot = null;
            int usersTotal = 0, usersEnabled = 0, admins = 0;
            bool secureBoot = false, pendingReboot = false;
            string? bitlocker = null, defender = null;

            try
            {
                using var s = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber, OSArchitecture, InstallDate, LastBootUpTime FROM Win32_OperatingSystem");
                var os = s.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                caption = os?["Caption"]?.ToString()?.Trim();
                version = os?["Version"]?.ToString();
                build = os?["BuildNumber"]?.ToString();
                arch = os?["OSArchitecture"]?.ToString();
                if (os?["InstallDate"] is string idStr) installDate = ManagementDateTimeConverter.ToDateTime(idStr);
                if (os?["LastBootUpTime"] is string lbStr) lastBoot = ManagementDateTimeConverter.ToDateTime(lbStr);
            }
            catch { }
            try { computerName = Environment.MachineName; } catch { }
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Workgroup, PartOfDomain FROM Win32_ComputerSystem");
                workgroup = s.Get().Cast<ManagementBaseObject>().FirstOrDefault()?["Workgroup"]?.ToString();
            }
            catch { }
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Disabled FROM Win32_UserAccount WHERE LocalAccount=True");
                var users = s.Get().Cast<ManagementBaseObject>().ToList();
                usersTotal = users.Count;
                usersEnabled = users.Count(u => u["Disabled"] is bool dis && !dis);
            }
            catch { }
            try
            {
                // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε: "να δείχνει το My Device όλες τις λεπτομέρειες" - το
                // προηγούμενο query έλεγχε μόνο αν υπάρχει η ομάδα Administrators, όχι πόσα μέλη
                // έχει). Win32_GroupUser είναι το επίσημο associator ανάμεσα σε Win32_Group και τα
                // μέλη του (Win32_UserAccount/Win32_SystemAccount) - PartComponent σε κάθε γραμμή
                // είναι ΕΝΑ μέλος. SID S-1-5-32-544 = ενσωματωμένη τοπική ομάδα Administrators.
                using var s = new ManagementObjectSearcher(
                    "ASSOCIATORS OF {Win32_Group.Domain='" + Environment.MachineName + "',Name='Administrators'} WHERE AssocClass=Win32_GroupUser");
                admins = s.Get().Count;
            }
            catch { admins = -1; }
            try
            {
                using var s = new ManagementObjectSearcher("SELECT HotFixID, InstalledOn FROM Win32_QuickFixEngineering");
                var hf = s.Get().Cast<ManagementBaseObject>().OrderByDescending(h => h["InstalledOn"]?.ToString()).FirstOrDefault();
                hotfix = hf?["HotFixID"]?.ToString();
            }
            catch { }
            try
            {
                using var proc = Process.Start(new ProcessStartInfo("cscript.exe", "//nologo \"" + Environment.SystemDirectory + "\\slmgr.vbs\" /xpr")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
                var output = proc?.StandardOutput.ReadToEnd();
                proc?.WaitForExit(3000);
                var activated = output != null && (output.Contains("permanent", StringComparison.OrdinalIgnoreCase) ||
                                                     output.Contains("licensed", StringComparison.OrdinalIgnoreCase));
                activation = activated ? LanguageService.T("Sys_Activated") : LanguageService.T("Sys_NotAvailable");
            }
            catch { activation = LanguageService.T("Sys_NotAvailable"); }
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
                secureBoot = Convert.ToInt32(key?.GetValue("UEFISecureBootEnabled") ?? 0) == 1;
            }
            catch { }
            try
            {
                using var s = new ManagementObjectSearcher(@"root\CIMV2\Security\MicrosoftVolumeEncryption", "SELECT DriveLetter, ProtectionStatus FROM Win32_EncryptableVolume WHERE DriveLetter='C:'");
                var vol = s.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                var status = vol?["ProtectionStatus"] is { } ps ? Convert.ToInt32(ps) : -1;
                // ΔΙΟΡΘΩΣΗ (γνωστό κενό #03) - το ίδιο το WMI query εδώ ήταν ήδη σωστό (επίσημο,
                // τεκμηριωμένο BitLocker provider - ίδιο μοτίβο elevation-gating με το ήδη επιβεβαιωμένο
                // TPM query), απλώς οι τιμές ήταν hardcoded Αγγλικά αντί για μεταφρασμένες.
                bitlocker = status switch
                {
                    0 => LanguageService.T("Sys_Disabled"),
                    1 => LanguageService.T("Sys_Enabled"),
                    2 => LanguageService.T("Sys_BitLockerLocked"),
                    _ => LanguageService.T("Sys_NotAvailable"),
                };
            }
            catch { bitlocker = LanguageService.T("Sys_NotAvailable"); }
            // ΔΙΟΡΘΩΣΗ (γνωστό κενό #03 του roadmap: "Defender στο My Device δεν επιβεβαιώθηκε") -
            // ΠΡΑΓΜΑΤΙΚΟ bug, όχι θέμα elevation: "root\Microsoft\SecurityClient" ΔΕΝ έχει καθόλου
            // κλάση "AntiVirusProduct" (αυτή ζει στο root\SecurityCenter2 - το γενικό, πολυ-προμηθευτή
            // WMI provider του Windows Security Center) - το query απέτυχε ΠΑΝΤΑ, ανεξάρτητα από
            // δικαιώματα, γι' αυτό έδειχνε πάντα "Unknown". Αντικαταστάθηκε με το ΙΔΙΟ, ήδη ζωντανά
            // επιβεβαιωμένο μηχανισμό του HealthScoreService.CheckDefender (root\Microsoft\Windows\
            // Defender's MSFT_MpComputerStatus για πραγματικό χρόνο, + root\SecurityCenter2 για το
            // όνομα του ενεργού AV αν το Defender είναι ανενεργό λόγω άλλου antivirus - στο μηχάνημα
            // αυτό εντόπισε ήδη σωστά "ESET Security" μέσω ΤΟΥ ΙΔΙΟΥ query στο Health Score της Αρχικής).
            try
            {
                var defenderScope = new ManagementScope(@"root\Microsoft\Windows\Defender");
                defenderScope.Connect();
                using var s = new ManagementObjectSearcher(defenderScope, new ObjectQuery("SELECT RealTimeProtectionEnabled FROM MSFT_MpComputerStatus"));
                var status = s.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                var realTimeOn = status != null && (bool)(status["RealTimeProtectionEnabled"] ?? true);

                if (realTimeOn)
                {
                    defender = LanguageService.T("Sys_Enabled");
                }
                else
                {
                    string? otherAvName = null;
                    try
                    {
                        var scScope = new ManagementScope(@"root\SecurityCenter2");
                        scScope.Connect();
                        using var scSearcher = new ManagementObjectSearcher(scScope, new ObjectQuery("SELECT displayName, productState FROM AntiVirusProduct"));
                        foreach (ManagementBaseObject av in scSearcher.Get())
                        {
                            var name = av["displayName"]?.ToString() ?? "";
                            if (name.Contains("Windows Defender", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("Microsoft Defender", StringComparison.OrdinalIgnoreCase))
                                continue;
                            var productState = Convert.ToInt32(av["productState"]);
                            var middleByte = productState.ToString("X6").Substring(2, 2);
                            if (middleByte is "10" or "11") { otherAvName = name; break; }
                        }
                    }
                    catch { }

                    defender = otherAvName != null
                        ? $"{LanguageService.T("Sys_Disabled")} ({otherAvName})"
                        : LanguageService.T("Sys_Disabled");
                }
            }
            catch { defender = LanguageService.T("Sys_NotAvailable"); }
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
                pendingReboot = key != null;
            }
            catch { }
            try { locale = System.Globalization.CultureInfo.CurrentCulture.DisplayName; } catch { }

            return new OsInfo(caption, version, build, null, arch, computerName, workgroup, usersTotal, usersEnabled, admins,
                installDate, lastBoot, hotfix, activation, secureBoot, bitlocker, defender, pendingReboot, locale);
        }

        private static IReadOnlyList<NetworkAdapterInfo> GetNetworkAdapters()
        {
            var result = new List<NetworkAdapterInfo>();
            try
            {
                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up &&
                        nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Ethernet &&
                        nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211) continue;

                    var props = nic.GetIPProperties();
                    var ipv4 = props.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address.ToString();
                    var gw = props.GatewayAddresses.FirstOrDefault()?.Address.ToString();
                    var dns = props.DnsAddresses.FirstOrDefault()?.ToString();
                    var stats = nic.GetIPv4Statistics();
                    result.Add(new NetworkAdapterInfo(
                        nic.Name, nic.Description, nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up,
                        nic.Speed, ipv4, gw, dns, nic.GetPhysicalAddress().ToString(), stats.BytesReceived, stats.BytesSent));
                }
            }
            catch { }
            return result.OrderByDescending(a => a.Connected).Take(4).ToList();
        }

        private static CpuInfo GetCpu()
        {
            string? name = null, vendor = null, socket = null; int cores = 0, threads = 0;
            double baseClock = 0, maxClock = 0; string? l2 = null, l3 = null; bool virtFw = false;
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Name, Manufacturer, SocketDesignation, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, CurrentClockSpeed, L2CacheSize, L3CacheSize, VirtualizationFirmwareEnabled FROM Win32_Processor");
                var cpu = s.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                name = cpu?["Name"]?.ToString()?.Trim();
                vendor = cpu?["Manufacturer"]?.ToString()?.Trim();
                socket = cpu?["SocketDesignation"]?.ToString()?.Trim();
                cores = cpu?["NumberOfCores"] is { } c ? Convert.ToInt32(c) : 0;
                threads = cpu?["NumberOfLogicalProcessors"] is { } t ? Convert.ToInt32(t) : 0;
                maxClock = cpu?["MaxClockSpeed"] is { } mc ? Convert.ToDouble(mc) : 0;
                baseClock = cpu?["CurrentClockSpeed"] is { } cc ? Convert.ToDouble(cc) : maxClock;
                l2 = cpu?["L2CacheSize"] is { } l2c ? $"{Math.Round(Convert.ToDouble(l2c) / 1024.0, 1)} MB" : null;
                l3 = cpu?["L3CacheSize"] is { } l3c ? $"{Math.Round(Convert.ToDouble(l3c) / 1024.0, 1)} MB" : null;
                virtFw = cpu?["VirtualizationFirmwareEnabled"] is bool vf && vf;
            }
            catch { }
            return new CpuInfo(name, vendor, socket, cores, threads, baseClock, maxClock, l2, l3, virtFw);
        }

        private static BatteryInfo GetBattery()
        {
            try
            {
                using var s = new ManagementObjectSearcher("SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery");
                var b = s.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                if (b == null) return new BatteryInfo(false, null, "No Battery Detected (Desktop System)", GetPowerPlanName());
                var pct = b["EstimatedChargeRemaining"] is { } p ? Convert.ToInt32(p) : (int?)null;
                var statusCode = b["BatteryStatus"] is { } bs ? Convert.ToInt32(bs) : 0;
                var status = statusCode switch { 1 => "Discharging", 2 => "AC/Charging", 3 => "Fully Charged", _ => "Unknown" };
                return new BatteryInfo(true, pct, status, GetPowerPlanName());
            }
            catch { return new BatteryInfo(false, null, "N/A", GetPowerPlanName()); }
        }

        private static string? GetPowerPlanName()
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo("powercfg.exe", "/getactivescheme") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
                var output = proc?.StandardOutput.ReadToEnd();
                proc?.WaitForExit(3000);
                if (output == null) return null;
                var idx = output.IndexOf('(');
                var idx2 = output.IndexOf(')');
                return idx >= 0 && idx2 > idx ? output.Substring(idx + 1, idx2 - idx - 1) : null;
            }
            catch { return null; }
        }

        private static RamInfo GetRam()
        {
            double total = 0;
            var modules = new List<MemoryModuleInfo>();
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Capacity, Speed, SMBIOSMemoryType, Manufacturer, DeviceLocator FROM Win32_PhysicalMemory");
                foreach (var m in s.Get().Cast<ManagementBaseObject>())
                {
                    var capGb = m["Capacity"] is { } cap ? Math.Round(Convert.ToDouble(cap) / 1024.0 / 1024.0 / 1024.0, 1) : 0;
                    total += capGb;
                    var typeCode = m["SMBIOSMemoryType"] is { } tc ? Convert.ToInt32(tc) : 0;
                    var type = typeCode switch { 26 => "DDR4", 34 => "DDR5", 24 => "DDR3", 21 => "DDR2", 20 => "DDR", _ => null };
                    var speed = m["Speed"] is { } sp ? Convert.ToDouble(sp) : 0;
                    var mfr = m["Manufacturer"]?.ToString()?.Trim();
                    var loc = m["DeviceLocator"]?.ToString() ?? "?";
                    modules.Add(new MemoryModuleInfo(loc, capGb, type, speed, mfr));
                }
            }
            catch { }
            return new RamInfo(Math.Round(total, 1), modules);
        }

        private static IReadOnlyList<GpuFullInfo> GetGpus()
        {
            var result = new List<GpuFullInfo>();
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Name, AdapterCompatibility, AdapterRAM, DriverVersion, DriverDate, Status, CurrentHorizontalResolution, CurrentVerticalResolution FROM Win32_VideoController");
                foreach (var g in s.Get().Cast<ManagementBaseObject>())
                {
                    var name = g["Name"]?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var vendor = g["AdapterCompatibility"]?.ToString()?.Trim();
                    // AdapterRAM is a 32-bit field, historically overflows/wraps for VRAM >= 4GB on
                    // many recent GPUs - a well-known WMI limitation, not something to "fix" here.
                    var vramRaw = g["AdapterRAM"] is { } ram ? Convert.ToInt64(Convert.ToUInt32(ram)) : 0;
                    double? vramGb = vramRaw > 0 ? Math.Round(vramRaw / 1024.0 / 1024.0 / 1024.0, 1) : null;
                    var driverVer = g["DriverVersion"]?.ToString();
                    var driverDate = g["DriverDate"] is string dd ? ManagementDateTimeConverter.ToDateTime(dd).ToString("yyyy-MM-dd") : null;
                    var status = g["Status"]?.ToString();
                    var hRes = g["CurrentHorizontalResolution"];
                    var vRes = g["CurrentVerticalResolution"];
                    var resolution = hRes != null && vRes != null ? $"{hRes} x {vRes}" : null;
                    result.Add(new GpuFullInfo(name, vendor, vramGb, driverVer, driverDate, status, resolution));
                }
            }
            catch { }
            return result;
        }
    }
}
