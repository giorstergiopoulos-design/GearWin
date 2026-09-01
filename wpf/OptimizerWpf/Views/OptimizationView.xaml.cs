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
            TxtWingetStatus.Text = "Σάρωση σε εξέλιξη...";
            _updates.Clear();

            var results = await WingetService.ScanAsync();
            foreach (var u in results) _updates.Add(new WingetUpdateRow(u));

            TxtWingetStatus.Text = results.Count == 0
                ? "Δεν βρέθηκαν διαθέσιμες ενημερώσεις."
                : $"Βρέθηκαν {results.Count} διαθέσιμες ενημερώσεις εφαρμογών:";
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            var selectAll = ChkSelectAll.IsChecked == true;
            foreach (var row in _updates) row.IsSelected = selectAll;
        }

        private async void BtnUpgradeSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _updates.Where(u => u.IsSelected).ToList();
            if (selected.Count == 0) return;

            TxtWingetStatus.Text = $"Αναβάθμιση {selected.Count} εφαρμογών...";
            foreach (var row in selected)
            {
                await WingetService.UpgradeAsync(row.Update.Id);
                _updates.Remove(row);
            }
            TxtWingetStatus.Text = "Ολοκληρώθηκε. Πατήστε 'Σάρωση' για να δείτε τυχόν υπόλοιπες ενημερώσεις.";
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
