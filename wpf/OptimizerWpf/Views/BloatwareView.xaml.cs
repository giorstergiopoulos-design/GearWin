using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class BloatwareView : UserControl
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        private void TxtRecommendedSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var term = TxtRecommendedSearch.Text?.Trim();
            _recommendedView.Filter = string.IsNullOrEmpty(term)
                ? null
                : o => o is RecommendedAppRow r && r.App.Title.Contains(term, System.StringComparison.OrdinalIgnoreCase);
        }

        private void TxtInstalledAppsSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var term = TxtInstalledAppsSearch.Text?.Trim();
            _installedAppsView.Filter = string.IsNullOrEmpty(term)
                ? null
                : o => o is DeepUninstallRow r && r.App.DisplayName.Contains(term, System.StringComparison.OrdinalIgnoreCase);
        }

        // ΝΕΟ - ρητό αίτημα χρήστη: "να μην ξανασκαναρονται καθε φορα (προτεινομενες εφαρμογες
        // winget)" - το BloatwareView δημιουργείται εξ αρχής σε κάθε επίσκεψη της καρτέλας (βλ.
        // MainWindow.ShowTabContent), οπότε χωρίς αυτή τη στατική cache ο σειριακός έλεγχος winget ανά
        // εφαρμογή (CheckRecommendedInstalledAsync) επαναλαμβανόταν από την αρχή κάθε φορά. Η cache
        // ζει όσο τρέχει η εφαρμογή· ενημερώνεται ΚΑΙ από τα Install/Uninstall handlers παρακάτω ώστε
        // να μην ξεμείνει stale μέσα στην ίδια συνεδρία.
        private static readonly Dictionary<string, bool> s_recommendedInstalledCache = new();
        private static bool s_recommendedScanned;

        private readonly ObservableCollection<BuiltinAppRow> _builtin = new();
        private readonly ObservableCollection<RecommendedAppRow> _recommended = new();
        // ΔΙΟΡΘΩΣΗ (bug εντοπίστηκε - χρήστης ανέφερε: "τα αποτελέσματα των σαρώσεων χάνονται όταν
        // αλλάζω καρτέλα") - static αντί για instance, ίδιο μοτίβο με OptimizationView/SystemView/
        // AdvancedView - το Deep Uninstall scan (BtnRefreshInstalledApps_Click) ΔΕΝ τρέχει αυτόματα
        // στον constructor (σε αντίθεση με το _recommended παραπάνω), οπότε χωρίς αυτό η λίστα
        // εμφανιζόταν εντελώς άδεια σε κάθε επιστροφή στην καρτέλα, ζητώντας νέο χειροκίνητο κλικ.
        private static readonly ObservableCollection<DeepUninstallRow> _installedApps = new();
        private readonly ListCollectionView _recommendedView;
        private readonly ICollectionView _installedAppsView;

        // ===== Πρόσθετες Λειτουργίες Windows (ενοποιημένη λίστα - μετακινήθηκε εδώ από την καρτέλα
        // Προηγμένα Εργαλεία, ρητό αίτημα χρήστη - ίδια ουσιαστικά λειτουργία με τα "Ενσωματωμένα
        // Στοιχεία Windows" παραπάνω) - static για τον ίδιο λόγο όπως παραπάνω, το scan δεν τρέχει
        // αυτόματα στον constructor. =====
        private static List<AllFeatureRow> _allFeatures = new();
        private static readonly ObservableCollection<AllFeatureRow> _displayedFeatures = new();

        public BloatwareView()
        {
            InitializeComponent();
            ListBuiltin.ItemsSource = _builtin;
            ListAllFeatures.ItemsSource = _displayedFeatures;
            LoadPopularFeatureChips();
            // Ρητό αίτημα χρήστη: "στις προτεινόμενες εφαρμογές υπήρχε ομαδοποίηση... να είναι
            // διακριτά τα είδη" - ομαδοποίηση κατά App.Group (ίδιες ομάδες με το ps1 original) μέσω
            // ListCollectionView, βλ. ItemsControl.GroupStyle στο XAML για τις επικεφαλίδες.
            _recommendedView = new ListCollectionView(_recommended);
            _recommendedView.GroupDescriptions.Add(new PropertyGroupDescription("App.Group"));
            ListRecommended.ItemsSource = _recommendedView;

            // Ρητό αίτημα χρήστη (πρόταση βελτίωσης που εγκρίθηκε: "κάνε τα όλα") - πεδία αναζήτησης
            // στις δύο μεγαλύτερες, πιο δύσκολο-να-σαρωθούν λίστες της καρτέλας.
            _installedAppsView = CollectionViewSource.GetDefaultView(_installedApps);
            ListInstalledApps.ItemsSource = _installedAppsView;

            _builtin.Add(new BuiltinAppRow("Microsoft Copilot", "Copilot"));
            _builtin.Add(new BuiltinAppRow("Xbox Gaming Overlay", "XboxGamingOverlay"));
            _builtin.Add(new BuiltinAppRow(LanguageService.T("Bloatware_NewsWeather"), "BingNews"));
            _builtin.Add(new BuiltinAppRow("Windows Media Player (Legacy)", "WMP"));
            _builtin.Add(new BuiltinAppRow(LanguageService.T("Bloatware_PhotoViewerClassic"), "PhotoViewer"));
            _builtin.Add(new BuiltinAppRow("HEVC Video Extensions", "HEVCVideoExtensions"));

            foreach (var app in BloatwareService.RecommendedApps) _recommended.Add(new RecommendedAppRow(app));
            _ = CheckRecommendedInstalledAsync();
        }

        // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "ενώ έχω εγκατεστημένο το Chrome, στις προτεινόμενες εμφανίζεται
        // πως δεν είναι") - το "winget list --id X --exact" ΔΕΝ βρίσκει πάντα εφαρμογές που ΕΙΝΑΙ
        // πράγματι εγκατεστημένες (π.χ. αν εγκαταστάθηκαν με άλλον τρόπο εκτός winget, ή λόγω γνωστού
        // quirk όπου το winget δεν αντιστοιχίζει πάντα σωστά ID↔εγκατεστημένο πακέτο όταν η ίδια η
        // εφαρμογή τρέχει elevated). Τώρα ελέγχεται ΚΑΙ το μητρώο Uninstall (ίδιος μηχανισμός με το
        // AppIconService - ανεξάρτητος από winget/elevation) ως δεύτερο, ανεξάρτητο σήμα - "εγκατεστημένη"
        // αν ΤΟ ΕΝΑ ΑΠΟ ΤΑ ΔΥΟ το επιβεβαιώνει, πιο ανθεκτικό από το να βασίζεται μόνο στο winget.
        private async System.Threading.Tasks.Task CheckRecommendedInstalledAsync()
        {
            // Λήψη logos ΠΑΝΤΑ (φθηνό, ήδη cached τοπικά μέσω LogoCacheService - βλ. σχόλιο εκεί).
            _ = System.Threading.Tasks.Task.WhenAll(_recommended.Select(async row => row.Icon = await LogoCacheService.GetLogoAsync(row.App.WingetId)));

            if (s_recommendedScanned)
            {
                foreach (var row in _recommended)
                    if (s_recommendedInstalledCache.TryGetValue(row.App.WingetId, out var installed)) row.IsInstalled = installed;
                TxtRecommendedStatus.Text = LanguageService.T("Bloatware_ReadyDot");
                return;
            }

            TxtRecommendedStatus.Text = LanguageService.T("Bloatware_CheckingInstalled");
            foreach (var row in _recommended)
            {
                var iconPath = AppIconService.FindInstalledIconPath(row.App.Title);
                var installedViaWinget = await BloatwareService.IsWingetAppInstalledAsync(row.App.WingetId);
                row.IsInstalled = installedViaWinget || iconPath != null;
                s_recommendedInstalledCache[row.App.WingetId] = row.IsInstalled;
                if (row.Icon == null && iconPath != null) row.Icon = AppIconService.GetIconForFile(iconPath);
            }
            s_recommendedScanned = true;
            TxtRecommendedStatus.Text = LanguageService.T("Bloatware_ReadyDot");
        }

        private void BtnRefreshRecommended_Click(object sender, RoutedEventArgs e)
        {
            s_recommendedScanned = false;
            s_recommendedInstalledCache.Clear();
            _ = CheckRecommendedInstalledAsync();
        }

        private static string BuiltinTitle(BuiltinAppRow row) => row.Name;

        private async void BtnBuiltinInstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: BuiltinAppRow row }) return;
            TxtBuiltinStatus.Text = $"{LanguageService.T("Bloatware_InstallingPrefix")}{row.Name}...";
            StatusService.SetBusy($"{LanguageService.T("Bloatware_InstallingPrefix")}{row.Name}...");
            var ok = row.Key switch
            {
                "Copilot" => await BloatwareService.InstallAppxAsync("Copilot", "9NXTC3B7QF27"),
                "XboxGamingOverlay" => await BloatwareService.InstallAppxAsync("XboxGamingOverlay", "9NZKPSTSNW4P"),
                "BingNews" => await BloatwareService.InstallAppxAsync("BingNews", "9WZDNCRFHVFW"),
                "WMP" => await BloatwareService.InstallWindowsMediaPlayerAsync(),
                "HEVCVideoExtensions" => await BloatwareService.InstallAppxAsync("HEVCVideoExtensions", "9N4WGH0Z6VHQ"),
                "PhotoViewer" => RunSync(BloatwareService.InstallPhotoViewer),
                _ => false,
            };
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtBuiltinStatus.Text = ok ? $"{row.Name}{LanguageService.T("Bloatware_CompletedSuffix")}" : $"{row.Name}{LanguageService.T("Bloatware_FailedManualSuffix")}";
        }

        private async void BtnBuiltinRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: BuiltinAppRow row }) return;
            TxtBuiltinStatus.Text = $"{LanguageService.T("Bloatware_RemovingPrefix")}{row.Name}...";
            StatusService.SetBusy($"{LanguageService.T("Bloatware_RemovingPrefix")}{row.Name}...");
            var ok = row.Key switch
            {
                "WMP" => await BloatwareService.RemoveWindowsMediaPlayerAsync(),
                "PhotoViewer" => RunSync(BloatwareService.RemovePhotoViewer),
                _ => await BloatwareService.RemoveAppxAsync(row.Key),
            };
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtBuiltinStatus.Text = ok ? $"{row.Name}{LanguageService.T("Bloatware_RemovedSuffix")}" : $"{row.Name}{LanguageService.T("Bloatware_FailedSuffix")}";
        }

        private static bool RunSync(System.Action action) { action(); return true; }

        private async void BtnRecommendedToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: RecommendedAppRow row } button) return;
            button.IsEnabled = false;
            StatusService.SetBusy($"{(row.IsInstalled ? LanguageService.T("Bloatware_Uninstall") : LanguageService.T("Bloatware_Install"))} {row.App.Title}...");
            // K-Lite έχει πρόσθετο βήμα μετά την εγκατάσταση (ορισμός WMP/MPC-HC ως προεπιλογή +
            // καρφίτσωμα στη γραμμή εργασιών, port του ps1 original ~14731 - βλ. BloatwareService).
            var ok = row.IsInstalled
                ? await BloatwareService.WingetUninstallAsync(row.App.WingetId)
                : row.App.WingetId == "CodecGuide.K-LiteCodecPack.Mega"
                    ? await BloatwareService.InstallKLiteAsync()
                    : await BloatwareService.WingetInstallAsync(row.App.WingetId);
            StatusService.SetIdle(LanguageService.T("Ready"));
            button.IsEnabled = true;
            if (ok) { row.IsInstalled = !row.IsInstalled; s_recommendedInstalledCache[row.App.WingetId] = row.IsInstalled; }
            else ThemedMessageBox.Show($"{LanguageService.T("Bloatware_ActionFailedPrefix")}{row.App.Title}{LanguageService.T("Bloatware_ActionFailedSuffix")}", LanguageService.T("Bloatware_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // ΝΕΟ - roadmap "Μαζική επιλογή ανά κατηγορία" - εγκαθιστά όλες τις εφαρμογές της ομάδας που
        // ΔΕΝ είναι ήδη εγκατεστημένες, μία-μία (winget δεν υποστηρίζει αξιόπιστα παράλληλες κλήσεις
        // στην ίδια σύνοδο) με ενημέρωση της γραμμής κατάστασης ανά εφαρμογή.
        private async void BtnInstallGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string groupName } button) return;
            var toInstall = _recommended.Where(r => r.App.Group == groupName && !r.IsInstalled).ToList();
            if (toInstall.Count == 0) return;
            if (ThemedMessageBox.Show($"{LanguageService.T("Bloatware_InstallAllConfirmPrefix")}{toInstall.Count}{LanguageService.T("Bloatware_InstallAllConfirmSuffix")}",
                    LanguageService.T("Bloatware_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;

            button.IsEnabled = false;
            var failed = 0;
            foreach (var row in toInstall)
            {
                StatusService.SetBusy($"{LanguageService.T("Bloatware_Install")} {row.App.Title}...");
                var ok = row.App.WingetId == "CodecGuide.K-LiteCodecPack.Mega"
                    ? await BloatwareService.InstallKLiteAsync()
                    : await BloatwareService.WingetInstallAsync(row.App.WingetId);
                if (ok) { row.IsInstalled = true; s_recommendedInstalledCache[row.App.WingetId] = true; } else failed++;
            }
            StatusService.SetIdle(LanguageService.T("Ready"));
            button.IsEnabled = true;
            if (failed > 0)
                ThemedMessageBox.Show($"{failed}{LanguageService.T("Bloatware_InstallAllFailedSuffix")}", LanguageService.T("Bloatware_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BtnOpenUwpManager_Click(object sender, RoutedEventArgs e)
        {
            var window = new UwpAppManagerWindow { Owner = Window.GetWindow(this) };
            window.ShowDialog();
        }

        // ΝΕΟ - roadmap "Διασταύρωση με εκκίνηση" - σήμανση εγκατεστημένων εφαρμογών που είναι ΕΠΙΣΗΣ
        // ρυθμισμένες να τρέχουν στην εκκίνηση, με ασαφή αντιστοίχιση ονόματος (η ίδια εφαρμογή συχνά
        // εμφανίζεται με ελαφρώς διαφορετικό όνομα στο Uninstall registry έναντι στο Run key).
        private async void BtnRefreshInstalledApps_Click(object sender, RoutedEventArgs e)
        {
            StatusService.SetBusy(LanguageService.T("Bloatware_LoadingInstalled"));
            _installedApps.Clear();
            var startupItems = await SystemService.LoadStartupItemsAsync();
            foreach (var app in await BloatwareService.LoadInstalledAppsAsync())
            {
                var alsoAtStartup = app.DisplayName.Length >= 4 && startupItems.Any(s =>
                    s.Name.Length >= 4 &&
                    (app.DisplayName.Contains(s.Name, System.StringComparison.CurrentCultureIgnoreCase) ||
                     s.Name.Contains(app.DisplayName, System.StringComparison.CurrentCultureIgnoreCase)));
                _installedApps.Add(new DeepUninstallRow(app, alsoAtStartup));
            }
            StatusService.SetIdle(LanguageService.T("Ready"));
        }

        private async void BtnDeepUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: DeepUninstallRow row }) return;
            if (ThemedMessageBox.Show($"{LanguageService.T("Bloatware_UninstallConfirmPrefix")}{row.App.DisplayName}{LanguageService.T("Bloatware_UninstallConfirmSuffix")}",
                    LanguageService.T("Bloatware_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            StatusService.SetBusy($"{LanguageService.T("Bloatware_Uninstall")} {row.App.DisplayName}...");
            await BloatwareService.UninstallAppAsync(row.App.UninstallString);
            StatusService.SetIdle(LanguageService.T("Ready"));
            _installedApps.Remove(row);

            var residuals = BloatwareService.FindResidualFolders(row.App.DisplayName);
            if (residuals.Count == 0) return;
            var msg = LanguageService.T("Bloatware_ResidualFoldersFoundPrefix") + string.Join("\n", residuals) + LanguageService.T("Bloatware_ResidualFoldersFoundSuffix");
            if (ThemedMessageBox.Show(msg, LanguageService.T("Bloatware_ResidualFilesTitle"), MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
            foreach (var r in residuals) BloatwareService.DeleteResidualFolder(r);
        }

        // ===== Πρόσθετες Λειτουργίες Windows (ενοποιημένη λίστα - βλ. σχόλιο στο XAML) =====

        private void LoadPopularFeatureChips()
        {
            // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε: "κάνε τα κουμπιά πιο φαρδιά και πλατιά στις πρόσθετες
            // λειτουργίες των windows") - μεγαλύτερο Padding/FontSize ώστε τα chips να είναι πιο
            // άνετα/ευανάγνωστα - το WrapPanel γύρω τους ήδη τυλίγει σε νέα γραμμή όποτε χρειάζεται,
            // οπότε δεν υπάρχει κίνδυνος περικοπής, απλά πιο άνετο μέγεθος ανά chip.
            foreach (var f in AdvancedToolsService.CuratedFeatures)
            {
                var btn = new Button
                {
                    Content = f.DisplayName,
                    Style = (Style)FindResource("AccentButtonStyle"),
                    FontSize = 15,
                    Padding = new Thickness(24, 13, 24, 13),
                    Margin = new Thickness(0, 0, 10, 10),
                    Tag = f.DisplayName,
                };
                btn.Click += (_, _) => { TxtAllFeaturesSearch.Text = (string)btn.Tag; };
                PanelPopularFeatures.Children.Add(btn);
            }
        }

        private async void BtnRefreshAllFeatures_Click(object sender, RoutedEventArgs e)
        {
            StatusService.SetBusy(LanguageService.T("Adv_ScanningFeatures"));
            var curatedNames = AdvancedToolsService.CuratedFeatures.ToDictionary(f => f.FeatureName, f => f.DisplayName);
            var (scanned, error) = await AdvancedToolsService.ScanAllFeaturesAsync();
            if (scanned == null)
            {
                var msg = string.IsNullOrWhiteSpace(error)
                    ? LanguageService.T("Adv_FeatureScanFailed")
                    : $"{LanguageService.T("Adv_FeatureScanFailed")}\n\n{error}";
                StatusService.SetIdle(LanguageService.T("Adv_FeatureScanFailed"));
                ThemedMessageBox.Show(msg, LanguageService.T("Adv_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _allFeatures = scanned
                .Select(f => new AllFeatureRow(f.FeatureName, curatedNames.TryGetValue(f.FeatureName, out var friendly) ? friendly : f.FeatureName) { IsEnabled = f.Enabled })
                .OrderBy(f => f.DisplayName)
                .ToList();
            StatusService.SetIdle(LanguageService.T("Ready"));
            UpdateAllFeaturesDisplay();
        }

        private void TxtAllFeaturesSearch_TextChanged(object sender, TextChangedEventArgs e) => UpdateAllFeaturesDisplay();

        private void UpdateAllFeaturesDisplay()
        {
            _displayedFeatures.Clear();
            var term = TxtAllFeaturesSearch.Text?.Trim() ?? "";
            foreach (var f in _allFeatures.Where(f => term == "" || f.DisplayName.Contains(term, System.StringComparison.OrdinalIgnoreCase) || f.FeatureName.Contains(term, System.StringComparison.OrdinalIgnoreCase)).Take(100))
                _displayedFeatures.Add(f);
        }

        private async void ToggleAllFeature_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton { Tag: AllFeatureRow row } button) return;
            if (ThemedMessageBox.Show($"{(row.IsEnabled ? LanguageService.T("Adv_FeatureEnable") : LanguageService.T("Adv_FeatureDisable"))}{LanguageService.T("Adv_FeatureConfirmMid")}{row.DisplayName}{LanguageService.T("Adv_FeatureConfirmSuffix")}",
                    LanguageService.T("Adv_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                row.IsEnabled = !row.IsEnabled;
                return;
            }
            button.IsEnabled = false;
            StatusService.SetBusy($"{row.DisplayName}...");
            await AdvancedToolsService.SetFeatureEnabledAsync(row.FeatureName, row.IsEnabled);
            StatusService.SetIdle(LanguageService.T("Ready"));
            button.IsEnabled = true;
        }
    }

    public class AllFeatureRow : INotifyPropertyChanged
    {
        public string FeatureName { get; }
        // Φιλικό όνομα για τις γνωστές λειτουργίες (πρώην ξεχωριστή "curated" λίστα, τώρα ενοποιημένη
        // εδώ) - αλλιώς το ίδιο το τεχνικό FeatureName (π.χ. "TelnetClient").
        public string DisplayName { get; }
        private bool _isEnabled;
        public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; PropertyChanged?.Invoke(this, new(nameof(IsEnabled))); } }
        public AllFeatureRow(string featureName, string displayName) { FeatureName = featureName; DisplayName = displayName; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public record BuiltinAppRow(string Name, string Key);

    public class RecommendedAppRow : INotifyPropertyChanged
    {
        public RecommendedApp App { get; }
        private bool _isInstalled;
        public bool IsInstalled
        {
            get => _isInstalled;
            set { _isInstalled = value; PropertyChanged?.Invoke(this, new(nameof(IsInstalled))); PropertyChanged?.Invoke(this, new(nameof(ButtonLabel))); }
        }
        public string ButtonLabel => IsInstalled ? LanguageService.T("Bloatware_Uninstall") : LanguageService.T("Bloatware_Install");

        // Ρητό αίτημα χρήστη: "βάλε τα logos των εφαρμογών" - πραγματικό εικονίδιο ΜΟΝΟ αν η εφαρμογή
        // είναι ήδη εγκατεστημένη (βλ. AppIconService.FindInstalledIconPath) - καμία τοπική πηγή
        // λογότυπου υπάρχει για ΜΗ εγκατεστημένες εφαρμογές χωρίς λήψη από το διαδίκτυο.
        private System.Windows.Media.ImageSource? _icon;
        public System.Windows.Media.ImageSource? Icon
        {
            get => _icon;
            set { _icon = value; PropertyChanged?.Invoke(this, new(nameof(Icon))); }
        }

        public RecommendedAppRow(RecommendedApp app) => App = app;
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    // Εικονίδιο ανά κατηγορία προτεινόμενων εφαρμογών (port του ps1's ξεχωριστό IconType ανά ομάδα,
    // ~14761-14792) - χρησιμοποιείται στο GroupStyle.HeaderTemplate του ListRecommended.
    public class GroupHeaderIconConverter : IValueConverter
    {
        // Κλειδί = ουδέτερο-ως-προς-γλώσσα RecommendedApp.Group (βλ. BloatwareService.RecommendedApps).
        private static readonly System.Collections.Generic.Dictionary<string, (string Icon, string NameKey)> Groups = new()
        {
            ["Browsers"] = ("\U0001F310", "Bloatware_GroupBrowsers"),
            ["Compression"] = ("\U0001F5DC", "Bloatware_GroupCompression"),
            ["Multimedia"] = ("\U0001F3AC", "Bloatware_GroupMultimedia"),
            ["Communication"] = ("✉", "Bloatware_GroupCommunication"),
            ["Tools"] = ("\U0001F527", "Bloatware_GroupTools"),
            ["Documents"] = ("\U0001F4C4", "Bloatware_GroupDocuments"),
            ["SystemLibs"] = ("⚙", "Bloatware_GroupSystemLibs"),
        };

        public object Convert(object? value, System.Type targetType, object parameter, CultureInfo culture)
        {
            var key = value as string ?? "";
            return Groups.TryGetValue(key, out var g) ? $"{g.Icon} {LanguageService.T(g.NameKey)}" : key;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture) =>
            throw new System.NotSupportedException();
    }

    public record DeepUninstallRow(InstalledApp App, bool AlsoRunsAtStartup = false);
}
