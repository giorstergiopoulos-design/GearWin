using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Win32;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class NetworkView : UserControl
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        // ΔΙΟΡΘΩΣΗ (bug εντοπίστηκε - χρήστης ανέφερε: "τα αποτελέσματα των σαρώσεων χάνονται όταν
        // αλλάζω καρτέλα") - static αντί για instance, ίδιο μοτίβο με τις υπόλοιπες καρτέλες - τα
        // κανόνες τείχους προστασίας/ανοιχτές θύρες ΔΕΝ φορτώνονται αυτόματα στον constructor.
        private static List<FirewallRule> _allRules = new();
        private static readonly ObservableCollection<FirewallRuleRow> _displayedRules = new();
        private static string? s_firewallStatusCache;
        private static IReadOnlyList<ListeningPort>? s_openPortsCache;
        private static string? s_openPortsStatusCache;

        // ΝΕΟ - roadmap "Κίνηση δικτύου ανά εφαρμογή" (v4.3.5).
        private readonly ObservableCollection<NetworkTrafficRowVm> _trafficRows = new();
        private DispatcherTimer? _trafficTimer;

        public NetworkView()
        {
            InitializeComponent();
            ListFirewallRules.ItemsSource = _displayedRules;
            ListNetworkTraffic.ItemsSource = _trafficRows;
            if (s_firewallStatusCache != null) TxtFirewallStatus.Text = s_firewallStatusCache;
            if (s_openPortsCache != null)
            {
                ListOpenPorts.ItemsSource = s_openPortsCache;
                TxtOpenPortsStatus.Text = s_openPortsStatusCache;
            }
            TxtHostsContent.Text = NetworkService.ReadHostsFile();
            RefreshTelemetryButton();
            _ = CheckPublicWifiAsync();
            _ = CheckDohAsync();

            // ΔΙΟΡΘΩΣΗ ("ελάφρυνση εφαρμογής", ίδιο μοτίβο με το HomeView) - κάθε επιστροφή σε αυτή
            // την καρτέλα δημιουργεί ΝΕΟ NetworkView instance· χωρίς αυτό, ένα ενεργό ETW session
            // (system-wide, ΜΟΝΑΔΙΚΟ - βλ. NetworkTrafficService) θα συνέχιζε να καταναλώνει CPU/
            // events στο παρασκήνιο κάθε φορά που ο χρήστης αλλάζει καρτέλα χωρίς να το σταματήσει
            // ρητά, ΚΑΙ θα εμπόδιζε την επόμενη Έναρξη (το ίδιο session name δεν μπορεί να ανοίξει
            // δύο φορές).
            Unloaded += (_, _) => StopTrafficMonitoring();
        }

        // ΝΕΟ - roadmap "Ανίχνευση δημόσιου Wi-Fi".
        private async Task CheckPublicWifiAsync()
        {
            if (await NetworkService.IsOnPublicNetworkAsync()) PublicWifiWarning.Visibility = Visibility.Visible;
        }

        // ΝΕΟ - roadmap "εντοπισμός ενεργού DoH" - βλ. NetworkService.IsDohEnabledAsync.
        private async Task CheckDohAsync()
        {
            if (await NetworkService.IsDohEnabledAsync()) TxtDohWarning.Visibility = Visibility.Visible;
        }

        // ΝΕΟ - roadmap "έλεγχος παραβιασμένων κωδικών" - βλ. HibpService. Ο κωδικός διαβάζεται ΜΟΝΟ
        // στη μνήμη για τη διάρκεια αυτής της κλήσης και καθαρίζεται αμέσως μετά - ΔΕΝ καταγράφεται σε
        // κανένα log/αρχείο της εφαρμογής.
        private async void BtnHibpCheck_Click(object sender, RoutedEventArgs e)
        {
            var password = PwdHibpCheck.Password;
            if (string.IsNullOrEmpty(password)) return;

            BtnHibpCheck.IsEnabled = false;
            TxtHibpResult.Text = LanguageService.T("Hibp_Checking");
            try
            {
                var count = await HibpService.CheckPasswordAsync(password);
                TxtHibpResult.Text = count switch
                {
                    < 0 => LanguageService.T("Hibp_Error"),
                    0 => LanguageService.T("Hibp_Safe"),
                    _ => $"{LanguageService.T("Hibp_FoundPrefix")}{count:N0}{LanguageService.T("Hibp_FoundSuffix")}",
                };
            }
            catch
            {
                TxtHibpResult.Text = LanguageService.T("Hibp_Error");
            }
            finally
            {
                PwdHibpCheck.Password = "";
                BtnHibpCheck.IsEnabled = true;
            }
        }

        // ΝΕΟ v3.2.0 - toggle button (όχι μόνο "Εφάρμοσε") ώστε να δείχνει πάντα την πραγματική
        // τρέχουσα κατάσταση του hosts file, ακόμα κι αν ο χρήστης το άλλαξε χειροκίνητα από αλλού.
        private void RefreshTelemetryButton()
        {
            var blocked = NetworkService.IsTelemetryBlocked();
            BtnToggleTelemetry.Content = blocked ? LanguageService.T("Net_TelemetryUnblockBtn") : LanguageService.T("Net_TelemetryBlockBtn");
            TxtTelemetryStatus.Text = blocked ? LanguageService.T("Net_TelemetryBlockedStatus") : LanguageService.T("Net_TelemetryNotBlockedStatus");
        }

        private void BtnToggleTelemetry_Click(object sender, RoutedEventArgs e)
        {
            var blocked = NetworkService.IsTelemetryBlocked();
            var msg = blocked ? LanguageService.T("Net_TelemetryUnblockConfirm") : LanguageService.T("Net_TelemetryBlockConfirm");
            if (ThemedMessageBox.Show(msg, LanguageService.T("Net_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var ok = blocked ? NetworkService.UnblockTelemetry() : NetworkService.BlockTelemetry();
            if (!ok) { ThemedMessageBox.Show(LanguageService.T("Net_TelemetryFailed"), LanguageService.T("Net_ConfirmTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            TxtHostsContent.Text = NetworkService.ReadHostsFile();
            RefreshTelemetryButton();
        }

        private async void BtnFullReset_Click(object sender, RoutedEventArgs e)
        {
            StatusService.SetBusy(LanguageService.T("Net_FullResetBusy"));
            TxtNetworkOutput.Text = await NetworkService.FullResetAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
        }

        private async void BtnIpConfig_Click(object sender, RoutedEventArgs e) => TxtNetworkOutput.Text = await NetworkService.GetIpConfigAllAsync();
        private async void BtnRoutingTable_Click(object sender, RoutedEventArgs e) => TxtNetworkOutput.Text = await NetworkService.GetRoutingTableAsync();

        private async void BtnRestartWifi_Click(object sender, RoutedEventArgs e)
        {
            // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): καμία επιβεβαίωση πριν από μια διακοπτική
            // ενέργεια (χάνεται προσωρινά η σύνδεση Wi-Fi) - όλες οι υπόλοιπες τέτοιες ενέργειες στην
            // εφαρμογή ζητούν επιβεβαίωση, ΚΑΙ καμία ένδειξη ολοκλήρωσης μετά.
            if (ThemedMessageBox.Show(LanguageService.T("Net_RestartWifiConfirm"),
                    LanguageService.T("Net_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            StatusService.SetBusy(LanguageService.T("Net_RestartingWifi"));
            var ok = await NetworkService.RestartWifiAdaptersAsync();
            StatusService.SetIdle(ok ? LanguageService.T("Net_WifiRestartedOk") : LanguageService.T("Net_WifiRestartFailed"));
        }

        // ΝΕΟ - roadmap "Γρήγορη εναλλαγή DNS" - βλ. NetworkService.SetDnsAsync/ResetDnsAsync.
        private async Task ApplyDnsAsync(string label, System.Func<Task<bool>> apply)
        {
            StatusService.SetBusy($"{LanguageService.T("Network_DnsApplying")} {label}...");
            var ok = await apply();
            var msg = ok ? $"{LanguageService.T("Network_DnsAppliedPrefix")}{label}" : LanguageService.T("Network_DnsFailed");
            TxtDnsStatus.Text = msg;
            StatusService.SetIdle(LanguageService.T("Ready"));
        }

        private Task BtnDnsCloudflare_ClickAsync() => ApplyDnsAsync("Cloudflare", () => NetworkService.SetDnsAsync("1.1.1.1", "1.0.0.1"));
        private void BtnDnsCloudflare_Click(object sender, RoutedEventArgs e) => _ = BtnDnsCloudflare_ClickAsync();
        private Task BtnDnsGoogle_ClickAsync() => ApplyDnsAsync("Google", () => NetworkService.SetDnsAsync("8.8.8.8", "8.8.4.4"));
        private void BtnDnsGoogle_Click(object sender, RoutedEventArgs e) => _ = BtnDnsGoogle_ClickAsync();
        private Task BtnDnsQuad9_ClickAsync() => ApplyDnsAsync("Quad9", () => NetworkService.SetDnsAsync("9.9.9.9", "149.112.112.112"));
        private void BtnDnsQuad9_Click(object sender, RoutedEventArgs e) => _ = BtnDnsQuad9_ClickAsync();
        private Task BtnDnsReset_ClickAsync() => ApplyDnsAsync(LanguageService.T("Network_DnsReset"), NetworkService.ResetDnsAsync);
        private void BtnDnsReset_Click(object sender, RoutedEventArgs e) => _ = BtnDnsReset_ClickAsync();

        private void BtnOpenFirewall_Click(object sender, RoutedEventArgs e) => NetworkService.OpenFirewallManager();

        private async void BtnLoadFirewallRules_Click(object sender, RoutedEventArgs e)
        {
            TxtFirewallStatus.Text = LanguageService.T("Net_LoadingRules");
            StatusService.SetBusy(LanguageService.T("Net_LoadingFirewallRules"));
            _allRules = (await NetworkService.LoadFirewallRulesAsync()).ToList();
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtFirewallStatus.Text = $"{LanguageService.T("Net_RulesLoadedPrefix")}{_allRules.Count}{LanguageService.T("Net_RulesLoadedSuffix")}";
            s_firewallStatusCache = TxtFirewallStatus.Text;
            UpdateFirewallDisplay();
        }

        private async void BtnLoadOpenPorts_Click(object sender, RoutedEventArgs e)
        {
            TxtOpenPortsStatus.Text = LanguageService.T("Net_LoadingRules");
            StatusService.SetBusy(LanguageService.T("Network_OpenPorts"));
            var ports = await NetworkService.LoadListeningPortsAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            ListOpenPorts.ItemsSource = ports;
            TxtOpenPortsStatus.Text = $"{LanguageService.T("Net_RulesLoadedPrefix")}{ports.Count}{LanguageService.T("Network_OpenPortsSuffix")}";
            s_openPortsCache = ports;
            s_openPortsStatusCache = TxtOpenPortsStatus.Text;
        }

        private void TxtFirewallSearch_TextChanged(object sender, TextChangedEventArgs e) => UpdateFirewallDisplay();

        private void UpdateFirewallDisplay()
        {
            _displayedRules.Clear();
            var term = TxtFirewallSearch.Text?.Trim();
            if (string.IsNullOrEmpty(term))
            {
                // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): η γραμμή κατάστασης ΔΕΝ ενημερωνόταν ποτέ
                // μετά τη φόρτωση - έμενε μόνιμα στο αρχικό μήνυμα ακόμα κι όταν η αναζήτηση δεν έβρισκε
                // τίποτα (ή βρήκε λιγότερα από τον συνολικό αριθμό κανόνων).
                if (_allRules.Count > 0) TxtFirewallStatus.Text = $"{LanguageService.T("Net_RulesLoadedPrefix")}{_allRules.Count}{LanguageService.T("Net_RulesLoadedSuffix")}";
                return;
            }
            var matches = _allRules.Where(r => r.DisplayName.Contains(term, System.StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var r in matches.Take(50)) _displayedRules.Add(new FirewallRuleRow(r));
            TxtFirewallStatus.Text = matches.Count == 0
                ? LanguageService.T("Net_NoResults")
                : matches.Count > 50 ? $"{matches.Count}{LanguageService.T("Net_ResultsCappedSuffix")}" : $"{matches.Count}{LanguageService.T("Net_ResultsSuffix")}";
        }

        private async void ToggleFirewallRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton { Tag: FirewallRuleRow row }) return;
            var ok = await NetworkService.SetFirewallRuleEnabledAsync(row.Rule.Name, row.IsEnabled);
            if (!ok) ThemedMessageBox.Show(LanguageService.T("Net_ChangeFailed"), LanguageService.T("Net_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async void BtnExportFirewallPolicy_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "Firewall Policy (*.wfw)|*.wfw", FileName = $"FirewallPolicy_{System.DateTime.Now:yyyy-MM-dd}.wfw" };
            if (dlg.ShowDialog() != true) return;
            var ok = await NetworkService.ExportFirewallPolicyAsync(dlg.FileName);
            TxtFirewallToolsStatus.Text = ok ? LanguageService.T("Net_ExportDone") : LanguageService.T("Net_ExportFailed");
        }

        private async void BtnImportFirewallPolicy_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Firewall Policy (*.wfw)|*.wfw" };
            if (dlg.ShowDialog() != true) return;
            if (ThemedMessageBox.Show(LanguageService.T("Net_ImportConfirm"), LanguageService.T("Net_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var ok = await NetworkService.ImportFirewallPolicyAsync(dlg.FileName);
            TxtFirewallToolsStatus.Text = ok ? LanguageService.T("Net_ImportDone") : LanguageService.T("Net_ImportFailed");
            _allRules.Clear();
        }

        private async void BtnResetFirewall_Click(object sender, RoutedEventArgs e)
        {
            if (ThemedMessageBox.Show(LanguageService.T("Net_ResetFirewallConfirm"),
                    LanguageService.T("Net_WarningTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var ok = await NetworkService.ResetFirewallAsync();
            TxtFirewallToolsStatus.Text = ok ? LanguageService.T("Net_FirewallReset") : LanguageService.T("Net_Failed");
            _allRules.Clear();
        }

        private void BtnHostsLoad_Click(object sender, RoutedEventArgs e) => TxtHostsContent.Text = NetworkService.ReadHostsFile();

        private void BtnHostsSave_Click(object sender, RoutedEventArgs e)
        {
            if (ThemedMessageBox.Show(LanguageService.T("Net_HostsSaveConfirm"),
                    LanguageService.T("Net_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var ok = NetworkService.SaveHostsFile(TxtHostsContent.Text);
            ThemedMessageBox.Show(ok ? LanguageService.T("Net_Saved") : LanguageService.T("Net_Failed"), LanguageService.T("Net_HostsFileTitle"), MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Error);
        }

        private void BtnHostsReset_Click(object sender, RoutedEventArgs e)
        {
            if (ThemedMessageBox.Show(LanguageService.T("Net_HostsResetConfirm"),
                    LanguageService.T("Net_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
            TxtHostsContent.Text = NetworkService.DefaultHostsContent;
        }

        private void BtnHostsNotepad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", $"\"{NetworkService.HostsFilePath}\"") { UseShellExecute = true });
            }
            catch (System.Exception ex)
            {
                // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): καμία προστασία - αδιαχείριστη εξαίρεση αν
                // αποτύχει η εκκίνηση του Σημειωματάριου.
                ThemedMessageBox.Show($"{LanguageService.T("Net_NotepadOpenFailedPrefix")}{ex.Message}", LanguageService.T("Net_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnQuickScan_Click(object sender, RoutedEventArgs e)
        {
            NetworkService.StartQuickScan();
            // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): καμία ένδειξη ότι πατήθηκε το κουμπί - η ίδια η
            // σάρωση τρέχει στο Windows Security app (ζωντανή πρόοδος εκεί, ίδιο με ps1 original), αλλά
            // η εφαρμογή δεν επιβεβαίωνε καν ότι ξεκίνησε.
            StatusService.SetIdle(LanguageService.T("Net_DefenderScanStarted"));
        }

        // ===== Κίνηση δικτύου ανά εφαρμογή (v4.3.5) - βλ. NetworkTrafficService =====

        private void BtnToggleTraffic_Click(object sender, RoutedEventArgs e)
        {
            if (NetworkTrafficService.IsRunning) StopTrafficMonitoring();
            else StartTrafficMonitoring();
        }

        private void StartTrafficMonitoring()
        {
            if (!NetworkTrafficService.Start())
            {
                // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: αν αποτύχει η δημιουργία του ETW session (π.χ. δεν τρέχει ως
                // Administrator παρόλο που κανονικά η εφαρμογή απαιτεί elevation, ή κάποιο άλλο
                // εργαλείο κρατά αποκλειστικά το session) - ρητό μήνυμα αποτυχίας, ΟΧΙ σιωπηλή κενή
                // λίστα που θα έδειχνε ψευδώς "καμία κίνηση δικτύου".
                TxtTrafficStatus.Text = $"{LanguageService.T("Network_TrafficUnavailable")}{NetworkTrafficService.LastError}";
                PanelTrafficList.Visibility = Visibility.Collapsed;
                return;
            }

            BtnToggleTraffic.Content = LanguageService.T("Network_TrafficStop");
            TxtTrafficStatus.Text = LanguageService.T("Network_TrafficMonitoring");
            PanelTrafficList.Visibility = Visibility.Visible;
            _trafficRows.Clear();

            _trafficTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _trafficTimer.Tick += (_, _) => RefreshTrafficRows();
            _trafficTimer.Start();
        }

        private void StopTrafficMonitoring()
        {
            _trafficTimer?.Stop();
            _trafficTimer = null;
            NetworkTrafficService.Stop();
            BtnToggleTraffic.Content = LanguageService.T("Network_TrafficStart");
            TxtTrafficStatus.Text = "";
        }

        private void RefreshTrafficRows()
        {
            var usage = NetworkTrafficService.SampleAndReset(1.0);
            _trafficRows.Clear();
            // Cap στα top 30 - ίδιο σκεπτικό με το ήδη υπάρχον cap 50 στα αποτελέσματα αναζήτησης
            // firewall κανόνων: ο χρήστης ενδιαφέρεται για ΠΟΙΟΣ καταναλώνει εύρος ζώνης ΤΩΡΑ, όχι
            // για μια πλήρη λίστα δεκάδων αδρανών διεργασιών με 0 KB/s.
            foreach (var u in usage.Take(30)) _trafficRows.Add(new NetworkTrafficRowVm(u));
        }
    }

    public class NetworkTrafficRowVm
    {
        public int Pid { get; }
        public string ProcessName { get; }
        public string DownloadDisplay { get; }
        public string UploadDisplay { get; }

        public NetworkTrafficRowVm(ProcessNetworkUsage usage)
        {
            Pid = usage.Pid;
            ProcessName = usage.ProcessName;
            DownloadDisplay = FormatRate(usage.DownloadKBps);
            UploadDisplay = FormatRate(usage.UploadKBps);
        }

        private static string FormatRate(double kbps) =>
            kbps >= 1024 ? $"{kbps / 1024.0:0.0} MB/s" : $"{kbps:0.0} KB/s";
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
