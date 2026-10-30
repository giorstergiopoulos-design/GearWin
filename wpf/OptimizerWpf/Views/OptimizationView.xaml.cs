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

        // ΔΙΟΡΘΩΣΗ (bug εντοπίστηκε - χρήστης ανέφερε: "τα αποτελέσματα των σαρώσεων χάνονται όταν
        // αλλάζω καρτέλα") - κάθε αλλαγή καρτέλας δημιουργεί ΝΕΟ OptimizationView (βλ.
        // MainWindow.ShowTabContent, σκόπιμο για να μη μένουν timers ζωντανοί στο παρασκήνιο - βλ.
        // ίδιο σχόλιο στο HomeView) - οι instance ObservableCollection ξεκινούσαν πάντα άδειες, άρα
        // τα αποτελέσματα μιας (συχνά αργής) σάρωσης χάνονταν αμέσως μόλις ο χρήστης άλλαζε καρτέλα
        // και ξαναγύριζε. Οι τρεις λίστες είναι πλέον static - ΤΟ ΙΔΙΟ session-lifetime αντικείμενο
        // επαναχρησιμοποιείται από κάθε νέο instance (ίδιο μοτίβο με το BloatwareView's
        // s_recommendedInstalledCache) - καμία αλλαγή χρειάζεται στον υπόλοιπο κώδικα του αρχείου,
        // αφού το Add/Remove/Clear στην ΙΔΙΑ συλλογή παραμένει ορατό στο επόμενο instance.
        private static readonly ObservableCollection<WingetUpdateRow> _updates = new();
        private readonly ObservableCollection<object> _unified = new();
        private static string _updateFilter = "all";

        /// <summary>Καθαρή λογική (χωρίς UI): η ενιαία λίστα για ένα φίλτρο (all/apps/windows/drivers).</summary>
        internal static List<object> BuildUnified(IEnumerable<WingetUpdateRow> apps, IEnumerable<WingetUpdateRow> windows, IEnumerable<UnifiedDriverRow> drivers, string filter, bool showPreview = true, string? vendor = null)
        {
            var list = new List<object>();
            void Group(string key, System.Collections.IEnumerable rows)
            {
                var items = rows.Cast<object>().ToList();
                if (items.Count == 0) return;
                list.Add(new UpdateGroupHeader(key, items.Count));   // διαχωριστής ανά κατηγορία μέσα στην ίδια λίστα
                list.AddRange(items);
            }
            if (filter is "all" or "apps") Group("Opt_FilterApps", apps);
            if (filter is "all" or "windows") Group("Opt_FilterWindows", windows);
            if (filter is "all" or "drivers") Group("Opt_FilterDrivers", drivers.Where(d => (showPreview || !d.IsPreview) && (vendor == null || string.Equals(d.Vendor, vendor, StringComparison.OrdinalIgnoreCase))));
            return list;
        }

        private void RebuildUnified()
        {
            if (_rebuilding) return;   // η αλλαγή των στοιχείων του ComboBox πυροδοτεί SelectionChanged
            _rebuilding = true;
            try { RebuildUnifiedCore(); } finally { _rebuilding = false; }
        }

        private void RebuildUnifiedCore()
        {
            _unified.Clear();
            var vendors = _driverUpdates.Select(d => d.Vendor).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v).ToList();
            var selected = ComboVendor.SelectedIndex > 0 ? ComboVendor.SelectedItem as string : null;
            ComboVendor.Items.Clear();
            ComboVendor.Items.Add(LanguageService.T("Opt_AllVendors"));
            foreach (var v in vendors) ComboVendor.Items.Add(v!);
            ComboVendor.SelectedIndex = selected != null && vendors.Contains(selected) ? vendors.IndexOf(selected) + 1 : 0;
            var vendorFilter = ComboVendor.SelectedIndex > 0 ? ComboVendor.SelectedItem as string : null;
            foreach (var o in BuildUnified(_updates, _windowsUpdates, _driverUpdates, _updateFilter, ChkShowPreview.IsChecked == true, vendorFilter)) _unified.Add(o);
            ChipAll.Content = $"{LanguageService.T("Opt_FilterAll")} ({_updates.Count + _windowsUpdates.Count + _driverUpdates.Count})";
            ChipApps.Content = $"{LanguageService.T("Opt_FilterApps")} ({_updates.Count})";
            ChipWindows.Content = $"{LanguageService.T("Opt_FilterWindows")} ({_windowsUpdates.Count})";
            ChipDrivers.Content = $"{LanguageService.T("Opt_FilterDrivers")} ({_driverUpdates.Count})";
        }

        private bool _rebuilding;
        private void DriverFilter_Changed(object sender, RoutedEventArgs e) { if (ComboVendor != null && ChkShowPreview != null) RebuildUnified(); }

        private void UpdateFilter_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton { Tag: string tag }) return;
            _updateFilter = tag;
            if (_unified != null && ChipDrivers != null) RebuildUnified();
        }
        // 6.9.1: οι ενημερώσεις Windows (πηγή "windowsupdate") σε δικό τους πλαίσιο
        private static readonly ObservableCollection<WingetUpdateRow> _windowsUpdates = new();
        private static bool IsWindowsSource(WingetUpdate u) => string.Equals(u.Source, "windowsupdate", StringComparison.OrdinalIgnoreCase);
        private static readonly ObservableCollection<UnifiedDriverRow> _driverUpdates = new();
        private static readonly ObservableCollection<DriverStoreEntry> _driverStoreEntries = new();

        // Μικρά πεδία κατάστασης (κείμενο γραμμής κατάστασης, ορατότητα, κάρτες κατασκευαστή) που ΔΕΝ
        // ζουν μέσα σε κάποια από τις παραπάνω συλλογές, αλλά χάνονταν με τον ίδιο ακριβώς τρόπο - ίδιο
        // static-cache μοτίβο, αποκαθίστανται ρητά στον constructor.
        private static VendorScanResult? s_vendorCache;
        private static string? s_driverStoreStatusCache;

        // 6.5.3: ΜΙΑ σάρωση τη φορά και κατάσταση που επιβιώνει της αλλαγής καρτέλας (βλ. ScanState). Πριν, ένα νέο View έδειχνε το κουμπί
        // "ελεύθερο" ενώ η σάρωση του παλιού έτρεχε ακόμα: ο χρήστης "ξεκινούσε από την αρχή" και τα αποτελέσματα διπλασιάζονταν.
        internal static readonly ScanState AppScan = new();
        internal static readonly ScanState DriverScan = new();
        private bool _driverWasBusy;

        // Δημόσια πρόσβαση μόνο-για-ανάγνωση στο ήδη υπάρχον cache (όχι νέο σκανάρισμα) - το
        // GamesMediaView το διαβάζει για το "ο οδηγός της κάρτας γραφικών είναι παλιός" hint, ΧΩΡΙΣ
        // να πυροδοτεί τη δική του (αργή, δικτυακή) σάρωση οδηγών κατασκευαστή σε άλλη καρτέλα.
        public static VendorScanResult? CachedVendorScan => s_vendorCache;

        public OptimizationView()
        {
            InitializeComponent();
            // 6.9.1: ενιαία λίστα (εφαρμογές + Windows + οδηγοί) με φίλτρα· ανακατασκευάζεται όταν αλλάζει οποιαδήποτε από τις τρεις πηγές
            ListWingetUpdates.ItemsSource = _unified;
            _updates.CollectionChanged += (_, _) => RebuildUnified();
            _windowsUpdates.CollectionChanged += (_, _) => RebuildUnified();
            _driverUpdates.CollectionChanged += (_, _) => RebuildUnified();
            RebuildUnified();
            ListDriverUpdates.ItemsSource = _driverUpdates;
            ListDriverStore.ItemsSource = _driverStoreEntries;

            BorderDriverResults.Visibility = Visibility.Collapsed;   // 6.9.1: οι οδηγοί εμφανίζονται στην ενιαία λίστα ενημερώσεων
            if (s_vendorCache != null) ShowVendorExtras(s_vendorCache);
            _driverWasBusy = DriverScan.Busy;
            ApplyAppScanUi();
            ApplyDriverScanUi();
            Loaded += (_, _) => { AppScan.Changed += OnAppScanChanged; DriverScan.Changed += OnDriverScanChanged; ApplyAppScanUi(); ApplyDriverScanUi(); };
            Unloaded += (_, _) => { AppScan.Changed -= OnAppScanChanged; DriverScan.Changed -= OnDriverScanChanged; };
            if (s_driverStoreStatusCache != null) TxtDriverStoreStatus.Text = s_driverStoreStatusCache;

            ToggleScheduledMaintenance.IsChecked = AppSettingsService.Current.ScheduledMaintenanceEnabled;
            UpdateScheduledMaintenanceStatusText();
            ToggleAutoGamingMode.IsChecked = AppSettingsService.Current.AutoGamingModeEnabled;
            ToggleNightlyUpdates.IsChecked = AppSettingsService.Current.NightlyUpdatesEnabled;
            ToggleNightlyWindowsUpdates.IsChecked = AppSettingsService.Current.NightlyWindowsUpdates;
            // ΔΙΟΡΘΩΣΗ (έλεγχος ορθής λειτουργίας): οι διακόπτες Office/Gaming Mode δεν αρχικοποιούνταν ΠΟΤΕ - κάθε εναλλαγή
            // καρτέλας ξαναχτίζει το view, άρα ένα ενεργό Gaming Mode εμφανιζόταν ξανά ως "κλειστό" ενώ τα Windows έμεναν σε αυτό.
            ToggleOfficeMode.IsChecked = AppSettingsService.Current.OfficeModeActive;
            ToggleGamingMode.IsChecked = AppSettingsService.Current.GamingModeActive;
            ChkGamingFreeRam.IsChecked = AppSettingsService.Current.GamingModeFreeRam;

            // ΝΕΟ - ROADMAP.md REQ-570-03 - βλ. σχόλιο στο TweakService.IsUltimatePerformanceActive.
            // Off the UI thread (spawns powercfg.exe) - ίδιο μοτίβο με τις υπόλοιπες αργές
            // αρχικοποιήσεις κατάστασης σε αυτή την εφαρμογή.
            _ = InitUltimatePerformanceToggleAsync();
        }

        private async Task InitUltimatePerformanceToggleAsync()
        {
            var isOn = await Task.Run(TweakService.IsUltimatePerformanceActive);
            ToggleUltimatePerformance.IsChecked = isOn;
        }

        // ΝΕΟ - ROADMAP.md REQ-570-03 - ίδιες μέθοδοι με το πρώην tweak (TweakService.
        // UltimatePerformanceOn/Off), απλώς καλούνται πλέον απευθείας από εδώ αντί μέσω SimpleTweak.
        private async void ToggleUltimatePerformance_Click(object sender, RoutedEventArgs e)
        {
            var isOn = ToggleUltimatePerformance.IsChecked == true;
            ToggleUltimatePerformance.IsEnabled = false;
            try
            {
                // Εκτός UI thread: καλεί πολλές φορές το powercfg.exe σύγχρονα (πάγωνε το παράθυρο).
                await Task.Run(() => { if (isOn) TweakService.UltimatePerformanceOn(); else TweakService.UltimatePerformanceOff(); });
                // Ο διακόπτης αντικατοπτρίζει την ΠΡΑΓΜΑΤΙΚΗ κατάσταση (το template μπορεί να λείπει σε κάποια builds).
                ToggleUltimatePerformance.IsChecked = await Task.Run(TweakService.IsUltimatePerformanceActive);
            }
            finally { ToggleUltimatePerformance.IsEnabled = true; }
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
        // Εκτός UI thread: τα Set/Disable Office/Gaming Mode τρέχουν πολλές εξωτερικές εντολές (powercfg, sc,
        // επανεκκίνηση Explorer, αναμονές) σύγχρονα — πάγωναν το παράθυρο για αρκετά δευτερόλεπτα στο κλικ.
        private async void ToggleOfficeMode_Click(object sender, RoutedEventArgs e)
        {
            var isOn = ToggleOfficeMode.IsChecked == true;
            var gamingWasOn = isOn && ToggleGamingMode.IsChecked == true;
            if (gamingWasOn) ToggleGamingMode.IsChecked = false;
            ToggleOfficeMode.IsEnabled = false;
            try
            {
                if (isOn)
                {
                    StatusService.SetBusy(LanguageService.T("Opt_EnablingOfficeMode"));
                    // ΔΙΟΡΘΩΣΗ: πριν, το Gaming Mode εμφανιζόταν "κλειστό" αλλά τα Windows έμεναν στις ρυθμίσεις του - τώρα απενεργοποιείται πραγματικά.
                    if (gamingWasOn) await Task.Run(() => PowerModeService.DisableGamingMode(restartExplorer: false));
                    await Task.Run(PowerModeService.SetOfficeMode);
                    AppSettingsService.Current.OfficeModeActive = true;
                    AppSettingsService.Current.GamingModeActive = false;
                    AppSettingsService.Save();
                    TxtGamingModeStatus.Text = "";
                    StatusService.SetIdle(LanguageService.T("Opt_OfficeModeEnabled"));
                }
                else
                {
                    StatusService.SetBusy(LanguageService.T("Opt_DisablingOfficeMode"));
                    await Task.Run(() => PowerModeService.DisableOfficeMode());
                    AppSettingsService.Current.OfficeModeActive = false;
                    AppSettingsService.Save();
                    StatusService.SetIdle(LanguageService.T("Opt_OfficeModeDisabled"));
                }
            }
            catch { StatusService.SetIdle(LanguageService.T("Ready")); }
            finally { ToggleOfficeMode.IsEnabled = true; }
        }

        private async void ToggleGamingMode_Click(object sender, RoutedEventArgs e)
        {
            var isOn = ToggleGamingMode.IsChecked == true;
            ToggleGamingMode.IsEnabled = false;
            try
            {
                if (isOn)
                {
                    var officeWasOn = ToggleOfficeMode.IsChecked == true;
                    if (officeWasOn) ToggleOfficeMode.IsChecked = false;
                    StatusService.SetBusy(LanguageService.T("Opt_EnablingGamingMode"));
                    if (officeWasOn) await Task.Run(() => PowerModeService.DisableOfficeMode(restartExplorer: false));
                    // Το Gaming Mode αλλάζει ρυθμίσεις συστήματος (registry, πλάνο ενέργειας) - ίδια προστασία με τις άλλες μαζικές ενέργειες.
                    var guard = await RestoreGuardService.EnsureRecentAsync();
                    await Task.Run(PowerModeService.SetGamingMode);
                    AppSettingsService.Current.GamingModeActive = true;
                    AppSettingsService.Current.OfficeModeActive = false;
                    AppSettingsService.Save();
                    ChangeJournalService.Record(LanguageService.T("Optimization_GamingMode") + ": " + LanguageService.T("Journal_On"), () =>
                    {
                        PowerModeService.DisableGamingMode();
                        AppSettingsService.Current.GamingModeActive = false;
                        AppSettingsService.Save();
                    });
                    TxtGamingModeStatus.Text = guard == RestoreGuardResult.Created ? LanguageService.T("Opt_RestorePointCreated") : "";
                    if (AppSettingsService.Current.GamingModeFreeRam) await FreeRamAsync();
                    StatusService.SetIdle(LanguageService.T("Opt_GamingModeEnabled"));
                }
                else
                {
                    StatusService.SetBusy(LanguageService.T("Opt_DisablingGamingMode"));
                    await Task.Run(() => PowerModeService.DisableGamingMode());
                    AppSettingsService.Current.GamingModeActive = false;
                    AppSettingsService.Save();
                    TxtGamingModeStatus.Text = "";
                    StatusService.SetIdle(LanguageService.T("Opt_GamingModeDisabled"));
                }
            }
            catch { StatusService.SetIdle(LanguageService.T("Ready")); }
            finally { ToggleGamingMode.IsEnabled = true; }
        }

        private void ChkGamingFreeRam_Click(object sender, RoutedEventArgs e)
        {
            AppSettingsService.Current.GamingModeFreeRam = ChkGamingFreeRam.IsChecked == true;
            AppSettingsService.Save();
        }

        private async void BtnFreeRamNow_Click(object sender, RoutedEventArgs e)
        {
            BtnFreeRamNow.IsEnabled = false;
            try { await FreeRamAsync(); }
            finally { BtnFreeRamNow.IsEnabled = true; }
        }

        // Ελευθερώνει RAM (MemoryCleanerService) και δείχνει την ΠΡΑΓΜΑΤΙΚΗ μέτρηση ελεύθερης μνήμης πριν/μετά.
        private async Task FreeRamAsync()
        {
            StatusService.SetBusy(LanguageService.T("Opt_FreeingRam"));
            try
            {
                var r = await MemoryCleanerService.CleanAsync();
                TxtFreeRamResult.Text = MemoryCleanerService.Describe(r);
            }
            catch { TxtFreeRamResult.Text = ""; }
            finally { StatusService.SetIdle(LanguageService.T("Ready")); }
        }

        private void ToggleNightlyWindowsUpdates_Click(object sender, RoutedEventArgs e)
        {
            AppSettingsService.Current.NightlyWindowsUpdates = ToggleNightlyWindowsUpdates.IsChecked == true;
            AppSettingsService.Save();
        }

        private async void ToggleNightlyUpdates_Click(object sender, RoutedEventArgs e)
        {
            var enable = ToggleNightlyUpdates.IsChecked == true;
            ToggleNightlyUpdates.IsEnabled = false;
            try
            {
                var ok = await AutoUpdateService.SetEnabledAsync(enable);
                if (!ok)
                {
                    // Ειλικρινής επαναφορά του διακόπτη αντί για ψεύτικο "ενεργό" - ίδιο μοτίβο με την εκκίνηση με τα Windows.
                    ToggleNightlyUpdates.IsChecked = !enable;
                    TxtNightlyUpdatesStatus.Text = LanguageService.T(enable ? "Autostart_UnsafeLocation" : "Autostart_Failed");
                    return;
                }
                AppSettingsService.Current.NightlyUpdatesEnabled = enable;
                AppSettingsService.Save();
                TxtNightlyUpdatesStatus.Text = enable ? LanguageService.T("Opt_NightlyUpdatesOn") : "";
            }
            finally { ToggleNightlyUpdates.IsEnabled = true; }
        }

        private void BtnOpenGamesTab_Click(object sender, RoutedEventArgs e) => TrayIconService.OpenTab("GamesMedia");

        private void OnAppScanChanged() { if (Dispatcher.CheckAccess()) ApplyAppScanUi(); else Dispatcher.BeginInvoke(new Action(ApplyAppScanUi)); }

        private void ApplyAppScanUi()
        {
            var busy = AppScan.Busy;
            ProgressWinget.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            BtnScanWinget.SetScanning(busy, AppScan.Installing && busy ? "Opt_InstallingCaps" : null);
            BtnScanWinget.IsEnabled = !busy;
            BtnUpgradeSelected.IsEnabled = !busy;
            if (AppScan.Status != null) TxtWingetStatus.Text = AppScan.Status;
        }

        private void OnDriverScanChanged() { if (Dispatcher.CheckAccess()) ApplyDriverScanUi(); else Dispatcher.BeginInvoke(new Action(ApplyDriverScanUi)); }

        private void ApplyDriverScanUi()
        {
            var busy = DriverScan.Busy;
            BtnScanDrivers.SetScanning(busy);
            BtnScanDrivers.IsEnabled = !busy;
            BtnCancelDriverScan.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            if (DriverScan.Status != null) TxtDriverStatus.Text = DriverScan.Status;
            BorderDriverResults.Visibility = Visibility.Collapsed;
            // Όταν τελειώνει μια σάρωση που ξεκίνησε από ΑΛΛΟ (πλέον κλειστό) View, οι έξτρα κάρτες κατασκευαστή πρέπει να εμφανιστούν εδώ.
            if (_driverWasBusy && !busy && s_vendorCache != null)
            {
                TxtVendorExtra.Text = "";
                PanelVendorExtraButtons.Children.Clear();
                ShowVendorExtras(s_vendorCache);
            }
            _driverWasBusy = busy;
        }

        // Μία σάρωση τη φορά, από όποιο View κι αν πατηθεί το κουμπί (βλ. ScanState). Η νέα λίστα αντικαθιστά την παλιά ΜΟΝΟ όταν
        // ολοκληρωθεί και χωρίς διπλότυπα (ίδια πηγή + ίδιο id), ώστε ούτε μια πολύ γρήγορη διπλή πάτηση ούτε δύο πηγές να τα δείξουν δύο φορές.
        private async void BtnScanWinget_Click(object sender, RoutedEventArgs e)
        {
            if (!AppScan.TryBegin()) return;
            SetBusy(true);
            AppScan.SetStatus(LanguageService.T("Opt_ScanInProgress"));

            // ΔΙΟΡΘΩΣΗ - roadmap "Καμία επαναφορά progress bar σε exception": χωρίς try/finally, μια
            // εξαίρεση μέσα στο ScanAsync άφηνε το ProgressWinget μόνιμα ορατό και τα κουμπιά μόνιμα
            // απενεργοποιημένα (το μόνο "δίχτυ ασφαλείας" ήταν το καθολικό DispatcherUnhandledException).
            try
            {
                _scanCts = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(8));   // συνολικό όριο χρόνου
                WingetService.ScanToken = _scanCts.Token;
                BtnCancelAppScan.Visibility = Visibility.Visible;
                var results = await WingetService.ScanAsync();
                _scanCts.Token.ThrowIfCancellationRequested();
                ReplaceUpdates(results);

                AppScan.SetStatus(((_updates.Count + _windowsUpdates.Count) == 0
                    ? LanguageService.T("Opt_NoUpdatesFound")
                    : $"{LanguageService.T("Opt_UpdatesFoundPrefix")}{_updates.Count + _windowsUpdates.Count}{LanguageService.T("Opt_UpdatesFoundSuffix")}")
                    + " · " + string.Format(LanguageService.T("Opt_LastScanned"), DateTime.Now.ToString("HH:mm:ss")));
                UpdatesHubService.ReportAppScan(_updates.Count);
                UpdatesHubService.ReportWindowsUpdateScan(_windowsUpdates.Count);
                RefreshIgnoredButton();
            }
            catch (OperationCanceledException)
            {
                AppScan.SetStatus(LanguageService.T("Health_ToolCancelled"));
            }
            catch (Exception ex)
            {
                AppScan.SetStatus($"{LanguageService.T("Opt_ScanFailed")}{ex.Message}");
            }
            finally
            {
                WingetService.ScanToken = System.Threading.CancellationToken.None;
                _scanCts?.Dispose(); _scanCts = null;
                BtnCancelAppScan.Visibility = Visibility.Collapsed;
                SetBusy(false);
                AppScan.End();
            }
        }

        private static System.Threading.CancellationTokenSource? _scanCts;
        private void BtnCancelAppScan_Click(object sender, RoutedEventArgs e) => _scanCts?.Cancel();

        // 6.9.1: λίστα «αγνόησης» ενημερώσεων (πηγή|id) που ο χρήστης δεν θέλει να βλέπει.
        internal static string IgnoreKey(WingetUpdate u) => (u.Source ?? "") + "|" + u.Id;
        private static readonly List<WingetUpdateRow> _ignoredRows = new();

        private void RefreshIgnoredButton()
        {
            BtnIgnored.Content = string.Format(LanguageService.T("Opt_IgnoredButton"), _ignoredRows.Count);
            BtnIgnored.Visibility = _ignoredRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnIgnoreUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: WingetUpdateRow row }) return;
            var key = IgnoreKey(row.Update);
            if (!AppSettingsService.Current.IgnoredUpdates.Contains(key, StringComparer.OrdinalIgnoreCase)) AppSettingsService.Current.IgnoredUpdates.Add(key);
            AppSettingsService.Save();
            _updates.Remove(row);
            _ignoredRows.Add(row);
            UpdatesHubService.ReportAppScan(_updates.Count);
            RefreshIgnoredButton();
        }

        private void BtnIgnored_Click(object sender, RoutedEventArgs e)
        {
            var names = string.Join("\n", _ignoredRows.Take(15).Select(r => "• " + r.Update.Name)) + (_ignoredRows.Count > 15 ? "\n…" : "");
            if (ThemedMessageBox.Show(string.Format(LanguageService.T("Opt_IgnoredRestoreConfirm"), names), LanguageService.T("Opt_IgnoredButton").Replace("({0})", "").Trim(),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            AppSettingsService.Current.IgnoredUpdates.Clear();
            AppSettingsService.Save();
            foreach (var r in _ignoredRows) _updates.Add(r);
            _ignoredRows.Clear();
            UpdatesHubService.ReportAppScan(_updates.Count);
            RefreshIgnoredButton();
        }

        internal static int UpdateCountForTests => _updates.Count;

        /// <summary>Καθαρή λογική (χωρίς UI): χωρίζει τα αποτελέσματα σε εφαρμογές / ενημερώσεις Windows / αγνοημένες (οι αγνοημένες αφορούν μόνο εφαρμογές).</summary>
        internal static (List<WingetUpdate> Apps, List<WingetUpdate> Windows, List<WingetUpdate> Ignored) SplitUpdates(IEnumerable<WingetUpdate> results, IEnumerable<string> ignoredKeys)
        {
            var ignored = new HashSet<string>(ignoredKeys, StringComparer.OrdinalIgnoreCase);
            var all = DeduplicateUpdates(results);
            var windows = all.Where(IsWindowsSource).ToList();
            var apps = all.Where(u => !IsWindowsSource(u)).ToList();
            return (apps.Where(u => !ignored.Contains(IgnoreKey(u))).ToList(), windows, apps.Where(u => ignored.Contains(IgnoreKey(u))).ToList());
        }

        private static void RemoveUpdateRow(WingetUpdateRow row) { if (!_updates.Remove(row)) _windowsUpdates.Remove(row); }

        /// <summary>Αντικαθιστά τη λίστα ενημερώσεων με τα νέα αποτελέσματα, χωρίς διπλότυπα (κλειδί: πηγή + id, χωρίς διάκριση πεζών-κεφαλαίων).</summary>
        internal static void ReplaceUpdates(IEnumerable<WingetUpdate> results)
        {
            var (apps, windows, ignoredUpdates) = SplitUpdates(results, AppSettingsService.Current.IgnoredUpdates);
            _ignoredRows.Clear();
            _ignoredRows.AddRange(ignoredUpdates.Select(u => new WingetUpdateRow(u)));
            _updates.Clear();
            foreach (var u in apps) _updates.Add(new WingetUpdateRow(u));
            _windowsUpdates.Clear();
            foreach (var u in windows) _windowsUpdates.Add(new WingetUpdateRow(u));
        }

        /// <summary>Καθαρή λογική (χωρίς UI): κρατά την πρώτη εμφάνιση κάθε πηγή+id, χωρίς διάκριση πεζών-κεφαλαίων, με την αρχική σειρά.</summary>
        internal static List<WingetUpdate> DeduplicateUpdates(IEnumerable<WingetUpdate> results)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<WingetUpdate>();
            foreach (var u in results)
                if (seen.Add((u.Source ?? "") + "|" + u.Id)) list.Add(u);
            return list;
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
            foreach (var row in _updates.Concat(_windowsUpdates)) row.IsSelected = selectAll;
        }

        // ΝΕΟ (χρήστης ανέφερε: "δεν βγαίνει κανένα μήνυμα επιτυχούς εγκατάστασης, ελέγξτο, pop-up ή
        // γραμμή κατάστασης"): τώρα υπάρχουν ΚΑΙ τα δύο - η γραμμή κατάστασης της κάρτας δείχνει
        // αριθμό επιτυχιών/αποτυχιών, ΚΑΙ ένα MessageBox συνοψίζει το αποτέλεσμα στο τέλος (η
        // WingetService.UpgradeAsync ελέγχει πλέον το πραγματικό exit code, βλ. εκεί - πριν πάντα
        // επέστρεφε "επιτυχία" εκτός αν πετάγονταν exception, ΣΧΕΔΟΝ ΠΟΤΕ, άρα μια πραγματική αποτυχία
        // δεν θα αναφερόταν ποτέ).
        // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη - ROADMAP.md REQ-570-01: "οι ενημερώσεις λογισμικού βγάζουν
        // pop-up εγκατάστασης χωρίς να έχουν εγκατασταθεί") - εντοπίστηκε η πραγματική αιτία: για
        // πηγή "msstore", το GetUpgradeCommandForSource απλώς ΑΝΟΙΓΕΙ τη σελίδα Downloads & Updates
        // του Store (καμία CLI δυνατότητα υπάρχει για σιωπηλή εγκατάσταση συγκεκριμένου Store app) -
        // το "cmd /c start ms-windows-store://..." επιστρέφει exit code 0 ΣΧΕΔΟΝ ΑΜΕΣΩΣ μόλις ανοίξει
        // το URL, πολύ ΠΡΙΝ προλάβει να ξεκινήσει (πόσο μάλλον να ολοκληρωθεί) η πραγματική ενημέρωση
        // μέσα στο Store. Πριν, αυτό μετρούσε ΠΑΝΤΑ ως "επιτυχία" - ψευδές. Τα msstore στοιχεία τώρα
        // ΞΕΧΩΡΙΖΟΥΝ σε δικό τους μήνυμα ("άνοιξαν στο Store"), ΔΕΝ αφαιρούνται από τη λίστα (δεν είναι
        // γνωστό αν πράγματι ενημερώθηκαν) και ΔΕΝ μετρούν ως επιτυχία/αποτυχία - ίδιο πνεύμα
        // ειλικρίνειας με τις υπόλοιπες "μόνο ό,τι πραγματικά επιβεβαιώθηκε" ενδείξεις της εφαρμογής.
        private async void BtnUpgradeSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _updates.Concat(_windowsUpdates).Where(u => u.IsSelected).ToList();
            var selectedDrivers = _driverUpdates.Where(d => d.IsSelected && d.CanSelect).ToList();
            if (selected.Count == 0 && selectedDrivers.Count == 0) return;
            if (selected.Count == 0) { await UpdateSelectedDriversAsync(selectedDrivers); return; }
            var selWindows = selected.Count(r => IsWindowsSource(r.Update));
            if (ThemedMessageBox.Show(string.Format(LanguageService.T("Opt_UpdateAllConfirm"), selected.Count - selWindows, selWindows), LanguageService.T("Opt_UpgradeSelected"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            if (selWindows > 0) await RestoreGuardService.EnsureRecentAsync();
            if (!AppScan.TryBegin(installing: true)) return;

            SetBusy(true, installing: true);
            var succeeded = new List<string>();
            var failed = new List<string>();
            var openedInStore = new List<string>();
            var triggeredStoreOnce = false;
            WingetService.RestartRequiredAfterUpdates = false;
            // try/finally: μια εξαίρεση στη μέση του βρόχου άφηνε το progress bar μόνιμα ορατό και τα κουμπιά
            // απενεργοποιημένα (ίδιο μοτίβο που ήδη διορθώθηκε στο BtnScanWinget_Click).
            try
            {
            foreach (var row in selected)
            {
                AppScan.SetStatus($"{LanguageService.T("Opt_UpgradingPrefix")}{row.Update.Name}... ({succeeded.Count + failed.Count + openedInStore.Count + 1}/{selected.Count})");

                if (row.Update.Source == "msstore")
                {
                    // Μηχανισμός WMT 6.7 (StoreGuiUpdateService): MDM update scan μία φορά, μετά για κάθε
                    // εφαρμογή αυτοματισμός του Store UI (πάτημα "Update" + παρακολούθηση της γραμμής) -
                    // πλέον επιβεβαιωμένο αποτέλεσμα αντί για απλό άνοιγμα του Store.
                    if (!triggeredStoreOnce) { await Task.Run(WingetService.TriggerMsStoreUpdateScan); triggeredStoreOnce = true; }
                    StoreUpdateOutcome outcome;
                    try { outcome = await StoreGuiUpdateService.UpdateAsync(row.Update.Name, null, System.Threading.CancellationToken.None); }
                    catch { outcome = StoreUpdateOutcome.Failed; }
                    switch (outcome)
                    {
                        case StoreUpdateOutcome.Updated:
                        case StoreUpdateOutcome.AlreadyCurrent:
                            succeeded.Add(row.Update.Name); RemoveUpdateRow(row); break;
                        case StoreUpdateOutcome.TimedOut:
                            openedInStore.Add(row.Update.Name); break;
                        default:
                            failed.Add(row.Update.Name); break;
                    }
                    continue;
                }

                bool ok; string? reason = null;
                try { (ok, reason) = await WingetService.UpgradeDetailedAsync(row.Update.Id, row.Update.Source); }
                catch { ok = false; } // ένα προβληματικό πακέτο δεν πρέπει να σταματά τα υπόλοιπα
                if (ok) { succeeded.Add(row.Update.Name); RemoveUpdateRow(row); }
                else { failed.Add(reason == null ? row.Update.Name : $"{row.Update.Name} [{reason}]"); }
            }
            }
            finally { SetBusy(false); AppScan.End(); }

            // Όπως το Close-WmtStoreGui: το Store κλείνει μόνο όταν όλες οι ενημερώσεις του επιβεβαιώθηκαν.
            if (triggeredStoreOnce && openedInStore.Count == 0 && failed.Count == 0)
                await Task.Run(StoreGuiUpdateService.CloseStoreWindow);

            var summary = failed.Count == 0
                ? $"{LanguageService.T("Opt_UpgradeAllSucceededPrefix")}{succeeded.Count}{LanguageService.T("Opt_UpgradeAllSucceededSuffix")}"
                : $"{LanguageService.T("Opt_UpgradePartialPrefix")}{succeeded.Count}{LanguageService.T("Opt_UpgradePartialMid")}{failed.Count}{LanguageService.T("Opt_UpgradePartialSuffix")}{string.Join(", ", failed)}).";
            if (openedInStore.Count > 0)
                summary += $" {openedInStore.Count}{LanguageService.T("Opt_UpgradeOpenedInStoreSuffix")}";
            if (WingetService.RestartRequiredAfterUpdates)
                summary += " " + LanguageService.T("Opt_RestartRequiredAfterUpdates");
            AppScan.SetStatus(summary);
            StatusService.SetIdle(summary);   // 6.9.1: η γραμμή κατάστασης δείχνει το αποτέλεσμα
            ThemedMessageBox.Show(summary, LanguageService.T("Opt_UpgradeAppsTitle"), MessageBoxButton.OK,
                failed.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            if (selectedDrivers.Count > 0) await UpdateSelectedDriversAsync(selectedDrivers);
        }

        // 6.9.1: «Ενημέρωση επιλεγμένων» και για οδηγούς: μία επιβεβαίωση, σημείο επαναφοράς + αντίγραφο οδηγών, μετά σειριακά με πρόοδο «Οδηγοί i/n».
        private async Task UpdateSelectedDriversAsync(List<UnifiedDriverRow> rows)
        {
            if (ThemedMessageBox.Show(string.Format(LanguageService.T("Opt_DriverSelectedConfirm"), rows.Count), LanguageService.T("Opt_FilterDrivers"),
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            using var scope = StatusService.BeginScope(LanguageService.T("Opt_CreatingRestorePoint"));
            await SystemService.CreateRestorePointAsync();
            scope.Update(LanguageService.T("Opt_DriverExporting"));
            await DriverService.ExportCurrentDriversAsync();
            int ok = 0, failed = 0, i = 0;
            foreach (var row in rows)
            {
                i++;
                scope.Update(string.Format(LanguageService.T("Opt_UpdateProgress"), LanguageService.T("Opt_FilterDrivers"), i, rows.Count, row.Title));
                bool done;
                try
                {
                    done = row.WuUpdate != null ? await DriverService.InstallAndWaitAsync(row.WuUpdate.UpdateId)
                        : row.CatalogCandidate != null && await DriverService.InstallCatalogDriverAsync(row.CatalogCandidate);
                }
                catch { done = false; }
                DriverHistoryService.Record(row.Title, row.WuUpdate != null ? "Windows Update" : "Microsoft Update Catalog", done ? "installed" : "failed");
                if (done) { ok++; _driverUpdates.Remove(row); } else failed++;
            }
            scope.End($"{LanguageService.T("Opt_FilterDrivers")}: {ok} / {rows.Count}" + (failed > 0 ? $" ({failed})" : ""));
        }

        // ΔΙΟΡΘΩΣΗ (GEARWIN.MD: "όταν γίνεται εγκατάσταση αντί του σάρωση στο κουμπί να εμφανίζεται
        // εγκατάσταση") - το ίδιο SetBusy εξυπηρετούσε ΚΑΙ τη σάρωση (BtnScanWinget_Click) ΚΑΙ την
        // εγκατάσταση (BtnUpgradeSelected_Click), πάντα με το ίδιο κείμενο "ΣΑΡΩΣΗ" - installing=true
        // δείχνει "ΕΓΚΑΤΑΣΤΑΣΗ" αντί γι' αυτό μόνο στο δεύτερο μονοπάτι.
        private void SetBusy(bool busy, bool installing = false)
        {
            if (busy) StatusService.SetBusy(LanguageService.T(installing ? "Opt_InstallingUpdates" : "Opt_CheckingWingetUpdates"));
            else StatusService.SetIdle(LanguageService.T("Ready"));
        }

        // ΝΕΟ - roadmap "winget pin" - βλ. σχόλιο στο XAML/WingetService.PinAsync.
        // 6.8.0: απεγκατάσταση εφαρμογής/εργαλείου (και AI agents που μπήκαν με npm/pip/κ.λπ.) από τη λίστα, με ρητή επιβεβαίωση.
        private async void BtnUninstallApp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: WingetUpdateRow row } btn) return;
            if (WingetService.GetUninstallCommandForSource(row.Update.Source, row.Update.Id) == null) return;
            if (ThemedMessageBox.Show(string.Format(LanguageService.T("Opt_UninstallConfirm"), row.Update.Name), LanguageService.T("Opt_UninstallButton"),
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            btn.IsEnabled = false;
            try
            {
                var (ok, reason) = await WingetService.UninstallAsync(row.Update.Id, row.Update.Source);
                if (ok)
                {
                    _updates.Remove(row);
                    StatusService.SetIdle(string.Format(LanguageService.T("Opt_UninstallDone"), row.Update.Name));
                }
                else
                {
                    ThemedMessageBox.Show(string.Format(LanguageService.T("Opt_UninstallFailed"), row.Update.Name) + (reason == null ? "" : "\n" + reason),
                        LanguageService.T("Opt_UninstallButton"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex) { ThemedMessageBox.Show(ex.Message, LanguageService.T("Opt_UninstallButton"), MessageBoxButton.OK, MessageBoxImage.Warning); }
            finally { btn.IsEnabled = true; }
        }

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

        // try/catch/finally: οι 4 διαδοχικές σαρώσεις (vendor/WU/devices/Catalog) δεν είχαν καμία προστασία — μια εξαίρεση
        // άφηνε το κουμπί σάρωσης να "γυρίζει" και να μένει απενεργοποιημένο μέχρι επανεκκίνηση της εφαρμογής.
        private static System.Threading.CancellationTokenSource? s_driverCts;

        // Αναμονή μιας εργασίας που δεν υποστηρίζει ακύρωση: ο χρήστης επιστρέφει αμέσως, η εργασία
        // παρασκηνίου απλώς τελειώνει και το αποτέλεσμά της απορρίπτεται.
        private static async Task<T> OrCancelAsync<T>(Task<T> work, System.Threading.CancellationToken ct)
        {
            using var gate = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
            var done = await Task.WhenAny(work, Task.Delay(System.Threading.Timeout.Infinite, gate.Token));
            gate.Cancel(); // απελευθερώνει το Delay όταν τελείωσε πρώτη η πραγματική εργασία
            if (done != work) throw new OperationCanceledException(ct);
            return await work;
        }

        private void BtnCancelDriverScan_Click(object sender, RoutedEventArgs e) => s_driverCts?.Cancel();

        // 6.9.0: προσθήκη φακέλου με git repos στον έλεγχο ενημερώσεων (ο έλεγχος γίνεται στην επόμενη σάρωση).
        private void BtnAddGitFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog { ShowNewFolderButton = false };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dlg.SelectedPath)) return;
            var roots = AppSettingsService.Current.GitRepoRoots;
            if (!roots.Contains(dlg.SelectedPath, StringComparer.OrdinalIgnoreCase)) roots.Add(dlg.SelectedPath);
            AppSettingsService.Save();
            StatusService.SetIdle(string.Format(LanguageService.T("Opt_GitFolderAdded"), dlg.SelectedPath));
        }

        // 6.8.0: εφαρμογές (+Windows Update) και οδηγοί μαζί - οι δύο σαρώσεις είναι ανεξάρτητες και η καθεμία single-flight (ScanState).
        private void BtnScanAll_Click(object sender, RoutedEventArgs e)
        {
            BtnScanWinget_Click(sender, e);
            BtnScanDrivers_Click(sender, e);
        }

        private async void BtnScanDrivers_Click(object sender, RoutedEventArgs e)
        {
            if (!DriverScan.TryBegin()) return;
            s_driverCts = new System.Threading.CancellationTokenSource();
            var scope = StatusService.BeginScope(LanguageService.T("Opt_CheckingVendorSources"));
            s_driverScope = scope;
            try { await ScanDriversCoreAsync(s_driverCts.Token); }
            catch (OperationCanceledException) { DriverScan.SetStatus(LanguageService.T("Health_ToolCancelled")); }
            catch (Exception ex) { DriverScan.SetStatus($"{LanguageService.T("Opt_ScanFailed")}{ex.Message}"); }
            finally
            {
                s_driverCts?.Dispose();
                s_driverCts = null;
                s_driverScope = null;
                // 6.9.0: η γραμμή κατάστασης δείχνει το αποτέλεσμα όταν τελειώσει η σάρωση (όχι «παγωμένο» μήνυμα προόδου)
                scope.End(_driverUpdates.Count == 0 ? LanguageService.T("Ready")
                    : $"{LanguageService.T("Opt_DriverUpdatesFoundPrefix")}{_driverUpdates.Count}{LanguageService.T("Opt_DriverUpdatesFoundSuffix")}");
                DriverScan.End();
            }
        }

        private static StatusService.Scope? s_driverScope;

        private async Task ScanDriversCoreAsync(System.Threading.CancellationToken ct)
        {
            _driverUpdates.Clear();
            TxtVendorExtra.Text = "";
            PanelVendorExtraButtons.Children.Clear();

            // Ρητό αίτημα χρήστη ("ενσωμάτωσε όλους τους μηχανισμούς... όσο πιο κοντινή απόδοση στο
            // IObit Driver Booster") - AMD/NVIDIA τρέχουν ΠΡΩΤΑ (ίδια σειρά με το ps1's Complete-
            // DriverScan, "εμφανίζεται πάντα πρώτη") ώστε τα ονόματα GPU τους να είναι διαθέσιμα για
            // trust-overlap πριν προστεθούν τα WU αποτελέσματα.
            DriverScan.SetStatus(LanguageService.T("Opt_CheckingVendorSources"));
            s_driverScope?.Update(LanguageService.T("Opt_ScanningVendorSources"));
            var vendor = await OrCancelAsync(DriverService.ScanVendorSourcesAsync(), ct);
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

            DriverScan.SetStatus(LanguageService.T("Opt_CheckingWuDrivers"));
            s_driverScope?.Update(LanguageService.T("Opt_ScanningWuDrivers"));
            var wuResult = await OrCancelAsync(DriverService.ScanAsync(), ct);
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
                    Vendor = u.Manufacturer,
                    IsPreview = UnifiedDriverRow.LooksPreview(u.Title, null),
                });
            }

            DriverScan.SetStatus(LanguageService.T("Opt_EnumeratingDevices"));
            s_driverScope?.Update(LanguageService.T("Opt_EnumeratingDevicesShort"));
            var devices = await OrCancelAsync(DriverService.GetSystemDevicesAsync(), ct);
            var progress = new Progress<string>(name => s_driverScope?.Update($"{LanguageService.T("Opt_CheckingCatalogPrefix")}{name}..."));
            var (candidates, looksBlocked) = await DriverService.ScanCatalogAsync(devices, progress, ct);
            foreach (var c in candidates)
                _driverUpdates.Add(new UnifiedDriverRow
                {
                    Title = c.Title,
                    SourceBadge = "Update Catalog",
                    SubText = $"{c.DeviceName} · v{c.Version}",
                    CatalogCandidate = c,
                    Vendor = null,
                    IsPreview = UnifiedDriverRow.LooksPreview(c.Title, c.IsWhql),
                });


            var parts = new List<string>();
            parts.Add(wuResult.PolicyBlocked
                ? LanguageService.T("Opt_WuPolicyBlocked")
                : $"Windows Update: {wuResult.Updates.Count}{LanguageService.T("Opt_UpdatesSuffix")}");
            parts.Add(looksBlocked
                ? $"{LanguageService.T("Opt_CatalogNoResponsePrefix")}{devices.Count}{LanguageService.T("Opt_CatalogNoResponseSuffix")}"
                : $"Update Catalog: {candidates.Count}{LanguageService.T("Opt_CatalogUpdatesMid")}{devices.Count}{LanguageService.T("Opt_CatalogUpdatesSuffix")}");
            DriverScan.SetStatus(_driverUpdates.Count == 0
                ? $"{LanguageService.T("Opt_NoDriverUpdatesFound")} {string.Join(" ", parts)}"
                : $"{LanguageService.T("Opt_DriverUpdatesFoundPrefix")}{_driverUpdates.Count}{LanguageService.T("Opt_DriverUpdatesFoundSuffix")} {string.Join(" ", parts)}");
            UpdatesHubService.ReportDriverScan(_driverUpdates.Count);
            s_vendorCache = vendor;

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
                if (sdio.Installed)
                {
                    // 6.9.1: μετά την εγκατάσταση του SDI - ρητά κουμπιά «Λήψη» (ευρετήρια/ενημερώσεις packs) και «Ενσωμάτωση» (εγκατάσταση, με επιβεβαίωση + σημείο επαναφοράς)
                    var btnDl = new Button { Content = LanguageService.T("Opt_SdioDownloadBtn"), Style = (Style)FindResource("FlatButtonStyle"), Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 10, 0) };
                    btnDl.Click += async (_, __) => await RunSdioAsync(SdioAction.Download, btnDl);
                    var btnInt = new Button { Content = LanguageService.T("Opt_SdioIntegrateBtn"), Style = (Style)FindResource("AccentButtonStyle"), Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 10, 0) };
                    btnInt.Click += async (_, __) =>
                    {
                        if (ThemedMessageBox.Show(LanguageService.T("Opt_SdioIntegrateConfirm"), LanguageService.T("Opt_SdioIntegrateBtn"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                        var guard = await RestoreGuardService.EnsureRecentAsync();
                        if (guard == RestoreGuardResult.Failed && ThemedMessageBox.Show(LanguageService.T("Guard_PointFailedContinue"), LanguageService.T("Opt_SdioIntegrateBtn"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                        await RunSdioAsync(SdioAction.Integrate, btnInt);
                    };
                    PanelVendorExtraButtons.Children.Add(btnDl);
                    PanelVendorExtraButtons.Children.Add(btnInt);
                }
                else if (!sdio.Installed)
                {
                    lines.Add(LanguageService.T("Opt_SdioNotInstalled"));
                    var btnGet = new Button { Content = LanguageService.T("Opt_SdioInstallBtn"), Style = (Style)FindResource("AccentButtonStyle"), Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 10, 0) };
                    btnGet.Click += async (_, __) =>
                    {
                        if (ThemedMessageBox.Show(LanguageService.T("Opt_SdioInstallConfirm"), LanguageService.T("Opt_SdioInstallBtn"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                        btnGet.IsEnabled = false;
                        try
                        {
                            var (ok, reason) = await WingetService.InstallSdioAsync();
                            ThemedMessageBox.Show(ok ? LanguageService.T("Opt_SdioInstalledOk") : LanguageService.T("Opt_SdioInstallFailed") + (reason == null ? "" : "\n" + reason),
                                LanguageService.T("Opt_SdioInstallBtn"), MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
                        }
                        catch (Exception ex) { ThemedMessageBox.Show(ex.Message, LanguageService.T("Opt_SdioInstallBtn"), MessageBoxButton.OK, MessageBoxImage.Warning); }
                        finally { btnGet.IsEnabled = true; }
                    };
                    PanelVendorExtraButtons.Children.Add(btnGet);
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

        private async System.Threading.Tasks.Task RunSdioAsync(SdioAction action, Button button)
        {
            button.IsEnabled = false;
            StatusService.SetBusy(LanguageService.T(action == SdioAction.Download ? "Opt_SdioDownloadBtn" : "Opt_SdioIntegrateBtn"));
            try
            {
                var (started, exit, logDir) = await SdioService.RunAsync(action);
                var ok = started && exit == 0;
                ThemedMessageBox.Show(ok ? string.Format(LanguageService.T("Opt_SdioRunDone"), logDir) : string.Format(LanguageService.T("Opt_SdioRunFailed"), exit) + "\n" + logDir,
                    LanguageService.T("Opt_SdioDownloadBtn"), MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex) { ThemedMessageBox.Show(ex.Message, LanguageService.T("Opt_SdioDownloadBtn"), MessageBoxButton.OK, MessageBoxImage.Warning); }
            finally { StatusService.SetIdle(LanguageService.T("Ready")); button.IsEnabled = true; }
        }

        private void BtnDriverHistory_Click(object sender, RoutedEventArgs e)
        {
            var list = DriverHistoryService.Load();
            var text = list.Count == 0 ? LanguageService.T("Opt_DriverHistoryEmpty")
                : string.Join("\n", list.AsEnumerable().Reverse().Take(25).Select(h => $"{h.Time:yyyy-MM-dd HH:mm}  {h.Title} - {h.Source} - {h.Result}"));
            ThemedMessageBox.Show(text, LanguageService.T("Opt_DriverHistoryBtn"), MessageBoxButton.OK, MessageBoxImage.Information);
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

                await RestoreGuardService.EnsureRecentAsync();   // 6.9.0: σημείο επαναφοράς πριν από κάθε εγκατάσταση οδηγού
                using (var ex = StatusService.BeginScope(LanguageService.T("Opt_DriverExporting"))) { await DriverService.ExportCurrentDriversAsync(); ex.End(LanguageService.T("Ready")); }
                button.IsEnabled = false;
                var installingText = $"{LanguageService.T("Opt_WuInstallingPrefix")}{row.WuUpdate.Title}";
                TxtDriverStatus.Text = installingText;
                StatusService.SetBusy(installingText);
                bool installed;
                try { installed = await DriverService.InstallAndWaitAsync(row.WuUpdate.UpdateId); }
                catch { installed = false; }
                finally { StatusService.SetIdle(LanguageService.T("Ready")); }
                DriverHistoryService.Record(row.WuUpdate.Title, "Windows Update", installed ? "installed" : "failed");
                // 6.9.0: το αποτέλεσμα φαίνεται (κάρτα + γραμμή κατάστασης), η γραμμή δεν μένει «παγωμένη» στο «Εγκατάσταση…»
                var resultText = string.Format(LanguageService.T(installed ? "Opt_DriverWuDone" : "Opt_DriverWuFailed"), row.WuUpdate.Title);
                TxtDriverStatus.Text = resultText;
                StatusService.SetIdle(resultText);
                if (installed) _driverUpdates.Remove(row); else button.IsEnabled = true;
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
            StatusService.SetIdle(LanguageService.T("Opt_DriverExporting"));
            StatusService.SetBusy(LanguageService.T("Opt_DriverExporting"));
            await DriverService.ExportCurrentDriversAsync();   // 6.9.1: αντίγραφο τρεχόντων οδηγών πριν από κάθε εγκατάσταση
            StatusService.SetIdle(LanguageService.T("Ready"));

            StatusService.SetBusy($"{LanguageService.T("Opt_DownloadVerifyInstallPrefix")}{candidate.Title}...");
            var ok = await DriverService.InstallCatalogDriverAsync(candidate);
            DriverHistoryService.Record(candidate.Title, "Microsoft Update Catalog", ok ? "installed (hash verified)" : "failed");
            StatusService.SetIdle(LanguageService.T("Ready"));

            if (ok)
            {
                _driverUpdates.Remove(row);
                BorderDriverResults.Visibility = Visibility.Collapsed;
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

            bool ok = false;
            try { ok = await DriverService.BackupAsync(destPath); }
            catch { /* αποτυχία → μήνυμα παρακάτω */ }
            finally
            {
                StatusService.SetIdle(LanguageService.T("Ready"));
                BtnDriverBackup.IsEnabled = true;
                BtnDriverRestore.IsEnabled = true;
            }
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

            bool ok = false;
            try { ok = await DriverService.RestoreAsync(dialog.FolderName); }
            catch { /* αποτυχία → μήνυμα παρακάτω */ }
            finally
            {
                StatusService.SetIdle(LanguageService.T("Ready"));
                BtnDriverBackup.IsEnabled = true;
                BtnDriverRestore.IsEnabled = true;
            }
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

                // Λογική WMT 6.6: κρατάμε ό,τι είναι σε χρήση, μετά υψηλότερη έκδοση, μετά ημερομηνία (βλ. SelectOldDrivers).
                var duplicates = DriverService.SelectOldDrivers(all).ToList();

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
                s_driverStoreStatusCache = TxtDriverStoreStatus.Text;
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
    // Επικεφαλίδα κατηγορίας στην ενιαία λίστα ενημερώσεων (Εφαρμογές / Windows / Οδηγοί)
    public sealed class UpdateGroupHeader
    {
        public string Title { get; }
        public UpdateGroupHeader(string key, int count) { Title = $"{LanguageService.T(key)} ({count})"; }
    }

    public class UnifiedDriverRow
    {
        // 6.9.1: επιλογή για «Ενημέρωση επιλεγμένων», φίλτρα κατασκευαστή και preview/beta/μη-WHQL
        public bool IsSelected { get; set; }
        public string? Vendor { get; init; }
        public bool IsPreview { get; init; }
        public bool CanSelect => VendorDownloadUrl == null && (WuUpdate != null || CatalogCandidate != null);
        private static readonly System.Text.RegularExpressions.Regex PreviewRx = new(@"\b(preview|beta|insider|prerelease|pre-release|test)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
        public static bool LooksPreview(string? title, bool? whql) => whql == false || (title != null && PreviewRx.IsMatch(title));
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
        // 6.9.1: Windows Update γραμμές δεν έχουν Απεγκατάσταση/Παράβλεψη
        public bool ShowAppActions => !string.Equals(Update.Source, "windowsupdate", StringComparison.OrdinalIgnoreCase);

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
