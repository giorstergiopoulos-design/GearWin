using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class AdvancedView : UserControl
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        // ΔΙΟΡΘΩΣΗ (bug εντοπίστηκε - χρήστης ανέφερε: "τα αποτελέσματα των σαρώσεων χάνονται όταν
        // αλλάζω καρτέλα") - static αντί για instance, ίδιο μοτίβο με OptimizationView/SystemView -
        // κάθε αλλαγή καρτέλας δημιουργεί ΝΕΟ AdvancedView (βλ. MainWindow.ShowTabContent).
        private static readonly ObservableCollection<GhostDeviceInfo> _ghostDevices = new();
        private static readonly ObservableCollection<ScheduledTaskRow> _scheduledTasks = new();
        private static string? s_ghostStatusCache;
        private static string? s_scheduledTasksStatusCache;
        // Instance (ΟΧΙ static) - φορτώνεται αυτόματα ξανά κάθε φορά (LoadWslAsync παρακάτω), ο
        // χρήστης θέλει τη φρέσκια κατάσταση των διανομών WSL, όχι παλιό στιγμιότυπο.
        private readonly ObservableCollection<WslDistroRow> _wslDistros = new();

        public AdvancedView()
        {
            InitializeComponent();
            ListGhostDevices.ItemsSource = _ghostDevices;
            ListScheduledTasks.ItemsSource = _scheduledTasks;
            ListWslDistros.ItemsSource = _wslDistros;
            if (s_ghostStatusCache != null) TxtGhostStatus.Text = s_ghostStatusCache;
            if (s_scheduledTasksStatusCache != null) TxtScheduledTasksStatus.Text = s_scheduledTasksStatusCache;
            LoadBootTimeline();
            RefreshUsbHistory();
            RefreshAutologonStatus();
            _ = LoadWslAsync();
        }

        // ΝΕΟ - roadmap "WSL2 manager" - βλ. σχόλιο στο XAML/WslService.cs. Η κάρτα μένει Collapsed
        // (default στο XAML) αν το WSL δεν είναι καν εγκατεστημένο στο μηχάνημα - άσκοπο να δείχνει
        // κενή κάρτα "καμία διανομή" σε ένα σύστημα χωρίς WSL καθόλου.
        private async System.Threading.Tasks.Task LoadWslAsync()
        {
            if (!await WslService.IsAvailableAsync()) return;
            BorderWsl.Visibility = Visibility.Visible;
            await RefreshWslAsync();
        }

        private async System.Threading.Tasks.Task RefreshWslAsync()
        {
            var distros = await WslService.ListDistrosAsync();
            _wslDistros.Clear();
            foreach (var d in distros) _wslDistros.Add(new WslDistroRow(d));
        }

        private async void BtnRefreshWsl_Click(object sender, RoutedEventArgs e) => await RefreshWslAsync();

        private void BtnWslOpenShell_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: WslDistroRow row }) return;
            WslService.OpenShell(row.Distro.Name);
        }

        private async void BtnWslSetDefault_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: WslDistroRow row }) return;
            await WslService.SetDefaultAsync(row.Distro.Name);
            await RefreshWslAsync();
        }

        private async void BtnWslTerminate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: WslDistroRow row }) return;
            await WslService.TerminateAsync(row.Distro.Name);
            await RefreshWslAsync();
        }

        // ΚΑΤΑΣΤΡΟΦΙΚΟ - διαγράφει ΟΛΑ τα δεδομένα της διανομής, βλ. σχόλιο στο WslService.UnregisterAsync.
        private async void BtnWslUnregister_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: WslDistroRow row }) return;
            if (ThemedMessageBox.Show($"{LanguageService.T("Wsl_UnregisterConfirmPrefix")}{row.Distro.Name}{LanguageService.T("Wsl_UnregisterConfirmSuffix")}",
                    LanguageService.T("Adv_WarningTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            TxtWslStatus.Text = LanguageService.T("Wsl_Working");
            var ok = await WslService.UnregisterAsync(row.Distro.Name);
            TxtWslStatus.Text = ok ? "" : LanguageService.T("Wsl_ActionFailed");
            await RefreshWslAsync();
        }

        private async void BtnWslShutdownAll_Click(object sender, RoutedEventArgs e)
        {
            TxtWslStatus.Text = LanguageService.T("Wsl_Working");
            var ok = await WslService.ShutdownAllAsync();
            TxtWslStatus.Text = ok ? "" : LanguageService.T("Wsl_ActionFailed");
            await RefreshWslAsync();
        }

        private async void BtnWslUpdate_Click(object sender, RoutedEventArgs e)
        {
            TxtWslStatus.Text = LanguageService.T("Wsl_Updating");
            var ok = await WslService.UpdateAsync();
            TxtWslStatus.Text = LanguageService.T(ok ? "Wsl_UpdateDone" : "Wsl_ActionFailed");
        }

        // ΝΕΟ - roadmap "RGB conflict detector" - βλ. σχόλιο στο XAML/RgbConflictService.cs.
        private void BtnCheckRgbConflicts_Click(object sender, RoutedEventArgs e)
        {
            var found = RgbConflictService.DetectRunning();
            if (found.Count == 0)
            {
                TxtRgbResult.Text = LanguageService.T("Rgb_NoneFound");
            }
            else if (found.Count == 1)
            {
                TxtRgbResult.Text = $"{LanguageService.T("Rgb_OneFoundPrefix")}{found[0].Vendor}.";
            }
            else
            {
                TxtRgbResult.Text = $"{LanguageService.T("Rgb_ConflictPrefix")}{string.Join(", ", found.Select(f => f.Vendor))}. {LanguageService.T("Rgb_ConflictSuffix")}";
            }
        }

        // ΝΕΟ - roadmap "Βοηθός ρύθμισης Autologon".
        private void RefreshAutologonStatus() =>
            TxtAutologonStatus.Text = AdvancedToolsService.IsAutologonEnabled()
                ? LanguageService.T("Advanced_AutologonStatusOn")
                : LanguageService.T("Advanced_AutologonStatusOff");

        private void BtnEnableAutologon_Click(object sender, RoutedEventArgs e)
        {
            var username = TxtAutologonUsername.Text.Trim();
            var password = PwdAutologon.Password;
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                ThemedMessageBox.Show(LanguageService.T("Advanced_AutologonMissingFields"), LanguageService.T("Adv_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ThemedMessageBox.Show(LanguageService.T("Advanced_AutologonConfirm"), LanguageService.T("Adv_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            var domain = Environment.UserDomainName != Environment.MachineName ? Environment.UserDomainName : null;
            var ok = AdvancedToolsService.EnableAutologon(username, password, domain);
            PwdAutologon.Clear();
            ThemedMessageBox.Show(LanguageService.T(ok ? "Advanced_AutologonEnableDone" : "Advanced_AutologonEnableFailed"),
                LanguageService.T("Advanced_Autologon"), MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            RefreshAutologonStatus();
        }

        private void BtnDisableAutologon_Click(object sender, RoutedEventArgs e)
        {
            var ok = AdvancedToolsService.DisableAutologon();
            ThemedMessageBox.Show(LanguageService.T(ok ? "Advanced_AutologonDisableDone" : "Advanced_AutologonDisableFailed"),
                LanguageService.T("Advanced_Autologon"), MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            RefreshAutologonStatus();
        }

        // ΝΕΟ - roadmap "Γρήγορες συντομεύσεις σε δημοφιλή optional features".
        // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "τα κουμπιά των πρόσθετων λειτουργιών δεν είναι διακριτά") - ίδιο
        // γνωστό bug-class με το BtnCheckMsStore/BtnScanWinget αλλού στην εφαρμογή: το FlatButtonStyle's
        // Background (BtnDefaultBrush) είναι πολύ κοντά στο χρώμα φόντου της ίδιας της κάρτας σε
        // αρκετά θέματα - "χανόταν". Επιπλέον, το ControlTemplate του FlatButtonStyle ΑΓΝΟΕΙ όποιο
        // BorderBrush/BorderThickness οριστεί στο instance (hardcoded DynamicResource μέσα στο ίδιο
        // το template, όχι TemplateBinding) - δεν μπορεί να διορθωθεί απλά προσθέτοντας περίγραμμα σε
        // κάθε chip. Το AccentButtonStyle (συμπαγές accent χρώμα) είναι ΠΑΝΤΑ ορατά διαφορετικό από
        // το φόντο κάθε κάρτας σε κάθε θέμα, εξ ορισμού - ίδια λύση με τα δύο παραπάνω κουμπιά.
        // ΝΕΟ - roadmap "Χρονοδιάγραμμα εκκίνησης".
        private void LoadBootTimeline()
        {
            var entries = AdvancedToolsService.GetRecentBootTimes()
                .Select(b => new BootTimelineRow($"{b.When:yyyy-MM-dd HH:mm}", $"{b.Seconds:0.#}s"))
                .ToList();
            ListBootTimeline.ItemsSource = entries;
            TxtNoBootTimeline.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnRefreshBootTimeline_Click(object sender, RoutedEventArgs e) => LoadBootTimeline();

        private record BootTimelineRow(string When, string Seconds);

        // ===== Συντήρηση & Διάγνωση =====

        private void BtnFullMaintenance_Click(object sender, RoutedEventArgs e) { AdvancedToolsService.FullMaintenance(); TxtMaintenanceStatus.Text = LanguageService.T("Adv_MaintenanceStarted"); }
        private void BtnEnablePrivileges_Click(object sender, RoutedEventArgs e) { var ok = AdvancedToolsService.EnablePrivilege("SeDebugPrivilege"); TxtMaintenanceStatus.Text = ok ? LanguageService.T("Adv_Enabled") : LanguageService.T("Adv_Failed"); }
        private void BtnNetworkReset_Click(object sender, RoutedEventArgs e) { AdvancedToolsService.NetworkReset(); TxtMaintenanceStatus.Text = LanguageService.T("Adv_NetworkResetStarted"); }

        private void BtnClearEventLogs_Click(object sender, RoutedEventArgs e)
        {
            if (ThemedMessageBox.Show(LanguageService.T("Adv_ClearLogsConfirm"), LanguageService.T("Adv_WarningTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var count = AdvancedToolsService.ClearEventLogs();
            TxtMaintenanceStatus.Text = $"{LanguageService.T("Adv_LogsClearedPrefix")}{count}{LanguageService.T("Adv_LogsClearedSuffix")}";
        }

        private void BtnGc_Click(object sender, RoutedEventArgs e) { AdvancedToolsService.CollectGarbage(); TxtMaintenanceStatus.Text = LanguageService.T("Adv_Completed"); }
        private void BtnDeepDiagnostics_Click(object sender, RoutedEventArgs e) { var path = AdvancedToolsService.WriteDeepDiagnosticsReport(); TxtMaintenanceStatus.Text = $"{LanguageService.T("Adv_SavedPrefix")}{path}"; }
        private void BtnDeviceManager_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.OpenDeviceManager();
        private void BtnWuTroubleshoot_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.OpenWindowsUpdateTroubleshooter();
        private void BtnSystemReport_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.CreateSystemReport();
        private void BtnResetWuServices_Click(object sender, RoutedEventArgs e) { AdvancedToolsService.ResetWindowsUpdateServices(); TxtMaintenanceStatus.Text = LanguageService.T("Adv_WuResetStarted"); }
        private void BtnCleanXbox_Click(object sender, RoutedEventArgs e) { var count = AdvancedToolsService.CleanXboxCredentials(); TxtMaintenanceStatus.Text = $"{LanguageService.T("Adv_XboxClearedPrefix")}{count}{LanguageService.T("Adv_XboxClearedSuffix")}"; }
        private void BtnInstallGpedit_Click(object sender, RoutedEventArgs e) { AdvancedToolsService.InstallGroupPolicyEditor(); TxtMaintenanceStatus.Text = LanguageService.T("Adv_InstallStarted"); }

        // ===== Εργαλειοθήκη =====

        private void BtnSnip_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.OpenSnippingTool();
        private void BtnNotepad_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.OpenNotepad();
        private void BtnCalc_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.OpenCalculator();
        private void BtnControlPanel_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.OpenControlPanel();
        private void BtnTaskMgr_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.OpenTaskManager();

        private void BtnRegedit_Click(object sender, RoutedEventArgs e)
        {
            if (ThemedMessageBox.Show(LanguageService.T("Adv_RegeditConfirm"), LanguageService.T("Adv_CautionTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            AdvancedToolsService.OpenRegistryEditor();
        }

        private void BtnGpuReset_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.GpuDriverSoftReset();

        // ===== Συσκευές-Φαντάσματα =====

        // ΝΕΟ - roadmap "ιστορικό συσκευών USB" - βλ. UsbHistoryService, καθαρά ενημερωτικό.
        private void RefreshUsbHistory() => ListUsbHistory.ItemsSource = Services.UsbHistoryService.GetHistory();
        private void BtnRefreshUsbHistory_Click(object sender, RoutedEventArgs e) => RefreshUsbHistory();

        private async void BtnScanGhostDevices_Click(object sender, RoutedEventArgs e)
        {
            TxtGhostStatus.Text = LanguageService.T("Adv_Scanning");
            StatusService.SetBusy(LanguageService.T("Adv_ScanningGhostDevices"));
            _ghostDevices.Clear();
            var results = await AdvancedToolsService.ScanGhostDevicesAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            foreach (var d in results) _ghostDevices.Add(d);
            TxtGhostStatus.Text = $"{LanguageService.T("Adv_GhostFoundPrefix")}{results.Count}{LanguageService.T("Adv_GhostFoundSuffix")}";
            s_ghostStatusCache = TxtGhostStatus.Text;
        }

        // ΔΙΟΡΘΩΣΗ (ρητό αίτημα από deep review §0.4ιβ - ο ps1 original δεν είχε confirm dialog εδώ):
        // προστέθηκε επιβεβαίωση πριν την αφαίρεση.
        private async void BtnRemoveGhostDevice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: GhostDeviceInfo device } button) return;
            if (ThemedMessageBox.Show($"{LanguageService.T("Adv_RemoveGhostPrefix")}{device.FriendlyName}{LanguageService.T("Adv_RemoveGhostSuffix")}", LanguageService.T("Adv_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            button.IsEnabled = false;
            var ok = await AdvancedToolsService.RemoveGhostDeviceAsync(device.InstanceId);
            if (ok) _ghostDevices.Remove(device);
            else { button.IsEnabled = true; ThemedMessageBox.Show(LanguageService.T("Adv_RemoveFailed"), LanguageService.T("Adv_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        // ===== Προγραμματισμένες Εργασίες (roadmap) =====

        private async void BtnScanScheduledTasks_Click(object sender, RoutedEventArgs e)
        {
            TxtScheduledTasksStatus.Text = LanguageService.T("Adv_Scanning");
            StatusService.SetBusy(LanguageService.T("Adv_Scanning"));
            _scheduledTasks.Clear();
            var results = await AdvancedToolsService.ScanScheduledTasksAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            foreach (var t in results) _scheduledTasks.Add(new ScheduledTaskRow(t));
            TxtScheduledTasksStatus.Text = $"{LanguageService.T("Adv_GhostFoundPrefix")}{results.Count}{LanguageService.T("Adv_GhostFoundSuffix")}";
            s_scheduledTasksStatusCache = TxtScheduledTasksStatus.Text;
        }

        private async void ToggleScheduledTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton { Tag: ScheduledTaskRow row } toggle) return;
            var wantEnabled = toggle.IsChecked == true;
            toggle.IsEnabled = false;
            var ok = await AdvancedToolsService.SetScheduledTaskEnabledAsync(row.Task.TaskName, row.Task.TaskPath, wantEnabled);
            toggle.IsEnabled = true;
            if (ok) row.IsEnabled = wantEnabled;
            else
            {
                row.IsEnabled = !wantEnabled; // επαναφορά οπτικά, η αλλαγή απέτυχε
                ThemedMessageBox.Show(LanguageService.T("Adv_RemoveFailed"), LanguageService.T("Adv_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    public class ScheduledTaskRow : INotifyPropertyChanged
    {
        public ScheduledTaskInfo Task { get; }
        private bool _isEnabled;
        public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; PropertyChanged?.Invoke(this, new(nameof(IsEnabled))); } }
        public ScheduledTaskRow(ScheduledTaskInfo task) { Task = task; _isEnabled = task.State != "Disabled"; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    // Bindable wrapper για WslService.WslDistro - το DefaultBadge προϋπολογίζεται εδώ (αντί για
    // converter στο XAML) ίδιο ελαφρύ μοτίβο με τα υπόλοιπα read-only rows σε αυτό το αρχείο.
    public class WslDistroRow
    {
        public WslService.WslDistro Distro { get; }
        public string Name => Distro.Name;
        public string State => Distro.State;
        public string Version => Distro.Version;
        public string DefaultBadge => Distro.IsDefault ? "★" : "";
        public WslDistroRow(WslService.WslDistro distro) => Distro = distro;
    }
}
