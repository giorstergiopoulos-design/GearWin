using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class PowerToolsCards : UserControl
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);
        private readonly ObservableCollection<StartupRow> _startup = new();
        private readonly ObservableCollection<ServiceRowVm> _services = new();

        // ===== Εύρεση διπλότυπων αρχείων (ενοποιημένη - μετακινήθηκε εδώ από την καρτέλα Προηγμένα
        // Εργαλεία, ρητό αίτημα χρήστη - ίδια καρτέλα με το υπόλοιπο "Αποθηκευτικός Χώρος") =====
        private static readonly ObservableCollection<DuplicateGroupVm> _duplicateGroups = new();
        private static string? s_duplicateFolder;
        private static string? s_duplicateStatusCache;

        // ΝΕΟ - roadmap "Lazy πρώτη φόρτωση καρτελών" - το DriveInfo.GetDrives() (I/O σε κάθε
        // μονάδα δίσκου) γινόταν σύγχρονα μέσα στον constructor, πριν καν εμφανιστεί το πρώτο frame
        // της καρτέλας - μετακινήθηκε στο ήδη καθιερωμένο Task.Run+async μοτίβο της υπόλοιπης
        // καρτέλας, ώστε η εναλλαγή καρτέλας να μη μπλοκάρει στιγμιαία το UI thread.
        private async System.Threading.Tasks.Task LoadBenchmarkDrivesAsync()
        {
            var names = await System.Threading.Tasks.Task.Run(() =>
                DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).Select(d => d.Name.TrimEnd('\\')).ToList());
            foreach (var name in names) CmbBenchmarkDrive.Items.Add(name);
            if (CmbBenchmarkDrive.Items.Count > 0) CmbBenchmarkDrive.SelectedIndex = 0;
        }

        private async System.Threading.Tasks.Task LoadStartupAsync()
        {
            _startup.Clear();
            var items = await SystemService.LoadStartupItemsAsync();
            var delays = await System.Threading.Tasks.Task.Run(() => SystemService.GetStartupDelayEstimates(items));
            foreach (var i in items)
            {
                var row = new StartupRow(i);
                if (delays.TryGetValue(i.Name, out var delay) && delay.HasValue)
                {
                    var seconds = delay.Value.TotalSeconds;
                    row.DelayText = $"+{seconds:0.#}s {LanguageService.T("Sys_StartupDelaySuffix")}";
                    // ΝΕΟ - roadmap "Startup impact, όχι μόνο πλήθος": κατηγοριοποίηση Χαμηλή/Μέτρια/
                    // Υψηλή πάνω στο ΗΔΗ πραγματικό μετρημένο delay (GetStartupDelayEstimates, βλ. πάνω)
                    // - καμία νέα εικασία/heuristic, μόνο ευκολότερη ανάγνωση της ίδιας πραγματικής τιμής.
                    row.ImpactLabel = seconds < 2 ? LanguageService.T("Sys_StartupImpactLow")
                        : seconds < 5 ? LanguageService.T("Sys_StartupImpactMedium")
                        : LanguageService.T("Sys_StartupImpactHigh");
                    row.ImpactColor = new SolidColorBrush(seconds < 2 ? Color.FromRgb(0x4C, 0xAF, 0x50)
                        : seconds < 5 ? Color.FromRgb(0xFF, 0x98, 0x00)
                        : Color.FromRgb(0xE5, 0x39, 0x35));
                }
                _startup.Add(row);
            }
        }

        private async void BtnRefreshStartup_Click(object sender, RoutedEventArgs e) => await LoadStartupAsync();

        private void ToggleStartupItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton { Tag: StartupRow row }) return;
            var ok = SystemService.SetStartupItemEnabled(row.Item, row.IsEnabled);
            // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): καμία ένδειξη επιτυχίας/αποτυχίας - σε αποτυχία
            // ο διακόπτης επαναφέρεται στην πραγματική κατάσταση (ΟΧΙ αυτό που έδειχνε μόλις πατήθηκε).
            if (ok)
            {
                StatusService.SetIdle(row.IsEnabled ? $"{row.Item.Name}{LanguageService.T("Sys_StartupEnabledSuffix")}" : $"{row.Item.Name}{LanguageService.T("Sys_StartupDisabledSuffix")}");
            }
            else
            {
                row.IsEnabled = !row.IsEnabled;
                ThemedMessageBox.Show($"{LanguageService.T("Sys_ChangeForPrefix")}{row.Item.Name}{LanguageService.T("Sys_ChangeFailedSuffix")}", LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ===== Εύρεση διπλότυπων αρχείων (βλ. σχόλιο στο XAML) =====

        private void BtnChooseDuplicateFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = LanguageService.T("Advanced_ChooseFolder") };
            if (dialog.ShowDialog() != true) return;
            s_duplicateFolder = dialog.FolderName;
            TxtDuplicateFolder.Text = s_duplicateFolder;
            BtnScanDuplicates.IsEnabled = true;
        }

        private System.Threading.CancellationTokenSource? _duplicateCts;

        // Το κουμπί σάρωσης λειτουργεί ως "Ακύρωση" όσο τρέχει η σάρωση.
        private async void BtnScanDuplicates_Click(object sender, RoutedEventArgs e)
        {
            if (_duplicateCts != null) { _duplicateCts.Cancel(); return; }
            if (s_duplicateFolder == null) return;
            var scanLabel = BtnScanDuplicates.Content;
            BtnScanDuplicates.Content = LanguageService.T("Cancel");
            BtnDeleteDuplicates.IsEnabled = false;
            _duplicateGroups.Clear();
            TxtDuplicateStatus.Text = LanguageService.T("Advanced_ScanningDuplicates");
            StatusService.SetBusy(LanguageService.T("Advanced_ScanningDuplicates"));
            _duplicateCts = new System.Threading.CancellationTokenSource();

            IReadOnlyList<DuplicateGroup> groups;
            try { groups = await DuplicateFileService.ScanAsync(s_duplicateFolder, null, _duplicateCts.Token); }
            catch (OperationCanceledException)
            {
                TxtDuplicateStatus.Text = LanguageService.T("Health_ToolCancelled");
                s_duplicateStatusCache = TxtDuplicateStatus.Text;
                return;
            }
            finally
            {
                StatusService.SetIdle(LanguageService.T("Ready"));
                _duplicateCts.Dispose();
                _duplicateCts = null;
                BtnScanDuplicates.Content = scanLabel;
            }

            foreach (var g in groups) _duplicateGroups.Add(new DuplicateGroupVm(g));
            TxtDuplicateStatus.Text = groups.Count == 0
                ? LanguageService.T("Advanced_NoDuplicatesFound")
                : string.Format(LanguageService.T("Advanced_DuplicatesFoundPrefix"), groups.Count);
            BtnDeleteDuplicates.IsEnabled = groups.Count > 0;
            s_duplicateStatusCache = TxtDuplicateStatus.Text;
        }

        private async void BtnDeleteDuplicates_Click(object sender, RoutedEventArgs e)
        {
            // ΑΣΦΑΛΕΙΑ ΔΕΔΟΜΕΝΩΝ: αν ο χρήστης έχει επιλέξει ΟΛΑ τα αντίγραφα μιας ομάδας, η διαγραφή θα έσβηνε και το
            // τελευταίο αντίτυπο (η διαγραφή είναι μόνιμη). Σε κάθε τέτοια ομάδα κρατάμε πάντα το πρώτο αρχείο.
            var selected = new List<string>();
            foreach (var g in _duplicateGroups)
            {
                var picked = g.Files.Where(f => f.IsSelected).Select(f => f.Path).ToList();
                if (picked.Count == g.Files.Count && picked.Count > 0) picked.RemoveAt(0);
                selected.AddRange(picked);
            }
            if (selected.Count == 0)
            {
                ThemedMessageBox.Show(LanguageService.T("Health_NoSelectionMsg"), LanguageService.T("Advanced_DuplicateFinder"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (ThemedMessageBox.Show(string.Format(LanguageService.T("Advanced_DeleteDuplicatesConfirm"), selected.Count),
                    LanguageService.T("Health_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            BtnDeleteDuplicates.IsEnabled = false;
            StatusService.SetBusy(LanguageService.T("Advanced_DeletingDuplicates"));
            var deleted = await DuplicateFileService.DeleteAsync(selected);
            StatusService.SetIdle(LanguageService.T("Ready"));

            var deletedSet = new HashSet<string>(selected);
            foreach (var g in _duplicateGroups.ToList())
            {
                foreach (var f in g.Files.Where(f => deletedSet.Contains(f.Path)).ToList()) g.Files.Remove(f);
                if (g.Files.Count < 2) _duplicateGroups.Remove(g);
            }
            TxtDuplicateStatus.Text = string.Format(LanguageService.T("Advanced_DuplicatesDeletedPrefix"), deleted);
            BtnDeleteDuplicates.IsEnabled = _duplicateGroups.Count > 0;
            s_duplicateStatusCache = TxtDuplicateStatus.Text;
        }

        // ΝΕΟ - roadmap "Κλείδωμα φακέλων με κωδικούς" (ρητό αίτημα χρήστη) - βλ. FolderLockService για
        // τον μηχανισμό (AES-256-GCM, δεν χρησιμοποιεί BitLocker λόγω περιορισμού Windows Home).
        private string? _lockFolderPath;
        private string? _unlockVaultPath;

        private void BtnChooseLockFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = LanguageService.T("Advanced_ChooseFolder") };
            if (dialog.ShowDialog() != true) return;
            _lockFolderPath = dialog.FolderName;
            TxtLockFolderPath.Text = _lockFolderPath;
        }

        private async void BtnLockFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lockFolderPath))
            {
                ThemedMessageBox.Show(LanguageService.T("FolderLock_NoFolderSelected"), LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var pwd = PwdLock.Password;
            if (string.IsNullOrEmpty(pwd) || pwd != PwdLockConfirm.Password)
            {
                ThemedMessageBox.Show(LanguageService.T("FolderLock_PasswordMismatch"), LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ThemedMessageBox.Show($"{LanguageService.T("FolderLock_LockConfirmPrefix")}{_lockFolderPath}{LanguageService.T("FolderLock_LockConfirmSuffix")}",
                    LanguageService.T("Sys_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            BtnLockFolder.IsEnabled = false;
            var progress = new Progress<string>(msg => { TxtFolderLockStatus.Text = msg; StatusService.SetBusy(msg); });
            try
            {
                await FolderLockService.LockFolderAsync(_lockFolderPath, pwd, progress);
                TxtFolderLockStatus.Text = $"{LanguageService.T("FolderLock_LockedDonePrefix")}{_lockFolderPath}{FolderLockService.VaultExtension}";
                TxtLockFolderPath.Text = "";
                PwdLock.Password = "";
                PwdLockConfirm.Password = "";
                _lockFolderPath = null;
            }
            catch (Exception ex)
            {
                TxtFolderLockStatus.Text = $"{LanguageService.T("FolderLock_Failed")}{ex.Message}";
            }
            finally
            {
                StatusService.SetIdle(LanguageService.T("Ready"));
                BtnLockFolder.IsEnabled = true;
            }
        }

        private void BtnChooseVault_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = LanguageService.T("FolderLock_ChooseVault"), Filter = $"Vault (*{FolderLockService.VaultExtension})|*{FolderLockService.VaultExtension}" };
            if (dialog.ShowDialog() != true) return;
            _unlockVaultPath = dialog.FileName;
            TxtUnlockVaultPath.Text = _unlockVaultPath;
        }

        private async void BtnUnlockFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_unlockVaultPath))
            {
                ThemedMessageBox.Show(LanguageService.T("FolderLock_NoVaultSelected"), LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var pwd = PwdUnlock.Password;
            if (string.IsNullOrEmpty(pwd)) return;

            var destination = _unlockVaultPath[..^FolderLockService.VaultExtension.Length];
            BtnUnlockFolder.IsEnabled = false;
            var progress = new Progress<string>(msg => { TxtFolderLockStatus.Text = msg; StatusService.SetBusy(msg); });
            try
            {
                await FolderLockService.UnlockFolderAsync(_unlockVaultPath, pwd, destination, progress);
                TxtFolderLockStatus.Text = $"{LanguageService.T("FolderLock_UnlockedDonePrefix")}{destination}";
                TxtUnlockVaultPath.Text = "";
                PwdUnlock.Password = "";
                _unlockVaultPath = null;
            }
            catch (UnauthorizedAccessException)
            {
                TxtFolderLockStatus.Text = LanguageService.T("FolderLock_WrongPassword");
            }
            catch (Exception ex)
            {
                TxtFolderLockStatus.Text = $"{LanguageService.T("FolderLock_Failed")}{ex.Message}";
            }
            finally
            {
                StatusService.SetIdle(LanguageService.T("Ready"));
                BtnUnlockFolder.IsEnabled = true;
            }
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
            StatusService.SetBusy($"{LanguageService.T("Sys_UpdatingServicePrefix")}{row.Service.DisplayName}...");
            var ok = row.IsAutomatic
                ? await SystemService.RestoreServiceStartModeAsync(row.Service.ServiceName)
                : await SystemService.SetServiceManualAsync(row.Service.ServiceName);
            StatusService.SetIdle(LanguageService.T("Ready"));
            if (!ok) ThemedMessageBox.Show(LanguageService.T("Sys_ServiceChangeFailed"), LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async void BtnStartBenchmark_Click(object sender, RoutedEventArgs e)
        {
            if (CmbBenchmarkDrive.SelectedItem is not string drive) return;
            BtnStartBenchmark.IsEnabled = false;
            TxtBenchWrite.Text = "..."; TxtBenchRead.Text = "...";
            StatusService.SetBusy($"{LanguageService.T("Sys_BenchmarkRunningPrefix")}{drive}...");
            var result = await SystemService.RunBenchmarkAsync(drive);
            StatusService.SetIdle(LanguageService.T("Ready"));
            BtnStartBenchmark.IsEnabled = true;
            if (result.Error != null)
            {
                TxtBenchWrite.Text = "--"; TxtBenchRead.Text = "--";
                ThemedMessageBox.Show(result.Error, LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            TxtBenchWrite.Text = $"{result.WriteMbps} MB/s";
            TxtBenchRead.Text = $"{result.ReadMbps} MB/s";
        }

        public PowerToolsCards()
        {
            InitializeComponent();
            ListStartup.ItemsSource = _startup;
            ListServices.ItemsSource = _services;
            ListDuplicateGroups.ItemsSource = _duplicateGroups;

            if (s_duplicateFolder != null)
            {
                TxtDuplicateFolder.Text = s_duplicateFolder;
                BtnScanDuplicates.IsEnabled = true;
            }
            if (s_duplicateStatusCache != null) TxtDuplicateStatus.Text = s_duplicateStatusCache;
            BtnDeleteDuplicates.IsEnabled = _duplicateGroups.Count > 0;

            _ = LoadBenchmarkDrivesAsync();
            _ = LoadStartupAsync();
            _ = LoadServicesAsync();
        }
    }
}
