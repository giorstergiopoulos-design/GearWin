using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using OptimizerWpf.Services;
using Wpf.Ui.Controls;
// NavItem/NavItems/SidebarShortcut/SidebarShortcuts ζουν στο ριζικό namespace OptimizerWpf (ίδιο με
// MainWindow.xaml.cs), όχι στο OptimizerWpf.Views - κανένα άλλο αρχείο στο Views/ τα είχε ξαναχρειαστεί.
using OptimizerWpf;

namespace OptimizerWpf.Views
{
    // Μία γραμμή της "Βαθμολογίας Υγείας" (Δίσκος/Ασφάλεια/Εκκίνηση/Οδηγοί) - βλ. σχόλιο στο XAML.
    public record HealthRowVm(string Label, string Status, Brush DotColor);

    // Ένα "καρφιτσωμένο" tweak σαν πλακίδιο - ίδια δεδομένα με το HomeView's LoadPinnedTweaks (TweakRowVm/
    // AppSettingsService.PinnedTweakKeys), όχι το "pin ένα εργαλείο/παράθυρο" του mockup (δεν υπάρχει
    // τέτοιο σύστημα ακόμα στην εφαρμογή) - SubLabel = Tweak.Description (πραγματικό κείμενο, όχι εικασία).
    public record PinnedShortcutVm(GlyphKind IconKind, string Label, string SubLabel);

    // ΝΕΟ (GEARWIN.MD, ρητό αίτημα χρήστη: "κάνε τον πίνακα ελέγχου που είναι το κύριο παράθυρο της
    // εφαρμογής & βλέπουμε") - πιλοτικό WPF-UI παράθυρο, βλ. πλήρες σχόλιο στο XAML. Ανοίγει μόνο με
    // "--dashboard-preview" (App.xaml.cs) - δεν αγγίζει το κανονικό MainWindow/startup με κανέναν τρόπο.
    public partial class DashboardPreviewWindow : FluentWindow
    {
        // Η ui:ThemesDictionary μέσα στο Resources.MergedDictionaries δεν παίρνει πεδίο code-behind
        // μέσω x:Name (δεν είναι FrameworkElement) - προσπελάσιμη μόνο ως το πρώτο στοιχείο της λίστας
        // (η σειρά στο XAML είναι σταθερή/ελεγχόμενη εδώ).
        private Wpf.Ui.Markup.ThemesDictionary WpfUiThemeDictionary => (Wpf.Ui.Markup.ThemesDictionary)Resources.MergedDictionaries[0];

        public DashboardPreviewWindow()
        {
            InitializeComponent();

            TxtSidebarVersion.Text = App.DisplayVersion;
            var wpfUiTheme = ThemeManager.IsDarkMode ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light;
            WpfUiThemeDictionary.Theme = wpfUiTheme;
            // ΔΙΟΡΘΩΣΗ (χρήστης είδε screenshot: "φτιάξε τα χρώματα") - χωρίς αυτό, τα ui:Button
            // Appearance="Primary" κ.λπ. χρησιμοποιούν το ΔΙΚΟ ΤΟΥΣ προεπιλεγμένο accent (μπλε) του
            // WPF-UI αντί για το πραγματικό accent (π.χ. τιρκουάζ) του τρέχοντος θέματος της εφαρμογής -
            // ApplicationAccentColorManager είναι η δημόσια, τεκμηριωμένη μέθοδος του ίδιου του πακέτου
            // γι' αυτό ακριβώς (swap τα dynamic resources που διαβάζουν τα WPF-UI controls).
            Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(ThemeManager.CurrentTheme.Accent, wpfUiTheme, systemGlassColor: false, systemAccentColor: false);

            ListTabs.ItemsSource = NavItems.All;
            ListShortcuts.ItemsSource = SidebarShortcuts.All;
            LoadPinnedShortcuts();

            // ΔΙΟΡΘΩΣΗ (χρήστης είδε screenshot: "φτιάξε... το κινούμενο φόντο") - έλειπε η σύνδεση του
            // ThemedBackgroundControl με τον ThemeManager (ίδια κλήση με το SplashWindow_Loaded).
            Loaded += (_, _) => ThemeManager.AttachBackground(DashboardBackground);
            Loaded += async (_, _) => await RefreshAsync();
        }

        // ── Πλευρικό μενού ────────────────────────────────────────────────────────────────

        // Απλοποιημένη σύμπτυξη (μόνο πλάτος στήλης + απόκρυψη ετικετών ομάδων/κεφαλίδας) - το
        // ΚΑΝΟΝΙΚΟ, ήδη δοκιμασμένο rail-collapse μηχανισμό της εφαρμογής (MainWindow.xaml's
        // RailCollapsed/MotionDesk-στυλ animation) δεν αντιγράφεται εδώ 1-προς-1, αφού αυτό είναι ένα
        // πιλοτικό/οπτικό πέρασμα - βλ. σχόλιο στο XAML.
        private bool _collapsed;
        private void BtnCollapseSidebar_Click(object sender, RoutedEventArgs e)
        {
            _collapsed = !_collapsed;
            SidebarColumn.Width = new GridLength(_collapsed ? 76 : 260);
            SidebarTitleBlock.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
            TxtTabsGroupLabel.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
            TxtShortcutsGroupLabel.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
            BtnCollapseSidebar.Content = _collapsed ? "»" : "Σύμπτυξη μενού";
        }

        private void NavRow_Click(object sender, RoutedEventArgs e)
        {
            // Μόνο ο "Πίνακας Ελέγχου" (Home) είναι πραγματικά συνδεδεμένος σε αυτό το πιλοτικό
            // παράθυρο - οι άλλες 8 καρτέλες/6 συντομεύσεις δείχνουν σωστά (πραγματικά NavItems/
            // SidebarShortcuts) αλλά δεν ανοίγουν ακόμα δικό τους περιεχόμενο εδώ (εκτός σκοπού: "κάνε
            // ΤΟΝ ΠΙΝΑΚΑ ΕΛΕΓΧΟΥ", όχι ολόκληρη την εφαρμογή σε WPF-UI).
            if (sender is not System.Windows.Controls.RadioButton { Tag: NavItem item } || item.Tag != "Home") return;
        }

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ToggleLightDark();
            var wpfUiTheme = ThemeManager.IsDarkMode ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light;
            WpfUiThemeDictionary.Theme = wpfUiTheme;
            Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(ThemeManager.CurrentTheme.Accent, wpfUiTheme, systemGlassColor: false, systemAccentColor: false);
        }

        // ── Κεφαλίδα / ενημερώσεις ────────────────────────────────────────────────────────

        private async Task RefreshAsync()
        {
            RenderUpdatesSummary();
            await RefreshHealthAsync();
        }

        private void RenderUpdatesSummary()
        {
            var app = UpdatesHubService.AppUpdatesAvailable;
            var drv = UpdatesHubService.DriverUpdatesAvailable;
            TxtAppUpdateCount.Text = app?.ToString() ?? "—";
            TxtDriverUpdateCount.Text = drv?.ToString() ?? "—";

            var total = (app ?? 0) + (drv ?? 0);
            if (app == null && drv == null)
            {
                TxtUpdatesPill.Text = "Έλεγχος ενημερώσεων";
                UpdatesInfoBar.IsOpen = false;
            }
            else if (total == 0)
            {
                TxtUpdatesPill.Text = "Ενημερωμένο";
                UpdatesInfoBar.IsOpen = false;
            }
            else
            {
                TxtUpdatesPill.Text = $"{total} ενημερώσεις";
                UpdatesInfoBar.Severity = InfoBarSeverity.Informational;
                UpdatesInfoBar.Title = "Βρέθηκαν ενημερώσεις";
                UpdatesInfoBar.Message = $"{app ?? 0} εφαρμογές και {drv ?? 0} οδηγοί περιμένουν αναβάθμιση.";
                UpdatesInfoBar.IsOpen = true;
            }
        }

        private void BtnUpdatesPill_Click(object sender, RoutedEventArgs e) => _ = BtnReviewUpgradeAsync();
        private void BtnReviewUpgrade_Click(object sender, RoutedEventArgs e) => _ = BtnReviewUpgradeAsync();
        private void BtnIgnoreOnce_Click(object sender, RoutedEventArgs e) => UpdatesInfoBar.IsOpen = false;

        // Πραγματική σάρωση winget (ίδια WingetService.ScanAsync() με την καρτέλα Βελτιστοποίηση) - τα
        // ονόματα εφαρμογών εδώ είναι πάντα πραγματικά, ποτέ παράδειγμα/εικασία (ίδιο ήθος με το
        // NetworkTrafficService/ImpactTrackingService αλλού στην εφαρμογή).
        private async Task BtnReviewUpgradeAsync()
        {
            BtnReviewUpgrade.IsEnabled = false;
            TxtUpdatesStatus.Text = "Έλεγχος ενημερώσεων λογισμικού (winget)...";
            try
            {
                var results = await WingetService.ScanAsync();
                UpdatesHubService.ReportAppScan(results.Count);
                TxtAppUpdateNames.Text = results.Count == 0 ? "" : string.Join(", ", results.Take(4).Select(r => r.Name)) + (results.Count > 4 ? $" +{results.Count - 4}" : "");
                TxtUpdatesStatus.Text = results.Count == 0 ? "Καμία ενημέρωση λογισμικού." : $"Βρέθηκαν {results.Count} ενημερώσεις λογισμικού.";
            }
            catch (Exception ex) { TxtUpdatesStatus.Text = ex.Message; }
            finally { BtnReviewUpgrade.IsEnabled = true; }
            RenderUpdatesSummary();
        }

        // ── Βαθμολογία Υγείας ─────────────────────────────────────────────────────────────

        private async Task RefreshHealthAsync()
        {
            var result = await Task.Run(HealthScoreService.Compute);
            TxtScore.Text = result.Score.ToString();
            var good = (Brush)FindResource("AccentBrush");
            var warn = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            var bad = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));
            ScoreArc.Data = PcManagerHomeView.BuildArc(result.Score / 100.0, 50, 45.5);
            ScoreArc.Stroke = result.Score >= 80 ? good : result.Score >= 60 ? warn : bad;

            bool Has(string fixType) => result.Issues.Any(i => i.FixType == fixType);

            var drv = UpdatesHubService.DriverUpdatesAvailable;
            var rows = new[]
            {
                new HealthRowVm("Δίσκος & Αποθήκευση", Has("Storage") ? "Έλεγχος" : "Καλή", Has("Storage") ? warn : good),
                new HealthRowVm("Ασφάλεια", Has("Defender") ? "Έλεγχος" : Has("DefenderOtherAV") ? "Ενεργό (τρίτο AV)" : "Καλή", Has("Defender") ? bad : good),
                new HealthRowVm("Εκκίνηση & Υπηρεσίες", Has("Startup") ? "Πολλά στοιχεία" : "Καλή", Has("Startup") ? warn : good),
                new HealthRowVm("Οδηγοί Συσκευών", drv == null ? "Άγνωστο" : drv == 0 ? "Ενημερωμένοι" : $"{drv} εκκρεμούν", drv is null or 0 ? good : warn),
            };
            ListHealthRows.ItemsSource = rows;
        }

        // ── Καρφιτσωμένες συντομεύσεις ────────────────────────────────────────────────────

        private void LoadPinnedShortcuts()
        {
            var pins = AppSettingsService.Current.PinnedTweakKeys;
            var all = TweakService.AllMainTweaks().Select(t => new TweakRowVm(t, "Main"))
                .Concat(TweakService.AiCopilotTweaksSimple().Select(t => new TweakRowVm(t, "Ai")))
                .Concat(TweakService.PerfTweaksSimple().Select(t => new TweakRowVm(t, "Perf")))
                .Concat(TweakService.LighterWindowsTweaksSimple().Select(t => new TweakRowVm(t, "Lighter")))
                .Where(r => pins.Contains(r.PinKey) || pins.Contains(r.LegacyPinKey))
                .Select(r => new PinnedShortcutVm(GlyphKind.Wrench, r.Tweak.Label, r.Tweak.Description))
                .ToList();

            ListPinnedShortcuts.ItemsSource = all;
            TxtNoPinned.Visibility = all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
