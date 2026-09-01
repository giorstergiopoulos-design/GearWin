using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class BloatwareView : UserControl
    {
        private readonly ObservableCollection<BuiltinAppRow> _builtin = new();
        private readonly ObservableCollection<RecommendedAppRow> _recommended = new();
        private readonly ObservableCollection<DeepUninstallRow> _installedApps = new();

        public BloatwareView()
        {
            InitializeComponent();
            ListBuiltin.ItemsSource = _builtin;
            ListRecommended.ItemsSource = _recommended;
            ListInstalledApps.ItemsSource = _installedApps;

            _builtin.Add(new BuiltinAppRow("Microsoft Copilot", "Copilot"));
            _builtin.Add(new BuiltinAppRow("Xbox Gaming Overlay", "XboxGamingOverlay"));
            _builtin.Add(new BuiltinAppRow("Ειδήσεις & Καιρός", "BingNews"));
            _builtin.Add(new BuiltinAppRow("Windows Media Player (Legacy)", "WMP"));
            _builtin.Add(new BuiltinAppRow("Windows Photo Viewer (κλασικό)", "PhotoViewer"));
            _builtin.Add(new BuiltinAppRow("HEVC Video Extensions", "HEVCVideoExtensions"));

            foreach (var app in BloatwareService.RecommendedApps) _recommended.Add(new RecommendedAppRow(app));
            _ = CheckRecommendedInstalledAsync();
        }

        private async System.Threading.Tasks.Task CheckRecommendedInstalledAsync()
        {
            TxtRecommendedStatus.Text = "Έλεγχος εγκατεστημένων εφαρμογών...";
            foreach (var row in _recommended)
                row.IsInstalled = await BloatwareService.IsWingetAppInstalledAsync(row.App.WingetId);
            TxtRecommendedStatus.Text = "Έτοιμο.";
        }

        private static string BuiltinTitle(BuiltinAppRow row) => row.Name;

        private async void BtnBuiltinInstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: BuiltinAppRow row }) return;
            TxtBuiltinStatus.Text = $"Εγκατάσταση {row.Name}...";
            StatusService.SetBusy($"Εγκατάσταση {row.Name}...");
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
            StatusService.SetIdle("Έτοιμο για χρήση");
            TxtBuiltinStatus.Text = ok ? $"{row.Name}: ολοκληρώθηκε." : $"{row.Name}: απέτυχε ή χρειάζεται χειροκίνητη ολοκλήρωση.";
        }

        private async void BtnBuiltinRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: BuiltinAppRow row }) return;
            TxtBuiltinStatus.Text = $"Αφαίρεση {row.Name}...";
            StatusService.SetBusy($"Αφαίρεση {row.Name}...");
            var ok = row.Key switch
            {
                "WMP" => await BloatwareService.RemoveWindowsMediaPlayerAsync(),
                "PhotoViewer" => RunSync(BloatwareService.RemovePhotoViewer),
                _ => await BloatwareService.RemoveAppxAsync(row.Key),
            };
            StatusService.SetIdle("Έτοιμο για χρήση");
            TxtBuiltinStatus.Text = ok ? $"{row.Name}: αφαιρέθηκε." : $"{row.Name}: απέτυχε.";
        }

        private static bool RunSync(System.Action action) { action(); return true; }

        private async void BtnRecommendedToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: RecommendedAppRow row } button) return;
            button.IsEnabled = false;
            StatusService.SetBusy($"{(row.IsInstalled ? "Απεγκατάσταση" : "Εγκατάσταση")} {row.App.Title}...");
            var ok = row.IsInstalled
                ? await BloatwareService.WingetUninstallAsync(row.App.WingetId)
                : await BloatwareService.WingetInstallAsync(row.App.WingetId);
            StatusService.SetIdle("Έτοιμο για χρήση");
            button.IsEnabled = true;
            if (ok) row.IsInstalled = !row.IsInstalled;
            else MessageBox.Show($"Η ενέργεια για {row.App.Title} απέτυχε.", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BtnOpenUwpManager_Click(object sender, RoutedEventArgs e)
        {
            var window = new UwpAppManagerWindow { Owner = Window.GetWindow(this) };
            window.ShowDialog();
        }

        private async void BtnRefreshInstalledApps_Click(object sender, RoutedEventArgs e)
        {
            StatusService.SetBusy("Φόρτωση εγκατεστημένων εφαρμογών...");
            _installedApps.Clear();
            foreach (var app in await BloatwareService.LoadInstalledAppsAsync()) _installedApps.Add(new DeepUninstallRow(app));
            StatusService.SetIdle("Έτοιμο για χρήση");
        }

        private async void BtnDeepUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: DeepUninstallRow row }) return;
            if (MessageBox.Show($"Θα εκκινήσει ο επίσημος uninstaller για '{row.App.DisplayName}'. Ολοκληρώστε τα βήματα στο παράθυρο που θα ανοίξει. Συνέχεια;",
                    "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            StatusService.SetBusy($"Απεγκατάσταση {row.App.DisplayName}...");
            await BloatwareService.UninstallAppAsync(row.App.UninstallString);
            StatusService.SetIdle("Έτοιμο για χρήση");
            _installedApps.Remove(row);

            var residuals = BloatwareService.FindResidualFolders(row.App.DisplayName);
            if (residuals.Count == 0) return;
            var msg = "Βρέθηκαν υπολειπόμενοι φάκελοι:\n" + string.Join("\n", residuals) + "\n\nΑποστολή στον Κάδο Ανακύκλωσης;";
            if (MessageBox.Show(msg, "Υπολειπόμενα Αρχεία", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
            foreach (var r in residuals) BloatwareService.DeleteResidualFolder(r);
        }
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
        public string ButtonLabel => IsInstalled ? "Απεγκατάσταση" : "Εγκατάσταση";
        public RecommendedAppRow(RecommendedApp app) => App = app;
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public record DeepUninstallRow(InstalledApp App);
}
