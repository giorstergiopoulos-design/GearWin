using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
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
        public MediaExtension Extension { get; }
        public string Name { get; }
        public bool Missing { get; }
        public string Status { get; }
        public Brush StatusBrush { get; }
    }

    public partial class MultimediaView : UserControl
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        private string? _ffmpeg;
        private CancellationTokenSource? _convertCts;

        public MultimediaView()
        {
            InitializeComponent();
            foreach (var (preset, key) in new[]
            {
                (MediaPreset.VideoToMp4H264, "Media_PresetH264"), (MediaPreset.VideoToMp4H265, "Media_PresetH265"),
                (MediaPreset.ShareSized720p, "Media_Preset720"), (MediaPreset.VideoToGif, "Media_PresetGif"), (MediaPreset.ExtractMp3, "Media_PresetMp3"),
            })
                CmbPreset.Items.Add(new ComboBoxItem { Content = LanguageService.T(key), Tag = preset });
            CmbPreset.SelectedIndex = 0;

            Loaded += async (_, _) => await LoadAllAsync();
        }

        private async System.Threading.Tasks.Task LoadAllAsync()
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
            ListCodecs.ItemsSource = codecs.Select(c => new CodecRowVm(c.Extension, c.Installed)).ToList();

            DetectFfmpeg();
        }

        private async System.Threading.Tasks.Task LoadAccessAsync()
        {
            var (cam, mic) = await System.Threading.Tasks.Task.Run(() => (PrivacyAccessService.Read("webcam"), PrivacyAccessService.Read("microphone")));
            ListCameraAccess.ItemsSource = FormatAccess(cam);
            ListMicAccess.ItemsSource = FormatAccess(mic);
        }

        private static System.Collections.Generic.List<string> FormatAccess(System.Collections.Generic.IReadOnlyList<DeviceAccessEntry> entries)
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
            if (sender is Button { Tag: CodecRowVm row }) Open(MediaCodecService.StoreSearchUri(row.Extension));
        }

        // ── Μετατροπέας ─────────────────────────────────────────────────────────────────────

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
                var (ok, message) = await MediaConverterService.ConvertAsync(_ffmpeg, preset, input, output, progress, _convertCts.Token);
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
