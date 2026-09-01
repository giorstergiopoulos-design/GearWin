using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class OptimizationView : UserControl
    {
        private readonly ObservableCollection<WingetUpdateRow> _updates = new();
        private readonly ObservableCollection<DriverUpdate> _driverUpdates = new();
        private readonly ObservableCollection<DriverStoreEntry> _driverStoreEntries = new();

        public OptimizationView()
        {
            InitializeComponent();
            ListWingetUpdates.ItemsSource = _updates;
            ListDriverUpdates.ItemsSource = _driverUpdates;
            ListDriverStore.ItemsSource = _driverStoreEntries;
        }

        // Πλέον καλεί το ΠΛΗΡΕΣ Office/Gaming Mode (Set-OfficeMode/Disable-OfficeMode κ.λπ. του
        // Optimizer.ps1) αντί για μόνο powercfg - βλ. PowerModeService.cs. Επανεκκίνηση Explorer
        // (Απενεργοποίηση και των δύο modes) προκαλεί σύντομο "τρεμόπαιγμα" της επιφάνειας εργασίας -
        // αναμενόμενο, ίδιο με το WinForms original.
        private void ToggleOfficeMode_Click(object sender, RoutedEventArgs e)
        {
            var isOn = ToggleOfficeMode.IsChecked == true;
            if (isOn && ToggleGamingMode.IsChecked == true) ToggleGamingMode.IsChecked = false;
            if (isOn)
            {
                StatusService.SetBusy("Ενεργοποίηση Office Mode...");
                PowerModeService.SetOfficeMode();
                StatusService.SetIdle("Το Office Mode ενεργοποιήθηκε επιτυχώς");
            }
            else
            {
                StatusService.SetBusy("Απενεργοποίηση Office Mode & επανεκκίνηση Explorer...");
                PowerModeService.DisableOfficeMode();
                StatusService.SetIdle("Το Office Mode απενεργοποιήθηκε & ο Explorer επανεκκινήθηκε");
            }
        }

        private void ToggleGamingMode_Click(object sender, RoutedEventArgs e)
        {
            var isOn = ToggleGamingMode.IsChecked == true;
            if (isOn)
            {
                if (ToggleOfficeMode.IsChecked == true) ToggleOfficeMode.IsChecked = false;
                StatusService.SetBusy("Ενεργοποίηση Gaming Mode...");
                PowerModeService.SetGamingMode();
                StatusService.SetIdle("Το Gaming Mode ενεργοποιήθηκε (συνιστάται επανεκκίνηση)");
            }
            else
            {
                StatusService.SetBusy("Απενεργοποίηση Gaming Mode & επανεκκίνηση Explorer...");
                PowerModeService.DisableGamingMode();
                StatusService.SetIdle("Το Gaming Mode απενεργοποιήθηκε (χρειάζεται επανεκκίνηση PC για πλήρη επαναφορά VBS)");
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

        private void BtnCheckMsStore_Click(object sender, RoutedEventArgs e)
        {
            StatusService.SetBusy("Ενεργοποίηση σάρωσης ενημερώσεων Microsoft Store...");
            WingetService.TriggerMsStoreUpdateScanAndOpen();
            StatusService.SetIdle("Έτοιμο για χρήση");
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
                var ok = await WingetService.UpgradeAsync(row.Update.Id, row.Update.Source);
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

        // ===== Ενημερώσεις Οδηγών Συσκευών (Windows Update) - port του cardOpt4 =====

        private async void BtnScanDrivers_Click(object sender, RoutedEventArgs e)
        {
            ProgressDriver.Visibility = Visibility.Visible;
            BtnScanDrivers.IsEnabled = false;
            TxtDriverStatus.Text = "Σάρωση εγκατεστημένων οδηγών & έλεγχος Windows Update σε εξέλιξη (μπορεί να διαρκέσει έως 1-2 λεπτά)...";
            StatusService.SetBusy("Σάρωση ενημερώσεων οδηγών...");
            _driverUpdates.Clear();

            var result = await DriverService.ScanAsync();
            StatusService.SetIdle("Έτοιμο για χρήση");
            ProgressDriver.Visibility = Visibility.Collapsed;
            BtnScanDrivers.IsEnabled = true;

            if (result.PolicyBlocked)
            {
                TxtDriverStatus.Text = "Μια πολιτική Windows Update αποκλείει τις ενημερώσεις οδηγών σε αυτό το σύστημα.";
                return;
            }
            foreach (var u in result.Updates) _driverUpdates.Add(u);
            TxtDriverStatus.Text = result.Updates.Count == 0
                ? "Το Windows Update δεν επέστρεψε αυτόματες προτάσεις ενημέρωσης οδηγών."
                : $"Βρέθηκαν {result.Updates.Count} διαθέσιμες ενημερώσεις οδηγών:";
        }

        private void BtnInstallDriver_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: DriverUpdate update }) return;
            DriverService.InstallInBackground(update.UpdateId);
            TxtDriverStatus.Text = $"Λήψη/εγκατάσταση οδηγού σε εξέλιξη στο παρασκήνιο: {update.Title}";
        }

        // ===== Αντίγραφο Ασφαλείας Οδηγών - port του cardOpt5 =====

        private async void BtnDriverBackup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Επιλέξτε φάκελο αποθήκευσης αντιγράφου οδηγών" };
            if (dialog.ShowDialog() != true) return;

            var destPath = System.IO.Path.Combine(dialog.FolderName, $"DriverBackup_{DateTime.Now:yyyy-MM-dd}");
            TxtDriverBackupStatus.Text = "Δημιουργία αντιγράφου ασφαλείας σε εξέλιξη - μπορεί να διαρκέσει λίγα λεπτά...";
            StatusService.SetBusy("Αντίγραφο ασφαλείας οδηγών...");
            BtnDriverBackup.IsEnabled = false;
            BtnDriverRestore.IsEnabled = false;

            var ok = await DriverService.BackupAsync(destPath);
            StatusService.SetIdle("Έτοιμο για χρήση");
            BtnDriverBackup.IsEnabled = true;
            BtnDriverRestore.IsEnabled = true;
            TxtDriverBackupStatus.Text = ok
                ? $"Το αντίγραφο ασφαλείας ολοκληρώθηκε: {destPath}"
                : "Η δημιουργία αντιγράφου απέτυχε ή ακυρώθηκε.";
        }

        private async void BtnDriverRestore_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Επιλέξτε τον φάκελο αντιγράφου ασφαλείας οδηγών" };
            if (dialog.ShowDialog() != true) return;

            var confirm = MessageBox.Show(
                $"Εγκατάσταση όλων των οδηγών από: {dialog.FolderName}; Συνιστάται μόνο αν οι οδηγοί προέρχονται από αυτόν τον ίδιο υπολογιστή.",
                "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            TxtDriverBackupStatus.Text = "Επαναφορά οδηγών σε εξέλιξη...";
            StatusService.SetBusy("Επαναφορά οδηγών...");
            BtnDriverBackup.IsEnabled = false;
            BtnDriverRestore.IsEnabled = false;

            var ok = await DriverService.RestoreAsync(dialog.FolderName);
            StatusService.SetIdle("Έτοιμο για χρήση");
            BtnDriverBackup.IsEnabled = true;
            BtnDriverRestore.IsEnabled = true;
            TxtDriverBackupStatus.Text = ok ? "Η επαναφορά οδηγών ολοκληρώθηκε." : "Η επαναφορά απέτυχε.";
        }

        // ===== Καθαρισμός Αποθήκης Οδηγών - port του cardDriverStore =====

        private async void BtnScanDriverStore_Click(object sender, RoutedEventArgs e)
        {
            ProgressDriverStore.Visibility = Visibility.Visible;
            BtnScanDriverStore.IsEnabled = false;
            TxtDriverStoreStatus.Text = "Σάρωση αποθήκης οδηγών σε εξέλιξη...";
            StatusService.SetBusy("Σάρωση αποθήκης οδηγών...");
            _driverStoreEntries.Clear();

            var all = await DriverService.ScanStoreAsync();
            StatusService.SetIdle("Έτοιμο για χρήση");
            ProgressDriverStore.Visibility = Visibility.Collapsed;
            BtnScanDriverStore.IsEnabled = true;

            // Ομαδοποίηση κατά OriginalFileName - κρατάμε πάντα την πιο πρόσφατη (κατά ημερομηνία),
            // προτείνουμε προς διαγραφή ΜΟΝΟ τις παλιότερες, ίδια λογική με το ps1 original.
            var duplicates = all
                .GroupBy(d => d.OriginalFileName)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.OrderByDescending(d => d.Date).Skip(1))
                .ToList();

            foreach (var d in duplicates) _driverStoreEntries.Add(d);
            TxtDriverStoreStatus.Text = duplicates.Count == 0
                ? "Δεν εντοπίστηκαν παλιότερες, διπλότυπες εκδόσεις οδηγών."
                : $"Βρέθηκαν {duplicates.Count} παλιότερες εκδόσεις οδηγών προς διαγραφή:";
        }

        private async void BtnDeleteStoreDriver_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: DriverStoreEntry entry } button) return;
            button.IsEnabled = false;
            var ok = await DriverService.DeleteStoreDriverAsync(entry.Driver);
            if (ok) _driverStoreEntries.Remove(entry);
            else
            {
                button.IsEnabled = true;
                MessageBox.Show($"Η διαγραφή του οδηγού {entry.OriginalFileName} απέτυχε (μπορεί να βρίσκεται σε ενεργή χρήση).",
                    "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
