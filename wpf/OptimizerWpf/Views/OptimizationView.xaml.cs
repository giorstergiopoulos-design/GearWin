using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class OptimizationView : UserControl
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        private readonly ObservableCollection<WingetUpdateRow> _updates = new();
        private readonly ObservableCollection<UnifiedDriverRow> _driverUpdates = new();
        private readonly ObservableCollection<DriverStoreEntry> _driverStoreEntries = new();

        public OptimizationView()
        {
            InitializeComponent();
            ListWingetUpdates.ItemsSource = _updates;
            ListDriverUpdates.ItemsSource = _driverUpdates;
            ListDriverStore.ItemsSource = _driverStoreEntries;

            ToggleScheduledMaintenance.IsChecked = AppSettingsService.Current.ScheduledMaintenanceEnabled;
            UpdateScheduledMaintenanceStatusText();
            ToggleAutoGamingMode.IsChecked = AppSettingsService.Current.AutoGamingModeEnabled;
        }

        // ΝΕΟ - roadmap "Αυτόματο Gaming Mode".
        private void ToggleAutoGamingMode_Click(object sender, RoutedEventArgs e)
        {
            var enabled = ToggleAutoGamingMode.IsChecked == true;
            if (enabled) AutoGamingModeService.Start(); else AutoGamingModeService.Stop();
            AppSettingsService.Current.AutoGamingModeEnabled = enabled;
            AppSettingsService.Save();
        }

        // ΝΕΟ - roadmap "Προγραμματισμένη συντήρηση".
        private void UpdateScheduledMaintenanceStatusText() =>
            TxtScheduledMaintenanceStatus.Text = AppSettingsService.Current.ScheduledMaintenanceEnabled
                ? LanguageService.T("Opt_ScheduledMaintenanceOnStatus")
                : "";

        private async void ToggleScheduledMaintenance_Click(object sender, RoutedEventArgs e)
        {
            var wantEnabled = ToggleScheduledMaintenance.IsChecked == true;
            ToggleScheduledMaintenance.IsEnabled = false;
            StatusService.SetBusy(LanguageService.T("Ready"));
            var ok = await AdvancedToolsService.SetScheduledMaintenanceAsync(wantEnabled);
            StatusService.SetIdle(LanguageService.T("Ready"));
            ToggleScheduledMaintenance.IsEnabled = true;
            if (ok)
            {
                AppSettingsService.Current.ScheduledMaintenanceEnabled = wantEnabled;
                AppSettingsService.Save();
                UpdateScheduledMaintenanceStatusText();
            }
            else
            {
                ToggleScheduledMaintenance.IsChecked = !wantEnabled;
                ThemedMessageBox.Show(LanguageService.T("Opt_ScheduledMaintenanceFailed"), LanguageService.T("Adv_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Πλέον καλεί το ΠΛΗΡΕΣ Office/Gaming Mode (Set-OfficeMode/Disable-OfficeMode κ.λπ. του
        // Optimizer.ps1) αντί για μόνο powercfg - βλ. PowerModeService.cs. Επανεκκίνηση Explorer
        // (Απενεργοποίηση και των δύο modes) προκαλεί σύντομο "τρεμόπαιγμα" της επιφάνειας εργασίας -
        // αναμενόμενο, ίδιο με το WinForms original.
        private void ToggleOfficeMode_Click(object sender, RoutedEventArgs e)
        {
            var isOn = ToggleOfficeMode.IsChecked == true;
            if (isOn && ToggleGamingMode.IsChecked == true) ToggleGamingMode.IsChecked = false;
            if (isOn)
            {
                StatusService.SetBusy(LanguageService.T("Opt_EnablingOfficeMode"));
                PowerModeService.SetOfficeMode();
                StatusService.SetIdle(LanguageService.T("Opt_OfficeModeEnabled"));
            }
            else
            {
                StatusService.SetBusy(LanguageService.T("Opt_DisablingOfficeMode"));
                PowerModeService.DisableOfficeMode();
                StatusService.SetIdle(LanguageService.T("Opt_OfficeModeDisabled"));
            }
        }

        private void ToggleGamingMode_Click(object sender, RoutedEventArgs e)
        {
            var isOn = ToggleGamingMode.IsChecked == true;
            if (isOn)
            {
                if (ToggleOfficeMode.IsChecked == true) ToggleOfficeMode.IsChecked = false;
                StatusService.SetBusy(LanguageService.T("Opt_EnablingGamingMode"));
                PowerModeService.SetGamingMode();
                StatusService.SetIdle(LanguageService.T("Opt_GamingModeEnabled"));
            }
            else
            {
                StatusService.SetBusy(LanguageService.T("Opt_DisablingGamingMode"));
                PowerModeService.DisableGamingMode();
                StatusService.SetIdle(LanguageService.T("Opt_GamingModeDisabled"));
            }
        }

        private async void BtnScanWinget_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            TxtWingetStatus.Text = LanguageService.T("Opt_ScanInProgress");
            _updates.Clear();

            // ΔΙΟΡΘΩΣΗ - roadmap "Καμία επαναφορά progress bar σε exception": χωρίς try/finally, μια
            // εξαίρεση μέσα στο ScanAsync άφηνε το ProgressWinget μόνιμα ορατό και τα κουμπιά μόνιμα
            // απενεργοποιημένα - το μόνο "δίχτυ ασφαλείας" ήταν το καθολικό DispatcherUnhandledException
            // (App.xaml.cs), που δείχνει message box αλλά ΔΕΝ αγγίζει καθόλου την τοπική κατάσταση UI.
            try
            {
                var results = await WingetService.ScanAsync();
                foreach (var u in results) _updates.Add(new WingetUpdateRow(u));

                TxtWingetStatus.Text = results.Count == 0
                    ? LanguageService.T("Opt_NoUpdatesFound")
                    : $"{LanguageService.T("Opt_UpdatesFoundPrefix")}{results.Count}{LanguageService.T("Opt_UpdatesFoundSuffix")}";
                UpdatesHubService.ReportAppScan(results.Count);
            }
            catch (Exception ex)
            {
                TxtWingetStatus.Text = $"{LanguageService.T("Opt_ScanFailed")}{ex.Message}";
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void BtnCheckMsStore_Click(object sender, RoutedEventArgs e)
        {
            StatusService.SetBusy(LanguageService.T("Opt_TriggeringMsStoreScan"));
            WingetService.TriggerMsStoreUpdateScanAndOpen();
            StatusService.SetIdle(LanguageService.T("Ready"));
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            var selectAll = ChkSelectAll.IsChecked == true;
            foreach (var row in _updates) row.IsSelected = selectAll;
        }

        // ΝΕΟ (χρήστης ανέφερε: "δεν βγαίνει κανένα μήνυμα επιτυχούς εγκατάστασης, ελέγξτο, pop-up ή
        // γραμμή κατάστασης"): τώρα υπάρχουν ΚΑΙ τα δύο - η γραμμή κατάστασης της κάρτας δείχνει
        // αριθμό επιτυχιών/αποτυχιών, ΚΑΙ ένα MessageBox συνοψίζει το αποτέλεσμα στο τέλος (η
        // WingetService.UpgradeAsync ελέγχει πλέον το πραγματικό exit code, βλ. εκεί - πριν πάντα
        // επέστρεφε "επιτυχία" εκτός αν πετάγονταν exception, ΣΧΕΔΟΝ ΠΟΤΕ, άρα μια πραγματική αποτυχία
        // δεν θα αναφερόταν ποτέ).
        private async void BtnUpgradeSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _updates.Where(u => u.IsSelected).ToList();
            if (selected.Count == 0) return;

            SetBusy(true);
            var succeeded = new List<string>();
            var failed = new List<string>();
            foreach (var row in selected)
            {
                TxtWingetStatus.Text = $"{LanguageService.T("Opt_UpgradingPrefix")}{row.Update.Name}... ({succeeded.Count + failed.Count + 1}/{selected.Count})";
                var ok = await WingetService.UpgradeAsync(row.Update.Id, row.Update.Source);
                if (ok) { succeeded.Add(row.Update.Name); _updates.Remove(row); }
                else { failed.Add(row.Update.Name); }
            }
            SetBusy(false);

            var summary = failed.Count == 0
                ? $"{LanguageService.T("Opt_UpgradeAllSucceededPrefix")}{succeeded.Count}{LanguageService.T("Opt_UpgradeAllSucceededSuffix")}"
                : $"{LanguageService.T("Opt_UpgradePartialPrefix")}{succeeded.Count}{LanguageService.T("Opt_UpgradePartialMid")}{failed.Count}{LanguageService.T("Opt_UpgradePartialSuffix")}{string.Join(", ", failed)}).";
            TxtWingetStatus.Text = summary;
            ThemedMessageBox.Show(summary, LanguageService.T("Opt_UpgradeAppsTitle"), MessageBoxButton.OK,
                failed.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void SetBusy(bool busy)
        {
            ProgressWinget.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            BtnScanWinget.SetScanning(busy);
            BtnScanWinget.IsEnabled = !busy;
            BtnUpgradeSelected.IsEnabled = !busy;
            if (busy) StatusService.SetBusy(LanguageService.T("Opt_CheckingWingetUpdates"));
            else StatusService.SetIdle(LanguageService.T("Ready"));
        }

        // ΝΕΟ - roadmap "winget pin" - βλ. σχόλιο στο XAML/WingetService.PinAsync.
        private async void BtnPinWingetApp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: WingetUpdateRow row }) return;
            var ok = await WingetService.PinAsync(row.Update.Id);
            if (ok) row.IsPinned = true;
        }

        // ΝΕΟ - roadmap "winget export/import" - βλ. σχόλιο στο XAML/WingetService.ExportAsync.
        private async void BtnExportWingetList_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "winget-apps.json" };
            if (dlg.ShowDialog() != true) return;

            TxtWingetPortableStatus.Text = LanguageService.T("Opt_WingetExporting");
            StatusService.SetBusy(LanguageService.T("Opt_WingetExporting"));
            var ok = await WingetService.ExportAsync(dlg.FileName);
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtWingetPortableStatus.Text = LanguageService.T(ok ? "Opt_WingetExportDone" : "Opt_WingetExportFailed");
        }

        private async void BtnImportWingetList_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
            if (dlg.ShowDialog() != true) return;
            if (ThemedMessageBox.Show(LanguageService.T("Opt_WingetImportConfirm"), LanguageService.T("Opt_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            TxtWingetPortableStatus.Text = LanguageService.T("Opt_WingetImporting");
            StatusService.SetBusy(LanguageService.T("Opt_WingetImporting"));
            var ok = await WingetService.ImportAsync(dlg.FileName);
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtWingetPortableStatus.Text = LanguageService.T(ok ? "Opt_WingetImportDone" : "Opt_WingetImportFailed");
        }

        // ===== Ενημερώσεις Οδηγών Συσκευών - ΕΝΙΑΙΑ λειτουργία (ρητό αίτημα χρήστη: "οι δύο
        // ενημερώσεις οδηγών να ενσωματωθούν σε μια ενιαία λειτουργία") - μία σάρωση, μία λίστα,
        // ένα κουμπί εγκατάστασης ανά γραμμή που δρομολογείται στη σωστή πηγή (Windows Update /
        // Microsoft Update Catalog) ανάλογα με την προέλευση του κάθε αποτελέσματος. Συνδυάζει το
        // port του cardOpt4 (~11847, Windows Update) με το Catalog integration (HANDOFF.md §0.4ιε).
        // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: καμία αξιόπιστη αποδιπλότυπηση ανάμεσα στις 2 πηγές - βλ. σχόλιο XAML.

        private async void BtnScanDrivers_Click(object sender, RoutedEventArgs e)
        {
            BtnScanDrivers.SetScanning(true);
            BtnScanDrivers.IsEnabled = false;
            _driverUpdates.Clear();
            TxtVendorExtra.Text = "";
            PanelVendorExtraButtons.Children.Clear();

            // Ρητό αίτημα χρήστη ("ενσωμάτωσε όλους τους μηχανισμούς... όσο πιο κοντινή απόδοση στο
            // IObit Driver Booster") - AMD/NVIDIA τρέχουν ΠΡΩΤΑ (ίδια σειρά με το ps1's Complete-
            // DriverScan, "εμφανίζεται πάντα πρώτη") ώστε τα ονόματα GPU τους να είναι διαθέσιμα για
            // trust-overlap πριν προστεθούν τα WU αποτελέσματα.
            TxtDriverStatus.Text = LanguageService.T("Opt_CheckingVendorSources");
            StatusService.SetBusy(LanguageService.T("Opt_ScanningVendorSources"));
            var vendor = await DriverService.ScanVendorSourcesAsync();
            var confirmedGpuNames = new List<string>();

            foreach (var a in vendor.Amd.Where(a => a.IsNewer))
            {
                confirmedGpuNames.Add(a.GpuName);
                _driverUpdates.Add(new UnifiedDriverRow
                {
                    Title = $"{a.GpuName}{LanguageService.T("Opt_PossibleNewerDriverSuffix")}",
                    SourceBadge = LanguageService.T("Opt_AmdOfficialBadge"),
                    SubText = $"{LanguageService.T("Opt_InstalledPrefix")}{a.InstalledVersion ?? LanguageService.T("Opt_Unknown")} ({a.InstalledDate ?? LanguageService.T("Opt_UnknownDate")})  ·  AMD Adrenalin: {a.LatestVersion} ({a.LatestReleaseDate})",
                    ActionLabel = LanguageService.T("Opt_Download"),
                    VendorDownloadUrl = a.DownloadUrl,
                });
            }
            foreach (var n in vendor.Nvidia.Where(n => n.IsNewer))
            {
                confirmedGpuNames.Add(n.GpuName);
                _driverUpdates.Add(new UnifiedDriverRow
                {
                    Title = $"{n.GpuName}{LanguageService.T("Opt_PossibleNewerDriverSuffix")}",
                    SourceBadge = LanguageService.T("Opt_NvidiaOfficialBadge"),
                    SubText = $"{LanguageService.T("Opt_InstalledPrefix")}{n.InstalledVersion ?? LanguageService.T("Opt_Unknown")} ({n.InstalledDate ?? LanguageService.T("Opt_UnknownDate")})  ·  NVIDIA: {n.LatestVersion} ({n.LatestReleaseDate})",
                    ActionLabel = LanguageService.T("Opt_Download"),
                    VendorDownloadUrl = n.DownloadUrl,
                });
            }

            TxtDriverStatus.Text = LanguageService.T("Opt_CheckingWuDrivers");
            StatusService.SetBusy(LanguageService.T("Opt_ScanningWuDrivers"));
            var wuResult = await DriverService.ScanAsync();
            foreach (var u in wuResult.Updates)
            {
                // Trust/stability scoring (port του Test-DriverSourceOverlap) - όταν η AMD/NVIDIA
                // ΕΠΙΒΕΒΑΙΩΣΕ ανεξάρτητα ότι το ΙΔΙΟ φυσικό GPU έχει νεότερο οδηγό, η ετικέτα αναβαθμίζεται.
                var confirmed = DriverService.HasSourceOverlap(u.Model, confirmedGpuNames);
                _driverUpdates.Add(new UnifiedDriverRow
                {
                    Title = u.Title,
                    SourceBadge = confirmed ? LanguageService.T("Opt_ConfirmedBadge") : "Windows Update",
                    SubText = $"{u.Manufacturer} · {u.DriverClass} · {u.Date}",
                    WuUpdate = u,
                });
            }

            TxtDriverStatus.Text = LanguageService.T("Opt_EnumeratingDevices");
            StatusService.SetBusy(LanguageService.T("Opt_EnumeratingDevicesShort"));
            var devices = await DriverService.GetSystemDevicesAsync();
            var progress = new Progress<string>(name => StatusService.SetBusy($"{LanguageService.T("Opt_CheckingCatalogPrefix")}{name}..."));
            var (candidates, looksBlocked) = await DriverService.ScanCatalogAsync(devices, progress);
            foreach (var c in candidates)
                _driverUpdates.Add(new UnifiedDriverRow
                {
                    Title = c.Title,
                    SourceBadge = "Update Catalog",
                    SubText = $"{c.DeviceName} · v{c.Version}",
                    CatalogCandidate = c,
                });

            StatusService.SetIdle(LanguageService.T("Ready"));
            BtnScanDrivers.SetScanning(false);
            BtnScanDrivers.IsEnabled = true;
            BorderDriverResults.Visibility = _driverUpdates.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            var parts = new List<string>();
            parts.Add(wuResult.PolicyBlocked
                ? LanguageService.T("Opt_WuPolicyBlocked")
                : $"Windows Update: {wuResult.Updates.Count}{LanguageService.T("Opt_UpdatesSuffix")}");
            parts.Add(looksBlocked
                ? $"{LanguageService.T("Opt_CatalogNoResponsePrefix")}{devices.Count}{LanguageService.T("Opt_CatalogNoResponseSuffix")}"
                : $"Update Catalog: {candidates.Count}{LanguageService.T("Opt_CatalogUpdatesMid")}{devices.Count}{LanguageService.T("Opt_CatalogUpdatesSuffix")}");
            TxtDriverStatus.Text = _driverUpdates.Count == 0
                ? $"{LanguageService.T("Opt_NoDriverUpdatesFound")} {string.Join(" ", parts)}"
                : $"{LanguageService.T("Opt_DriverUpdatesFoundPrefix")}{_driverUpdates.Count}{LanguageService.T("Opt_DriverUpdatesFoundSuffix")} {string.Join(" ", parts)}";
            UpdatesHubService.ReportDriverScan(_driverUpdates.Count);

            ShowVendorExtras(vendor);
        }

        // Dell Command Update / SDIO / OEM (Lenovo/HP) - port των αντίστοιχων ενοτήτων του
        // Complete-DriverScan. Ξεχωριστές, συνοπτικές πηγές (όχι ανά-συσκευή γραμμές) - ΚΑΝΕΝΑ
        // αυτόματο install, μόνο άνοιγμα του επίσημου εργαλείου/φακέλου αναφοράς.
        private void ShowVendorExtras(VendorScanResult vendor)
        {
            var lines = new List<string>();

            if (vendor.Dell is { Installed: true } dell)
            {
                lines.Add(dell.Updates.Count > 0
                    ? $"{LanguageService.T("Opt_DellUpdatesPrefix")}{dell.Updates.Count}{LanguageService.T("Opt_DellUpdatesSuffix")}"
                    : LanguageService.T("Opt_DellNoUpdates"));
                var btnDell = new Button { Content = LanguageService.T("Opt_OpenDellCommandUpdate"), Style = (Style)FindResource("FlatButtonStyle"), Margin = new Thickness(0, 0, 10, 0) };
                btnDell.Click += (_, __) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("dcu-ui.exe") { UseShellExecute = true }); } catch { } };
                PanelVendorExtraButtons.Children.Add(btnDell);
            }

            if (vendor.Sdio is { } sdio)
            {
                if (sdio.Installed && sdio.HasDriverPacks && sdio.ReportPath != null)
                {
                    lines.Add(LanguageService.T("Opt_SdioPacksFound"));
                    var btnSdio = new Button { Content = LanguageService.T("Opt_OpenSdioReport"), Style = (Style)FindResource("FlatButtonStyle"), Margin = new Thickness(0, 0, 10, 0) };
                    var path = sdio.ReportPath;
                    btnSdio.Click += (_, __) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true }); } catch { } };
                    PanelVendorExtraButtons.Children.Add(btnSdio);
                }
                else if (!sdio.Installed)
                {
                    lines.Add(LanguageService.T("Opt_SdioNotInstalled"));
                }
            }

            if (vendor.Oem is { } oem)
            {
                lines.Add(oem.OemName == "Lenovo"
                    ? (oem.LenovoPackageCount is { } n
                        ? $"{LanguageService.T("Opt_LenovoDetectedPrefix")}{n}{LanguageService.T("Opt_LenovoDetectedSuffix")}"
                        : LanguageService.T("Opt_LenovoDetectedNoCount"))
                    : LanguageService.T("Opt_HpDetected"));
            }

            TxtVendorExtra.Text = string.Join(" ", lines);
        }

        private async void BtnInstallDriver_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: UnifiedDriverRow row } button) return;

            if (row.VendorDownloadUrl != null)
            {
                // AMD/NVIDIA - ίδια φιλοσοφία με το ps1 original: ΚΑΝΕΝΑ αυτόματο install, μόνο άνοιγμα
                // της επίσημης σελίδας λήψης του κατασκευαστή σε browser (ο χρήστης τρέχει τον δικό του
                // επίσημο installer).
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(row.VendorDownloadUrl) { UseShellExecute = true }); } catch { }
                return;
            }

            if (row.WuUpdate != null)
            {
                // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "η ενημέρωση των οδηγών συστήματος να μην γίνεται
                // αυτόματα") - πριν, το κλικ στο "Εγκατάσταση" ξεκινούσε αμέσως τη λήψη/εγκατάσταση
                // χωρίς καμία επιβεβαίωση (μοναδικό σημείο στην εφαρμογή χωρίς confirm dialog πριν από
                // εγκατάσταση οδηγού - ασυνεπές με το Catalog path παρακάτω, το οποίο ήδη ζητούσε
                // επιβεβαίωση). Τώρα ζητά ρητή επιβεβαίωση, ίδιο μοτίβο με το Catalog.
                var wuConfirm = ThemedMessageBox.Show(
                    $"{LanguageService.T("Opt_WuInstallConfirmPrefix")}{row.WuUpdate.Title}{LanguageService.T("Opt_WuInstallConfirmSuffix")}",
                    LanguageService.T("Opt_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (wuConfirm != MessageBoxResult.Yes) return;

                DriverService.InstallInBackground(row.WuUpdate.UpdateId);
                TxtDriverStatus.Text = $"{LanguageService.T("Opt_WuInstallingPrefix")}{row.WuUpdate.Title}";
                return;
            }
            if (row.CatalogCandidate == null) return;
            var candidate = row.CatalogCandidate;

            // Ρητό αίτημα χρήστη ("βρες λύσεις για τα ρίσκα και ενσωμάτωσε") - σημείο επαναφοράς πριν
            // από εγκατάσταση οδηγού από πηγή εκτός Windows Update (η WU έχει ήδη τη δική της
            // εσωτερική προστασία/rollback, το Catalog download όχι).
            // ΔΙΟΡΘΩΣΗ (γνωστό κενό #04 του roadmap: "δεν δημιουργείται αυτόματα σημείο επαναφοράς") -
            // πριν, το σημείο επαναφοράς ήταν ΞΕΧΩΡΙΣΤΗ, προαιρετική ερώτηση (YesNoCancel) - ο χρήστης
            // μπορούσε να απαντήσει "Όχι" και να προχωρήσει την εγκατάσταση χωρίς κανένα δίχτυ
            // ασφαλείας. Τώρα είναι ΑΥΤΟΜΑΤΟ μέρος της ίδιας επιβεβαίωσης εγκατάστασης (απλό Yes/No) -
            // η μόνη επιλογή που μένει είναι "θέλω να εγκαταστήσω αυτόν τον οδηγό;", όχι πια "θέλω ΚΑΙ
            // δίχτυ ασφαλείας;" σαν ξεχωριστό βήμα.
            var confirm = ThemedMessageBox.Show(
                $"{LanguageService.T("Opt_CatalogInstallConfirmPrefix")}{candidate.Title}{LanguageService.T("Opt_CatalogInstallConfirmMid")}{candidate.DeviceName}.\n\n" +
                LanguageService.T("Opt_CatalogInstallConfirmSuffix"),
                LanguageService.T("Opt_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            button.IsEnabled = false;
            StatusService.SetBusy(LanguageService.T("Opt_CreatingRestorePoint"));
            await SystemService.CreateRestorePointAsync();

            StatusService.SetBusy($"{LanguageService.T("Opt_DownloadVerifyInstallPrefix")}{candidate.Title}...");
            var ok = await DriverService.InstallCatalogDriverAsync(candidate);
            StatusService.SetIdle(LanguageService.T("Ready"));

            if (ok)
            {
                _driverUpdates.Remove(row);
                BorderDriverResults.Visibility = _driverUpdates.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                ThemedMessageBox.Show($"{LanguageService.T("Opt_DriverInstalledPrefix")}\n{DriverService.LastInstallLog}", LanguageService.T("Opt_SuccessTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                button.IsEnabled = true;
                ThemedMessageBox.Show(LanguageService.T("Opt_DriverInstallFailedMsg"),
                    LanguageService.T("Opt_FailureTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== Αντίγραφο Ασφαλείας Οδηγών - port του cardOpt5 =====

        private async void BtnDriverBackup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = LanguageService.T("Opt_ChooseBackupFolder") };
            if (dialog.ShowDialog() != true) return;

            var destPath = System.IO.Path.Combine(dialog.FolderName, $"DriverBackup_{DateTime.Now:yyyy-MM-dd}");
            TxtDriverBackupStatus.Text = LanguageService.T("Opt_BackupInProgress");
            StatusService.SetBusy(LanguageService.T("Opt_DriverBackupBusy"));
            BtnDriverBackup.IsEnabled = false;
            BtnDriverRestore.IsEnabled = false;

            var ok = await DriverService.BackupAsync(destPath);
            StatusService.SetIdle(LanguageService.T("Ready"));
            BtnDriverBackup.IsEnabled = true;
            BtnDriverRestore.IsEnabled = true;
            TxtDriverBackupStatus.Text = ok
                ? $"{LanguageService.T("Opt_BackupCompletedPrefix")}{destPath}"
                : LanguageService.T("Opt_BackupFailedOrCancelled");
        }

        private async void BtnDriverRestore_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = LanguageService.T("Opt_ChooseRestoreFolder") };
            if (dialog.ShowDialog() != true) return;

            var confirm = ThemedMessageBox.Show(
                $"{LanguageService.T("Opt_RestoreConfirmPrefix")}{dialog.FolderName}{LanguageService.T("Opt_RestoreConfirmSuffix")}",
                LanguageService.T("Opt_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            TxtDriverBackupStatus.Text = LanguageService.T("Opt_RestoreInProgress");
            StatusService.SetBusy(LanguageService.T("Opt_RestoringDrivers"));
            BtnDriverBackup.IsEnabled = false;
            BtnDriverRestore.IsEnabled = false;

            var ok = await DriverService.RestoreAsync(dialog.FolderName);
            StatusService.SetIdle(LanguageService.T("Ready"));
            BtnDriverBackup.IsEnabled = true;
            BtnDriverRestore.IsEnabled = true;
            TxtDriverBackupStatus.Text = ok ? LanguageService.T("Opt_RestoreCompleted") : LanguageService.T("Opt_RestoreFailed");
        }

        // ===== Καθαρισμός Αποθήκης Οδηγών - port του cardDriverStore =====

        private async void BtnScanDriverStore_Click(object sender, RoutedEventArgs e)
        {
            ProgressDriverStore.Visibility = Visibility.Visible;
            BtnScanDriverStore.IsEnabled = false;
            TxtDriverStoreStatus.Text = LanguageService.T("Opt_DriverStoreScanInProgress");
            StatusService.SetBusy(LanguageService.T("Opt_ScanningDriverStore"));
            _driverStoreEntries.Clear();

            // ΔΙΟΡΘΩΣΗ - roadmap "Καμία επαναφορά progress bar σε exception" - ίδιο μοτίβο διόρθωσης
            // με το BtnScanWinget_Click παραπάνω.
            try
            {
                var all = await DriverService.ScanStoreAsync();

                // Ομαδοποίηση κατά OriginalFileName - κρατάμε πάντα την πιο πρόσφατη (κατά ημερομηνία),
                // προτείνουμε προς διαγραφή ΜΟΝΟ τις παλιότερες, ίδια λογική με το ps1 original.
                var duplicates = all
                    .GroupBy(d => d.OriginalFileName)
                    .Where(g => g.Count() > 1)
                    .SelectMany(g => g.OrderByDescending(d => d.Date).Skip(1))
                    .ToList();

                foreach (var d in duplicates) _driverStoreEntries.Add(d);
                TxtDriverStoreStatus.Text = duplicates.Count == 0
                    ? LanguageService.T("Opt_NoDuplicateDriversFound")
                    : $"{LanguageService.T("Opt_DuplicateDriversFoundPrefix")}{duplicates.Count}{LanguageService.T("Opt_DuplicateDriversFoundSuffix")}";
            }
            catch (Exception ex)
            {
                TxtDriverStoreStatus.Text = $"{LanguageService.T("Opt_ScanFailed")}{ex.Message}";
            }
            finally
            {
                StatusService.SetIdle(LanguageService.T("Ready"));
                ProgressDriverStore.Visibility = Visibility.Collapsed;
                BtnScanDriverStore.IsEnabled = true;
            }
        }

        private async void BtnDeleteStoreDriver_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: DriverStoreEntry entry } button) return;
            button.IsEnabled = false;
            var ok = await DriverService.DeleteStoreDriverAsync(entry.Driver);
            if (ok) _driverStoreEntries.Remove(entry);
            else
            {
                button.IsEnabled = true;
                ThemedMessageBox.Show($"{LanguageService.T("Opt_DriverDeleteFailedPrefix")}{entry.OriginalFileName}{LanguageService.T("Opt_DriverDeleteFailedSuffix")}",
                    LanguageService.T("Opt_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

    }

    // Ενοποιημένη γραμμή προβολής που "τυλίγει" είτε ένα DriverUpdate (Windows Update) είτε ένα
    // CatalogCandidate (Microsoft Update Catalog) - επιτρέπει ΜΙΑ κοινή λίστα/ένα κοινό DataTemplate
    // στο XAML, με το κουμπί εγκατάστασης να δρομολογείται στη σωστή πηγή ανάλογα με ποιο από τα δύο
    // είναι μη-null (βλ. BtnInstallDriver_Click).
    public class UnifiedDriverRow
    {
        public string Title { get; init; } = "";
        public string SubText { get; init; } = "";
        public string SourceBadge { get; init; } = "";
        public string ActionLabel { get; init; } = LanguageService.T("Bloatware_Install");
        public DriverUpdate? WuUpdate { get; init; }
        public CatalogCandidate? CatalogCandidate { get; init; }
        // AMD/NVIDIA - port του Start-DriverScan (βλ. DriverService.ScanVendorSourcesAsync). ΚΑΝΕΝΑ
        // αυτόματο install, μόνο "Λήψη" ανοίγει την επίσημη σελίδα του κατασκευαστή (ίδια φιλοσοφία με
        // το ps1 original - ο χρήστης εγκαθιστά ο ίδιος μέσω του επίσημου installer του κατασκευαστή).
        public string? VendorDownloadUrl { get; init; }
    }

    // Bindable row wrapping a WingetUpdate with a checkbox state (WingetUpdate itself is an
    // immutable record - IsSelected needs to be a real, notifying property for the "Επιλογή Όλων"
    // checkbox to be able to check/uncheck every row programmatically and have the UI follow).
    public class WingetUpdateRow : INotifyPropertyChanged
    {
        public WingetUpdate Update { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }

        // ΝΕΟ - roadmap "winget pin": μονόδρομη ένδειξη (καμία σάρωση τρέχουσας κατάστασης pin -
        // θα χρειαζόταν ξεχωριστό "winget pin list" query ανά γραμμή, περιττή πολυπλοκότητα) - μόλις
        // ο χρήστης πατήσει "Καρφίτσωμα" και το winget pin add πετύχει, το κουμπί δείχνει
        // επιβεβαίωση και απενεργοποιείται, βλ. BtnPinWingetApp_Click.
        private bool _isPinned;
        public bool IsPinned
        {
            get => _isPinned;
            set { _isPinned = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPinned))); }
        }

        public WingetUpdateRow(WingetUpdate update) => Update = update;

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
