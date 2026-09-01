using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class OptimizationView : UserControl
    {
        private readonly ObservableCollection<WingetUpdateRow> _updates = new();

        public OptimizationView()
        {
            InitializeComponent();
            ListWingetUpdates.ItemsSource = _updates;
        }

        private void ToggleOfficeMode_Click(object sender, RoutedEventArgs e)
        {
            // Turning either mode off (or Office Mode on) both mean "back to Balanced" in this
            // increment - see the honesty note in PowerModeService about what's not ported yet.
            var isOn = ToggleOfficeMode.IsChecked == true;
            if (isOn && ToggleGamingMode.IsChecked == true) ToggleGamingMode.IsChecked = false;
            PowerModeService.SetBalancedPlan();
        }

        private void ToggleGamingMode_Click(object sender, RoutedEventArgs e)
        {
            var isOn = ToggleGamingMode.IsChecked == true;
            if (isOn)
            {
                if (ToggleOfficeMode.IsChecked == true) ToggleOfficeMode.IsChecked = false;
                PowerModeService.SetHighPerformancePlan();
            }
            else
            {
                PowerModeService.SetBalancedPlan();
            }
        }

        private async void BtnScanWinget_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            TxtWingetStatus.Text = "Σάρωση σε εξέλιξη...";
            _updates.Clear();

            var results = await WingetService.ScanAsync();
            foreach (var u in results) _updates.Add(new WingetUpdateRow(u));

            TxtWingetStatus.Text = results.Count == 0
                ? "Δεν βρέθηκαν διαθέσιμες ενημερώσεις."
                : $"Βρέθηκαν {results.Count} διαθέσιμες ενημερώσεις εφαρμογών:";
            SetBusy(false);
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
                TxtWingetStatus.Text = $"Αναβάθμιση {row.Update.Name}... ({succeeded.Count + failed.Count + 1}/{selected.Count})";
                var ok = await WingetService.UpgradeAsync(row.Update.Id);
                if (ok) { succeeded.Add(row.Update.Name); _updates.Remove(row); }
                else { failed.Add(row.Update.Name); }
            }
            SetBusy(false);

            var summary = failed.Count == 0
                ? $"Ολοκληρώθηκε: {succeeded.Count} εφαρμογές αναβαθμίστηκαν επιτυχώς."
                : $"Ολοκληρώθηκε: {succeeded.Count} επιτυχείς, {failed.Count} απέτυχαν ({string.Join(", ", failed)}).";
            TxtWingetStatus.Text = summary;
            MessageBox.Show(summary, "Αναβάθμιση Εφαρμογών", MessageBoxButton.OK,
                failed.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void SetBusy(bool busy)
        {
            ProgressWinget.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            BtnScanWinget.IsEnabled = !busy;
            BtnUpgradeSelected.IsEnabled = !busy;
            if (busy) StatusService.SetBusy("Έλεγχος ενημερώσεων εφαρμογών (winget)...");
            else StatusService.SetIdle("Έτοιμο για χρήση");
        }
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

        public WingetUpdateRow(WingetUpdate update) => Update = update;

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
