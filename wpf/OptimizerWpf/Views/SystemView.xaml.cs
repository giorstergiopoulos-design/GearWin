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
        private readonly ObservableCollection<ProcessRowVm> _processes = new();

        // ΔΙΟΡΘΩΣΗ (bug εντοπίστηκε - χρήστης ανέφερε: "τα αποτελέσματα των σαρώσεων χάνονται όταν
        // αλλάζω καρτέλα") - σε αντίθεση με τις παραπάνω 4 λίστες, η εύρεση μεγάλων/διπλότυπων αρχείων
        // είναι μια αργή, ρητά ζητημένη από τον χρήστη σάρωση - ΔΕΝ πρέπει να χάνεται σε κάθε αλλαγή
        // καρτέλας (βλ. MainWindow.ShowTabContent - κάθε επιστροφή δημιουργεί ΝΕΟ SystemView). Static
        // αντί για instance - ίδιο μοτίβο με το OptimizationView/BloatwareView.
        private static readonly ObservableCollection<StorageResultRow> _storage = new();
        private static string? s_storageStatusCache;

        public SystemView()
        {
            InitializeComponent();
            ListProcesses.ItemsSource = _processes;
            ListStorageResults.ItemsSource = _storage;

            if (s_storageStatusCache != null) TxtStorageStatus.Text = s_storageStatusCache;
            _ = LoadProcessesAsync();

            // ΝΕΟ v3.2.0 - τα κουμπιά Google Drive/Dropbox εμφανίζονται ΜΟΝΟ αν εντοπιστεί πραγματικά
            // εγκατεστημένος ο αντίστοιχος client (βλ. SystemService.FindGoogleDriveFolder/
            // FindDropboxFolder) - άσκοπο να δείχνει κουμπί για κάτι που ο χρήστης δεν έχει καν.
            if (SystemService.FindGoogleDriveFolder() != null) BtnGoogleDrive.Visibility = Visibility.Visible;
            if (SystemService.FindDropboxFolder() != null) BtnDropbox.Visibility = Visibility.Visible;

            _ = LoadMyDeviceAsync();

            InitFontsSection();
        }

        // REQ-580-02 follow-up: μεταφέρθηκε εδώ από το Views/AppearanceSettingsWindow.xaml(.cs) -
        // ζωντανή προεπισκόπηση γραμματοσειρών (TxtFontPreviewText's Text bindάρεται απευθείας μέσω
        // ElementName σε κάθε γραμμή - καμία χειροκίνητη ανανέωση χρειάζεται σε κάθε πληκτρολόγηση).
        private void InitFontsSection()
        {
            TxtFontPreviewText.Text = LanguageService.T("Appr_FontsDefaultPreviewText");

            var installedFonts = System.Windows.Media.Fonts.SystemFontFamilies
                .Select(f => f.Source)
                .Distinct()
                .OrderBy(name => name, System.StringComparer.OrdinalIgnoreCase)
                .Select(name => new InstalledFontRow(name, new FontFamily(name)))
                .ToList();
            ListInstalledFonts.ItemsSource = installedFonts;

            ListSuggestedFonts.ItemsSource = FontSuggestionService.SuggestedFonts;
        }

        private void BtnPreviewDownloadFont_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: SuggestedFont font })
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(font.Url) { UseShellExecute = true });
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
            if (SystemService.KillProcess(row.Row.Pid, row.Row.Name)) _processes.Remove(row);
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

        // ΝΕΟ - roadmap "κατανάλωση ενέργειας ανά εφαρμογή" - βλ. σχόλιο στο XAML: παραπομπή στην
        // επίσημη οθόνη ρυθμίσεων των Windows αντί για κατασκευασμένα/αναξιόπιστα νούμερα.
        private void BtnOpenBatteryUsage_Click(object sender, RoutedEventArgs e) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:batterysaver-usagedetails") { UseShellExecute = true });
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

    public record InstalledFontRow(string Name, FontFamily FontFamily);
}
