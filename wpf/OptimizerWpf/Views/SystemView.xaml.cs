using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class SystemView : UserControl
    {
        private readonly ObservableCollection<StartupRow> _startup = new();
        private readonly ObservableCollection<ProcessRowVm> _processes = new();
        private readonly ObservableCollection<StorageResultRow> _storage = new();
        private readonly ObservableCollection<RestorePointInfo> _restorePoints = new();
        private readonly ObservableCollection<ServiceRowVm> _services = new();

        public SystemView()
        {
            InitializeComponent();
            ListStartup.ItemsSource = _startup;
            ListProcesses.ItemsSource = _processes;
            ListStorageResults.ItemsSource = _storage;
            ListRestorePoints.ItemsSource = _restorePoints;
            ListServices.ItemsSource = _services;
            foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                CmbBenchmarkDrive.Items.Add(d.Name.TrimEnd('\\'));
            if (CmbBenchmarkDrive.Items.Count > 0) CmbBenchmarkDrive.SelectedIndex = 0;

            _ = LoadStartupAsync();
            _ = LoadProcessesAsync();
            _ = LoadRestorePointsAsync();
            _ = LoadServicesAsync();
        }

        private async System.Threading.Tasks.Task LoadStartupAsync()
        {
            _startup.Clear();
            foreach (var i in await SystemService.LoadStartupItemsAsync()) _startup.Add(new StartupRow(i));
        }

        private async void BtnRefreshStartup_Click(object sender, RoutedEventArgs e) => await LoadStartupAsync();

        private void ToggleStartupItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton { Tag: StartupRow row }) return;
            SystemService.SetStartupItemEnabled(row.Item, row.IsEnabled);
        }

        private async System.Threading.Tasks.Task LoadProcessesAsync()
        {
            _processes.Clear();
            foreach (var p in await SystemService.LoadProcessesAsync()) _processes.Add(new ProcessRowVm(p));
        }

        private async void BtnRefreshProcesses_Click(object sender, RoutedEventArgs e) => await LoadProcessesAsync();

        private void BtnKillProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ProcessRowVm row }) return;
            if (MessageBox.Show($"Τερματισμός της διεργασίας '{row.Row.Name}' (PID {row.Row.Pid}); Τυχόν μη αποθηκευμένη εργασία θα χαθεί.",
                    "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (SystemService.KillProcess(row.Row.Pid)) _processes.Remove(row);
        }

        private async void BtnFindDuplicates_Click(object sender, RoutedEventArgs e)
        {
            TxtStorageStatus.Text = "Αναζήτηση διπλότυπων αρχείων...";
            StatusService.SetBusy("Αναζήτηση διπλότυπων...");
            _storage.Clear();
            var results = await SystemService.FindDuplicatesAsync();
            StatusService.SetIdle("Έτοιμο για χρήση");
            foreach (var r in results) _storage.Add(new StorageResultRow(r.Path, r.SizeMb));
            TxtStorageStatus.Text = $"Βρέθηκαν {results.Count} διπλότυπα αρχεία.";
        }

        private async void BtnFindLarge_Click(object sender, RoutedEventArgs e)
        {
            TxtStorageStatus.Text = "Αναζήτηση μεγάλων αρχείων...";
            StatusService.SetBusy("Αναζήτηση μεγάλων αρχείων...");
            _storage.Clear();
            var results = await SystemService.FindLargeFilesAsync();
            StatusService.SetIdle("Έτοιμο για χρήση");
            foreach (var r in results) _storage.Add(new StorageResultRow(r.Path, r.SizeMb));
            TxtStorageStatus.Text = $"Βρέθηκαν {results.Count} μεγάλα αρχεία.";
        }

        private async void BtnOneDrive_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Θα σημανθούν όλα τα αρχεία OneDrive ως 'μόνο-διαδικτυακά' για εξοικονόμηση χώρου δίσκου. Συνέχεια;",
                    "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
            StatusService.SetBusy("OneDrive - ελευθέρωση χώρου...");
            var ok = await SystemService.OneDriveFreeUpSpaceAsync();
            StatusService.SetIdle("Έτοιμο για χρήση");
            TxtStorageStatus.Text = ok ? "Ολοκληρώθηκε." : "Απέτυχε ή δεν εντοπίστηκε OneDrive.";
        }

        private void BtnRecycleFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: StorageResultRow row } button) return;
            if (MessageBox.Show($"Αποστολή στον Κάδο Ανακύκλωσης: {row.Path};", "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (SystemService.SendToRecycleBin(row.Path)) _storage.Remove(row);
        }

        private async System.Threading.Tasks.Task LoadRestorePointsAsync()
        {
            TxtRestorePointsStatus.Text = "Φόρτωση...";
            _restorePoints.Clear();
            foreach (var p in await SystemService.ListRestorePointsAsync()) _restorePoints.Add(p);
            TxtRestorePointsStatus.Text = $"{_restorePoints.Count} σημεία επαναφοράς.";
        }

        private async void BtnRefreshRestorePoints_Click(object sender, RoutedEventArgs e) => await LoadRestorePointsAsync();

        private async void BtnCreateRestorePoint_Click(object sender, RoutedEventArgs e)
        {
            StatusService.SetBusy("Δημιουργία σημείου επαναφοράς...");
            var ok = await SystemService.CreateRestorePointAsync();
            StatusService.SetIdle("Έτοιμο για χρήση");
            TxtRestorePointsStatus.Text = ok ? "Δημιουργήθηκε νέο σημείο επαναφοράς." : "Απέτυχε (τα Windows επιτρέπουν μόνο 1 αυτόματο σημείο ανά 24ωρο).";
            if (ok) await LoadRestorePointsAsync();
        }

        private void BtnOpenProtectionSettings_Click(object sender, RoutedEventArgs e) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("SystemPropertiesProtection.exe") { UseShellExecute = true });

        // Ρητά ΔΕΝ υλοποιείται delete-by-sequence - βλ. σχόλιο στο SystemService.RestoreToPointAsync.
        private async void BtnRestoreToPoint_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: RestorePointInfo point }) return;
            if (MessageBox.Show($"Ο υπολογιστής θα επανέλθει στο σημείο '{point.Description}' και θα επανεκκινήσει ΑΜΕΣΩΣ. Συνέχεια;",
                    "ΠΡΟΣΟΧΗ - Άμεση Επανεκκίνηση", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            StatusService.SetBusy("Επαναφορά συστήματος...");
            await SystemService.RestoreToPointAsync(point.SequenceNumber);
        }

        private async System.Threading.Tasks.Task LoadServicesAsync()
        {
            _services.Clear();
            foreach (var s in await SystemService.LoadServicesAsync()) _services.Add(new ServiceRowVm(s));
        }

        private async void BtnRefreshServices_Click(object sender, RoutedEventArgs e) => await LoadServicesAsync();

        private async void ToggleService_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton { Tag: ServiceRowVm row }) return;
            StatusService.SetBusy($"Ενημέρωση υπηρεσίας {row.Service.DisplayName}...");
            var ok = row.IsAutomatic
                ? await SystemService.RestoreServiceStartModeAsync(row.Service.ServiceName)
                : await SystemService.SetServiceManualAsync(row.Service.ServiceName);
            StatusService.SetIdle("Έτοιμο για χρήση");
            if (!ok) MessageBox.Show("Η αλλαγή απέτυχε (χρειάζονται δικαιώματα διαχειριστή).", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async void BtnStartBenchmark_Click(object sender, RoutedEventArgs e)
        {
            if (CmbBenchmarkDrive.SelectedItem is not string drive) return;
            BtnStartBenchmark.IsEnabled = false;
            TxtBenchWrite.Text = "..."; TxtBenchRead.Text = "...";
            StatusService.SetBusy($"Δοκιμή ταχύτητας δίσκου {drive}...");
            var result = await SystemService.RunBenchmarkAsync(drive);
            StatusService.SetIdle("Έτοιμο για χρήση");
            BtnStartBenchmark.IsEnabled = true;
            if (result.Error != null)
            {
                TxtBenchWrite.Text = "--"; TxtBenchRead.Text = "--";
                MessageBox.Show(result.Error, "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            TxtBenchWrite.Text = $"{result.WriteMbps} MB/s";
            TxtBenchRead.Text = $"{result.ReadMbps} MB/s";
        }
    }

    public class StartupRow : INotifyPropertyChanged
    {
        public StartupItem Item { get; private set; }
        private bool _isEnabled;
        public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; PropertyChanged?.Invoke(this, new(nameof(IsEnabled))); } }
        public StartupRow(StartupItem item) { Item = item; _isEnabled = !item.IsDisabled; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public record ProcessRowVm(ProcessRow Row)
    {
        public string Name => Row.Name;
        public double WorkingSetMb => Row.WorkingSetMb;
        public bool CanKill => !Row.IsCritical;
    }

    public record StorageResultRow(string Path, double SizeMb);

    public class ServiceRowVm : INotifyPropertyChanged
    {
        public ServiceRow Service { get; }
        private bool _isAutomatic;
        public bool IsAutomatic { get => _isAutomatic; set { _isAutomatic = value; PropertyChanged?.Invoke(this, new(nameof(IsAutomatic))); } }
        public ServiceRowVm(ServiceRow service) { Service = service; _isAutomatic = service.StartMode is "Auto" or "Automatic"; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
