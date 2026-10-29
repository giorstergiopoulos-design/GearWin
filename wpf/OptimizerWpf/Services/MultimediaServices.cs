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

    // ΔΙΟΡΘΩΣΗ (GEARWIN.MD: "εκτός από τους επίσημους κωδικοποιητές να εμφανίζονται και εκείνοι που
    // έχουν εγκατασταθεί από τον χρήστη από εφαρμογές όπως το k-lite") - το MediaCodecService.Check()
    // πάνω βλέπει ΜΟΝΟ τα 7 Appx "Media Feature Pack" του Microsoft Store· το K-Lite/LAV/ffdshow κ.λπ.
    // καταχωρούνται ως κλασικά (COM, όχι Appx) DirectShow filters, άρα δεν εμφανίζονταν ΠΟΤΕ. Δύο
    // ανιχνεύσεις: (1) το ίδιο file-existence check που ήδη εμπιστεύεται το BloatwareService.
    // InstallKLiteAsync για το K-Lite/MPC-HC, (2) απαρίθμηση HKEY_CLASSES_ROOT\Filter (instantiable
    // DirectShow filters) φιλτραρισμένη σε γνωστά third-party ονόματα ώστε να μη πλημμυρίσει η λίστα
    // με δεκάδες built-in Windows filters.
    public record ThirdPartyCodec(string Name);

    public static class ThirdPartyCodecService
    {
        private static readonly string[] KnownMarkers =
        {
            "LAV", "ffdshow", "DivX", "Xvid", "CoreAVC", "K-Lite", "MPC-HC", "MPC Video", "MPC Audio",
            "Haali", "AC3Filter", "CyberLink", "Perian", "Real", "QuickTime",
        };

        public static IReadOnlyList<ThirdPartyCodec> Check()
        {
            var names = new List<string>();

            try
            {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (new[] { Path.Combine(programFiles, "K-Lite Codec Pack"), Path.Combine(programFilesX86, "K-Lite Codec Pack") }.Any(Directory.Exists))
                    names.Add("K-Lite Codec Pack");
            }
            catch { }

            try
            {
                using var filterRoot = Registry.ClassesRoot.OpenSubKey("Filter");
                if (filterRoot != null)
                {
                    foreach (var clsid in filterRoot.GetSubKeyNames())
                    {
                        var friendly = ReadFilterFriendlyName(clsid);
                        if (friendly != null && LooksThirdParty(friendly) && !names.Contains(friendly))
                            names.Add(friendly);
                    }
                }
            }
            catch { }

            return names.Select(n => new ThirdPartyCodec(n)).ToList();
        }

        private static string? ReadFilterFriendlyName(string clsid)
        {
            try
            {
                using var filterKey = Registry.ClassesRoot.OpenSubKey($@"Filter\{clsid}");
                if (filterKey?.GetValue("FriendlyName") is string fn && !string.IsNullOrWhiteSpace(fn)) return fn;
            }
            catch { }
            try
            {
                using var clsidKey = Registry.ClassesRoot.OpenSubKey($@"CLSID\{clsid}");
                if (clsidKey?.GetValue(null) is string name && !string.IsNullOrWhiteSpace(name)) return name;
            }
            catch { }
            return null;
        }

        private static bool LooksThirdParty(string friendlyName) =>
            KnownMarkers.Any(m => friendlyName.Contains(m, StringComparison.OrdinalIgnoreCase));
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
}
