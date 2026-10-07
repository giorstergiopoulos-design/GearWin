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
        // Πρόταση χρήστη: "batch conversion" - πολλά αρχεία αντί για ένα.
        private List<string> _mediaInputs = new();

        // Κρατά τη λίστα ζωντανή (όχι μόνο ItemsSource) ώστε η Εφαρμογή Προφίλ Παιχνιδιού να μπορεί
        // να αλλάξει IsOn + να καλέσει OnAction/OffAction στα ΙΔΙΑ TweakRowVm που βλέπει ο χρήστης.
        private List<TweakRowVm> _gameTweakRows = new();

        public GamesMediaView()
        {
            InitializeComponent();

            // Games init
            CmbLauncher.Items.Add(new ComboBoxItem { Content = LanguageService.T("Games_FilterAll"), Tag = "" });
            foreach (var l in new[] { "Steam", "Epic", "GOG", "Ubisoft", "Xbox" })
                CmbLauncher.Items.Add(new ComboBoxItem { Content = l, Tag = l });
            CmbLauncher.SelectedIndex = 0;
            CmbGpuPref.SelectedIndex = 0;
            _gameTweakRows = GamingTweaksService.GlobalTweaks().Select(t => new TweakRowVm(t, "Gaming")).ToList();
            ListGameTweaks.ItemsSource = _gameTweakRows;
            _gamesLoading = false;
            ShowDriverFreshnessHint();

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
            BtnSaveGameProfile.IsEnabled = true;
            BtnLoadGameProfile.IsEnabled = GameProfileService.HasProfile(dlg.FileName);
            TxtGameSettingsStatus.Text = "";
        }

        // ── Παιχνίδια: Προφίλ ανά παιχνίδι (REQ-590-06) ──────────────────────────────────

        private void BtnSaveGameProfile_Click(object sender, RoutedEventArgs e)
        {
            var exe = TxtGameExe.Text;
            if (string.IsNullOrEmpty(exe)) return;
            var data = new GameProfileData
            {
                GpuPreference = (GpuPreference)CmbGpuPref.SelectedIndex,
                DisableFullscreenOptimizations = ChkDisableFso.IsChecked == true,
                GlobalTweaks = _gameTweakRows.Where(r => r.Tweak.Id != null).ToDictionary(r => r.Tweak.Id!, r => r.IsOn),
            };
            var ok = GameProfileService.Save(exe, data);
            TxtGameSettingsStatus.Text = LanguageService.T(ok ? "Games_ProfileSaved" : "Autostart_Failed");
            if (ok) BtnLoadGameProfile.IsEnabled = true;
        }

        private void BtnLoadGameProfile_Click(object sender, RoutedEventArgs e)
        {
            var exe = TxtGameExe.Text;
            if (string.IsNullOrEmpty(exe)) return;
            var data = GameProfileService.Load(exe);
            if (data == null) { TxtGameSettingsStatus.Text = LanguageService.T("Games_ProfileNotFound"); return; }

            if (data.GpuPreference is { } gpu) CmbGpuPref.SelectedIndex = (int)gpu;
            if (data.DisableFullscreenOptimizations is { } fso) ChkDisableFso.IsChecked = fso;
            GamingTweaksService.SetGpuPreference(exe, data.GpuPreference);
            GamingTweaksService.SetFullscreenOptimizationsDisabled(exe, data.DisableFullscreenOptimizations == true);

            foreach (var row in _gameTweakRows)
            {
                if (row.Tweak.Id == null || !data.GlobalTweaks.TryGetValue(row.Tweak.Id, out var wantOn) || wantOn == row.IsOn) continue;
                try { if (wantOn) row.Tweak.OnAction(); else row.Tweak.OffAction(); row.IsOn = wantOn; row.RecordChange(wantOn); }
                catch { /* ένα αποτυχημένο tweak δεν ακυρώνει την εφαρμογή των υπολοίπων */ }
            }
            TxtGameSettingsStatus.Text = LanguageService.T("Games_ProfileApplied");
        }

        // ── Παιχνίδια: GPU driver freshness hint ─────────────────────────────────────────

        // Διαβάζει ΜΟΝΟ το ήδη υπάρχον cache της σάρωσης οδηγών κατασκευαστή (καρτέλα Βελτιστοποίηση) -
        // ποτέ δεν ξεκινά τη δική της (αργή, δικτυακή) σάρωση μόνο για να δείξει αυτό το hint.
        private void ShowDriverFreshnessHint()
        {
            TxtDriverHint.Text = "";
            var vendor = OptimizationView.CachedVendorScan;
            if (vendor == null) return;
            foreach (var a in vendor.Amd)
                if (!string.IsNullOrEmpty(a.InstalledVersion) && !a.InstalledVersion.Equals(a.LatestVersion, StringComparison.OrdinalIgnoreCase))
                { TxtDriverHint.Text = string.Format(LanguageService.T("Games_DriverStale"), a.GpuName, a.InstalledVersion, a.LatestVersion); return; }
            foreach (var n in vendor.Nvidia)
                if (!string.IsNullOrEmpty(n.InstalledVersion) && !n.InstalledVersion.Equals(n.LatestVersion, StringComparison.OrdinalIgnoreCase))
                { TxtDriverHint.Text = string.Format(LanguageService.T("Games_DriverStale"), n.GpuName, n.InstalledVersion, n.LatestVersion); return; }
        }

        // ── Παιχνίδια: Cache launcher ─────────────────────────────────────────────────────

        private async void BtnCleanLauncherCache_Click(object sender, RoutedEventArgs e)
        {
            BtnCleanLauncherCache.IsEnabled = false;
            try
            {
                var freed = await QuickCleanService.CleanAsync(new[] { "LauncherCache" });
                ImpactTrackingService.RecordBytesFreed(freed);
                TxtShaderStatus.Text = string.Format(LanguageService.T("Games_LauncherCacheDone"), QuickCleanService.FormatSize(freed));
            }
            catch (Exception ex) { TxtShaderStatus.Text = ex.Message; }
            finally { BtnCleanLauncherCache.IsEnabled = true; }
        }

        // ── Παιχνίδια: Αντίγραφο ασφαλείας αποθηκεύσεων ──────────────────────────────────

        private async void BtnBackupSaves_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Zip|*.zip", FileName = $"GearWin-GameSaves-{DateTime.Now:yyyyMMdd}.zip",
            };
            if (dlg.ShowDialog() != true) return;

            BtnBackupSaves.IsEnabled = false;
            TxtSaveBackupStatus.Text = LanguageService.T("Games_SaveBackupRunning");
            try
            {
                var ok = await GameSaveBackupService.BackupAsync(dlg.FileName);
                TxtSaveBackupStatus.Text = ok ? string.Format(LanguageService.T("Games_SaveBackupDone"), dlg.FileName) : LanguageService.T("Games_SaveBackupFailed");
            }
            catch (Exception ex) { TxtSaveBackupStatus.Text = ex.Message; }
            finally { BtnBackupSaves.IsEnabled = true; }
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
            // Πρόταση χρήστη: "HDR hint" - ευρετική σαν το από πάνω (BitsPerPixel>=30 => η οθόνη
            // ΑΝΑΦΕΡΕΙ βάθος χρώματος που υποστηρίζει HDR), όχι απόδειξη ότι το HDR είναι ενεργό.
            TxtHdrHint.Text = displays.Any(d => d.BitsPerPixel >= 30) ? LanguageService.T("Media_HdrHint") : "";

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

        // Πρόταση χρήστη: "ζωντανή δοκιμή μικροφώνου/κάμερας" - ανοίγει τα ΗΔΗ υπάρχοντα πραγματικά
        // εργαλεία δοκιμής των Windows αντί να χτίσουμε δική μας λήψη ήχου/εικόνας (μη επαληθεύσιμο
        // χωρίς πραγματική κάμερα/μικρόφωνο σε αυτό το περιβάλλον ανάπτυξης).
        private void BtnTestCamera_Click(object sender, RoutedEventArgs e) => Open("microsoft.windows.camera:");

        private void BtnTestMic_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("control.exe", "mmsys.cpl,,1") { UseShellExecute = true }); }
            catch { }
        }

        private void BtnGetCodec_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: CodecRowVm { Extension: { } ext } }) Open(MediaCodecService.StoreSearchUri(ext));
        }

        // Πρόταση χρήστη: "one-click codec pack install" - η επίσημη σελίδα λήψης (όχι winget, δεν
        // υπάρχει επιβεβαιωμένο σταθερό package id γι' αυτό το πακέτο ώστε να το μαντέψουμε με σιγουριά).
        private void BtnGetCodecPack_Click(object sender, RoutedEventArgs e) => Open("https://www.codecguide.com/download_kl.htm");

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

        private static readonly string[] MediaExtensions =
            { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".flv", ".m4v", ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg" };

        // Πρόταση χρήστη: "batch conversion" - Multiselect=true αντί για ένα αρχείο τη φορά.
        private void BtnChooseMedia_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Media|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.flv;*.m4v;*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg|All files|*.*",
                CheckFileExists = true, Multiselect = true,
            };
            if (dlg.ShowDialog() != true) return;
            SetMediaInputs(dlg.FileNames);
        }

        private void SetMediaInputs(IEnumerable<string> files)
        {
            _mediaInputs = files.ToList();
            TxtMediaInput.Text = _mediaInputs.Count == 1
                ? Path.GetFileName(_mediaInputs[0])
                : string.Format(LanguageService.T("Media_FilesSelectedCount"), _mediaInputs.Count);
            BtnConvert.IsEnabled = _mediaInputs.Count > 0;
            TxtConvertStatus.Text = "";
        }

        // Πρόταση χρήστη: "drag-and-drop" στο μετατροπέα - φιλτράρει στις ίδιες επεκτάσεις πολυμέσων
        // με το OpenFileDialog, ώστε ένα τυχαίο αρχείο να μην καταλήξει σαν είσοδος στο ffmpeg.
        private void PanelConverter_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void PanelConverter_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
            var media = files.Where(f => MediaExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
            if (media.Count > 0) SetMediaInputs(media);
        }

        private async void BtnConvert_Click(object sender, RoutedEventArgs e)
        {
            if (_ffmpeg == null || _mediaInputs.Count == 0 || CmbPreset.SelectedItem is not ComboBoxItem { Tag: MediaPreset preset }) return;
            var quality = CmbQuality.SelectedItem is ComboBoxItem { Tag: MediaQuality q } ? q : MediaQuality.Medium;
            var inputs = _mediaInputs;

            _convertCts = new CancellationTokenSource();
            BtnConvert.IsEnabled = false;
            BtnCancelConvert.Visibility = Visibility.Visible;
            ConvertProgress.Value = 0;
            ConvertProgress.Visibility = Visibility.Visible;
            StatusService.SetBusy(LanguageService.T("Media_Converting"));
            var doneOutputs = new List<string>();
            var failedNames = new List<string>();
            try
            {
                for (var i = 0; i < inputs.Count; i++)
                {
                    var input = inputs[i];
                    var output = MediaConverterService.SuggestOutputPath(input, preset);
                    TxtConvertStatus.Text = inputs.Count == 1
                        ? LanguageService.T("Media_Converting")
                        : string.Format(LanguageService.T("Media_ConvertingBatch"), i + 1, inputs.Count, Path.GetFileName(input));
                    ConvertProgress.Value = 0;
                    var progress = new Progress<double>(v => ConvertProgress.Value = v);
                    var (ok, message) = await MediaConverterService.ConvertAsync(_ffmpeg, preset, quality, input, output, progress, _convertCts.Token);
                    if (_convertCts.IsCancellationRequested) break;
                    if (ok)
                    {
                        doneOutputs.Add(output);
                        ChangeJournalService.Record($"ffmpeg: {Path.GetFileName(output)}", () => { try { File.Delete(output); } catch { } });
                    }
                    else failedNames.Add(Path.GetFileName(input));
                }

                TxtConvertStatus.Text = _convertCts.IsCancellationRequested
                    ? LanguageService.T("Health_ToolCancelled")
                    : failedNames.Count == 0
                        ? string.Format(LanguageService.T("Media_ConvertDone"), doneOutputs.Count == 1 ? doneOutputs[0] : string.Format(LanguageService.T("Media_ConvertDoneCount"), doneOutputs.Count))
                        : LanguageService.T("Media_ConvertFailed") + string.Join(", ", failedNames);
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
