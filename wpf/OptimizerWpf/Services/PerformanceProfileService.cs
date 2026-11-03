using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace OptimizerWpf.Services
{
    public enum PerformanceTier { Low = 0, Balanced = 1, High = 2 }

    public sealed record HardwareSpecs(double RamGb, int LogicalCores, bool OnBattery, bool PowerSaver);

    // Πίνακας δυνατοτήτων ανά tier (ένα σημείο): πόσες σαρώσεις τρέχουν ταυτόχρονα, animations, ρυθμός ανανέωσης της Αρχικής.
    public sealed record PerformanceProfile(PerformanceTier Tier, int MaxParallelScans, bool AnimationsEnabled, bool TabFadeEnabled, double HomeRefreshSeconds);

    // 6.9.4 - επίπεδα απόδοσης: ανίχνευση hardware (RAM, λογικοί πυρήνες, μπαταρία/εξοικονόμηση), tier Low/Balanced/High, ρύθμιση χρήστη
    // (Αυτόματη/Χαμηλή/Ισορροπημένη/Υψηλή, AppSettings.PerformanceMode ή GEARWIN_FORCE_TIER) και δυναμική υποβάθμιση/επαναφορά (PerformanceGovernor).
    public static class PerformanceProfileService
    {
        public static event Action? Changed;

        // Καθαρή λογική (δοκιμάζεται με προσομοιωμένα specs)
        public static PerformanceTier Choose(HardwareSpecs s)
        {
            if (s.OnBattery && s.PowerSaver) return PerformanceTier.Low;
            if (s.RamGb < 8 || s.LogicalCores <= 2) return PerformanceTier.Low;
            if (s.LogicalCores >= 8 && s.RamGb >= 16 && !s.OnBattery) return PerformanceTier.High;
            return PerformanceTier.Balanced;
        }

        public static PerformanceProfile ProfileFor(PerformanceTier tier) => tier switch
        {
            PerformanceTier.Low => new PerformanceProfile(tier, 1, false, false, 4),
            PerformanceTier.Balanced => new PerformanceProfile(tier, 2, true, true, 2),
            _ => new PerformanceProfile(tier, 3, true, true, 1),
        };

        public static PerformanceTier? ParseMode(string? mode) => mode?.Trim().ToLowerInvariant() switch
        {
            "low" => PerformanceTier.Low,
            "balanced" => PerformanceTier.Balanced,
            "high" => PerformanceTier.High,
            _ => null,   // "auto" ή άγνωστο
        };

        // Βασικό tier: GEARWIN_FORCE_TIER > ρύθμιση χρήστη > ανίχνευση.
        public static PerformanceTier BaseTier(HardwareSpecs detected, string? mode, string? forced = null)
        {
            var f = ParseMode(forced);
            if (f.HasValue) return f.Value;
            var m = ParseMode(mode);
            return m ?? Choose(detected);
        }

        private static HardwareSpecs? _specs;
        private static int _downgrade;   // 0..2 επίπεδα προσωρινής υποβάθμισης από το governor

        public static HardwareSpecs Detected => _specs ??= Detect();

        public static HardwareSpecs Detect()
        {
            try
            {
                var ram = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024);
                var cores = Environment.ProcessorCount;
                var onBattery = false; var saver = false;
                if (GetSystemPowerStatus(out var ps)) { onBattery = ps.ACLineStatus == 0; saver = ps.SystemStatusFlag == 1; }
                return new HardwareSpecs(Math.Round(ram, 1), cores, onBattery, saver);
            }
            catch { return new HardwareSpecs(8, 4, false, false); }
        }

        public static PerformanceTier ConfiguredTier => BaseTier(Detected, AppSettingsService.Current.PerformanceMode, Environment.GetEnvironmentVariable("GEARWIN_FORCE_TIER"));

        public static PerformanceTier EffectiveTier => (PerformanceTier)Math.Max(0, (int)ConfiguredTier - _downgrade);

        public static PerformanceProfile Current => ProfileFor(EffectiveTier);

        public static bool Downgraded => _downgrade > 0;

        public static void Reevaluate() { _specs = null; Changed?.Invoke(); }

        // Γράφεται μία γραμμή «Performance tier» (έλεγχοι CI/διάγνωση)
        public static void LogStartup()
        {
            try
            {
                var dir = OncePerVersionStore.AppDataDir();
                Directory.CreateDirectory(dir);
                var d = Detected;
                var forced = Environment.GetEnvironmentVariable("GEARWIN_FORCE_TIER");
                File.AppendAllText(Path.Combine(dir, "performance.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Performance tier: {ConfiguredTier}{(ParseMode(forced).HasValue ? " (forced)" : AppSettingsService.Current.PerformanceMode is "low" or "balanced" or "high" ? " (setting)" : " (detected)")} - RAM {d.RamGb} GB, {d.LogicalCores} logical cores, battery {d.OnBattery}, power saver {d.PowerSaver}{Environment.NewLine}");
            }
            catch { }
        }

        internal static void SetDowngrade(int levels)
        {
            levels = Math.Clamp(levels, 0, 2);
            if (levels == _downgrade) return;
            _downgrade = levels;
            Changed?.Invoke();
        }

        // ---- governor: υποβάθμιση ενός επιπέδου αν το CPU > 85 % για 10 s, επαναφορά όταν < 50 % για 30 s (υστέρηση, χωρίς flicker)
        public const double HighCpu = 85, LowCpu = 50;
        public const int DowngradeSeconds = 10, RecoverSeconds = 30;

        // Καθαρή λογική: samples = ένα δείγμα CPU % ανά δευτερόλεπτο (το νεότερο τελευταίο).
        public static int NextDowngrade(int current, IReadOnlyList<double> samples)
        {
            if (samples.Count >= DowngradeSeconds && samples.Skip(samples.Count - DowngradeSeconds).All(c => c > HighCpu)) return Math.Min(2, current + 1);
            if (current > 0 && samples.Count >= RecoverSeconds && samples.Skip(samples.Count - RecoverSeconds).All(c => c < LowCpu)) return current - 1;
            return current;
        }

        private static DispatcherTimer? _timer;
        private static readonly List<double> Samples = new();
        private static ulong _lastIdle, _lastKernel, _lastUser;

        public static void StartGovernor()
        {
            if (_timer != null || ParseMode(Environment.GetEnvironmentVariable("GEARWIN_FORCE_TIER")).HasValue) return;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) =>
            {
                var cpu = SampleCpu();
                if (cpu < 0) return;
                Samples.Add(cpu);
                if (Samples.Count > RecoverSeconds + 5) Samples.RemoveAt(0);
                var next = NextDowngrade(_downgrade, Samples);
                if (next != _downgrade)
                {
                    // μετά από αλλαγή, νέα μέτρηση από την αρχή (υστέρηση)
                    Samples.Clear();
                    // δεν υποβαθμίζουμε κάτω από Low
                    if ((int)ConfiguredTier - next >= 0) SetDowngrade(next);
                }
            };
            _timer.Start();
        }

        public static void StopGovernor() { _timer?.Stop(); _timer = null; }

        private static double SampleCpu()
        {
            try
            {
                if (!GetSystemTimes(out var idle, out var kernel, out var user)) return -1;
                ulong i = unchecked((ulong)idle), k = unchecked((ulong)kernel), u = unchecked((ulong)user);
                var di = i - _lastIdle; var dk = k - _lastKernel; var du = u - _lastUser;
                var first = _lastKernel == 0;
                _lastIdle = i; _lastKernel = k; _lastUser = u;
                if (first) return -1;
                var total = dk + du;
                return total == 0 ? 0 : (total - di) * 100.0 / total;
            }
            catch { return -1; }
        }

        [StructLayout(LayoutKind.Sequential)] private struct SystemPowerStatus { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public int BatteryLifeTime, BatteryFullLifeTime; }
        [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
        [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    }
}
