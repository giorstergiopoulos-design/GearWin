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
        private readonly ObservableCollection<CuratedFeatureRow> _curated = new();
        private List<AllFeatureRow> _allFeatures = new();
        private readonly ObservableCollection<AllFeatureRow> _displayedFeatures = new();
        private readonly ObservableCollection<GhostDeviceInfo> _ghostDevices = new();

        public AdvancedView()
        {
            InitializeComponent();
            ListCuratedFeatures.ItemsSource = _curated;
            ListAllFeatures.ItemsSource = _displayedFeatures;
            ListGhostDevices.ItemsSource = _ghostDevices;
            _ = LoadCuratedFeaturesAsync();
        }

        private async System.Threading.Tasks.Task LoadCuratedFeaturesAsync()
        {
            foreach (var (featureName, displayName) in AdvancedToolsService.CuratedFeatures)
            {
                var enabled = await AdvancedToolsService.IsFeatureEnabledAsync(featureName);
                _curated.Add(new CuratedFeatureRow(featureName, displayName) { IsEnabled = enabled });
            }
        }

        // ===== Συντήρηση & Διάγνωση =====

        private void BtnFullMaintenance_Click(object sender, RoutedEventArgs e) { AdvancedToolsService.FullMaintenance(); TxtMaintenanceStatus.Text = "Ξεκίνησε στο παρασκήνιο (temp+SFC+DISM) - μπορεί να διαρκέσει αρκετά λεπτά."; }
        private void BtnEnablePrivileges_Click(object sender, RoutedEventArgs e) { var ok = AdvancedToolsService.EnablePrivilege("SeDebugPrivilege"); TxtMaintenanceStatus.Text = ok ? "Ενεργοποιήθηκαν." : "Απέτυχε."; }
        private void BtnNetworkReset_Click(object sender, RoutedEventArgs e) { AdvancedToolsService.NetworkReset(); TxtMaintenanceStatus.Text = "Ξεκίνησε η επαναφορά δικτύου."; }

        private void BtnClearEventLogs_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Θα διαγραφούν ΟΛΑ τα Event Logs του συστήματος - ΜΗ αναστρέψιμο. Συνέχεια;", "ΠΡΟΣΟΧΗ", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var count = AdvancedToolsService.ClearEventLogs();
            TxtMaintenanceStatus.Text = $"Καθαρίστηκαν {count} αρχεία καταγραφής.";
        }

        private void BtnGc_Click(object sender, RoutedEventArgs e) { AdvancedToolsService.CollectGarbage(); TxtMaintenanceStatus.Text = "Ολοκληρώθηκε."; }
        private void BtnDeepDiagnostics_Click(object sender, RoutedEventArgs e) { var path = AdvancedToolsService.WriteDeepDiagnosticsReport(); TxtMaintenanceStatus.Text = $"Αποθηκεύτηκε: {path}"; }
        private void BtnDeviceManager_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.OpenDeviceManager();
        private void BtnWuTroubleshoot_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.OpenWindowsUpdateTroubleshooter();
        private void BtnSystemReport_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.CreateSystemReport();
        private void BtnResetWuServices_Click(object sender, RoutedEventArgs e) { AdvancedToolsService.ResetWindowsUpdateServices(); TxtMaintenanceStatus.Text = "Ξεκίνησε το reset υπηρεσιών Windows Update."; }
        private void BtnCleanXbox_Click(object sender, RoutedEventArgs e) { var count = AdvancedToolsService.CleanXboxCredentials(); TxtMaintenanceStatus.Text = $"Καθαρίστηκαν {count} διαπιστευτήρια Xbox."; }
        private void BtnInstallGpedit_Click(object sender, RoutedEventArgs e) { AdvancedToolsService.InstallGroupPolicyEditor(); TxtMaintenanceStatus.Text = "Ξεκίνησε η εγκατάσταση."; }

        // ===== Εργαλειοθήκη =====

        private void BtnSnip_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.OpenSnippingTool();
        private void BtnNotepad_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.OpenNotepad();
        private void BtnCalc_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.OpenCalculator();
        private void BtnControlPanel_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.OpenControlPanel();
        private void BtnTaskMgr_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.OpenTaskManager();

        private void BtnRegedit_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Ο Επεξεργαστής Μητρώου επιτρέπει άμεσες αλλαγές στο σύστημα - χρησιμοποιήστε με προσοχή. Συνέχεια;", "Προσοχή", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            AdvancedToolsService.OpenRegistryEditor();
        }

        private void BtnGpuReset_Click(object sender, RoutedEventArgs e) => AdvancedToolsService.GpuDriverSoftReset();

        // ===== Πρόσθετες Λειτουργίες (curated) =====

        private async void ToggleCuratedFeature_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton { Tag: CuratedFeatureRow row } button) return;
            if (MessageBox.Show($"{(row.IsEnabled ? "Ενεργοποίηση" : "Απενεργοποίηση")} της λειτουργίας '{row.DisplayName}' - μπορεί να χρειαστεί επανεκκίνηση και αρκετά λεπτά. Συνέχεια;",
                    "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                row.IsEnabled = !row.IsEnabled; // revert visual toggle
                return;
            }
            button.IsEnabled = false;
            StatusService.SetBusy($"{row.DisplayName}...");
            await AdvancedToolsService.SetFeatureEnabledAsync(row.FeatureName, row.IsEnabled);
            StatusService.SetIdle("Έτοιμο για χρήση");
            button.IsEnabled = true;
        }

        // ===== Όλες οι Λειτουργίες =====

        private async void BtnRefreshAllFeatures_Click(object sender, RoutedEventArgs e)
        {
            StatusService.SetBusy("Σάρωση λειτουργιών Windows...");
            _allFeatures = (await AdvancedToolsService.ScanAllFeaturesAsync()).Select(f => new AllFeatureRow(f.FeatureName) { IsEnabled = f.Enabled }).ToList();
            StatusService.SetIdle("Έτοιμο για χρήση");
            UpdateAllFeaturesDisplay();
        }

        private void TxtAllFeaturesSearch_TextChanged(object sender, TextChangedEventArgs e) => UpdateAllFeaturesDisplay();

        private void UpdateAllFeaturesDisplay()
        {
            _displayedFeatures.Clear();
            var term = TxtAllFeaturesSearch.Text?.Trim() ?? "";
            foreach (var f in _allFeatures.Where(f => term == "" || f.FeatureName.Contains(term, System.StringComparison.OrdinalIgnoreCase)).Take(100))
                _displayedFeatures.Add(f);
        }

        private async void ToggleAllFeature_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton { Tag: AllFeatureRow row } button) return;
            if (MessageBox.Show($"{(row.IsEnabled ? "Ενεργοποίηση" : "Απενεργοποίηση")} της λειτουργίας '{row.FeatureName}'. Συνέχεια;",
                    "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                row.IsEnabled = !row.IsEnabled;
                return;
            }
            button.IsEnabled = false;
            StatusService.SetBusy($"{row.FeatureName}...");
            await AdvancedToolsService.SetFeatureEnabledAsync(row.FeatureName, row.IsEnabled);
            StatusService.SetIdle("Έτοιμο για χρήση");
            button.IsEnabled = true;
        }

        // ===== Συσκευές-Φαντάσματα =====

        private async void BtnScanGhostDevices_Click(object sender, RoutedEventArgs e)
        {
            TxtGhostStatus.Text = "Σάρωση...";
            StatusService.SetBusy("Σάρωση συσκευών-φαντασμάτων...");
            _ghostDevices.Clear();
            var results = await AdvancedToolsService.ScanGhostDevicesAsync();
            StatusService.SetIdle("Έτοιμο για χρήση");
            foreach (var d in results) _ghostDevices.Add(d);
            TxtGhostStatus.Text = $"Βρέθηκαν {results.Count} συσκευές-φαντάσματα.";
        }

        // ΔΙΟΡΘΩΣΗ (ρητό αίτημα από deep review §0.4ιβ - ο ps1 original δεν είχε confirm dialog εδώ):
        // προστέθηκε επιβεβαίωση πριν την αφαίρεση.
        private async void BtnRemoveGhostDevice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: GhostDeviceInfo device } button) return;
            if (MessageBox.Show($"Αφαίρεση της συσκευής-φάντασμα '{device.FriendlyName}'; Μη αναστρέψιμο.", "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            button.IsEnabled = false;
            var ok = await AdvancedToolsService.RemoveGhostDeviceAsync(device.InstanceId);
            if (ok) _ghostDevices.Remove(device);
            else { button.IsEnabled = true; MessageBox.Show("Η αφαίρεση απέτυχε.", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }

    public class CuratedFeatureRow : INotifyPropertyChanged
    {
        public string FeatureName { get; }
        public string DisplayName { get; }
        private bool _isEnabled;
        public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; PropertyChanged?.Invoke(this, new(nameof(IsEnabled))); } }
        public CuratedFeatureRow(string featureName, string displayName) { FeatureName = featureName; DisplayName = displayName; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class AllFeatureRow : INotifyPropertyChanged
    {
        public string FeatureName { get; }
        private bool _isEnabled;
        public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; PropertyChanged?.Invoke(this, new(nameof(IsEnabled))); } }
        public AllFeatureRow(string featureName) => FeatureName = featureName;
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
