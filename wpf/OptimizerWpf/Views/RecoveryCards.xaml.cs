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
    public partial class RecoveryCards : UserControl
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);
        private readonly ObservableCollection<RestorePointInfo> _restorePoints = new();


        // ΝΕΟ - roadmap "Backup/imaging" - λίστα υποψήφιων δίσκων-στόχων για το wbadmin system image
        // backup: ΟΛΟΙ οι έτοιμοι δίσκοι (σταθεροί + αφαιρούμενοι/USB, συνηθισμένος στόχος για τέτοιου
        // είδους backup) ΕΚΤΟΣ από τον δίσκο συστήματος - το wbadmin ούτως ή άλλως θα απέτυχε αν ο
        // χρήστης διάλεγε τον δίσκο συστήματος ως στόχο, αλλά προτιμότερο να μη φαίνεται καν επιλογή.
        private async System.Threading.Tasks.Task LoadBackupTargetDrivesAsync()
        {
            var systemDrive = Path.GetPathRoot(System.Environment.SystemDirectory)?.TrimEnd('\\') ?? "C:";
            var names = await System.Threading.Tasks.Task.Run(() =>
                DriveInfo.GetDrives()
                    .Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable))
                    .Select(d => d.Name.TrimEnd('\\'))
                    .Where(n => !string.Equals(n, systemDrive, System.StringComparison.OrdinalIgnoreCase))
                    .ToList());
            foreach (var name in names) ListBackupTargetDrives.Items.Add(name);
            if (ListBackupTargetDrives.Items.Count > 0) ListBackupTargetDrives.SelectedIndex = 0;
        }

        private async void BtnStartBackup_Click(object sender, RoutedEventArgs e)
        {
            if (ListBackupTargetDrives.SelectedItem is not string targetDrive) return;
            TxtBackupStatus.Text = LanguageService.T("Backup_Running");
            StatusService.SetBusy(LanguageService.T("Backup_Running"));
            var (success, _) = await BackupService.StartSystemImageBackupAsync(targetDrive);
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtBackupStatus.Text = LanguageService.T(success ? "Backup_Success" : "Backup_Failed");
        }

        private async void BtnViewBackupVersions_Click(object sender, RoutedEventArgs e)
        {
            var versions = await BackupService.GetBackupVersionsAsync();
            ListBackupVersions.ItemsSource = versions.Count > 0 ? versions : new List<string> { LanguageService.T("Backup_NoVersionsFound") };
        }

        private async System.Threading.Tasks.Task LoadRestorePointsAsync()
        {
            TxtRestorePointsStatus.Text = LanguageService.T("Sys_LoadingEllipsis");
            _restorePoints.Clear();
            foreach (var p in await SystemService.ListRestorePointsAsync()) _restorePoints.Add(p);
            TxtRestorePointsStatus.Text = $"{_restorePoints.Count}{LanguageService.T("Sys_RestorePointsSuffix")}";
        }

        private async void BtnRefreshRestorePoints_Click(object sender, RoutedEventArgs e) => await LoadRestorePointsAsync();

        private async void BtnCreateRestorePoint_Click(object sender, RoutedEventArgs e)
        {
            StatusService.SetBusy(LanguageService.T("Sys_CreatingRestorePoint"));
            var ok = await SystemService.CreateRestorePointAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtRestorePointsStatus.Text = ok ? LanguageService.T("Sys_RestorePointCreated") : LanguageService.T("Sys_RestorePointFailed");
            if (ok) await LoadRestorePointsAsync();
        }

        private void BtnOpenProtectionSettings_Click(object sender, RoutedEventArgs e) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("SystemPropertiesProtection.exe") { UseShellExecute = true });

        // Ρητά ΔΕΝ υλοποιείται delete-by-sequence - βλ. σχόλιο στο SystemService.RestoreToPointAsync.
        private async void BtnRestoreToPoint_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: RestorePointInfo point }) return;
            if (ThemedMessageBox.Show($"{LanguageService.T("Sys_RestoreConfirmPrefix")}{point.Description}{LanguageService.T("Sys_RestoreConfirmSuffix")}",
                    LanguageService.T("Sys_RestoreWarningTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            StatusService.SetBusy(LanguageService.T("Sys_RestoringSystem"));
            var ok = await SystemService.RestoreToPointAsync(point.SequenceNumber);
            // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): το αποτέλεσμα αγνοούνταν εντελώς - σε επιτυχία ο
            // υπολογιστής επανεκκινεί άμεσα (η γραμμή κατάστασης παύει να έχει σημασία), αλλά σε αποτυχία
            // έμενε μόνιμα κολλημένη σε "Επαναφορά συστήματος..." χωρίς καμία εξήγηση.
            if (!ok)
            {
                StatusService.SetIdle(LanguageService.T("Sys_RestoreFailed"));
                ThemedMessageBox.Show(LanguageService.T("Sys_RestoreFailed"), LanguageService.T("Sys_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public RecoveryCards()
        {
            InitializeComponent();
            ListRestorePoints.ItemsSource = _restorePoints;
            _ = LoadRestorePointsAsync();
            _ = LoadBackupTargetDrivesAsync();
        }
    }
}
