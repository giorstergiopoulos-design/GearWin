using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    public enum GpuPreference { Auto = 0, PowerSaving = 1, HighPerformance = 2 }

    // 6.1.0 - ρυθμίσεις παιχνιδιών: (1) καθολικά tweaks σε μορφή SimpleTweak (ίδιο UI με την καρτέλα Tweaks),
    // (2) ΑΝΑ-ΠΑΙΧΝΙΔΙ ρυθμίσεις με τα επίσημα registry κλειδιά των Windows: προτίμηση GPU (Ρυθμίσεις >
    // Οθόνη > Γραφικά) και απενεργοποίηση βελτιστοποιήσεων πλήρους οθόνης. Οι pure συναρτήσεις Build*/Parse*
    // είναι ξεχωριστές για έλεγχο με unit tests.
    public static class GamingTweaksService
    {
        private const string GpuKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
        private const string LayersKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        private const string DisableFso = "DISABLEDXMAXIMIZEDWINDOWEDMODE";

        // ── Pure ─────────────────────────────────────────────────────────────────────────────

        public static string BuildGpuPreferenceValue(GpuPreference p) => $"GpuPreference={(int)p};";

        public static GpuPreference? ParseGpuPreference(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0].Trim().Equals("GpuPreference", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(kv[1].Trim(), out var n) && Enum.IsDefined(typeof(GpuPreference), n))
                    return (GpuPreference)n;
            }
            return null;
        }

        // Τιμή του AppCompatFlags\Layers: "~ FLAG1 FLAG2" (το "~" πρώτο). Επιστρέφει τη νέα τιμή ή null αν δεν μένει κανένα flag.
        public static string? SetLayerFlag(string? current, string flag, bool enabled)
        {
            var flags = ParseLayerFlags(current);
            if (enabled) flags.Add(flag); else flags.Remove(flag);
            return flags.Count == 0 ? null : "~ " + string.Join(" ", flags);
        }

        public static HashSet<string> ParseLayerFlags(string? value) =>
            new((value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t != "~"), StringComparer.OrdinalIgnoreCase);

        public static bool HasLayerFlag(string? value, string flag) => ParseLayerFlags(value).Contains(flag);

        // ── Ανά παιχνίδι (HKCU - δεν χρειάζεται διαχειριστής) ──────────────────────────────

        public static GpuPreference? GetGpuPreference(string exePath)
        {
            try { using var k = Registry.CurrentUser.OpenSubKey(GpuKey); return ParseGpuPreference(k?.GetValue(exePath) as string); }
            catch { return null; }
        }

        public static bool SetGpuPreference(string exePath, GpuPreference? pref)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(GpuKey);
                if (k == null) return false;
                if (pref == null || pref == GpuPreference.Auto) k.DeleteValue(exePath, throwOnMissingValue: false);
                else k.SetValue(exePath, BuildGpuPreferenceValue(pref.Value), RegistryValueKind.String);
                return true;
            }
            catch { return false; }
        }

        public static bool GetFullscreenOptimizationsDisabled(string exePath)
        {
            try { using var k = Registry.CurrentUser.OpenSubKey(LayersKey); return HasLayerFlag(k?.GetValue(exePath) as string, DisableFso); }
            catch { return false; }
        }

        public static bool SetFullscreenOptimizationsDisabled(string exePath, bool disabled)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(LayersKey);
                if (k == null) return false;
                var next = SetLayerFlag(k.GetValue(exePath) as string, DisableFso, disabled);
                if (next == null) k.DeleteValue(exePath, throwOnMissingValue: false);
                else k.SetValue(exePath, next, RegistryValueKind.String);
                return true;
            }
            catch { return false; }
        }

        // ── Καθολικά tweaks ──────────────────────────────────────────────────────────────────

        private static bool? ReadDword(RegistryKey hive, string path, string name, int onValue)
        {
            try { using var k = hive.OpenSubKey(path); return k?.GetValue(name) is int v ? v == onValue : null; }
            catch { return null; }
        }

        private static void WriteDword(RegistryKey hive, string path, string name, int value)
        {
            using var k = hive.CreateSubKey(path) ?? throw new InvalidOperationException(path);
            k.SetValue(name, value, RegistryValueKind.DWord);
        }

        public static IReadOnlyList<SimpleTweak> GlobalTweaks() => new[]
        {
            // Λειτουργία Παιχνιδιού: ο scheduler δίνει προτεραιότητα στο παιχνίδι και περιορίζει background εργασίες.
            new SimpleTweak(LanguageService.T("Gaming_GameModeLabel"), LanguageService.T("Gaming_GameModeDesc"),
                () => WriteDword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1),
                () => WriteDword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 0),
                () => ReadDword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1) ?? true, "GameMode"),
            // HAGS: Hardware-accelerated GPU scheduling (HwSchMode 2 = ON, 1 = OFF) - απαιτεί επανεκκίνηση, HKLM.
            new SimpleTweak(LanguageService.T("Gaming_HagsLabel"), LanguageService.T("Gaming_HagsDesc"),
                () => WriteDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2),
                () => WriteDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 1),
                () => ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2), "Hags", RequiresAdmin: true),
            // Βελτιστοποιήσεις για παιχνίδια σε παράθυρο + VRR (DirectX user global settings).
            new SimpleTweak(LanguageService.T("Gaming_WindowedOptLabel"), LanguageService.T("Gaming_WindowedOptDesc"),
                () => SetGlobalDx("SwapEffectUpgradeEnable", true),
                () => SetGlobalDx("SwapEffectUpgradeEnable", false),
                () => GetGlobalDx("SwapEffectUpgradeEnable"), "WindowedOpt"),
            new SimpleTweak(LanguageService.T("Gaming_VrrLabel"), LanguageService.T("Gaming_VrrDesc"),
                () => SetGlobalDx("VRROptimizeEnable", true),
                () => SetGlobalDx("VRROptimizeEnable", false),
                () => GetGlobalDx("VRROptimizeEnable"), "VrrOptimize"),
        };

        private const string DxGlobalName = "DirectXUserGlobalSettings";

        public static Dictionary<string, bool> ParseDxGlobal(string? value)
        {
            var d = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in (value ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2) d[kv[0].Trim()] = kv[1].Trim() == "1";
            }
            return d;
        }

        public static string BuildDxGlobal(Dictionary<string, bool> d) =>
            string.Concat(d.Select(kv => $"{kv.Key}={(kv.Value ? 1 : 0)};"));

        private static bool? GetGlobalDx(string setting)
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(GpuKey);
                var d = ParseDxGlobal(k?.GetValue(DxGlobalName) as string);
                return d.TryGetValue(setting, out var v) ? v : false;
            }
            catch { return null; }
        }

        private static void SetGlobalDx(string setting, bool on)
        {
            using var k = Registry.CurrentUser.CreateSubKey(GpuKey) ?? throw new InvalidOperationException(GpuKey);
            var d = ParseDxGlobal(k.GetValue(DxGlobalName) as string);
            d[setting] = on;
            k.SetValue(DxGlobalName, BuildDxGlobal(d), RegistryValueKind.String);
        }
    }
}
