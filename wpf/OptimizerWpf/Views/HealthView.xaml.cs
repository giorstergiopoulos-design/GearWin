using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class HealthView : UserControl
    {
        private readonly ObservableCollection<RegistryFindingRow> _findings = new();

        public HealthView()
        {
            InitializeComponent();
            ListRegFindings.ItemsSource = _findings;
            ListBrowsers.ItemsSource = HealthCleanupService.DetectBrowsers();
            _ = RefreshWinREAsync();
        }

        private async void BtnRegCleanScan_Click(object sender, RoutedEventArgs e)
        {
            TxtRegCleanStatus.Text = "Σάρωση σε εξέλιξη...";
            StatusService.SetBusy("Σάρωση μητρώου...");
            _findings.Clear();

            var results = await HealthCleanupService.ScanRegistryAsync();
            StatusService.SetIdle("Έτοιμο για χρήση");
            foreach (var f in results) _findings.Add(new RegistryFindingRow(f));
            TxtRegCleanStatus.Text = results.Count == 0
                ? "Δεν εντοπίστηκαν στοιχεία προς καθαρισμό."
                : $"Βρέθηκαν {results.Count} στοιχεία - επιλέξτε ποια θέλετε να διαγράψετε.";
        }

        private async void BtnRegCleanDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = _findings.Where(f => f.IsSelected).Select(f => f.Finding).ToList();
            if (selected.Count == 0) return;

            var confirm = MessageBox.Show(
                $"Θα δημιουργηθεί αυτόματο αντίγραφο ασφαλείας (.reg) και θα διαγραφούν {selected.Count} επιλεγμένα στοιχεία. Συνέχεια;",
                "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            var backupDir = Path.Combine(Path.GetTempPath(), "OptimizerWpfRegBackup_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(backupDir);

            StatusService.SetBusy("Διαγραφή στοιχείων μητρώου...");
            var ok = await HealthCleanupService.DeleteFindingsAsync(selected, backupDir);
            StatusService.SetIdle("Έτοιμο για χρήση");

            TxtRegCleanStatus.Text = ok
                ? $"Διαγράφηκαν {selected.Count} στοιχεία. Αντίγραφο ασφαλείας: {backupDir}"
                : "Η διαγραφή απέτυχε εν μέρει ή πλήρως.";
            foreach (var f in selected) _findings.Remove(_findings.First(r => r.Finding == f));
        }

        private async void BtnClearBrowserCache_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: BrowserCacheEntry browser }) return;
            TxtBrowserCacheStatus.Text = $"Καθαρισμός cache για {browser.Label}...";
            StatusService.SetBusy($"Καθαρισμός cache {browser.Label}...");
            await HealthCleanupService.ClearBrowserCacheAsync(browser);
            StatusService.SetIdle("Έτοιμο για χρήση");
            TxtBrowserCacheStatus.Text = $"Ο cache του {browser.Label} καθαρίστηκε.";
        }

        private async void BtnWinRERefresh_Click(object sender, RoutedEventArgs e) => await RefreshWinREAsync();

        private async void BtnWinREEnable_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Θα ενεργοποιηθεί το Περιβάλλον Ανάκτησης Windows (αλλαγή boot configuration). Συνέχεια;",
                "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            StatusService.SetBusy("Ενεργοποίηση WinRE...");
            await HealthCleanupService.EnableWinREAsync();
            StatusService.SetIdle("Έτοιμο για χρήση");
            await RefreshWinREAsync();
        }

        private async System.Threading.Tasks.Task RefreshWinREAsync()
        {
            var result = await HealthCleanupService.GetWinREStatusAsync();
            if (!result.Success)
            {
                TxtWinREStatus.Text = "Κατάσταση: Άγνωστη (απαιτούνται δικαιώματα διαχειριστή)";
                TxtWinREStatus.Foreground = (Brush)FindResource("SubTextBrush");
            }
            else if (result.Enabled)
            {
                TxtWinREStatus.Text = "Κατάσταση: Ενεργοποιημένο";
                TxtWinREStatus.Foreground = new SolidColorBrush(Color.FromRgb(90, 200, 120));
            }
            else
            {
                TxtWinREStatus.Text = "Κατάσταση: Απενεργοποιημένο";
                TxtWinREStatus.Foreground = new SolidColorBrush(Color.FromRgb(230, 90, 90));
            }
        }
    }

    public class RegistryFindingRow : INotifyPropertyChanged
    {
        public RegistryFinding Finding { get; }
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }
        public RegistryFindingRow(RegistryFinding finding) => Finding = finding;
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
