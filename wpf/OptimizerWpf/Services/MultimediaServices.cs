using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    // ═════════ Οθόνες ═════════════════════════════════════════════════════════════════════════
    public record DisplayInfo(string Name, string DeviceName, int Width, int Height, int RefreshHz, int BitsPerPixel, bool IsPrimary);

    public static class DisplayInfoService
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields;
            public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

        private const int ENUM_CURRENT_SETTINGS = -1;

        public static IReadOnlyList<DisplayInfo> GetDisplays()
        {
            var list = new List<DisplayInfo>();
            try
            {
                var i = 1;
                foreach (var s in System.Windows.Forms.Screen.AllScreens)
                {
                    var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
                    var hz = 0; var bits = 0; int w = s.Bounds.Width, h = s.Bounds.Height;
                    if (EnumDisplaySettings(s.DeviceName, ENUM_CURRENT_SETTINGS, ref dm))
                    {
                        hz = dm.dmDisplayFrequency; bits = dm.dmBitsPerPel; w = dm.dmPelsWidth; h = dm.dmPelsHeight;
                    }
                    list.Add(new DisplayInfo($"{LanguageService.T("Media_Display")} {i++}", s.DeviceName.TrimEnd('\0'), w, h, hz, bits, s.Primary));
                }
            }
            catch { }
            return list;
        }

        // Ένδειξη για τον χρήστη: συχνά οι οθόνες 120/144/165 Hz τρέχουν στα 60 Hz γιατί δεν άλλαξε η ρύθμιση.
        public static bool LooksLikeHighRefreshAtSixty(int hz) => hz is >= 59 and <= 61;
    }

    // ═════════ Συσκευές ήχου ══════════════════════════════════════════════════════════════════
    public record AudioEndpoint(string Name, bool IsPlayback, bool IsActive);

    public static class AudioDeviceService
    {
        // DeviceState: 1 = ενεργή, 2 = απενεργοποιημένη, 4 = μη διαθέσιμη, 8 = αποσυνδεδεμένη.
        public static bool IsActiveState(int deviceState) => deviceState == 1;

        public static IReadOnlyList<AudioEndpoint> GetEndpoints()
        {
            var list = new List<AudioEndpoint>();
            foreach (var (flow, playback) in new[] { ("Render", true), ("Capture", false) })
            {
                try
                {
                    using var root = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\{flow}");
                    if (root == null) continue;
                    foreach (var guid in root.GetSubKeyNames())
                    {
                        using var ep = root.OpenSubKey(guid);
                        using var props = ep?.OpenSubKey("Properties");
                        var state = ep?.GetValue("DeviceState") is int s ? s : 0;
                        // {a45c254e-...},2 = όνομα συσκευής (π.χ. "Realtek Audio"), ,14 = όνομα endpoint ("Ηχεία")
                        var endpointName = props?.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2") as string
                                           ?? props?.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6") as string;
                        var deviceName = props?.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6") as string;
                        var name = !string.IsNullOrWhiteSpace(endpointName) ? endpointName! : deviceName;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        if (state == 8) continue; // αποσυνδεδεμένες δεν ενδιαφέρουν
                        list.Add(new AudioEndpoint(name!, playback, IsActiveState(state)));
                    }
                }
                catch { }
            }
            return list;
        }
    }

    // ═════════ Κωδικοποιητές / επεκτάσεις πολυμέσων ═══════════════════════════════════════════
    public record MediaExtension(string DisplayName, string PackagePrefix, string StoreSearch);

    public static class MediaCodecService
    {
        public static readonly IReadOnlyList<MediaExtension> Extensions = new[]
        {
            new MediaExtension("HEVC (H.265) Video Extensions", "Microsoft.HEVCVideoExtension", "HEVC Video Extensions"),
            new MediaExtension("AV1 Video Extension", "Microsoft.AV1VideoExtension", "AV1 Video Extension"),
            new MediaExtension("VP9 Video Extensions", "Microsoft.VP9VideoExtensions", "VP9 Video Extensions"),
            new MediaExtension("MPEG-2 Video Extension", "Microsoft.MPEG2VideoExtension", "MPEG-2 Video Extension"),
            new MediaExtension("WebP Image Extensions", "Microsoft.WebpImageExtension", "WebP Image Extensions"),
            new MediaExtension("HEIF Image Extensions", "Microsoft.HEIFImageExtension", "HEIF Image Extensions"),
            new MediaExtension("Raw Image Extension", "Microsoft.RawImageExtension", "Raw Image Extension"),
        };

        // Τα πακέτα Appx γράφουν το πλήρες όνομά τους στο μητρώο (Repository\Packages) - γρήγορο, χωρίς PowerShell.
        public static bool IsInstalled(IEnumerable<string> installedPackageFullNames, string prefix) =>
            installedPackageFullNames.Any(n => n.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase));

        public static IReadOnlyList<(MediaExtension Extension, bool Installed)> Check()
        {
            var names = new List<string>();
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages");
                if (k != null) names.AddRange(k.GetSubKeyNames());
            }
            catch { }
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Applications");
                if (k != null) names.AddRange(k.GetSubKeyNames());
            }
            catch { }
            return Extensions.Select(e => (e, IsInstalled(names, e.PackagePrefix))).ToList();
        }

        public static string StoreSearchUri(MediaExtension e) => "ms-windows-store://search/?query=" + Uri.EscapeDataString(e.StoreSearch);
    }

    // ═════════ Ποιος χρησιμοποιεί κάμερα/μικρόφωνο ════════════════════════════════════════════
    public record DeviceAccessEntry(string App, DateTime? LastStart, DateTime? LastStop, bool InUseNow, bool Allowed);

    public static class PrivacyAccessService
    {
        // Το Windows γράφει ανά εφαρμογή LastUsedTimeStart/Stop (FILETIME). Stop = 0 ενώ Start > 0 => χρησιμοποιείται τώρα.
        public static (DateTime? Start, DateTime? Stop, bool InUse) Interpret(long startFiletime, long stopFiletime)
        {
            DateTime? Conv(long ft) => ft > 0 ? DateTime.FromFileTimeUtc(ft).ToLocalTime() : null;
            return (Conv(startFiletime), Conv(stopFiletime), startFiletime > 0 && stopFiletime == 0);
        }

        // Ονόματα εφαρμογών: package family ("Microsoft.WindowsCamera_8wekyb3d8bbwe") ή διαδρομή με '#' αντί '\'.
        public static string FriendlyName(string subKey)
        {
            var name = subKey.Replace('#', '\\');
            if (name.Contains('\\')) return name[(name.LastIndexOf('\\') + 1)..];
            var us = name.IndexOf('_');
            return us > 0 ? name[..us] : name;
        }

        public static IReadOnlyList<DeviceAccessEntry> Read(string capability) // "webcam" | "microphone"
        {
            var list = new List<DeviceAccessEntry>();
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using var root = hive.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\{capability}");
                    if (root == null) continue;
                    foreach (var sub in root.GetSubKeyNames())
                    {
                        if (sub == "NonPackaged") continue;
                        AddEntry(root, sub, list);
                    }
                    using var np = root.OpenSubKey("NonPackaged");
                    if (np != null) foreach (var sub in np.GetSubKeyNames()) AddEntry(np, sub, list);
                }
                catch { }
            }
            return list.OrderByDescending(e => e.InUseNow).ThenByDescending(e => e.LastStart ?? DateTime.MinValue).ToList();
        }

        private static void AddEntry(RegistryKey parent, string sub, List<DeviceAccessEntry> list)
        {
            try
            {
                using var k = parent.OpenSubKey(sub);
                if (k == null) return;
                var start = k.GetValue("LastUsedTimeStart") is long a ? a : 0;
                var stop = k.GetValue("LastUsedTimeStop") is long b ? b : 0;
                var allowed = !string.Equals(k.GetValue("Value") as string, "Deny", StringComparison.OrdinalIgnoreCase);
                if (start == 0 && stop == 0) return; // δεν έχει χρησιμοποιηθεί ποτέ
                var (s, e, inUse) = Interpret(start, stop);
                list.Add(new DeviceAccessEntry(FriendlyName(sub), s, e, inUse, allowed));
            }
            catch { }
        }
    }

    // ═════════ Μετατροπέας πολυμέσων (ffmpeg) ════════════════════════════════════════════════
    public enum MediaPreset { VideoToMp4H264, VideoToMp4H265, VideoToGif, ExtractMp3, ShareSized720p }

    public static class MediaConverterService
    {
        public static string OutputExtension(MediaPreset p) => p switch
        {
            MediaPreset.VideoToGif => ".gif",
            MediaPreset.ExtractMp3 => ".mp3",
            _ => ".mp4",
        };

        // Επιστρέφει τη λίστα ορισμάτων ffmpeg (χωρίς shell quoting - περνιέται μέσω ArgumentList).
        public static IReadOnlyList<string> BuildArgs(MediaPreset preset, string input, string output)
        {
            var a = new List<string> { "-y", "-hide_banner", "-i", input };
            switch (preset)
            {
                case MediaPreset.VideoToMp4H264:
                    a.AddRange(new[] { "-c:v", "libx264", "-preset", "medium", "-crf", "23", "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart" });
                    break;
                case MediaPreset.VideoToMp4H265:
                    a.AddRange(new[] { "-c:v", "libx265", "-preset", "medium", "-crf", "28", "-tag:v", "hvc1", "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart" });
                    break;
                case MediaPreset.ShareSized720p:
                    a.AddRange(new[] { "-vf", "scale=-2:720", "-c:v", "libx264", "-preset", "veryfast", "-crf", "26", "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart" });
                    break;
                case MediaPreset.VideoToGif:
                    a.AddRange(new[] { "-vf", "fps=12,scale=480:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse", "-loop", "0" });
                    break;
                case MediaPreset.ExtractMp3:
                    a.AddRange(new[] { "-vn", "-c:a", "libmp3lame", "-q:a", "2" });
                    break;
            }
            a.Add(output);
            return a;
        }

        // Δεν γράφουμε ποτέ πάνω στο αρχικό: "βίντεο.mp4" -> "βίντεο (GearWin).mp4" (+ αριθμός αν υπάρχει ήδη).
        public static string SuggestOutputPath(string input, MediaPreset preset, Func<string, bool>? exists = null)
        {
            exists ??= File.Exists;
            var dir = Path.GetDirectoryName(input) ?? "";
            var baseName = Path.GetFileNameWithoutExtension(input);
            var ext = OutputExtension(preset);
            var candidate = Path.Combine(dir, $"{baseName} (GearWin){ext}");
            for (var n = 2; exists(candidate); n++) candidate = Path.Combine(dir, $"{baseName} (GearWin {n}){ext}");
            return candidate;
        }

        // Πρόοδος από τη γραμμή "time=00:01:23.45" του stderr του ffmpeg, σε σχέση με τη συνολική διάρκεια.
        public static double? ParseProgress(string line, TimeSpan total)
        {
            var m = Regex.Match(line, @"time=(\d+):(\d{2}):(\d{2})(?:\.(\d+))?");
            if (!m.Success || total.TotalSeconds <= 0) return null;
            var secs = int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60 + int.Parse(m.Groups[3].Value)
                       + (m.Groups[4].Success ? double.Parse("0." + m.Groups[4].Value, CultureInfo.InvariantCulture) : 0);
            return Math.Clamp(secs / total.TotalSeconds, 0, 1);
        }

        public static TimeSpan? ParseDuration(string ffmpegBanner)
        {
            var m = Regex.Match(ffmpegBanner, @"Duration:\s*(\d+):(\d{2}):(\d{2})(?:\.(\d+))?");
            if (!m.Success) return null;
            var secs = int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60 + int.Parse(m.Groups[3].Value)
                       + (m.Groups[4].Success ? double.Parse("0." + m.Groups[4].Value, CultureInfo.InvariantCulture) : 0);
            return TimeSpan.FromSeconds(secs);
        }

        public static string? FindFfmpeg()
        {
            var candidates = new List<string>();
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                candidates.Add(Path.Combine(dir.Trim('"'), "ffmpeg.exe"));
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            candidates.Add(Path.Combine(local, "Microsoft", "WinGet", "Links", "ffmpeg.exe"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", "ffmpeg.exe"));
            return candidates.FirstOrDefault(File.Exists);
        }

        public static async Task<(bool Ok, string Message)> ConvertAsync(string ffmpeg, MediaPreset preset, string input, string output,
            IProgress<double>? progress, CancellationToken ct)
        {
            var psi = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var a in BuildArgs(preset, input, output)) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg");
            using var reg = ct.Register(() => { try { p.Kill(true); } catch { } });
            TimeSpan? total = null;
            var tail = new StringBuilder();
            var stdout = p.StandardOutput.ReadToEndAsync();
            string? line;
            while ((line = await p.StandardError.ReadLineAsync()) != null)
            {
                total ??= ParseDuration(line);
                if (total is TimeSpan t && ParseProgress(line, t) is double frac) progress?.Report(frac);
                if (tail.Length > 2000) tail.Remove(0, 1000);
                tail.AppendLine(line);
            }
            await p.WaitForExitAsync();
            await stdout;
            if (ct.IsCancellationRequested)
            {
                try { File.Delete(output); } catch { } // ημιτελές αρχείο
                return (false, LanguageService.T("Health_ToolCancelled"));
            }
            return p.ExitCode == 0 ? (true, output) : (false, tail.ToString().Trim());
        }
    }
}
