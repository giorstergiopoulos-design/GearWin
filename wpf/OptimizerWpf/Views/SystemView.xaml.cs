using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class SystemView : UserControl
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        // Αυτές οι τέσσερις φορτώνονται αυτόματα ΞΑΝΑ σε κάθε constructor παρακάτω (LoadStartupAsync
        // κλπ.) - παραμένουν instance (ΟΧΙ static): ο χρήστης θέλει τη ΦΡΕΣΚΙΑ λίστα διεργασιών/
        // υπηρεσιών κάθε φορά που ανοίγει ξανά την καρτέλα Σύστημα, όχι μια παλιά στιγμιότυπη εικόνα.
        private readonly ObservableCollection<StartupRow> _startup = new();
        private readonly ObservableCollection<ProcessRowVm> _processes = new();
        private readonly ObservableCollection<RestorePointInfo> _restorePoints = new();
        private readonly ObservableCollection<ServiceRowVm> _services = new();

        // ΔΙΟΡΘΩΣΗ (bug εντοπίστηκε - χρήστης ανέφερε: "τα αποτελέσματα των σαρώσεων χάνονται όταν
        // αλλάζω καρτέλα") - σε αντίθεση με τις παραπάνω 4 λίστες, η εύρεση μεγάλων/διπλότυπων αρχείων
        // είναι μια αργή, ρητά ζητημένη από τον χρήστη σάρωση - ΔΕΝ πρέπει να χάνεται σε κάθε αλλαγή
        // καρτέλας (βλ. MainWindow.ShowTabContent - κάθε επιστροφή δημιουργεί ΝΕΟ SystemView). Static
        // αντί για instance - ίδιο μοτίβο με το OptimizationView/BloatwareView.
        private static readonly ObservableCollection<StorageResultRow> _storage = new();
        // ===== Εύρεση διπλότυπων αρχείων (ενοποιημένη - μετακινήθηκε εδώ από την καρτέλα Προηγμένα
        // Εργαλεία, ρητό αίτημα χρήστη - ίδια καρτέλα με το υπόλοιπο "Αποθηκευτικός Χώρος") =====
        private static readonly ObservableCollection<DuplicateGroupVm> _duplicateGroups = new();
        private static string? s_duplicateFolder;
        private static string? s_storageStatusCache;
        private static string? s_duplicateStatusCache;

        public SystemView()
        {
            InitializeComponent();
            ListStartup.ItemsSource = _startup;
            ListProcesses.ItemsSource = _processes;
            ListStorageResults.ItemsSource = _storage;
            ListRestorePoints.ItemsSource = _restorePoints;
            ListServices.ItemsSource = _services;
            ListDuplicateGroups.ItemsSource = _duplicateGroups;

            if (s_storageStatusCache != null) TxtStorageStatus.Text = s_storageStatusCache;
            if (s_duplicateFolder != null)
            {
                TxtDuplicateFolder.Text = s_duplicateFolder;
                BtnScanDuplicates.IsEnabled = true;
            }
            if (s_duplicateStatusCache != null) TxtDuplicateStatus.Text = s_duplicateStatusCache;
            BtnDeleteDuplicates.IsEnabled = _duplicateGroups.Count > 0;

            _ = LoadBenchmarkDrivesAsync();

            _ = LoadStartupAsync();
            _ = LoadProcessesAsync();
            _ = LoadRestorePointsAsync();
            _ = LoadServicesAsync();

            // ΝΕΟ v3.2.0 - τα κουμπιά Google Drive/Dropbox εμφανίζονται ΜΟΝΟ αν εντοπιστεί πραγματικά
            // εγκατεστημένος ο αντίστοιχος client (βλ. SystemService.FindGoogleDriveFolder/
            // FindDropboxFolder) - άσκοπο να δείχνει κουμπί για κάτι που ο χρήστης δεν έχει καν.
            if (SystemService.FindGoogleDriveFolder() != null) BtnGoogleDrive.Visibility = Visibility.Visible;
            if (SystemService.FindDropboxFolder() != null) BtnDropbox.Visibility = Visibility.Visible;

            _ = LoadMyDeviceAsync();
            _ = LoadBackupTargetDrivesAsync();
        }

        // ΝΕΟ - roadmap "Backup/imaging" - λίστα υποψήφιων δίσκων-στόχων για το wbadmin system image
        // backup: ΟΛΟΙ οι έτοιμοι δίσκοι (σταθεροί + αφαιρούμενοι/USB, συνηθισμένος στόχος για τέτοιου
        // είδους backup) ΕΚΤΟΣ από τον δίσκο συστήματος - το wbadmin ούτως ή άλλως θα απέτυχε αν ο
        // χρήστης διάλεγε τον δίσκο συστήματος ως στόχο, αλλά προτιμότερο να μη φαίνεται καν επιλογή.
        private async System.Threading.Tasks.Task LoadBackupTargetDrivesAsync()
        {
            var systemDrive = Path.GetPathRoot(System.Environment.SystemDirectory)?.TrimEnd('\\') ?? "C:";
            var names = await System.Threading.Tasks.Task.Run(() =>
                DriveInfo.GetDrives()
                    .Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable))
                    .Select(d => d.Name.TrimEnd('\\'))
                    .Where(n => !string.Equals(n, systemDrive, System.StringComparison.OrdinalIgnoreCase))
                    .ToList());
            foreach (var name in names) ListBackupTargetDrives.Items.Add(name);
            if (ListBackupTargetDrives.Items.Count > 0) ListBackupTargetDrives.SelectedIndex = 0;
        }

        private async void BtnStartBackup_Click(object sender, RoutedEventArgs e)
        {
            if (ListBackupTargetDrives.SelectedItem is not string targetDrive) return;
            TxtBackupStatus.Text = LanguageService.T("Backup_Running");
            StatusService.SetBusy(LanguageService.T("Backup_Running"));
            var (success, _) = await BackupService.StartSystemImageBackupAsync(targetDrive);
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtBackupStatus.Text = LanguageService.T(success ? "Backup_Success" : "Backup_Failed");
        }

        private async void BtnViewBackupVersions_Click(object sender, RoutedEventArgs e)
        {
            var versions = await BackupService.GetBackupVersionsAsync();
            ListBackupVersions.ItemsSource = versions.Count > 0 ? versions : new List<string> { LanguageService.T("Backup_NoVersionsFound") };
        }

        // ΝΕΟ - roadmap "Lazy πρώτη φόρτωση καρτελών" - το DriveInfo.GetDrives() (I/O σε κάθε
        // μονάδα δίσκου) γινόταν σύγχρονα μέσα στον constructor, πριν καν εμφανιστεί το πρώτο frame
        // της καρτέλας - μετακινήθηκε στο ήδη καθιερωμένο Task.Run+async μοτίβο της υπόλοιπης
        // καρτέλας, ώστε η εναλλαγή καρτέλας να μη μπλοκάρει στιγμιαία το UI thread.
        private async System.Threading.Tasks.Task LoadBenchmarkDrivesAsync()
        {
            var names = await System.Threading.Tasks.Task.Run(() =>
                DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).Select(d => d.Name.TrimEnd('\\')).ToList());
            foreach (var name in names) CmbBenchmarkDrive.Items.Add(name);
            if (CmbBenchmarkDrive.Items.Count > 0) CmbBenchmarkDrive.SelectedIndex = 0;
        }

        // ΝΕΟ v3.3.0 - βλ. σχόλιο στο SystemView.xaml/MyDeviceService.cs. Μία φορά στην εκκίνηση -
        // το hardware δεν αλλάζει μέσα στη διάρκεια μιας συνεδρίας, καμία ανάγκη για refresh timer.
        private async System.Threading.Tasks.Task LoadMyDeviceAsync()
        {
            // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "όταν σαρώνει η εφαρμογή... η γραμμή κατάστασης δεν
            // εμφανίζει το κυκλάκι") - αυτή η μέθοδος κάνει πραγματικές, όχι-στιγμιαίες κλήσεις WMI
            // (Motherboard/CPU/OS/Drives/κ.λπ. - βλ. MyDeviceService.GetSummaryAsync) χωρίς να αγγίζει
            // καθόλου το StatusService, ασυνεπές με κάθε άλλη σάρωση της εφαρμογής.
            StatusService.SetBusy(LanguageService.T("Sys_ScanningDevice"));
            var d = await MyDeviceService.GetSummaryAsync();
            var na = LanguageService.T("Sys_NotAvailable");
            string S(string? v) => string.IsNullOrWhiteSpace(v) ? na : v;

            var mb = d.Motherboard;
            ListMotherboard.ItemsSource = new[]
            {
                new InfoRow(LanguageService.T("Sys_MbSystem"), $"{S(mb.SystemManufacturer)} {S(mb.SystemModel)}"),
                new InfoRow(LanguageService.T("Sys_MbBoard"), $"{S(mb.BoardManufacturer)} {S(mb.BoardModel)}"),
                new InfoRow("UUID", S(mb.Uuid)),
                new InfoRow("BIOS", $"{S(mb.BiosVendor)}, {S(mb.BiosVersion)}"),
                new InfoRow(LanguageService.T("Sys_MbBiosDate"), S(mb.BiosDate)),
                new InfoRow("SMBIOS", S(mb.SmBiosVersion)),
                new InfoRow(LanguageService.T("Sys_MbSecureBoot"), mb.SecureBootEnabled ? LanguageService.T("Sys_Enabled") : LanguageService.T("Sys_Disabled")),
                new InfoRow(LanguageService.T("Sys_MbMemSlots"), $"{mb.MemorySlotsUsed}/{mb.MemorySlotsTotal}"),
                new InfoRow("TPM", S(mb.TpmStatus)),
            }.Concat(mb.Chipset.Select(c => new InfoRow("-", c))).ToList();

            // ΔΙΟΡΘΩΣΗ (roadmap "Τάση υγείας δίσκου") - το Health δεν είναι πια πάντα "Healthy" (βλ.
            // DriveTypeService.GetSmartHealthy) - μεταφρασμένο κείμενο + χρώμα ανά πραγματική κατάσταση.
            string HealthText(string h) => h switch
            {
                "Healthy" => LanguageService.T("Sys_DriveHealthy"),
                "Warning" => LanguageService.T("Sys_DriveWarning"),
                _ => LanguageService.T("Sys_DriveHealthUnknown"),
            };
            string HealthColor(string h) => h switch { "Healthy" => "#FF4CAF50", "Warning" => "#FFE53935", _ => "#FF9E9E9E" };

            ListDrivesInfo.ItemsSource = d.Drives.Select(dr => new DriveRow(
                $"{dr.Letter}: {(dr.Label == "(No label)" ? "" : dr.Label)}".Trim(), HealthText(dr.Health), HealthColor(dr.Health),
                $"{S(dr.Model)} | {dr.BusType} | {dr.FileSystem}",
                $"{dr.FreeGb:0.#} GB {LanguageService.T("Sys_DrivesFreeOf")} {dr.TotalGb:0.#} GB",
                dr.TotalGb > 0 ? (dr.TotalGb - dr.FreeGb) / dr.TotalGb * 100 : 0)).ToList();

            // ΝΕΟ - roadmap "Τάση υγείας δίσκου" - στιγμιότυπο (το πολύ 1/ημέρα ανά δίσκο), εμφανίζεται
            // στην καρτέλα Υγεία & Συντήρηση (βλ. HealthView).
            DiskHealthHistoryService.RecordIfNeeded(d.Drives.Select(dr => (dr.Letter, dr.Health, dr.FreeGb, dr.TotalGb)));

            var os = d.Os;
            ListOsInfo.ItemsSource = new[]
            {
                new InfoRow(LanguageService.T("Sys_OsCaption"), $"{S(os.Caption)} ({S(os.Architecture)})"),
                new InfoRow(LanguageService.T("Sys_OsBuild"), $"{S(os.Version)} (Build {S(os.Build)})"),
                new InfoRow(LanguageService.T("Sys_OsComputerName"), S(os.ComputerName)),
                new InfoRow(LanguageService.T("Sys_OsWorkgroup"), S(os.Workgroup)),
                new InfoRow(LanguageService.T("Sys_OsUsers"), $"{os.UsersTotal} ({os.UsersEnabled} {LanguageService.T("Sys_OsEnabled")})"),
                new InfoRow(LanguageService.T("Sys_OsAdmins"), os.LocalAdmins >= 0 ? $"{os.LocalAdmins} {LanguageService.T("Sys_OsMembers")}" : na),
                new InfoRow(LanguageService.T("Sys_OsInstalled"), os.InstallDate?.ToString("yyyy-MM-dd") ?? na),
                new InfoRow(LanguageService.T("Sys_OsLastBoot"), os.LastBoot?.ToString("yyyy-MM-dd HH:mm") ?? na),
                new InfoRow(LanguageService.T("Sys_OsHotfix"), S(os.LatestHotfix)),
                new InfoRow(LanguageService.T("Sys_OsActivation"), S(os.Activation)),
                new InfoRow(LanguageService.T("Sys_OsSecureBoot"), os.SecureBoot ? LanguageService.T("Sys_Enabled") : LanguageService.T("Sys_Disabled")),
                new InfoRow("BitLocker (C:)", S(os.BitLocker)),
                new InfoRow("Defender", S(os.DefenderStatus)),
                new InfoRow(LanguageService.T("Sys_OsPendingReboot"), os.PendingReboot ? LanguageService.T("Sys_Yes") : LanguageService.T("Sys_No")),
                new InfoRow(LanguageService.T("Sys_OsLocale"), S(os.Locale)),
            };

            ListNetInfo.ItemsSource = d.NetworkAdapters.Count == 0
                ? new[] { new InfoRow(LanguageService.T("Sys_NetNone"), "") }
                : d.NetworkAdapters.SelectMany((a, i) => new[]
                {
                    new InfoRow($"#{i + 1}", $"{a.Name} - {(a.Connected ? LanguageService.T("Sys_Connected") : LanguageService.T("Sys_Disconnected"))}"),
                    new InfoRow(LanguageService.T("Sys_NetSpeed"), a.SpeedBps > 0 ? $"{a.SpeedBps / 1_000_000_000.0:0.#} Gbps" : na),
                    new InfoRow("IPv4", S(a.Ipv4)),
                    new InfoRow(LanguageService.T("Sys_NetGateway"), S(a.Gateway)),
                    new InfoRow("DNS", S(a.Dns)),
                    new InfoRow("MAC", S(a.Mac)),
                    new InfoRow(LanguageService.T("Sys_NetTraffic"), $"RX {a.BytesReceived / 1024.0 / 1024.0:0.#} MB / TX {a.BytesSent / 1024.0 / 1024.0:0.#} MB"),
                }).ToList();

            var cpu = d.Cpu;
            ListCpuInfo.ItemsSource = new[]
            {
                new InfoRow(LanguageService.T("Sys_CpuName"), S(cpu.Name)),
                new InfoRow(LanguageService.T("Sys_CpuVendor"), S(cpu.Vendor)),
                new InfoRow(LanguageService.T("Sys_CpuSocket"), S(cpu.Socket)),
                new InfoRow(LanguageService.T("Sys_CpuCores"), $"{cpu.Cores}C / {cpu.Threads}T"),
                new InfoRow(LanguageService.T("Sys_CpuClock"), $"{cpu.BaseClockMhz:0} / {cpu.MaxClockMhz:0} MHz"),
                new InfoRow("L2 / L3", $"{S(cpu.L2Cache)} / {S(cpu.L3Cache)}"),
                new InfoRow(LanguageService.T("Sys_CpuVirt"), cpu.VirtualizationFirmware ? LanguageService.T("Sys_Enabled") : LanguageService.T("Sys_Disabled")),
            };

            var bat = d.Battery;
            ListBatteryInfo.ItemsSource = bat.Present
                ? new[]
                {
                    new InfoRow(LanguageService.T("Sys_BatteryCharge"), $"{bat.ChargePercent}%"),
                    new InfoRow(LanguageService.T("Sys_BatteryStatus"), S(bat.Status)),
                    new InfoRow(LanguageService.T("Sys_BatteryPlan"), S(bat.PowerPlan)),
                }
                : new[]
                {
                    new InfoRow(LanguageService.T("Sys_BatteryStatus"), S(bat.Status)),
                    new InfoRow(LanguageService.T("Sys_BatteryPlan"), S(bat.PowerPlan)),
                };

            var ram = d.Ram;
            ListRamInfo.ItemsSource = new[]
            {
                new InfoRow(LanguageService.T("Sys_RamTotal"), $"{ram.TotalGb} GB"),
                new InfoRow(LanguageService.T("Sys_RamModules"), ram.Modules.Count.ToString()),
            }.Concat(ram.Modules.Select(m => new InfoRow(m.Location, $"{m.CapacityGb} GB, {S(m.Type)}, {m.SpeedMhz:0} MHz{(m.Manufacturer != null ? $", {m.Manufacturer}" : "")}"))).ToList();

            // ΝΕΟ - roadmap "Σήμανση ξεπερασμένων drivers" - απλή ηλικία-βασισμένη σημείωση (>18 μήνες
            // από το DriverDate) απευθείας στην κάρτα υλικού, όχι αυστηρή σύγκριση με "τελευταία
            // διαθέσιμη" έκδοση (αυτό το κάνει ήδη ο πολυ-πηγαίος Driver Updater στη Βελτιστοποίηση).
            ListGpuInfo.ItemsSource = d.Gpus.Count == 0
                ? new[] { new GpuRow(na, "") }
                : d.Gpus.Select(g =>
                {
                    var outdated = System.DateTime.TryParse(g.DriverDate, out var driverDate) &&
                                   (System.DateTime.Now - driverDate).TotalDays > 548;
                    return new GpuRow(g.Name,
                        $"{S(g.Vendor)} | VRAM: {(g.VramGb.HasValue ? $"{g.VramGb} GB" : na)} | {LanguageService.T("Sys_GpuDriver")}: {S(g.DriverVersion)} ({S(g.DriverDate)}) | {S(g.Status)}{(g.Resolution != null ? $" | {g.Resolution}" : "")}",
                        outdated, outdated ? LanguageService.T("Sys_GpuDriverOutdatedHint") : "");
                }).ToList();

            StatusService.SetIdle(LanguageService.T("Ready"));

            // ΝΕΟ - roadmap "Εξαγωγή Η Συσκευή Μου" - το ίδιο κείμενο που δείχνουν οι κάρτες παραπάνω,
            // μαζεμένο σε ένα απλό .txt (βλ. BtnExportDevice_Click) - καμία επανάληψη WMI queries.
            _deviceExportText =
                BuildExportSection(LanguageService.T("Sys_MbTitle"), (IReadOnlyList<InfoRow>)ListMotherboard.ItemsSource) +
                BuildExportSection(LanguageService.T("Sys_OsTitle"), (IReadOnlyList<InfoRow>)ListOsInfo.ItemsSource) +
                BuildExportSection(LanguageService.T("Sys_CpuTitle"), (IReadOnlyList<InfoRow>)ListCpuInfo.ItemsSource) +
                BuildExportSection(LanguageService.T("Sys_RamTitle"), (IReadOnlyList<InfoRow>)ListRamInfo.ItemsSource) +
                BuildExportSection(LanguageService.T("Sys_NetTitle"), (IReadOnlyList<InfoRow>)ListNetInfo.ItemsSource) +
                BuildExportSection(LanguageService.T("Sys_BatteryTitle"), (IReadOnlyList<InfoRow>)ListBatteryInfo.ItemsSource) +
                $"=== {LanguageService.T("Sys_DrivesTitle")} ===\n" +
                string.Join("\n", d.Drives.Select(dr => $"{dr.Letter}: {dr.Label} - {dr.Model} | {dr.BusType} | {dr.FileSystem} | {dr.FreeGb:0.#}/{dr.TotalGb:0.#} GB")) + "\n\n" +
                $"=== {LanguageService.T("Sys_GpuTitle")} ===\n" +
                string.Join("\n", d.Gpus.Select(g => $"{g.Name} - {S(g.Vendor)}, {LanguageService.T("Sys_GpuDriver")} {S(g.DriverVersion)}"));
        }

        private static string BuildExportSection(string title, IReadOnlyList<InfoRow> rows) =>
            $"=== {title} ===\n" + string.Join("\n", rows.Select(r => $"{r.Label}: {r.Value}")) + "\n\n";

        private string _deviceExportText = "";

        // ΝΕΟ - βλ. σχόλιο στο MyDeviceService.ClearCache() και στο κουμπί στο SystemView.xaml -
        // ρητή διέξοδος για πραγματικά φρέσκια σάρωση, αφού η cache δεν ανιχνεύει μόνη της αλλαγές
        // υλικού που έγιναν εκτός εφαρμογής μέσα στο ίδιο session.
        private void BtnRefreshDevice_Click(object sender, RoutedEventArgs e)
        {
            MyDeviceService.ClearCache();
            _ = LoadMyDeviceAsync();
        }

        // ΔΙΟΡΘΩΣΗ - ρητό αίτημα χρήστη: "η εξαγωγή των πληροφοριών συστήματος να γίνεται σε αρχείο
        // PDF" (πριν ήταν απλό .txt) - βλ. PdfExportService (νέο). Ίδιο _deviceExportText περιεχόμενο
        // (ήδη σε "=== Τίτλος ===" format), απλά renders τώρα σε πραγματικό PDF αντί για Notepad.
        private void BtnExportDevice_Click(object sender, RoutedEventArgs e)
        {
            var path = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory), "MyDevice_Export.pdf");
            try
            {
                PdfExportService.ExportTextReport(path, LanguageService.T("Sys_DeviceExportTitle"), _deviceExportText);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (System.Exception ex)
            {
                ThemedMessageBox.Show($"{LanguageService.T("Sys_ExportFailedPrefix")}{ex.Message}", LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private record InfoRow(string Label, string Value);
        private record DriveRow(string Header, string Health, string HealthColor, string Details, string SpaceLine, double UsedPercent);
        private record GpuRow(string Name, string Details, bool IsOutdated = false, string OutdatedHint = "");

        private async System.Threading.Tasks.Task LoadStartupAsync()
        {
            _startup.Clear();
            var items = await SystemService.LoadStartupItemsAsync();
            var delays = await System.Threading.Tasks.Task.Run(() => SystemService.GetStartupDelayEstimates(items));
            foreach (var i in items)
            {
                var row = new StartupRow(i);
                if (delays.TryGetValue(i.Name, out var delay) && delay.HasValue)
                {
                    var seconds = delay.Value.TotalSeconds;
                    row.DelayText = $"+{seconds:0.#}s {LanguageService.T("Sys_StartupDelaySuffix")}";
                    // ΝΕΟ - roadmap "Startup impact, όχι μόνο πλήθος": κατηγοριοποίηση Χαμηλή/Μέτρια/
                    // Υψηλή πάνω στο ΗΔΗ πραγματικό μετρημένο delay (GetStartupDelayEstimates, βλ. πάνω)
                    // - καμία νέα εικασία/heuristic, μόνο ευκολότερη ανάγνωση της ίδιας πραγματικής τιμής.
                    row.ImpactLabel = seconds < 2 ? LanguageService.T("Sys_StartupImpactLow")
                        : seconds < 5 ? LanguageService.T("Sys_StartupImpactMedium")
                        : LanguageService.T("Sys_StartupImpactHigh");
                    row.ImpactColor = new SolidColorBrush(seconds < 2 ? Color.FromRgb(0x4C, 0xAF, 0x50)
                        : seconds < 5 ? Color.FromRgb(0xFF, 0x98, 0x00)
                        : Color.FromRgb(0xE5, 0x39, 0x35));
                }
                _startup.Add(row);
            }
        }

        private async void BtnRefreshStartup_Click(object sender, RoutedEventArgs e) => await LoadStartupAsync();

        private void ToggleStartupItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton { Tag: StartupRow row }) return;
            var ok = SystemService.SetStartupItemEnabled(row.Item, row.IsEnabled);
            // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): καμία ένδειξη επιτυχίας/αποτυχίας - σε αποτυχία
            // ο διακόπτης επαναφέρεται στην πραγματική κατάσταση (ΟΧΙ αυτό που έδειχνε μόλις πατήθηκε).
            if (ok)
            {
                StatusService.SetIdle(row.IsEnabled ? $"{row.Item.Name}{LanguageService.T("Sys_StartupEnabledSuffix")}" : $"{row.Item.Name}{LanguageService.T("Sys_StartupDisabledSuffix")}");
            }
            else
            {
                row.IsEnabled = !row.IsEnabled;
                ThemedMessageBox.Show($"{LanguageService.T("Sys_ChangeForPrefix")}{row.Item.Name}{LanguageService.T("Sys_ChangeFailedSuffix")}", LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ΝΕΟ - roadmap "Ιστορικό χρήσης πόρων" - κάθε ανανέωση καταγράφει ένα δείγμα μνήμης ανά
        // διεργασία (βλ. ProcessHistoryService), ώστε το sparkline να χτίζεται σταδιακά όσο ο χρήστης
        // ξαναεπισκέπτεται/ανανεώνει την καρτέλα, χωρίς φόντο-timer (ίδιο πνεύμα "ελαφρύτερη εφαρμογή").
        private async System.Threading.Tasks.Task LoadProcessesAsync()
        {
            _processes.Clear();
            foreach (var p in await SystemService.LoadProcessesAsync())
            {
                ProcessHistoryService.RecordSample(p.Name, p.WorkingSetMb);
                _processes.Add(new ProcessRowVm(p));
            }
        }

        private async void BtnRefreshProcesses_Click(object sender, RoutedEventArgs e) => await LoadProcessesAsync();

        private async void BtnFreeMemory_Click(object sender, RoutedEventArgs e)
        {
            if (ThemedMessageBox.Show(LanguageService.T("Sys_FreeMemoryConfirm"), LanguageService.T("Sys_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
            StatusService.SetBusy(LanguageService.T("Sys_FreeingMemory"));
            var (count, freedMb) = await SystemService.FreeBackgroundMemoryAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            await LoadProcessesAsync();
            ThemedMessageBox.Show($"{LanguageService.T("Sys_FreeMemoryDonePrefix")}{count}{LanguageService.T("Sys_FreeMemoryDoneMid")}{freedMb} MB{LanguageService.T("Sys_FreeMemoryDoneSuffix")}",
                LanguageService.T("Sys_ConfirmTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnKillProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ProcessRowVm row }) return;
            if (ThemedMessageBox.Show($"{LanguageService.T("Sys_KillConfirmPrefix")}{row.Row.Name}{LanguageService.T("Sys_KillConfirmMid")}{row.Row.Pid}{LanguageService.T("Sys_KillConfirmSuffix")}",
                    LanguageService.T("Sys_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): σιωπηλό no-op σε αποτυχία.
            if (SystemService.KillProcess(row.Row.Pid)) _processes.Remove(row);
            else ThemedMessageBox.Show($"{LanguageService.T("Sys_KillFailedPrefix")}{row.Row.Name}{LanguageService.T("Sys_KillConfirmMid")}{row.Row.Pid}{LanguageService.T("Sys_KillFailedSuffix")}", LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async void BtnFindLarge_Click(object sender, RoutedEventArgs e)
        {
            TxtStorageStatus.Text = LanguageService.T("Sys_SearchingLargeFiles");
            StatusService.SetBusy(LanguageService.T("Sys_SearchingLargeFiles"));
            _storage.Clear();
            var results = await SystemService.FindLargeFilesAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            foreach (var r in results) _storage.Add(new StorageResultRow(r.Path, r.SizeMb, r.LastWriteTime));
            TxtStorageStatus.Text = $"{LanguageService.T("Sys_FoundPrefix")}{results.Count}{LanguageService.T("Sys_LargeFilesSuffix")}";
            s_storageStatusCache = TxtStorageStatus.Text;
        }

        private async void BtnOneDrive_Click(object sender, RoutedEventArgs e)
        {
            if (ThemedMessageBox.Show(LanguageService.T("Sys_OneDriveConfirm"),
                    LanguageService.T("Sys_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
            StatusService.SetBusy(LanguageService.T("Sys_OneDriveFreeing"));
            var ok = await SystemService.OneDriveFreeUpSpaceAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtStorageStatus.Text = ok ? LanguageService.T("Sys_Completed") : LanguageService.T("Sys_OneDriveNotFound");
            s_storageStatusCache = TxtStorageStatus.Text;
        }

        // ΝΕΑ v3.2.0 - βλ. σχόλιο στο SystemService.cs's FindDuplicatesInFolderAsync/
        // FindGoogleDriveFolder/FindDropboxFolder: εύρεση μεγάλων αρχείων ΜΕΣΑ στον τοπικό φάκελο
        // συγχρονισμού, ίδια λίστα αποτελεσμάτων (ListStorageResults) με τα Εύρεση Διπλότυπων/Μεγάλων.
        private async void BtnGoogleDrive_Click(object sender, RoutedEventArgs e)
        {
            var folder = SystemService.FindGoogleDriveFolder();
            if (folder == null) { TxtStorageStatus.Text = LanguageService.T("Sys_CloudNotFound"); return; }
            TxtStorageStatus.Text = LanguageService.T("Sys_SearchingLargeFiles");
            StatusService.SetBusy(LanguageService.T("Sys_SearchingLargeFiles"));
            _storage.Clear();
            var results = await SystemService.FindLargeFilesInFolderAsync(folder);
            StatusService.SetIdle(LanguageService.T("Ready"));
            foreach (var r in results) _storage.Add(new StorageResultRow(r.Path, r.SizeMb, r.LastWriteTime));
            TxtStorageStatus.Text = $"{LanguageService.T("Sys_FoundPrefix")}{results.Count}{LanguageService.T("Sys_LargeFilesSuffix")}";
            s_storageStatusCache = TxtStorageStatus.Text;
        }

        private async void BtnDropbox_Click(object sender, RoutedEventArgs e)
        {
            var folder = SystemService.FindDropboxFolder();
            if (folder == null) { TxtStorageStatus.Text = LanguageService.T("Sys_CloudNotFound"); return; }
            TxtStorageStatus.Text = LanguageService.T("Sys_SearchingLargeFiles");
            StatusService.SetBusy(LanguageService.T("Sys_SearchingLargeFiles"));
            _storage.Clear();
            var results = await SystemService.FindLargeFilesInFolderAsync(folder);
            StatusService.SetIdle(LanguageService.T("Ready"));
            foreach (var r in results) _storage.Add(new StorageResultRow(r.Path, r.SizeMb, r.LastWriteTime));
            TxtStorageStatus.Text = $"{LanguageService.T("Sys_FoundPrefix")}{results.Count}{LanguageService.T("Sys_LargeFilesSuffix")}";
            s_storageStatusCache = TxtStorageStatus.Text;
        }

        private void BtnRecycleFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: StorageResultRow row } button) return;
            if (ThemedMessageBox.Show($"{LanguageService.T("Sys_RecycleConfirmPrefix")}{row.Path};", LanguageService.T("Sys_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (SystemService.SendToRecycleBin(row.Path)) _storage.Remove(row);
        }

        // ΝΕΟ - roadmap "Ασφαλής καταστροφέας αρχείων" - μόνιμη διαγραφή με επικάλυψη αντί για απλή
        // μετακίνηση στον Κάδο Ανακύκλωσης, για ευαίσθητα αρχεία. Ρητή, έντονη προειδοποίηση πριν
        // (μη αναστρέψιμη ενέργεια, ΚΑΙ η επικάλυψη δεν είναι εγγυημένη σε SSD/NVMe).
        private async void BtnShredFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: StorageResultRow row } button) return;
            if (ThemedMessageBox.Show($"{LanguageService.T("Sys_ShredConfirmPrefix")}{row.Path}{LanguageService.T("Sys_ShredConfirmSuffix")}",
                    LanguageService.T("Sys_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            button.IsEnabled = false;
            StatusService.SetBusy($"{LanguageService.T("Sys_Shredding")}{row.Path}...");
            var ok = await FileShredderService.ShredFileAsync(row.Path);
            StatusService.SetIdle(LanguageService.T("Ready"));
            if (ok) _storage.Remove(row);
            else
            {
                button.IsEnabled = true;
                ThemedMessageBox.Show(LanguageService.T("Sys_ShredFailed"), LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== Εύρεση διπλότυπων αρχείων (βλ. σχόλιο στο XAML) =====

        private void BtnChooseDuplicateFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = LanguageService.T("Advanced_ChooseFolder") };
            if (dialog.ShowDialog() != true) return;
            s_duplicateFolder = dialog.FolderName;
            TxtDuplicateFolder.Text = s_duplicateFolder;
            BtnScanDuplicates.IsEnabled = true;
        }

        private async void BtnScanDuplicates_Click(object sender, RoutedEventArgs e)
        {
            if (s_duplicateFolder == null) return;
            BtnScanDuplicates.IsEnabled = false;
            BtnDeleteDuplicates.IsEnabled = false;
            _duplicateGroups.Clear();
            TxtDuplicateStatus.Text = LanguageService.T("Advanced_ScanningDuplicates");
            StatusService.SetBusy(LanguageService.T("Advanced_ScanningDuplicates"));

            var groups = await DuplicateFileService.ScanAsync(s_duplicateFolder);
            StatusService.SetIdle(LanguageService.T("Ready"));
            BtnScanDuplicates.IsEnabled = true;

            foreach (var g in groups) _duplicateGroups.Add(new DuplicateGroupVm(g));
            TxtDuplicateStatus.Text = groups.Count == 0
                ? LanguageService.T("Advanced_NoDuplicatesFound")
                : string.Format(LanguageService.T("Advanced_DuplicatesFoundPrefix"), groups.Count);
            BtnDeleteDuplicates.IsEnabled = groups.Count > 0;
            s_duplicateStatusCache = TxtDuplicateStatus.Text;
        }

        private async void BtnDeleteDuplicates_Click(object sender, RoutedEventArgs e)
        {
            var selected = _duplicateGroups.SelectMany(g => g.Files).Where(f => f.IsSelected).Select(f => f.Path).ToList();
            if (selected.Count == 0)
            {
                ThemedMessageBox.Show(LanguageService.T("Health_NoSelectionMsg"), LanguageService.T("Advanced_DuplicateFinder"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (ThemedMessageBox.Show(string.Format(LanguageService.T("Advanced_DeleteDuplicatesConfirm"), selected.Count),
                    LanguageService.T("Health_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            BtnDeleteDuplicates.IsEnabled = false;
            StatusService.SetBusy(LanguageService.T("Advanced_DeletingDuplicates"));
            var deleted = await DuplicateFileService.DeleteAsync(selected);
            StatusService.SetIdle(LanguageService.T("Ready"));

            var deletedSet = new HashSet<string>(selected);
            foreach (var g in _duplicateGroups.ToList())
            {
                foreach (var f in g.Files.Where(f => deletedSet.Contains(f.Path)).ToList()) g.Files.Remove(f);
                if (g.Files.Count < 2) _duplicateGroups.Remove(g);
            }
            TxtDuplicateStatus.Text = string.Format(LanguageService.T("Advanced_DuplicatesDeletedPrefix"), deleted);
            BtnDeleteDuplicates.IsEnabled = _duplicateGroups.Count > 0;
            s_duplicateStatusCache = TxtDuplicateStatus.Text;
        }

        // ΝΕΟ - roadmap "Κλείδωμα φακέλων με κωδικούς" (ρητό αίτημα χρήστη) - βλ. FolderLockService για
        // τον μηχανισμό (AES-256-GCM, δεν χρησιμοποιεί BitLocker λόγω περιορισμού Windows Home).
        private string? _lockFolderPath;
        private string? _unlockVaultPath;

        private void BtnChooseLockFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = LanguageService.T("Advanced_ChooseFolder") };
            if (dialog.ShowDialog() != true) return;
            _lockFolderPath = dialog.FolderName;
            TxtLockFolderPath.Text = _lockFolderPath;
        }

        private async void BtnLockFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lockFolderPath))
            {
                ThemedMessageBox.Show(LanguageService.T("FolderLock_NoFolderSelected"), LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var pwd = PwdLock.Password;
            if (string.IsNullOrEmpty(pwd) || pwd != PwdLockConfirm.Password)
            {
                ThemedMessageBox.Show(LanguageService.T("FolderLock_PasswordMismatch"), LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ThemedMessageBox.Show($"{LanguageService.T("FolderLock_LockConfirmPrefix")}{_lockFolderPath}{LanguageService.T("FolderLock_LockConfirmSuffix")}",
                    LanguageService.T("Sys_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            BtnLockFolder.IsEnabled = false;
            var progress = new Progress<string>(msg => { TxtFolderLockStatus.Text = msg; StatusService.SetBusy(msg); });
            try
            {
                await FolderLockService.LockFolderAsync(_lockFolderPath, pwd, progress);
                TxtFolderLockStatus.Text = $"{LanguageService.T("FolderLock_LockedDonePrefix")}{_lockFolderPath}{FolderLockService.VaultExtension}";
                TxtLockFolderPath.Text = "";
                PwdLock.Password = "";
                PwdLockConfirm.Password = "";
                _lockFolderPath = null;
            }
            catch (Exception ex)
            {
                TxtFolderLockStatus.Text = $"{LanguageService.T("FolderLock_Failed")}{ex.Message}";
            }
            finally
            {
                StatusService.SetIdle(LanguageService.T("Ready"));
                BtnLockFolder.IsEnabled = true;
            }
        }

        private void BtnChooseVault_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = LanguageService.T("FolderLock_ChooseVault"), Filter = $"Vault (*{FolderLockService.VaultExtension})|*{FolderLockService.VaultExtension}" };
            if (dialog.ShowDialog() != true) return;
            _unlockVaultPath = dialog.FileName;
            TxtUnlockVaultPath.Text = _unlockVaultPath;
        }

        private async void BtnUnlockFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_unlockVaultPath))
            {
                ThemedMessageBox.Show(LanguageService.T("FolderLock_NoVaultSelected"), LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var pwd = PwdUnlock.Password;
            if (string.IsNullOrEmpty(pwd)) return;

            var destination = _unlockVaultPath[..^FolderLockService.VaultExtension.Length];
            BtnUnlockFolder.IsEnabled = false;
            var progress = new Progress<string>(msg => { TxtFolderLockStatus.Text = msg; StatusService.SetBusy(msg); });
            try
            {
                await FolderLockService.UnlockFolderAsync(_unlockVaultPath, pwd, destination, progress);
                TxtFolderLockStatus.Text = $"{LanguageService.T("FolderLock_UnlockedDonePrefix")}{destination}";
                TxtUnlockVaultPath.Text = "";
                PwdUnlock.Password = "";
                _unlockVaultPath = null;
            }
            catch (UnauthorizedAccessException)
            {
                TxtFolderLockStatus.Text = LanguageService.T("FolderLock_WrongPassword");
            }
            catch (Exception ex)
            {
                TxtFolderLockStatus.Text = $"{LanguageService.T("FolderLock_Failed")}{ex.Message}";
            }
            finally
            {
                StatusService.SetIdle(LanguageService.T("Ready"));
                BtnUnlockFolder.IsEnabled = true;
            }
        }

        private async System.Threading.Tasks.Task LoadRestorePointsAsync()
        {
            TxtRestorePointsStatus.Text = LanguageService.T("Sys_LoadingEllipsis");
            _restorePoints.Clear();
            foreach (var p in await SystemService.ListRestorePointsAsync()) _restorePoints.Add(p);
            TxtRestorePointsStatus.Text = $"{_restorePoints.Count}{LanguageService.T("Sys_RestorePointsSuffix")}";
        }

        private async void BtnRefreshRestorePoints_Click(object sender, RoutedEventArgs e) => await LoadRestorePointsAsync();

        private async void BtnCreateRestorePoint_Click(object sender, RoutedEventArgs e)
        {
            StatusService.SetBusy(LanguageService.T("Sys_CreatingRestorePoint"));
            var ok = await SystemService.CreateRestorePointAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtRestorePointsStatus.Text = ok ? LanguageService.T("Sys_RestorePointCreated") : LanguageService.T("Sys_RestorePointFailed");
            if (ok) await LoadRestorePointsAsync();
        }

        private void BtnOpenProtectionSettings_Click(object sender, RoutedEventArgs e) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("SystemPropertiesProtection.exe") { UseShellExecute = true });

        // ΝΕΟ - roadmap "κατανάλωση ενέργειας ανά εφαρμογή" - βλ. σχόλιο στο XAML: παραπομπή στην
        // επίσημη οθόνη ρυθμίσεων των Windows αντί για κατασκευασμένα/αναξιόπιστα νούμερα.
        private void BtnOpenBatteryUsage_Click(object sender, RoutedEventArgs e) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:batterysaver-usagedetails") { UseShellExecute = true });

        // Ρητά ΔΕΝ υλοποιείται delete-by-sequence - βλ. σχόλιο στο SystemService.RestoreToPointAsync.
        private async void BtnRestoreToPoint_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: RestorePointInfo point }) return;
            if (ThemedMessageBox.Show($"{LanguageService.T("Sys_RestoreConfirmPrefix")}{point.Description}{LanguageService.T("Sys_RestoreConfirmSuffix")}",
                    LanguageService.T("Sys_RestoreWarningTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            StatusService.SetBusy(LanguageService.T("Sys_RestoringSystem"));
            var ok = await SystemService.RestoreToPointAsync(point.SequenceNumber);
            // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): το αποτέλεσμα αγνοούνταν εντελώς - σε επιτυχία ο
            // υπολογιστής επανεκκινεί άμεσα (η γραμμή κατάστασης παύει να έχει σημασία), αλλά σε αποτυχία
            // έμενε μόνιμα κολλημένη σε "Επαναφορά συστήματος..." χωρίς καμία εξήγηση.
            if (!ok)
            {
                StatusService.SetIdle(LanguageService.T("Sys_RestoreFailed"));
                ThemedMessageBox.Show(LanguageService.T("Sys_RestoreFailed"), LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadServicesAsync()
        {
            _services.Clear();
            foreach (var s in await SystemService.LoadServicesAsync()) _services.Add(new ServiceRowVm(s));
        }

        private async void BtnRefreshServices_Click(object sender, RoutedEventArgs e) => await LoadServicesAsync();

        private async void ToggleService_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton { Tag: ServiceRowVm row }) return;
            StatusService.SetBusy($"{LanguageService.T("Sys_UpdatingServicePrefix")}{row.Service.DisplayName}...");
            var ok = row.IsAutomatic
                ? await SystemService.RestoreServiceStartModeAsync(row.Service.ServiceName)
                : await SystemService.SetServiceManualAsync(row.Service.ServiceName);
            StatusService.SetIdle(LanguageService.T("Ready"));
            if (!ok) ThemedMessageBox.Show(LanguageService.T("Sys_ServiceChangeFailed"), LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async void BtnStartBenchmark_Click(object sender, RoutedEventArgs e)
        {
            if (CmbBenchmarkDrive.SelectedItem is not string drive) return;
            BtnStartBenchmark.IsEnabled = false;
            TxtBenchWrite.Text = "..."; TxtBenchRead.Text = "...";
            StatusService.SetBusy($"{LanguageService.T("Sys_BenchmarkRunningPrefix")}{drive}...");
            var result = await SystemService.RunBenchmarkAsync(drive);
            StatusService.SetIdle(LanguageService.T("Ready"));
            BtnStartBenchmark.IsEnabled = true;
            if (result.Error != null)
            {
                TxtBenchWrite.Text = "--"; TxtBenchRead.Text = "--";
                ThemedMessageBox.Show(result.Error, LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            TxtBenchWrite.Text = $"{result.WriteMbps} MB/s";
            TxtBenchRead.Text = $"{result.ReadMbps} MB/s";
        }
    }

    public class StartupRow : INotifyPropertyChanged
    {
        public StartupItem Item { get; private set; }
        private bool _isEnabled;
        public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; PropertyChanged?.Invoke(this, new(nameof(IsEnabled))); } }

        // ΝΕΟ - roadmap "Εκτίμηση χρόνου εκκίνησης" - βλ. SystemService.GetStartupDelayEstimates για
        // τη μεθοδολογία/ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ. Κενό όταν η διεργασία δεν βρέθηκε τρέχουσα.
        public string DelayText { get; set; } = "";
        public bool HasDelay => !string.IsNullOrEmpty(DelayText);

        // ΝΕΟ - roadmap "Startup impact, όχι μόνο πλήθος" - βλ. LoadStartupAsync για τα κατώφλια.
        public string ImpactLabel { get; set; } = "";
        public Brush ImpactColor { get; set; } = Brushes.Transparent;

        public StartupRow(StartupItem item) { Item = item; _isEnabled = !item.IsDisabled; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public record ProcessRowVm(ProcessRow Row)
    {
        public string Name => Row.Name;
        public double WorkingSetMb => Row.WorkingSetMb;
        public bool CanKill => !Row.IsCritical;
        // ΝΕΟ - roadmap "Ιστορικό χρήσης πόρων".
        public string MemoryTrend => ProcessHistoryService.GetSparkline(Row.Name);
    }

    // ΝΕΟ - roadmap ""AI"-στυλ έξυπνες προτάσεις καθαρισμού" - ΟΧΙ πραγματικό ML (ίδιο πνεύμα
    // ειλικρίνειας με το Advanced SystemCare's "AI-powered", που στην πράξη είναι επίσης κανόνες) -
    // απλή βαθμολόγηση βάσει ηλικίας τελευταίας τροποποίησης + επέκτασης αρχείου, δείχνεται ως
    // "πιθανότητα ασφαλούς διαγραφής" (Χαμηλή/Μέτρια/Υψηλή), ΠΟΤΕ ως σιγουριά.
    public record StorageResultRow(string Path, double SizeMb, DateTime LastWriteTime)
    {
        private static readonly string[] LikelySafeExtensions = { ".tmp", ".temp", ".bak", ".old", ".log", ".cache", ".dmp", ".chk" };
        private static readonly string[] NeverSuggestExtensions = { ".exe", ".dll", ".msi", ".sys", ".bat", ".ps1", ".cmd" };

        internal int Score()
        {
            var days = (DateTime.Now - LastWriteTime).TotalDays;
            var score = days switch { >= 365 => 55, >= 180 => 40, >= 30 => 20, _ => 5 };
            var ext = System.IO.Path.GetExtension(Path).ToLowerInvariant();
            if (Array.IndexOf(LikelySafeExtensions, ext) >= 0) score += 35;
            else if (Array.IndexOf(NeverSuggestExtensions, ext) >= 0) score -= 40;
            return Math.Clamp(score, 0, 100);
        }

        public string SafetyLabel => Score() switch
        {
            >= 65 => LanguageService.T("Sys_SafetyHigh"),
            >= 35 => LanguageService.T("Sys_SafetyMedium"),
            _ => LanguageService.T("Sys_SafetyLow"),
        };

        public string SafetyColor => Score() switch
        {
            >= 65 => "#FF4CAF50",
            >= 35 => "#FFFF9800",
            _ => "#FF9E9E9E",
        };
    }

    public class ServiceRowVm : INotifyPropertyChanged
    {
        public ServiceRow Service { get; }
        private bool _isAutomatic;
        public bool IsAutomatic { get => _isAutomatic; set { _isAutomatic = value; PropertyChanged?.Invoke(this, new(nameof(IsAutomatic))); } }
        public ServiceRowVm(ServiceRow service) { Service = service; _isAutomatic = service.StartMode is "Auto" or "Automatic"; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class DuplicateFileRow : INotifyPropertyChanged
    {
        public string Path { get; }
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); } }
        public DuplicateFileRow(string path, bool isSelected) { Path = path; _isSelected = isSelected; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class DuplicateGroupVm
    {
        public string Header { get; }
        public ObservableCollection<DuplicateFileRow> Files { get; }

        // ΠΡΟ-επιλεγμένα όλα ΕΚΤΟΣ από το πρώτο - βλ. σχόλιο στο XAML.
        public DuplicateGroupVm(DuplicateGroup group)
        {
            Header = $"{QuickCleanService.FormatSize(group.SizeBytes)} × {group.Paths.Count}";
            Files = new ObservableCollection<DuplicateFileRow>(
                group.Paths.Select((p, i) => new DuplicateFileRow(p, i > 0)));
        }
    }
}
