using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
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

    public class CodecRowVm
    {
        public CodecRowVm(MediaExtension ext, bool installed)
        {
            Extension = ext;
            Name = ext.DisplayName;
            Missing = !installed;
            Status = LanguageService.T(installed ? "Media_CodecInstalled" : "Media_CodecMissing");
            StatusBrush = installed ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)) : new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
        }

        // ΔΙΟΡΘΩΣΗ (GEARWIN.MD: third-party codecs, π.χ. K-Lite) - βλ. ThirdPartyCodecService. Πάντα
        // "εγκατεστημένο" (η ίδια η ανίχνευση σημαίνει ότι βρέθηκε) - δεν υπάρχει "Get" κουμπί γι' αυτά.
        public CodecRowVm(ThirdPartyCodec codec)
        {
            Extension = null;
            Name = codec.Name;
            Missing = false;
            Status = LanguageService.T("Media_CodecInstalled");
            StatusBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        }

        public MediaExtension? Extension { get; }
        public string Name { get; }
        public bool Missing { get; }
        public string Status { get; }
        public Brush StatusBrush { get; }
    }

    // GEARWIN.MD (6.3.5, ρητό αίτημα χρήστη: "Παιχνιδια & Πολυμεσα μια καρτελα") - συγχώνευση των
    // πρώην GamesView + MultimediaView code-behind σε ΜΙΑ partial class. Καμία αλλαγή λογικής -
    // ΟΛΕΣ οι μέθοδοι/πεδία μεταφέρθηκαν αυτούσια από τα δύο πρώην αρχεία.
    public partial class GamesMediaView : UserControl
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        // ── Παιχνίδια ──────────────────────────────────────────────────────────────────────

        // Στατικό cache: η αλλαγή καρτέλας δημιουργεί νέο GamesMediaView - δεν ξανασαρώνουμε τους δίσκους κάθε φορά.
        private static IReadOnlyList<GameEntry>? s_gamesCache;
        private IReadOnlyList<GameEntry> _allGames = Array.Empty<GameEntry>();
        private bool _gamesLoading = true;

        // ── Πολυμέσα ───────────────────────────────────────────────────────────────────────
        private string? _ffmpeg;
        private CancellationTokenSource? _convertCts;

        public GamesMediaView()
        {
            InitializeComponent();

            // Games init
            CmbLauncher.Items.Add(new ComboBoxItem { Content = LanguageService.T("Games_FilterAll"), Tag = "" });
            foreach (var l in new[] { "Steam", "Epic", "GOG", "Ubisoft", "Xbox" })
                CmbLauncher.Items.Add(new ComboBoxItem { Content = l, Tag = l });
            CmbLauncher.SelectedIndex = 0;
            CmbGpuPref.SelectedIndex = 0;
            ListGameTweaks.ItemsSource = GamingTweaksService.GlobalTweaks().Select(t => new TweakRowVm(t, "Gaming")).ToList();
            _gamesLoading = false;

            if (s_gamesCache != null) { _allGames = s_gamesCache; ApplyFilter(); }
            else Loaded += async (_, _) => { if (_allGames.Count == 0) await ScanGamesAsync(); };

            // Media init
            // ΔΙΟΡΘΩΣΗ (GEARWIN.MD: "πολλά περισσότερα αρχεία ήχου/εικόνας με επιλογή ποιότητας") -
            // WebM/MKV (βίντεο) + OGG/AAC/WAV/FLAC (ήχος) προστέθηκαν στα 5 προηγούμενα presets, συν
            // ξεχωριστό CmbQuality (Low/Medium/High) - βλ. MediaConverterService.MediaQuality.
            foreach (var (preset, key) in new[]
            {
                (MediaPreset.VideoToMp4H264, "Media_PresetH264"), (MediaPreset.VideoToMp4H265, "Media_PresetH265"),
                (MediaPreset.VideoToWebm, "Media_PresetWebm"), (MediaPreset.VideoToMkvH264, "Media_PresetMkv"),
                (MediaPreset.ShareSized720p, "Media_Preset720"), (MediaPreset.VideoToGif, "Media_PresetGif"),
                (MediaPreset.ExtractMp3, "Media_PresetMp3"), (MediaPreset.ExtractAac, "Media_PresetAac"),
                (MediaPreset.ExtractOgg, "Media_PresetOgg"), (MediaPreset.ExtractWav, "Media_PresetWav"),
                (MediaPreset.ExtractFlac, "Media_PresetFlac"),
            })
                CmbPreset.Items.Add(new ComboBoxItem { Content = LanguageService.T(key), Tag = preset });
            CmbPreset.SelectedIndex = 0;

            foreach (var (quality, key) in new[]
            {
                (MediaQuality.Low, "Media_QualityLow"), (MediaQuality.Medium, "Media_QualityMedium"), (MediaQuality.High, "Media_QualityHigh"),
            })
                CmbQuality.Items.Add(new ComboBoxItem { Content = LanguageService.T(key), Tag = quality });
            CmbQuality.SelectedIndex = 1; // Medium - ίδια προεπιλογή με τις παλιές σταθερές τιμές

            Loaded += async (_, _) => await LoadMediaAsync();
        }

        // ── Παιχνίδια: Βιβλιοθήκη ─────────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task ScanGamesAsync()
        {
            BtnScanGames.IsEnabled = false;
            TxtGamesSummary.Text = LanguageService.T("Games_Scanning");
            StatusService.SetBusy(LanguageService.T("Games_Scanning"));
            try { _allGames = s_gamesCache = await GameLibraryService.ScanAllAsync(); }
            catch (Exception ex) { TxtGamesSummary.Text = ex.Message; }
            finally { StatusService.SetIdle(LanguageService.T("Ready")); BtnScanGames.IsEnabled = true; }
            ApplyFilter();
        }

        private async void BtnScanGames_Click(object sender, RoutedEventArgs e) => await ScanGamesAsync();

        private void Filter_Changed(object sender, SelectionChangedEventArgs e) { if (!_gamesLoading) ApplyFilter(); }
        private void Filter_TextChanged(object sender, TextChangedEventArgs e) { if (!_gamesLoading) ApplyFilter(); }

        private void ApplyFilter()
        {
            var launcher = (CmbLauncher.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            var term = TxtGameSearch.Text.Trim();
            var rows = _allGames
                .Where(g => launcher.Length == 0 || g.Launcher == launcher)
                .Where(g => term.Length == 0 || g.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                .Select(g => new GameRowVm(g)).ToList();
            ListGames.ItemsSource = rows;
            var total = rows.Sum(r => r.Entry.SizeBytes);
            TxtGamesSummary.Text = _allGames.Count == 0
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

        // ── Παιχνίδια: Ρυθμίσεις ανά παιχνίδι ────────────────────────────────────────────

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

        // ── Παιχνίδια: Καθολικά tweaks / shader cache ───────────────────────────────────

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

        // ── Πολυμέσα ──────────────────────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task LoadMediaAsync()
        {
            var displays = await System.Threading.Tasks.Task.Run(DisplayInfoService.GetDisplays);
            ListDisplays.ItemsSource = displays.Select(d =>
                $"{d.Name}{(d.IsPrimary ? " (" + LanguageService.T("Media_Primary") + ")" : "")} — {d.Width}×{d.Height} @ {d.RefreshHz} Hz, {d.BitsPerPixel}-bit").ToList();
            TxtDisplayHint.Text = displays.Any(d => DisplayInfoService.LooksLikeHighRefreshAtSixty(d.RefreshHz))
                ? LanguageService.T("Media_RefreshHint") : "";

            var audio = await System.Threading.Tasks.Task.Run(AudioDeviceService.GetEndpoints);
            string Line(AudioEndpoint a) => $"{(a.IsActive ? "●" : "○")} {a.Name}{(a.IsActive ? "" : "  (" + LanguageService.T("Media_Inactive") + ")")}";
            ListPlayback.ItemsSource = audio.Where(a => a.IsPlayback).Select(Line).ToList();
            ListRecording.ItemsSource = audio.Where(a => !a.IsPlayback).Select(Line).ToList();

            await LoadAccessAsync();

            var codecs = await System.Threading.Tasks.Task.Run(MediaCodecService.Check);
            var thirdParty = await System.Threading.Tasks.Task.Run(ThirdPartyCodecService.Check);
            ListCodecs.ItemsSource = codecs.Select(c => new CodecRowVm(c.Extension, c.Installed))
                .Concat(thirdParty.Select(c => new CodecRowVm(c)))
                .ToList();

            DetectFfmpeg();
        }

        private async System.Threading.Tasks.Task LoadAccessAsync()
        {
            var (cam, mic) = await System.Threading.Tasks.Task.Run(() => (PrivacyAccessService.Read("webcam"), PrivacyAccessService.Read("microphone")));
            ListCameraAccess.ItemsSource = FormatAccess(cam);
            ListMicAccess.ItemsSource = FormatAccess(mic);
        }

        private static List<string> FormatAccess(IReadOnlyList<DeviceAccessEntry> entries)
        {
            if (entries.Count == 0) return new() { LanguageService.T("Media_NoAccessRecords") };
            return entries.Take(8).Select(e =>
            {
                var when = e.InUseNow ? LanguageService.T("Media_InUseNow")
                    : e.LastStop is DateTime stop ? string.Format(LanguageService.T("Media_LastUsed"), stop.ToString("g")) : "";
                return $"{(e.InUseNow ? "🔴" : "•")} {e.App} — {when}{(e.Allowed ? "" : "  [" + LanguageService.T("Media_Blocked") + "]")}";
            }).ToList();
        }

        private static void Open(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch { }
        }

        private void BtnOpenDisplay_Click(object sender, RoutedEventArgs e) => Open("ms-settings:display-advanced");
        private void BtnOpenSound_Click(object sender, RoutedEventArgs e) => Open("ms-settings:sound");
        private void BtnOpenMixer_Click(object sender, RoutedEventArgs e) => Open("ms-settings:apps-volume");
        private void BtnOpenCameraPrivacy_Click(object sender, RoutedEventArgs e) => Open("ms-settings:privacy-webcam");
        private void BtnOpenMicPrivacy_Click(object sender, RoutedEventArgs e) => Open("ms-settings:privacy-microphone");
        private async void BtnRefreshAccess_Click(object sender, RoutedEventArgs e) => await LoadAccessAsync();

        private void BtnGetCodec_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: CodecRowVm { Extension: { } ext } }) Open(MediaCodecService.StoreSearchUri(ext));
        }

        // ── Πολυμέσα: Μετατροπέας ────────────────────────────────────────────────────────

        private void DetectFfmpeg()
        {
            _ffmpeg = MediaConverterService.FindFfmpeg();
            PanelNoFfmpeg.Visibility = _ffmpeg == null ? Visibility.Visible : Visibility.Collapsed;
            PanelConverter.IsEnabled = _ffmpeg != null;
        }

        private void BtnRecheckFfmpeg_Click(object sender, RoutedEventArgs e) => DetectFfmpeg();

        // Εγκατάσταση μέσω winget σε ορατή κονσόλα (ο χρήστης βλέπει πρόοδο/ερώτηση αδειών).
        private void BtnInstallFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("winget", "install --id Gyan.FFmpeg -e --accept-source-agreements --accept-package-agreements") { UseShellExecute = true });
            }
            catch (Exception ex) { TxtConvertStatus.Text = ex.Message; }
        }

        private void BtnChooseMedia_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Media|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.flv;*.m4v;*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg|All files|*.*",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog() != true) return;
            TxtMediaInput.Text = dlg.FileName;
            BtnConvert.IsEnabled = true;
            TxtConvertStatus.Text = "";
        }

        private async void BtnConvert_Click(object sender, RoutedEventArgs e)
        {
            if (_ffmpeg == null || string.IsNullOrEmpty(TxtMediaInput.Text) || CmbPreset.SelectedItem is not ComboBoxItem { Tag: MediaPreset preset }) return;
            var quality = CmbQuality.SelectedItem is ComboBoxItem { Tag: MediaQuality q } ? q : MediaQuality.Medium;
            var input = TxtMediaInput.Text;
            var output = MediaConverterService.SuggestOutputPath(input, preset);

            _convertCts = new CancellationTokenSource();
            BtnConvert.IsEnabled = false;
            BtnCancelConvert.Visibility = Visibility.Visible;
            ConvertProgress.Value = 0;
            ConvertProgress.Visibility = Visibility.Visible;
            TxtConvertStatus.Text = LanguageService.T("Media_Converting");
            StatusService.SetBusy(LanguageService.T("Media_Converting"));
            try
            {
                var progress = new Progress<double>(v => ConvertProgress.Value = v);
                var (ok, message) = await MediaConverterService.ConvertAsync(_ffmpeg, preset, quality, input, output, progress, _convertCts.Token);
                TxtConvertStatus.Text = ok ? string.Format(LanguageService.T("Media_ConvertDone"), message)
                    : (_convertCts.IsCancellationRequested ? message : LanguageService.T("Media_ConvertFailed") + message);
                if (ok) ChangeJournalService.Record($"ffmpeg: {Path.GetFileName(output)}", () => { try { File.Delete(output); } catch { } });
            }
            catch (Exception ex) { TxtConvertStatus.Text = LanguageService.T("Media_ConvertFailed") + ex.Message; }
            finally
            {
                StatusService.SetIdle(LanguageService.T("Ready"));
                _convertCts.Dispose(); _convertCts = null;
                BtnCancelConvert.Visibility = Visibility.Collapsed;
                ConvertProgress.Visibility = Visibility.Collapsed;
                BtnConvert.IsEnabled = true;
            }
        }

        private void BtnCancelConvert_Click(object sender, RoutedEventArgs e) => _convertCts?.Cancel();
    }
}
