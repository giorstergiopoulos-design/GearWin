using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    // Γραμμή λίστας παιχνιδιών (έτοιμη για binding).
    public class GameRowVm
    {
        public GameRowVm(GameEntry entry)
        {
            Entry = entry;
            var size = entry.SizeBytes > 0 ? QuickCleanService.FormatSize(entry.SizeBytes) : "";
            var played = entry.LastPlayed is DateTime d
                ? string.Format(LanguageService.T("Games_LastPlayed"), d.ToString("d"))
                : (entry.Launcher == "Steam" ? LanguageService.T("Games_NeverPlayed") : "");
            Details = string.Join("  ·  ", new[] { size, played, entry.InstallDir }.Where(s => !string.IsNullOrEmpty(s)));
        }

        public GameEntry Entry { get; }
        public string Details { get; }
        public bool UpdatePending => Entry.UpdatePending;
        public bool CanLaunch => !string.IsNullOrEmpty(Entry.LaunchTarget);
        public bool CanVerify => Entry.VerifyTarget != null;
        public bool CanUninstall => Entry.UninstallTarget != null;
    }

    public partial class GamesView : UserControl
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        // Στατικό cache: η αλλαγή καρτέλας δημιουργεί νέο GamesView - δεν ξανασαρώνουμε τους δίσκους κάθε φορά.
        private static IReadOnlyList<GameEntry>? s_cache;
        private IReadOnlyList<GameEntry> _all = Array.Empty<GameEntry>();
        private bool _loading = true;

        public GamesView()
        {
            InitializeComponent();
            CmbLauncher.Items.Add(new ComboBoxItem { Content = LanguageService.T("Games_FilterAll"), Tag = "" });
            foreach (var l in new[] { "Steam", "Epic", "GOG", "Ubisoft", "Xbox" })
                CmbLauncher.Items.Add(new ComboBoxItem { Content = l, Tag = l });
            CmbLauncher.SelectedIndex = 0;
            CmbGpuPref.SelectedIndex = 0;
            ListGameTweaks.ItemsSource = GamingTweaksService.GlobalTweaks().Select(t => new TweakRowVm(t, "Gaming")).ToList();
            _loading = false;

            if (s_cache != null) { _all = s_cache; ApplyFilter(); }
            else Loaded += async (_, _) => { if (_all.Count == 0) await ScanAsync(); };
        }

        private async System.Threading.Tasks.Task ScanAsync()
        {
            BtnScanGames.IsEnabled = false;
            TxtGamesSummary.Text = LanguageService.T("Games_Scanning");
            StatusService.SetBusy(LanguageService.T("Games_Scanning"));
            try { _all = s_cache = await GameLibraryService.ScanAllAsync(); }
            catch (Exception ex) { TxtGamesSummary.Text = ex.Message; }
            finally { StatusService.SetIdle(LanguageService.T("Ready")); BtnScanGames.IsEnabled = true; }
            ApplyFilter();
        }

        private async void BtnScanGames_Click(object sender, RoutedEventArgs e) => await ScanAsync();

        private void Filter_Changed(object sender, SelectionChangedEventArgs e) { if (!_loading) ApplyFilter(); }
        private void Filter_TextChanged(object sender, TextChangedEventArgs e) { if (!_loading) ApplyFilter(); }

        private void ApplyFilter()
        {
            var launcher = (CmbLauncher.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            var term = TxtGameSearch.Text.Trim();
            var rows = _all
                .Where(g => launcher.Length == 0 || g.Launcher == launcher)
                .Where(g => term.Length == 0 || g.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                .Select(g => new GameRowVm(g)).ToList();
            ListGames.ItemsSource = rows;
            var total = rows.Sum(r => r.Entry.SizeBytes);
            TxtGamesSummary.Text = _all.Count == 0
                ? LanguageService.T("Games_NoGames")
                : string.Format(LanguageService.T("Games_Summary"), rows.Count, QuickCleanService.FormatSize(total), rows.Count(r => r.UpdatePending));
        }

        private static void OpenUri(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch (Exception ex) { ThemedMessageBox.Show(ex.Message, "GearWin", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void BtnLaunchGame_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: GameRowVm row } || row.Entry.LaunchTarget is not { } target) return;
            // GOG δίνει σχετικό "exe" μέσα στον φάκελο - γίνεται πλήρης διαδρομή.
            if (row.Entry.Launcher == "GOG" && !target.Contains("://") && !Path.IsPathRooted(target))
                target = Path.Combine(row.Entry.InstallDir, target);
            OpenUri(target);
        }

        private void BtnGameFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: GameRowVm row } && Directory.Exists(row.Entry.InstallDir)) OpenUri(row.Entry.InstallDir);
        }

        private void BtnVerifyGame_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: GameRowVm { Entry.VerifyTarget: { } uri } }) OpenUri(uri);
        }

        private void BtnUninstallGame_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: GameRowVm row } || row.Entry.UninstallTarget is not { } uri) return;
            if (ThemedMessageBox.Show(string.Format(LanguageService.T("Games_UninstallConfirm"), row.Entry.Name), "GearWin",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            OpenUri(uri); // το Steam ζητά το δικό του επιβεβαιωτικό παράθυρο
        }

        // ── Ρυθμίσεις ανά παιχνίδι ────────────────────────────────────────────────────────

        private void BtnChooseExe_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Executable (*.exe)|*.exe", CheckFileExists = true };
            if (dlg.ShowDialog() != true) return;
            TxtGameExe.Text = dlg.FileName;
            var pref = GamingTweaksService.GetGpuPreference(dlg.FileName) ?? GpuPreference.Auto;
            CmbGpuPref.SelectedIndex = (int)pref;
            ChkDisableFso.IsChecked = GamingTweaksService.GetFullscreenOptimizationsDisabled(dlg.FileName);
            BtnApplyGameSettings.IsEnabled = true;
            TxtGameSettingsStatus.Text = "";
        }

        private void BtnApplyGameSettings_Click(object sender, RoutedEventArgs e)
        {
            var exe = TxtGameExe.Text;
            if (string.IsNullOrEmpty(exe)) { TxtGameSettingsStatus.Text = LanguageService.T("Games_SelectGameFirst"); return; }
            var pref = (GpuPreference)CmbGpuPref.SelectedIndex;
            var before = (GamingTweaksService.GetGpuPreference(exe) ?? GpuPreference.Auto, GamingTweaksService.GetFullscreenOptimizationsDisabled(exe));
            var ok = GamingTweaksService.SetGpuPreference(exe, pref)
                     & GamingTweaksService.SetFullscreenOptimizationsDisabled(exe, ChkDisableFso.IsChecked == true);
            TxtGameSettingsStatus.Text = LanguageService.T(ok ? "Games_Applied" : "Autostart_Failed");
            if (ok)
                ChangeJournalService.Record($"{Path.GetFileName(exe)}: GPU/FSO", () =>
                {
                    GamingTweaksService.SetGpuPreference(exe, before.Item1);
                    GamingTweaksService.SetFullscreenOptimizationsDisabled(exe, before.Item2);
                });
        }

        // ── Καθολικά tweaks / shader cache ───────────────────────────────────────────────

        private void ToggleGameTweak_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton { Tag: TweakRowVm row }) return;
            try { if (row.IsOn) row.Tweak.OnAction(); else row.Tweak.OffAction(); row.RecordChange(row.IsOn); }
            catch
            {
                row.IsOn = !row.IsOn;
                ThemedMessageBox.Show(LanguageService.T("Net_ChangeFailed"), LanguageService.T("Net_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BtnCleanShader_Click(object sender, RoutedEventArgs e)
        {
            BtnCleanShader.IsEnabled = false;
            try
            {
                var freed = await QuickCleanService.CleanAsync(new[] { "ShaderCache" });
                ImpactTrackingService.RecordBytesFreed(freed);
                TxtShaderStatus.Text = string.Format(LanguageService.T("Games_ShaderDone"), QuickCleanService.FormatSize(freed));
            }
            catch (Exception ex) { TxtShaderStatus.Text = ex.Message; }
            finally { BtnCleanShader.IsEnabled = true; }
        }
    }
}
