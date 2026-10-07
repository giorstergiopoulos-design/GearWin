using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    // Γραμμή του πίνακα ασφάλειας (έτοιμη για binding - βλ. MaintenanceCenterWindow.xaml).
    public record SecurityRow(string Label, string Detail, string Glyph, Brush Color);

    public partial class MaintenanceCenterWindow : Window
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        private IReadOnlyList<StartupItem> _startupItems = Array.Empty<StartupItem>();
        private bool _loading = true;

        public MaintenanceCenterWindow()
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            FitWidthToTabs();

            var s = AppSettingsService.Current;
            ChkAutoRestorePoint.IsChecked = s.AutoRestorePointBeforeRiskyActions;
            ChkTempAlerts.IsChecked = s.TempAlertsEnabled;
            ChkSmartAlerts.IsChecked = s.SmartAlertsEnabled;
            ChkShareAppearance.IsChecked = s.ShareAppearanceWithMotionDesk;
            TxtTempThreshold.Text = (s.TempAlertThresholdC > 0 ? s.TempAlertThresholdC : AlertService.DefaultTempThresholdC).ToString();
            _loading = false;

            RefreshChanges();
            ChangeJournalService.Changed += OnJournalChanged;
            Loaded += async (_, _) =>
            {
                await LoadSecurityAsync();
                await LoadStartupItemsAsync();
            };
        }

        private void Window_Closed(object sender, EventArgs e) => ChangeJournalService.Changed -= OnJournalChanged;

        // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "οι καρτέλες να είναι σε μία ευθεία, προσάρμοσε το μέγεθος
        // του παραθύρου ανάλογα") - 5 καρτέλες με PillTabItemStyle (Padding 18,9 + Margin 0,0,6,6, βλ.
        // Styles.xaml) δεν χωρούσαν πάντα σε μία γραμμή στο σταθερό Width="760" - ειδικά σε γλώσσες με
        // μακρύτερο κείμενο (π.χ. το ελληνικό "Ειδοποιήσεις & Ασφάλεια ενεργειών"). Ίδιο σκεπτικό με το
        // MainWindow.FitWidthToTabs (μέτρηση του ΠΛΑΤΥΤΕΡΟΥ συνόλου ανάμεσα σε ΟΛΕΣ τις 14 γλώσσες) αλλά
        // μέσω FormattedText αντί για μέτρηση ζωντανού TabPanel - το TabControl δεν εκθέτει το εσωτερικό
        // του TabPanel ως named element, ενώ η ίδια η γραμματοσειρά/μέγεθος/βάρος (PillTabItemStyle) +
        // το σταθερό Padding/Margin ανά pill είναι ήδη γνωστά - ακριβής υπολογισμός χωρίς να χρειάζεται
        // πρόσβαση στο visual tree του TabControl.
        private static readonly string[] TabLabelKeys =
            { "Center_TabSecurity", "Center_TabChanges", "Center_TabAlerts", "Center_TabStartup", "Center_TabReport" };

        private void FitWidthToTabs()
        {
            try
            {
                var typeface = new Typeface(FontFamily, FontStyle, FontWeights.SemiBold, FontStretch);
                var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                const double perTabChrome = 18 + 18 + 6; // PillTabItemStyle: Padding αριστερά+δεξιά + Margin δεξιά

                double widest = 0;
                foreach (var dict in LanguageService.AllTranslations.Values)
                {
                    double total = 0;
                    foreach (var key in TabLabelKeys)
                    {
                        var text = dict.TryGetValue(key, out var t) ? t : LanguageService.T(key);
                        var ft = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 13, Brushes.Black, dpi);
                        total += ft.Width + perTabChrome;
                    }
                    widest = Math.Max(widest, total);
                }

                var frame = 48; // Grid Margin="16" αριστερά+δεξιά (βλ. XAML) + περιθώριο παραθύρου/ασφάλεια μέτρησης κειμένου
                var needed = widest + frame;
                var max = SystemParameters.WorkArea.Width * 0.9;
                Width = Math.Clamp(needed, MinWidth, Math.Max(MinWidth, max));
            }
            catch { /* αν αποτύχει η μέτρηση μένει το πλάτος του XAML */ }
        }

        // ── Ασφάλεια ─────────────────────────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task LoadSecurityAsync()
        {
            BtnRefreshSecurity.IsEnabled = false;
            TxtSecurityStatus.Text = LanguageService.T("Sys_LoadingEllipsis");
            try
            {
                var items = await SecurityStatusService.GetAsync();
                ListSecurity.ItemsSource = items.Select(i => new SecurityRow(i.Label, i.Detail,
                    i.Level switch { SecurityLevel.Good => "✔", SecurityLevel.Warning => "⚠", _ => "ℹ" },
                    i.Level switch
                    {
                        SecurityLevel.Good => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                        SecurityLevel.Warning => new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)),
                        _ => new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)),
                    })).ToList();
                var warnings = items.Count(i => i.Level == SecurityLevel.Warning);
                TxtSecurityStatus.Text = warnings == 0
                    ? LanguageService.T("Center_SecurityAllGood")
                    : string.Format(LanguageService.T("Center_SecurityWarnings"), warnings);
            }
            catch (Exception ex) { TxtSecurityStatus.Text = ex.Message; }
            finally { BtnRefreshSecurity.IsEnabled = true; }
        }

        private async void BtnRefreshSecurity_Click(object sender, RoutedEventArgs e)
        {
            MyDeviceService.ClearCache();
            await LoadSecurityAsync();
        }

        // ── Αλλαγές / αναίρεση / σημείο επαναφοράς ──────────────────────────────────────────
        private void OnJournalChanged() => Dispatcher.Invoke(RefreshChanges);

        private void RefreshChanges()
        {
            var entries = ChangeJournalService.Entries;
            ListChanges.ItemsSource = entries;
            ListChanges.DisplayMemberPath = null;
            if (entries.Count == 0) TxtChangesStatus.Text = LanguageService.T("Center_NoChanges");
            else if (TxtChangesStatus.Text == LanguageService.T("Center_NoChanges")) TxtChangesStatus.Text = "";
            BtnUndoChange.IsEnabled = (ListChanges.SelectedItem as ChangeEntry)?.CanUndo == true;
        }

        private void ListChanges_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            BtnUndoChange.IsEnabled = (ListChanges.SelectedItem as ChangeEntry)?.CanUndo == true;

        private async void BtnUndoChange_Click(object sender, RoutedEventArgs e)
        {
            if (ListChanges.SelectedItem is not ChangeEntry entry) return;
            BtnUndoChange.IsEnabled = false;
            var ok = await ChangeJournalService.UndoAsync(entry);
            TxtChangesStatus.Text = LanguageService.T(ok ? "Center_UndoDone" : "Center_UndoFailed");
        }

        private async void BtnCreateRestorePoint_Click(object sender, RoutedEventArgs e)
        {
            BtnCreateRestorePoint.IsEnabled = false;
            TxtChangesStatus.Text = LanguageService.T("Sys_CreatingRestorePoint");
            var ok = await SystemService.CreateRestorePointAsync();
            TxtChangesStatus.Text = LanguageService.T(ok ? "Sys_RestorePointCreated" : "Sys_RestorePointFailed");
            BtnCreateRestorePoint.IsEnabled = true;
        }

        // ── Ειδοποιήσεις / ρυθμίσεις ────────────────────────────────────────────────────────
        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            var s = AppSettingsService.Current;
            s.AutoRestorePointBeforeRiskyActions = ChkAutoRestorePoint.IsChecked == true;
            s.TempAlertsEnabled = ChkTempAlerts.IsChecked == true;
            s.SmartAlertsEnabled = ChkSmartAlerts.IsChecked == true;
            if (int.TryParse(TxtTempThreshold.Text.Trim(), out var th) && th is >= 50 and <= 110) s.TempAlertThresholdC = th;
            else TxtTempThreshold.Text = (s.TempAlertThresholdC > 0 ? s.TempAlertThresholdC : AlertService.DefaultTempThresholdC).ToString();

            var share = ChkShareAppearance.IsChecked == true;
            if (share && !s.ShareAppearanceWithMotionDesk) ThemeManager.PublishSharedAppearance();
            s.ShareAppearanceWithMotionDesk = share;
            AppSettingsService.Save();
        }

        private void BtnCheckAlertsNow_Click(object sender, RoutedEventArgs e) => AlertService.CheckNow();

        // ── Καθυστέρηση εκκίνησης ───────────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task LoadStartupItemsAsync()
        {
            try { _startupItems = await SystemService.LoadStartupItemsAsync(); }
            catch { _startupItems = Array.Empty<StartupItem>(); }
            // Μόνο τα HKCU/HKLM Run items που ο SetStartupItemEnabled μπορεί να χειριστεί.
            CmbStartupItems.ItemsSource = _startupItems.ToList();
            if (_startupItems.Count > 0) CmbStartupItems.SelectedIndex = 0;
            RefreshDelayedList();
        }

        private void RefreshDelayedList()
        {
            var delays = StartupDelayService.Delays;
            TxtDelayedList.Text = delays.Count == 0
                ? LanguageService.T("Center_NoDelayed")
                : LanguageService.T("Center_DelayedHeader") + Environment.NewLine +
                  string.Join(Environment.NewLine, delays.Select(kv => $"• {kv.Key} — {kv.Value} s"));
        }

        private async void BtnApplyDelay_Click(object sender, RoutedEventArgs e)
        {
            if (CmbStartupItems.SelectedItem is not StartupItem item) return;
            if (!int.TryParse(TxtDelaySeconds.Text.Trim(), out var seconds) || seconds < 5)
            {
                TxtStartupStatus.Text = LanguageService.T("Center_DelayInvalid");
                return;
            }
            BtnApplyDelay.IsEnabled = false;
            var ok = await StartupDelayService.ApplyDelayAsync(item, seconds);
            TxtStartupStatus.Text = LanguageService.T(ok ? "Center_DelayApplied" : "Center_DelayFailed");
            BtnApplyDelay.IsEnabled = true;
            if (ok) await LoadStartupItemsAsync();
        }

        private async void BtnRemoveDelay_Click(object sender, RoutedEventArgs e)
        {
            if (CmbStartupItems.SelectedItem is not StartupItem item) return;
            var ok = await StartupDelayService.RemoveDelayAsync(item);
            TxtStartupStatus.Text = LanguageService.T(ok ? "Center_DelayRemoved" : "Center_DelayFailed");
            if (ok) await LoadStartupItemsAsync();
        }

        // ── Αναφορά συστήματος ──────────────────────────────────────────────────────────────
        private async void BtnReportTxt_Click(object sender, RoutedEventArgs e) => await ExportReportAsync("txt");
        private async void BtnReportPdf_Click(object sender, RoutedEventArgs e) => await ExportReportAsync("pdf");

        private async System.Threading.Tasks.Task ExportReportAsync(string ext)
        {
            BtnReportTxt.IsEnabled = BtnReportPdf.IsEnabled = false;
            TxtReportStatus.Text = LanguageService.T("Center_ReportBuilding");
            try
            {
                var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "";
                var content = await SystemReportService.GenerateAsync(version);
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    $"GearWin_SystemReport_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
                await System.Threading.Tasks.Task.Run(() => SystemReportService.Save(path, content));
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                TxtReportStatus.Text = LanguageService.T("Center_ReportSaved") + path;
            }
            catch (Exception ex) { TxtReportStatus.Text = LanguageService.T("Center_ReportFailed") + ex.Message; }
            finally { BtnReportTxt.IsEnabled = BtnReportPdf.IsEnabled = true; }
        }
    }
}
