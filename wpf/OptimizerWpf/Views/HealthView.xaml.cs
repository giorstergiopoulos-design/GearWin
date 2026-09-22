using System;
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
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        // ΔΙΟΡΘΩΣΗ (bug εντοπίστηκε - χρήστης ανέφερε: "τα αποτελέσματα των σαρώσεων χάνονται όταν
        // αλλάζω καρτέλα") - static αντί για instance, ίδιο μοτίβο με τις υπόλοιπες καρτέλες - το
        // BtnRegCleanScan_Click scan ΔΕΝ τρέχει αυτόματα στον constructor, άρα χωρίς αυτό η λίστα
        // ευρημάτων εξαφανιζόταν σε κάθε επιστροφή στην καρτέλα Υγεία & Συντήρηση.
        private static readonly ObservableCollection<RegistryFindingRow> _findings = new();
        private static string? s_regCleanStatusCache;
        // Instance (ΟΧΙ static) - φορτώνεται αυτόματα ξανά κάθε φορά (RefreshRegBackupsAsync
        // παρακάτω), ο χρήστης θέλει το φρέσκο ιστορικό αντιγράφων ασφαλείας, όχι παλιό στιγμιότυπο.
        private readonly ObservableCollection<RegistryBackupRow> _regBackups = new();

        public HealthView()
        {
            InitializeComponent();
            ListRegFindings.ItemsSource = _findings;
            ListRegBackups.ItemsSource = _regBackups;
            ListBrowsers.ItemsSource = HealthCleanupService.DetectBrowsers().Select(BrowserRow.From).ToList();
            if (s_regCleanStatusCache != null) TxtRegCleanStatus.Text = s_regCleanStatusCache;
            _ = RefreshWinREAsync();
            _ = RefreshRegBackupsAsync();
            LoadDiskTrend();
        }

        // ΝΕΟ - roadmap "Τάση υγείας δίσκου (S.M.A.R.T.)" - δείχνει τα στιγμιότυπα που έχει ήδη
        // καταγράψει το SystemView (βλ. DiskHealthHistoryService) - αν ο χρήστης δεν έχει ανοίξει ποτέ
        // την καρτέλα Σύστημα, δεν υπάρχει ακόμα ιστορικό - honest empty state, όχι ψεύτικα δεδομένα.
        private void LoadDiskTrend()
        {
            var all = DiskHealthHistoryService.GetAll();
            var rows = all.Where(kv => kv.Value.Count > 0).Select(kv =>
            {
                var entries = kv.Value.TakeLast(10);
                var summary = string.Join("\n", entries.Select(s =>
                {
                    var healthText = s.Health switch { "Healthy" => LanguageService.T("Sys_DriveHealthy"), "Warning" => LanguageService.T("Sys_DriveWarning"), _ => LanguageService.T("Sys_DriveHealthUnknown") };
                    return $"{s.Date}  {healthText,-14}  {s.FreeGb:0.#}/{s.TotalGb:0.#} GB";
                }));
                return new DiskTrendRow(kv.Key, summary);
            }).ToList();

            ListDiskTrend.ItemsSource = rows;
            TxtNoDiskTrend.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private record DiskTrendRow(string Drive, string Summary);

        // Ρητό αίτημα χρήστη: "στα κουμπιά βάλε τα logos των εφαρμογών" - πραγματικό εικονίδιο από το
        // ίδιο το εγκατεστημένο πρόγραμμα περιήγησης (βλ. AppIconService), γνωστές τοποθεσίες πρώτα
        // (πιο αξιόπιστο για browsers - το Edge π.χ. δεν έχει πάντα καθαρή εγγραφή Uninstall).
        internal static readonly Dictionary<string, string[]> BrowserExeCandidates = new()
        {
            ["Chrome"] = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            },
            ["Edge"] = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            },
            ["Brave"] = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
            },
            ["Opera"] = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Opera", "launcher.exe"),
            },
            ["Firefox"] = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mozilla Firefox", "firefox.exe"),
            },
        };

        // ===== Εργαλεία Υγείας & Συντήρησης Συστήματος (ρητό αίτημα χρήστη - βλ. σχόλιο στο XAML) =====

        private async void RunHealthToolAsync(string busyLabel, System.Func<System.Threading.Tasks.Task<(bool Success, string Output)>> action)
        {
            TxtHealthToolsStatus.Text = $"{busyLabel}...";
            StatusService.SetBusy(busyLabel);
            var (success, output) = await action();
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtHealthToolsStatus.Text = success ? $"{busyLabel}{LanguageService.T("Health_ToolCompletedSuffix")}" : $"{busyLabel}{LanguageService.T("Health_ToolFailedSuffix")}";
            var shown = output.Length > 3000 ? output[..3000] + LanguageService.T("Health_OutputTruncatedSuffix") : output;
            ThemedMessageBox.Show(string.IsNullOrWhiteSpace(shown) ? LanguageService.T("Health_NoOutput") : shown, busyLabel, MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void BtnSfc_Click(object sender, RoutedEventArgs e) => RunHealthToolAsync(LanguageService.T("Health_SfcBtn"), HealthCleanupService.RunSfcScanAsync);
        private void BtnDismCheckHealth_Click(object sender, RoutedEventArgs e) => RunHealthToolAsync("DISM CheckHealth", HealthCleanupService.RunDismCheckHealthAsync);

        private void BtnDismRestoreHealth_Click(object sender, RoutedEventArgs e)
        {
            if (ThemedMessageBox.Show(LanguageService.T("Health_DismRestoreConfirm"),
                    LanguageService.T("Health_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            RunHealthToolAsync(LanguageService.T("Health_DismRestoreFullBtn"), HealthCleanupService.RunDismRestoreHealthAsync);
        }

        private void BtnChkdsk_Click(object sender, RoutedEventArgs e)
        {
            if (ThemedMessageBox.Show(LanguageService.T("Health_ChkdskConfirm"),
                    LanguageService.T("Health_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            RunHealthToolAsync(LanguageService.T("Health_ChkdskBtn"), HealthCleanupService.RunChkdskAsync);
        }

        private void BtnWinSxsCleanup_Click(object sender, RoutedEventArgs e) => RunHealthToolAsync(LanguageService.T("Health_WinSxsBtn"), HealthCleanupService.RunWinSxsCleanupAsync);

        private async void BtnRetrim_Click(object sender, RoutedEventArgs e)
        {
            TxtHealthToolsStatus.Text = LanguageService.T("Health_RunningRetrim");
            StatusService.SetBusy(LanguageService.T("Health_OptimizingRetrim"));
            var ok = await HealthCleanupService.OptimizeSsdRetrimAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtHealthToolsStatus.Text = ok ? LanguageService.T("Health_RetrimDone") : LanguageService.T("Health_RetrimFailed");
        }

        private async void BtnFixShortcuts_Click(object sender, RoutedEventArgs e)
        {
            TxtHealthToolsStatus.Text = LanguageService.T("Health_ScanningShortcuts");
            StatusService.SetBusy(LanguageService.T("Health_FindingBrokenShortcuts"));
            var count = await HealthCleanupService.FixBrokenShortcutsAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtHealthToolsStatus.Text = $"{LanguageService.T("Health_ShortcutsFoundPrefix")}{count}{LanguageService.T("Health_ShortcutsFoundSuffix")}";
        }

        private void BtnDotNetVersion_Click(object sender, RoutedEventArgs e) =>
            ThemedMessageBox.Show(HealthCleanupService.GetDotNetFrameworkVersionInfo(), LanguageService.T("Health_DotNetBtn"), MessageBoxButton.OK, MessageBoxImage.Information);

        private async void BtnRegCleanScan_Click(object sender, RoutedEventArgs e)
        {
            TxtRegCleanStatus.Text = LanguageService.T("Health_ScanInProgress");
            StatusService.SetBusy(LanguageService.T("Health_ScanningRegistry"));
            _findings.Clear();

            var results = await HealthCleanupService.ScanRegistryAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            foreach (var f in results) _findings.Add(new RegistryFindingRow(f));
            TxtRegCleanStatus.Text = results.Count == 0
                ? LanguageService.T("Health_NoCleanupItems")
                : $"{LanguageService.T("Health_ItemsFoundPrefix")}{results.Count}{LanguageService.T("Health_ItemsFoundSuffix")}";
            s_regCleanStatusCache = TxtRegCleanStatus.Text;
        }

        private async void BtnRegCleanDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = _findings.Where(f => f.IsSelected).Select(f => f.Finding).ToList();
            if (selected.Count == 0)
            {
                // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): σιωπηλό return, καμία ένδειξη στον χρήστη -
                // port του ps1's Show-CustomMessage "Δεν έχετε επιλέξει καμία εγγραφή...".
                ThemedMessageBox.Show(LanguageService.T("Health_NoSelectionMsg"), LanguageService.T("Health_RegCleanTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = ThemedMessageBox.Show(
                $"{LanguageService.T("Health_DeleteConfirmPrefix")}{selected.Count}{LanguageService.T("Health_DeleteConfirmSuffix")}",
                LanguageService.T("Health_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            // ΔΙΟΡΘΩΣΗ (εντοπίστηκε κατά την υλοποίηση του Registry Backup History παρακάτω): πριν, το
            // backup πήγαινε σε προσωρινό, χρονοσφραγισμένο φάκελο μέσα στο %TEMP% - χάνεται εύκολα
            // (Windows/Disk Cleanup καθαρίζουν το TEMP περιοδικά) ΚΑΙ δεν ήταν ορατό στη νέα λίστα
            // ιστορικού backup (που διαβάζει το ΙΔΙΟ μόνιμο φάκελο με το HealthCheckWindow's optimize
            // flow). Τώρα χρησιμοποιεί το ΙΔΙΟ μόνιμο HealthCleanupService.RegistryBackupsDir - όλα τα
            // registry backups (από ΟΠΟΙΟΔΗΠΟΤΕ σημείο της εφαρμογής) καταλήγουν πλέον σε ΕΝΑ μέρος.
            var backupDir = HealthCleanupService.RegistryBackupsDir;
            Directory.CreateDirectory(backupDir);

            StatusService.SetBusy(LanguageService.T("Health_DeletingRegistryItems"));
            var ok = await HealthCleanupService.DeleteFindingsAsync(selected, backupDir);
            StatusService.SetIdle(LanguageService.T("Ready"));

            TxtRegCleanStatus.Text = ok
                ? $"{LanguageService.T("Health_DeletedItemsPrefix")}{selected.Count}{LanguageService.T("Health_DeletedItemsMid")}{backupDir}"
                : LanguageService.T("Health_DeletePartialFailed");
            s_regCleanStatusCache = TxtRegCleanStatus.Text;
            foreach (var f in selected) _findings.Remove(_findings.First(r => r.Finding == f));
            await RefreshRegBackupsAsync();
        }

        private async System.Threading.Tasks.Task RefreshRegBackupsAsync()
        {
            var backups = await HealthCleanupService.ListRegistryBackupsAsync();
            _regBackups.Clear();
            foreach (var b in backups) _regBackups.Add(new RegistryBackupRow(b));
            TxtRegBackupStatus.Text = backups.Count == 0
                ? LanguageService.T("Health_RegBackupEmpty")
                : $"{backups.Count} {LanguageService.T("Health_RegBackupCountSuffix")}";
        }

        private async void BtnRefreshRegBackups_Click(object sender, RoutedEventArgs e) => await RefreshRegBackupsAsync();

        private void BtnOpenRegBackupFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(HealthCleanupService.RegistryBackupsDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{HealthCleanupService.RegistryBackupsDir}\"") { UseShellExecute = true });
        }

        private async void BtnRestoreRegBackup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: RegistryBackupRow row }) return;
            var confirm = ThemedMessageBox.Show(
                $"{LanguageService.T("Health_RestoreConfirmPrefix")}{row.DisplayName}{LanguageService.T("Health_RestoreConfirmSuffix")}",
                LanguageService.T("Health_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            StatusService.SetBusy(LanguageService.T("Health_RestoringBackup"));
            var ok = await HealthCleanupService.RestoreRegistryBackupAsync(row.FullPath);
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtRegBackupStatus.Text = ok ? LanguageService.T("Health_RestoreDone") : LanguageService.T("Health_RestoreFailed");
            if (ok) (Window.GetWindow(this) as OptimizerWpf.MainWindow)?.ShowToast(LanguageService.T("Health_RestoreDone"));
        }

        private async void BtnClearBrowserCache_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: BrowserRow row }) return;
            var browser = row.Entry;
            // ΝΕΟ (roadmap: "σύγκριση πριν/μετά σε καθαρισμούς") - ελεύθερος χώρος στον δίσκο συστήματος
            // πριν/μετά τον καθαρισμό, ώστε ο χρήστης να βλέπει το πραγματικό όφελος, όχι μόνο "έγινε".
            var freeBefore = GetSystemDriveFreeGb();
            TxtBrowserCacheStatus.Text = $"{LanguageService.T("Health_ClearingCacheForPrefix")}{browser.Label}...";
            StatusService.SetBusy($"{LanguageService.T("Health_ClearingCachePrefix")}{browser.Label}...");
            await HealthCleanupService.ClearBrowserCacheAsync(browser);
            StatusService.SetIdle(LanguageService.T("Ready"));
            var freed = GetSystemDriveFreeGb() - freeBefore;
            var freedText = freed > 0.05 ? $" (+{freed:0.0} GB)" : "";
            TxtBrowserCacheStatus.Text = $"{LanguageService.T("Health_CacheClearedPrefix")}{browser.Label}{LanguageService.T("Health_CacheClearedSuffix")}{freedText}";
        }

        private static double GetSystemDriveFreeGb()
        {
            try { return new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!).AvailableFreeSpace / 1024.0 / 1024 / 1024; }
            catch { return 0; }
        }

        private async void BtnWinRERefresh_Click(object sender, RoutedEventArgs e) => await RefreshWinREAsync();

        private async void BtnWinREEnable_Click(object sender, RoutedEventArgs e)
        {
            var confirm = ThemedMessageBox.Show(
                LanguageService.T("Health_WinREEnableConfirm"),
                LanguageService.T("Health_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            StatusService.SetBusy(LanguageService.T("Health_EnablingWinRE"));
            await HealthCleanupService.EnableWinREAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            await RefreshWinREAsync();
        }

        // ΝΕΟ - roadmap "Αναφορά μπαταρίας".
        private async void BtnBatteryReport_Click(object sender, RoutedEventArgs e)
        {
            StatusService.SetBusy(LanguageService.T("Health_GeneratingBatteryReport"));
            var ok = await HealthCleanupService.GenerateBatteryReportAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtBatteryReportStatus.Text = ok ? LanguageService.T("Health_BatteryReportOpened") : LanguageService.T("Health_BatteryReportFailed");
            if (ok) (Window.GetWindow(this) as OptimizerWpf.MainWindow)?.ShowToast(LanguageService.T("Health_BatteryReportOpened"));
        }

        // ΝΕΟ - roadmap "Διαγνωστική αναφορά με ένα κλικ".
        private async void BtnDiagnosticsZip_Click(object sender, RoutedEventArgs e)
        {
            StatusService.SetBusy(LanguageService.T("Health_GeneratingDiagnosticsZip"));
            var path = await HealthCleanupService.GenerateDiagnosticsZipAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtDiagnosticsZipStatus.Text = path != null
                ? $"{LanguageService.T("Health_DiagnosticsZipSavedPrefix")}{path}"
                : LanguageService.T("Health_DiagnosticsZipFailed");
            // ΝΕΟ - roadmap "Toast/snackbar για ολοκλήρωση εργασιών παρασκηνίου" - ακριβώς το
            // παράδειγμα που περιγράφηκε στο ίδιο το roadmap item.
            if (path != null) (Window.GetWindow(this) as OptimizerWpf.MainWindow)?.ShowToast(LanguageService.T("Health_DiagnosticsZipToast"));
        }

        private async System.Threading.Tasks.Task RefreshWinREAsync()
        {
            var result = await HealthCleanupService.GetWinREStatusAsync();
            if (!result.Success)
            {
                TxtWinREStatus.Text = LanguageService.T("Health_WinREStatusUnknownAdmin");
                TxtWinREStatus.Foreground = (Brush)FindResource("SubTextBrush");
            }
            else if (result.Enabled)
            {
                TxtWinREStatus.Text = LanguageService.T("Health_WinREStatusEnabled");
                TxtWinREStatus.Foreground = new SolidColorBrush(Color.FromRgb(90, 200, 120));
            }
            else
            {
                TxtWinREStatus.Text = LanguageService.T("Health_WinREStatusDisabled");
                TxtWinREStatus.Foreground = new SolidColorBrush(Color.FromRgb(230, 90, 90));
            }
        }
    }

    public class RegistryFindingRow : INotifyPropertyChanged
    {
        public RegistryFinding Finding { get; }
        // ΝΕΟ - βελτίωση: η ακριβής registry τοποθεσία (+ValueName αν υπάρχει) που θα διαγραφεί -
        // βλ. σχόλιο στο HealthView.xaml.
        public string RegPathDisplay => string.IsNullOrEmpty(Finding.ValueName) ? Finding.RegPath : $"{Finding.RegPath}\\{Finding.ValueName}";
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }
        public RegistryFindingRow(RegistryFinding finding) => Finding = finding;
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class RegistryBackupRow
    {
        public string DisplayName { get; }
        public string SubText { get; }
        public string FullPath { get; }

        public RegistryBackupRow(RegistryBackupEntry entry)
        {
            DisplayName = entry.FileName;
            FullPath = entry.FullPath;
            SubText = $"{entry.CreatedAt:yyyy-MM-dd HH:mm}  •  {QuickCleanService.FormatSize(entry.SizeBytes)}";
        }
    }

    // "Τυλίγει" ένα BrowserCacheEntry με το εικονίδιό του (ρητό αίτημα χρήστη - βλ.
    // AppIconService/LogoCacheService). Προτεραιότητα στο κατεβασμένο/cached λογότυπο του επίσημου
    // site (συνεπές με τις προτεινόμενες εφαρμογές) - το τοπικό εικονίδιο του εγκατεστημένου exe
    // παραμένει fallback αν η λήψη αποτύχει (π.χ. χωρίς σύνδεση).
    public class BrowserRow : INotifyPropertyChanged
    {
        public BrowserCacheEntry Entry { get; init; } = null!;
        public string Label => Entry.Label;

        private ImageSource? _icon;
        public ImageSource? Icon
        {
            get => _icon;
            set { _icon = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public static BrowserRow From(BrowserCacheEntry entry)
        {
            var row = new BrowserRow { Entry = entry };
            _ = LoadIconAsync(row);
            return row;
        }

        private static async System.Threading.Tasks.Task LoadIconAsync(BrowserRow row)
        {
            row.Icon = await LogoCacheService.GetLogoAsync(row.Entry.Name);
            if (row.Icon != null) return;
            if (HealthView.BrowserExeCandidates.TryGetValue(row.Entry.Name, out var candidates))
                row.Icon = candidates.Select(AppIconService.GetIconForFile).FirstOrDefault(i => i != null);
            row.Icon ??= AppIconService.GetIconForFile(AppIconService.FindInstalledIconPath(row.Entry.Label) ?? "");
        }
    }
}
