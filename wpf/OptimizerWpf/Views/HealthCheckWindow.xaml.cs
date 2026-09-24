using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    // ΝΕΟ - ρητό αίτημα χρήστη: "θέλω αντίστοιχο γραφικό με το Health Check του PC Manager" +
    // "το 'PC status can be optimized' σε δευτερεύον παράθυρο". Δύο φάσεις: σάρωση (πραγματικά scans
    // από ήδη υπάρχοντα services, ΕΝΑ pill φωτίζεται τη φορά) -> αποτελέσματα (κάρτες σύνοψης +
    // Βελτιστοποίηση Τώρα). Καλείται από το Home tab.
    public partial class HealthCheckWindow : Window
    {
        private readonly ObservableCollection<ScanChecklistRow> _checklist = new();
        private readonly ObservableCollection<ResultCardVm> _results = new();
        private bool _cancelled;

        // Ευρήματα από τη σάρωση - διατηρούνται για να τα εφαρμόσει το BtnOptimizeNow_Click.
        private IReadOnlyList<QuickCleanItem> _spaceItems = Array.Empty<QuickCleanItem>();
        private IReadOnlyList<RegistryFinding> _traceFindings = Array.Empty<RegistryFinding>();

        // ΝΕΟ - ρητό αίτημα χρήστη: το γράφημα/παράθυρο αποτελεσμάτων να προσθέσει κάτι πέρα από την
        // απλή αντιγραφή του PC Manager's "PC status can be optimized" pill layout. Το ήδη υπάρχον,
        // ήδη δοκιμασμένο HealthScoreService (χρησιμοποιείται αλλού στην Αρχική) υπολογίζει ΜΙΑ
        // πραγματική 0-100 βαθμολογία υγείας από 5 ανεξάρτητους ελέγχους (χώρος δίσκου, εκκρεμής
        // επανεκκίνηση, Defender, πλήθος εκκίνησης, σημεία επαναφοράς) - επαναχρησιμοποιείται εδώ ΩΣ
        // ΕΧΕΙ (καμία διπλή υλοποίηση) για να δώσει στο αποτέλεσμα μια συγκεκριμένη αριθμητική
        // βαθμολογία αντί του γενικού στατικού "!" εικονιδίου, ΚΑΙ μια επιπλέον κάρτα για τυχόν
        // ευρήματα που δεν καλύπτονται ήδη από τα 5 στάδια σάρωσης (π.χ. Defender off, reboot pending).
        private HealthResult? _scoreResult;

        public HealthCheckWindow()
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            ListScanChecklist.ItemsSource = _checklist;
            ListResultCards.ItemsSource = _results;
            Loaded += async (_, _) => await RunScanAsync();
        }

        private void SetPillState(FrameworkElement pill, bool active)
        {
            var border = (System.Windows.Controls.Border)pill;
            border.BorderBrush = active ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("CardBorderBrush");
            border.BorderThickness = new Thickness(active ? 2.5 : 1.5);
        }

        private async Task RunScanAsync()
        {
            var stages = new (System.Windows.Controls.Border Pill, string TitleKey, string DescKey, Func<Task> Run)[]
            {
                (PillNetwork, "HealthCheck_StageNetwork", "HealthCheck_DescNetwork", ScanNetworkAsync),
                (PillSpace, "HealthCheck_StageSpace", "HealthCheck_DescSpace", ScanSpaceAsync),
                (PillTraces, "HealthCheck_StageTraces", "HealthCheck_DescTraces", ScanTracesAsync),
                (PillRestore, "HealthCheck_StageRestore", "HealthCheck_DescRestore", ScanRestoreAsync),
                (PillApps, "HealthCheck_StageApps", "HealthCheck_DescApps", ScanAppsAsync),
            };

            // ΔΙΟΡΘΩΣΗ - roadmap "Καμία επαναφορά progress bar σε exception": χωρίς αυτό, ένα Retry
            // μετά από αποτυχία θα διπλασίαζε τις γραμμές του checklist (το foreach παρακάτω τις
            // προσθέτει ξανά χωρίς Clear).
            _checklist.Clear();
            BtnRetryScan.Visibility = Visibility.Collapsed;
            foreach (var stage in stages)
                _checklist.Add(new ScanChecklistRow(LanguageService.T(stage.TitleKey), LanguageService.T(stage.DescKey)));

            BarScanProgress.Maximum = stages.Length;
            BarScanProgress.Value = 0;
            for (var i = 0; i < stages.Length; i++)
            {
                if (_cancelled) return;
                var stage = stages[i];
                TxtCurrentStage.Text = $"{LanguageService.T("HealthCheck_Scanning")}{LanguageService.T(stage.TitleKey)}...";
                SetPillState(stage.Pill, true);
                var stageStarted = DateTime.UtcNow;

                // ΔΙΟΡΘΩΣΗ - roadmap "Καμία επαναφορά progress bar σε exception": πριν, μια εξαίρεση
                // σε ΟΠΟΙΟΔΗΠΟΤΕ στάδιο (π.χ. QuickCleanService.ScanAsync μέσα στο ScanSpaceAsync)
                // άφηνε το BarScanProgress παγωμένο, το pill μόνιμα φωτισμένο, και το παράθυρο ΠΟΤΕ
                // δεν έφτανε στη φάση αποτελεσμάτων - μόνο το καθολικό DispatcherUnhandledException
                // έδειχνε ένα message box, χωρίς να αγγίζει καθόλου την τοπική κατάσταση UI εδώ.
                try
                {
                    await stage.Run();
                }
                catch (Exception ex)
                {
                    SetPillState(stage.Pill, false);
                    TxtCurrentStage.Text = $"{LanguageService.T("HealthCheck_StageFailedPrefix")}{LanguageService.T(stage.TitleKey)}{LanguageService.T("HealthCheck_StageFailedSuffix")}{ex.Message}";
                    BtnRetryScan.Visibility = Visibility.Visible;
                    return;
                }

                // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "το γραφικό στον έλεγχο υγείας μένει για πολύ λίγο ενεργό
                // ως παράθυρο") - τα πραγματικά scans (ScanNetworkAsync/ScanRestoreAsync/κ.λπ.) συχνά
                // ολοκληρώνονται μέσα σε μερικά ms σε ένα γρήγορο σύστημα - το pill άναβε και έσβηνε
                // ΤΟΣΟ γρήγορα που ο χρήστης δεν πρόλαβε καν να το δει, ακυρώνοντας ουσιαστικά τον λόγο
                // ύπαρξης του γραφικού (ρητό αίτημα: "ανάλογα τι σκανάρεται εκείνη τη στιγμή να
                // φωτίζεται" - χρειάζεται να ΦΑΙΝΕΤΑΙ). Ελάχιστη ορατή διάρκεια ανά στάδιο, ΜΟΝΟ όταν
                // το πραγματικό scan ήταν πιο γρήγορο - ΚΑΜΙΑ επιβράδυνση σε αργά στάδια/συστήματα.
                var elapsed = DateTime.UtcNow - stageStarted;
                var minStageDuration = TimeSpan.FromMilliseconds(650);
                if (elapsed < minStageDuration) await Task.Delay(minStageDuration - elapsed);

                SetPillState(stage.Pill, false);
                _checklist[i].MarkDone();
                BarScanProgress.Value = i + 1;
            }

            if (_cancelled) return;
            _scoreResult = await Task.Run(HealthScoreService.Compute);
            // ΝΕΟ - roadmap ιδέα #7 (ρητό αίτημα χρήστη: "κάνε τα 4-7") - βλ.
            // Services/HealthScoreDailyHistoryService.cs (ΞΕΧΩΡΙΣΤΟ από το ήδη υπάρχον, in-memory
            // HealthScoreHistoryService που τροφοδοτεί το sparkline της Αρχικής), ίδιο μοτίβο με το
            // Disk Trend (Health tab).
            HealthScoreDailyHistoryService.RecordIfNeeded(_scoreResult.Score, _scoreResult.Issues?.Count ?? 0);
            BuildResults();
            ScanPanel.Visibility = Visibility.Collapsed;
            ResultPanel.Visibility = Visibility.Visible;

            // ΝΕΟ - roadmap "Προγραμματισμένος Πλήρης Έλεγχος Υγείας" - καταγράφει ΠΟΤΕ ολοκληρώθηκε
            // μια σάρωση, ώστε η Αρχική να μπορεί να υπενθυμίσει αν έχουν περάσει 7+ μέρες.
            AppSettingsService.Current.LastHealthCheckRunAt = DateTime.Now;
            AppSettingsService.Save();
        }

        private async void BtnRetryScan_Click(object sender, RoutedEventArgs e) => await RunScanAsync();

        private (bool Connected, int? LatencyMs) _networkStatus;
        private long _spaceFreeableBytes;
        private long _browserCacheBytes;
        private int _tracesCount;
        private int _restorableCount;
        private int _appsCount;

        private async Task ScanNetworkAsync() => _networkStatus = await NetworkService.CheckInternetAsync();

        private async Task ScanSpaceAsync()
        {
            _spaceItems = await QuickCleanService.ScanAsync();
            _browserCacheBytes = await HealthCleanupService.ScanBrowserCacheSizeAsync();
            _spaceFreeableBytes = _spaceItems.Where(i => i.Recommended).Sum(i => i.SizeBytes) + _browserCacheBytes;
        }

        private async Task ScanTracesAsync()
        {
            _traceFindings = await HealthCleanupService.ScanRegistryAsync();
            _tracesCount = _traceFindings.Count;
        }

        private async Task ScanRestoreAsync() => _restorableCount = await Task.Run(TweakService.CountRestorableTweaks);

        private async Task ScanAppsAsync()
        {
            var apps = await BloatwareService.ScanUwpAppsAsync();
            _appsCount = apps.Count;
        }

        private void BuildResults()
        {
            var anyIssue = _spaceFreeableBytes > 1024 * 1024 || _tracesCount > 0 || _restorableCount > 0;
            TxtResultHeadline.Text = LanguageService.T(anyIssue ? "HealthCheck_HeadlineIssues" : "HealthCheck_HeadlineGood");

            var score = _scoreResult?.Score ?? 100;
            TxtScoreBadge.Text = score.ToString();
            ScoreBadge.Background = new SolidColorBrush(score >= 80 ? Color.FromRgb(0x4C, 0xAF, 0x50) : score >= 50 ? Color.FromRgb(0xFF, 0x98, 0x00) : Color.FromRgb(0xE5, 0x39, 0x35));
            TxtScoreLine.Text = $"{LanguageService.T("HealthCheck_ScorePrefix")}{score}/100";

            _results.Add(new ResultCardVm("Space", LanguageService.T("HealthCheck_CardSpaceTitlePrefix") + QuickCleanService.FormatSize(_spaceFreeableBytes),
                LanguageService.T("HealthCheck_CardSpaceDesc"), _spaceFreeableBytes > 1024 * 1024, canSelect: true, isSelected: _spaceFreeableBytes > 1024 * 1024,
                actionLabel: null));

            _results.Add(new ResultCardVm("Traces", $"{_tracesCount} {LanguageService.T("HealthCheck_CardTracesTitleSuffix")}",
                LanguageService.T("HealthCheck_CardTracesDesc"), _tracesCount > 0, canSelect: true, isSelected: _tracesCount > 0,
                actionLabel: null));

            _results.Add(new ResultCardVm("Restore", $"{_restorableCount} {LanguageService.T("HealthCheck_CardRestoreTitleSuffix")}",
                LanguageService.T("HealthCheck_CardRestoreDesc"), _restorableCount > 0, canSelect: true, isSelected: _restorableCount > 0,
                actionLabel: null));

            _results.Add(new ResultCardVm("Apps", $"{_appsCount} {LanguageService.T("HealthCheck_CardAppsTitleSuffix")}",
                LanguageService.T("HealthCheck_CardAppsDesc"), false, canSelect: false, isSelected: false,
                actionLabel: LanguageService.T("HealthCheck_CardAppsAction")));

            var netLabel = _networkStatus.Connected
                ? $"{LanguageService.T("HealthCheck_CardNetworkOk")}{(_networkStatus.LatencyMs.HasValue ? $" ({_networkStatus.LatencyMs} ms)" : "")}"
                : LanguageService.T("HealthCheck_CardNetworkFail");
            _results.Add(new ResultCardVm("Network", netLabel, LanguageService.T("HealthCheck_CardNetworkDesc"), false, canSelect: false, isSelected: false,
                actionLabel: null));

            // ΝΕΟ - βλ. σχόλιο στο _scoreResult field: ευρήματα του HealthScoreService (Defender off,
            // εκκρεμής επανεκκίνηση, πολλά προγράμματα εκκίνησης, χωρίς πρόσφατο σημείο επαναφοράς)
            // που ΔΕΝ καλύπτονται ήδη από τα 5 στάδια σάρωσης παραπάνω - μόνο ενημερωτική κάρτα (χωρίς
            // CheckBox/επιλογή), ίδιο πνεύμα ασφάλειας με την κάρτα "Apps": καμία αυτόματη ενέργεια σε
            // ρυθμίσεις ασφαλείας/συστήματος χωρίς ρητή επιβεβαίωση του χρήστη στην αντίστοιχη καρτέλα.
            var extraIssues = _scoreResult?.Issues ?? Array.Empty<HealthIssue>();
            var scoreDesc = extraIssues.Count > 0
                ? $"{LanguageService.T("HealthCheck_CardScoreDesc")} {string.Join("  •  ", extraIssues.Select(i => i.Title))}"
                : LanguageService.T("HealthCheck_CardScoreAllGood");
            // ΝΕΟ - roadmap "HealthScoreService ευρήματα - ενέργεια διόρθωσης": ίδιο πνεύμα με την
            // κάρτα "Apps" - κουμπί που πηγαίνει κατευθείαν στην καρτέλα Σύστημα (καλύπτει Startup Apps
            // + Σημεία Επαναφοράς, 2 από τα 4 πιθανά είδη ευρήματος) αντί για καμία ενέργεια. Το Defender
            // ΔΕΝ ελέγχεται από καμία καρτέλα της εφαρμογής (μόνο εμφανίζεται, ΠΟΤΕ αλλάζεται αυτόματα -
            // βλ. Help_Home_HealthScore) - η καρτέλα Σύστημα παραμένει ο πιο λογικός γενικός προορισμός.
            _results.Add(new ResultCardVm("Score", $"{extraIssues.Count} {LanguageService.T("HealthCheck_CardScoreTitleSuffix")}",
                scoreDesc, extraIssues.Count > 0, canSelect: false, isSelected: false,
                actionLabel: extraIssues.Count > 0 ? LanguageService.T("HealthCheck_CardScoreAction") : null));
        }

        private async void BtnExportReport_Click(object sender, RoutedEventArgs e)
        {
            BtnExportReport.IsEnabled = false;
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"{LanguageService.T("HealthCheck_ScorePrefix")}{_scoreResult?.Score ?? 100}/100");
                sb.AppendLine();
                foreach (var card in _results)
                {
                    sb.AppendLine($"=== {card.Headline} ===");
                    sb.AppendLine(card.Description);
                    sb.AppendLine();
                }

                // ΝΕΟ - roadmap ιδέα #7 (ρητό αίτημα χρήστη: "κάνε τα 4-7" - "αρχείο τάσης στον
                // χρόνο") - το PDF εξαγόμενο εδώ πλέον περιλαμβάνει ΟΛΟΚΛΗΡΟ το ιστορικό βαθμολογιών
                // (όχι μόνο το τρέχον στιγμιότυπο παραπάνω), ίδιο πνεύμα με το Disk Trend section.
                var history = HealthScoreDailyHistoryService.GetHistory();
                if (history.Count > 1)
                {
                    sb.AppendLine($"=== {LanguageService.T("HealthCheck_TrendSectionTitle")} ===");
                    foreach (var snap in history)
                        sb.AppendLine($"{snap.Date}   {snap.Score}/100   ({snap.IssueCount} {LanguageService.T("HealthCheck_TrendIssuesSuffix")})");
                    sb.AppendLine();
                }

                var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    $"HealthCheck_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
                await Task.Run(() => PdfExportService.ExportTextReport(path, LanguageService.T("HealthCheck_Title"), sb.ToString()));
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                TxtOptimizeResult.Text = $"{LanguageService.T("HealthCheck_ExportReportSaved")}{path}";
            }
            catch (Exception ex)
            {
                TxtOptimizeResult.Text = $"{LanguageService.T("HealthCheck_ExportReportFailed")} {ex.Message}";
            }
            finally
            {
                BtnExportReport.IsEnabled = true;
            }
        }

        private async void BtnOptimizeNow_Click(object sender, RoutedEventArgs e)
        {
            BtnOptimizeNow.IsEnabled = false;
            TxtOptimizeResult.Text = LanguageService.T("HealthCheck_Optimizing");
            StatusService.SetBusy(LanguageService.T("HealthCheck_Optimizing"));

            var summary = new List<string>();
            var spaceCleaned = false;
            var tracesCleaned = false;
            var restoreCleaned = false;

            var spaceCard = _results.FirstOrDefault(r => r.Key == "Space");
            if (spaceCard is { IsSelected: true })
            {
                var keys = _spaceItems.Where(i => i.Recommended).Select(i => i.Key).ToList();
                var freed = await QuickCleanService.CleanAsync(keys);
                if (_browserCacheBytes > 0)
                {
                    foreach (var browser in HealthCleanupService.DetectBrowsers())
                        await HealthCleanupService.ClearBrowserCacheAsync(browser);
                    freed += _browserCacheBytes;
                }
                ImpactTrackingService.RecordBytesFreed(freed);
                summary.Add(string.Format(LanguageService.T("HealthCheck_SummarySpace"), QuickCleanService.FormatSize(freed)));
                spaceCleaned = true;
            }

            var tracesCard = _results.FirstOrDefault(r => r.Key == "Traces");
            if (tracesCard is { IsSelected: true } && _traceFindings.Count > 0)
            {
                var backupDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf", "RegistryBackups");
                System.IO.Directory.CreateDirectory(backupDir);
                // ΔΙΟΡΘΩΣΗ - το DeleteFindingsAsync() ΕΠΙΣΤΡΕΦΕΙ bool επιτυχίας (ψευδές αν π.χ. reg.exe
                // export ή DeleteSubKeyTree αποτύχει λόγω δικαιωμάτων) - πριν αγνοούνταν εντελώς, οπότε
                // η περίληψη έλεγε ΠΑΝΤΑ "καθαρίστηκαν N καταχωρήσεις" ακόμα κι αν η διαγραφή απέτυχε
                // σιωπηλά. Τώρα ελέγχεται και αναφέρεται ειλικρινά αν κάτι πήγε στραβά.
                var tracesOk = await HealthCleanupService.DeleteFindingsAsync(_traceFindings, backupDir);
                summary.Add(tracesOk
                    ? string.Format(LanguageService.T("HealthCheck_SummaryTraces"), _traceFindings.Count)
                    : LanguageService.T("HealthCheck_SummaryTracesFailed"));
                tracesCleaned = true;
            }

            var restoreCard = _results.FirstOrDefault(r => r.Key == "Restore");
            if (restoreCard is { IsSelected: true })
            {
                var reverted = await Task.Run(TweakService.RestoreAllTrackedTweaks);
                summary.Add(string.Format(LanguageService.T("HealthCheck_SummaryRestore"), reverted));
                restoreCleaned = true;
            }

            // ΝΕΟ - ρητό αίτημα χρήστη: "όταν ελευθερώνονται τα στοιχεία να ενημερώνονται αυτόματα τα
            // πλακίδια με τα νέα αποτελέσματα" - πριν, οι κάρτες αποτελεσμάτων (_results) χτίζονταν ΜΙΑ
            // φορά στο BuildResults() μετά την αρχική σάρωση και ΠΟΤΕ δεν ξαναχτίζονταν - το πραγματικό
            // καθάρισμα ΓΙΝΟΤΑΝ (επιβεβαιωμένο: αρχεία/registry ΟΝΤΩΣ διαγράφονταν), αλλά οι κάρτες
            // συνέχιζαν να δείχνουν τους ΠΑΛΙΟΥΣ αριθμούς πριν τον καθαρισμό - φαινομενικά "δεν έγινε
            // τίποτα". Ξανατρέχουν εδώ ΜΟΝΟ τα στάδια που πραγματικά καθαρίστηκαν, μετά ξαναχτίζονται
            // όλες οι κάρτες (καθαρό _results.Clear() + BuildResults(), ίδιο helper με την αρχική
            // σάρωση - καμία διπλή υλοποίηση) ώστε τα πλακίδια να δείχνουν τα φρέσκα, μετά-τον-καθαρισμό
            // αποτελέσματα (π.χ. "0 καταχωρήσεις registry" αντί για το παλιό "19").
            if (spaceCleaned || tracesCleaned || restoreCleaned)
            {
                if (spaceCleaned) await ScanSpaceAsync();
                if (tracesCleaned) await ScanTracesAsync();
                if (restoreCleaned) await ScanRestoreAsync();
                _scoreResult = await Task.Run(HealthScoreService.Compute);
                _results.Clear();
                BuildResults();
            }

            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtOptimizeResult.Text = summary.Count > 0 ? string.Join("  •  ", summary) : LanguageService.T("HealthCheck_NothingSelected");
            BtnOptimizeNow.Content = LanguageService.T("HealthCheck_Done");
            BtnOptimizeNow.IsEnabled = true;
        }

        private void ResultCardAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button { Tag: string tag }) return;
            if (Owner is not MainWindow mw) return;
            switch (tag)
            {
                case "Apps":
                    // "App Uninstall" ΔΕΝ απεγκαθιστά αυτόματα (μη αναστρέψιμη ενέργεια) - ανοίγει την
                    // καρτέλα Εφαρμογές & Bloat για έλεγχο/επιβεβαίωση από τον χρήστη, ίδιο πνεύμα
                    // ασφάλειας με όλες τις υπόλοιπες καταστροφικές ενέργειες της εφαρμογής.
                    mw.SelectTab("Bloatware");
                    break;
                case "Score":
                    // ΝΕΟ - βλ. σχόλιο στο BuildResults(): τα ευρήματα HealthScoreService (Defender/
                    // reboot/startup/restore point) ΔΕΝ διορθώνονται αυτόματα εδώ - πηγαίνει στην
                    // καρτέλα Σύστημα για χειροκίνητο έλεγχο/ενέργεια.
                    mw.SelectTab("System");
                    break;
                default:
                    return;
            }
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cancelled = true;
            Close();
        }
    }

    public class ScanChecklistRow : INotifyPropertyChanged
    {
        public string Title { get; }
        public string Description { get; }
        private Visibility _doneVisibility = Visibility.Collapsed;
        public Visibility DoneVisibility { get => _doneVisibility; private set { _doneVisibility = value; PropertyChanged?.Invoke(this, new(nameof(DoneVisibility))); } }
        public event PropertyChangedEventHandler? PropertyChanged;

        public ScanChecklistRow(string title, string description) { Title = title; Description = description; }
        public void MarkDone() => DoneVisibility = Visibility.Visible;
    }

    public class ResultCardVm : INotifyPropertyChanged
    {
        public string Key { get; }
        public string Headline { get; }
        public Brush HeadlineColor { get; }
        public string Description { get; }
        public bool CanSelect { get; }
        public string? ActionLabel { get; }
        public Visibility ActionVisibility => string.IsNullOrEmpty(ActionLabel) ? Visibility.Collapsed : Visibility.Visible;

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); } }
        public event PropertyChangedEventHandler? PropertyChanged;

        public ResultCardVm(string key, string headline, string description, bool hasIssue, bool canSelect, bool isSelected, string? actionLabel)
        {
            Key = key;
            Headline = headline;
            Description = description;
            CanSelect = canSelect;
            _isSelected = isSelected;
            ActionLabel = actionLabel;
            HeadlineColor = hasIssue ? new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)) : new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        }
    }
}
