using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class NetworkView : UserControl
    {
        private List<FirewallRule> _allRules = new();
        private readonly ObservableCollection<FirewallRuleRow> _displayedRules = new();

        public NetworkView()
        {
            InitializeComponent();
            ListFirewallRules.ItemsSource = _displayedRules;
            TxtHostsContent.Text = NetworkService.ReadHostsFile();
        }

        private async void BtnFullReset_Click(object sender, RoutedEventArgs e)
        {
            StatusService.SetBusy("Πλήρης επαναφορά δικτύου...");
            TxtNetworkOutput.Text = await NetworkService.FullResetAsync();
            StatusService.SetIdle("Έτοιμο για χρήση");
        }

        private async void BtnIpConfig_Click(object sender, RoutedEventArgs e) => TxtNetworkOutput.Text = await NetworkService.GetIpConfigAllAsync();
        private async void BtnRoutingTable_Click(object sender, RoutedEventArgs e) => TxtNetworkOutput.Text = await NetworkService.GetRoutingTableAsync();

        private async void BtnRestartWifi_Click(object sender, RoutedEventArgs e)
        {
            StatusService.SetBusy("Επανεκκίνηση προσαρμογέων Wi-Fi...");
            await NetworkService.RestartWifiAdaptersAsync();
            StatusService.SetIdle("Έτοιμο για χρήση");
        }

        private void BtnOpenFirewall_Click(object sender, RoutedEventArgs e) => NetworkService.OpenFirewallManager();

        private async void BtnLoadFirewallRules_Click(object sender, RoutedEventArgs e)
        {
            TxtFirewallStatus.Text = "Φόρτωση κανόνων...";
            StatusService.SetBusy("Φόρτωση κανόνων firewall...");
            _allRules = (await NetworkService.LoadFirewallRulesAsync()).ToList();
            StatusService.SetIdle("Έτοιμο για χρήση");
            TxtFirewallStatus.Text = $"Φορτώθηκαν {_allRules.Count} κανόνες - πληκτρολογήστε για αναζήτηση.";
            UpdateFirewallDisplay();
        }

        private void TxtFirewallSearch_TextChanged(object sender, TextChangedEventArgs e) => UpdateFirewallDisplay();

        private void UpdateFirewallDisplay()
        {
            _displayedRules.Clear();
            var term = TxtFirewallSearch.Text?.Trim();
            if (string.IsNullOrEmpty(term)) return;
            foreach (var r in _allRules.Where(r => r.DisplayName.Contains(term, System.StringComparison.OrdinalIgnoreCase)).Take(50))
                _displayedRules.Add(new FirewallRuleRow(r));
        }

        private async void ToggleFirewallRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton { Tag: FirewallRuleRow row }) return;
            var ok = await NetworkService.SetFirewallRuleEnabledAsync(row.Rule.Name, row.IsEnabled);
            if (!ok) MessageBox.Show("Η αλλαγή απέτυχε.", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async void BtnExportFirewallPolicy_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "Firewall Policy (*.wfw)|*.wfw", FileName = $"FirewallPolicy_{System.DateTime.Now:yyyy-MM-dd}.wfw" };
            if (dlg.ShowDialog() != true) return;
            var ok = await NetworkService.ExportFirewallPolicyAsync(dlg.FileName);
            TxtFirewallToolsStatus.Text = ok ? "Η εξαγωγή ολοκληρώθηκε." : "Η εξαγωγή απέτυχε.";
        }

        private async void BtnImportFirewallPolicy_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Firewall Policy (*.wfw)|*.wfw" };
            if (dlg.ShowDialog() != true) return;
            if (MessageBox.Show("Θα αντικατασταθεί ΟΛΗ η τρέχουσα πολιτική firewall. Συνέχεια;", "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var ok = await NetworkService.ImportFirewallPolicyAsync(dlg.FileName);
            TxtFirewallToolsStatus.Text = ok ? "Η εισαγωγή ολοκληρώθηκε." : "Η εισαγωγή απέτυχε.";
            _allRules.Clear();
        }

        private async void BtnResetFirewall_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Θα διαγραφούν ΟΛΟΙ οι προσαρμοσμένοι κανόνες firewall - ΜΗ αναστρέψιμο χωρίς προηγούμενη εξαγωγή. Συνέχεια;",
                    "ΠΡΟΣΟΧΗ", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var ok = await NetworkService.ResetFirewallAsync();
            TxtFirewallToolsStatus.Text = ok ? "Η πολιτική firewall επαναφέρθηκε στις προεπιλογές." : "Απέτυχε.";
            _allRules.Clear();
        }

        private void BtnHostsLoad_Click(object sender, RoutedEventArgs e) => TxtHostsContent.Text = NetworkService.ReadHostsFile();

        private void BtnHostsSave_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Το αρχείο hosts θα αντικατασταθεί με το περιεχόμενο του πλαισίου (θα γίνει αυτόματο αντίγραφο ασφαλείας). Συνέχεια;",
                    "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var ok = NetworkService.SaveHostsFile(TxtHostsContent.Text);
            MessageBox.Show(ok ? "Αποθηκεύτηκε." : "Απέτυχε.", "Αρχείο Hosts", MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Error);
        }

        private void BtnHostsReset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Το πλαίσιο θα γεμίσει με το προεπιλεγμένο περιεχόμενο hosts των Windows (δεν αποθηκεύεται αυτόματα). Συνέχεια;",
                    "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
            TxtHostsContent.Text = NetworkService.DefaultHostsContent;
        }

        private void BtnHostsNotepad_Click(object sender, RoutedEventArgs e) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", $"\"{NetworkService.HostsFilePath}\"") { UseShellExecute = true });

        private void BtnQuickScan_Click(object sender, RoutedEventArgs e) => NetworkService.StartQuickScan();
    }

    public class FirewallRuleRow : INotifyPropertyChanged
    {
        public FirewallRule Rule { get; }
        private bool _isEnabled;
        public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; PropertyChanged?.Invoke(this, new(nameof(IsEnabled))); } }
        public FirewallRuleRow(FirewallRule rule) { Rule = rule; _isEnabled = rule.Enabled; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
