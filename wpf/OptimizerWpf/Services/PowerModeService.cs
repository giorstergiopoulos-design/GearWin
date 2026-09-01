using System.Diagnostics;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    // Πλήρης C# port του Office Mode / Gaming Mode του Optimizer.ps1 (Set-OfficeMode/Disable-OfficeMode/
    // Set-GamingMode/Disable-GamingMode, ~10855-10946) - όχι πια μόνο η εναλλαγή πλάνου ενέργειας.
    // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: η εφαρμογή δεν ζητάει ακόμα elevation - τα registry keys κάτω από
    // HKEY_LOCAL_MACHINE (Network Throttling, HAGS, VBS/HVCI κ.λπ.) θα αποτύχουν σιωπηλά χωρίς αυτήν,
    // ΑΚΡΙΒΩΣ όπως το Set-RegSafe του Optimizer.ps1 - το ίδιο ρίσκο υπήρχε ήδη εκεί, το Optimizer.ps1
    // απλά τρέχει ΠΑΝΤΑ ως Administrator.
    public static class PowerModeService
    {
        public static bool SetBalancedPlan() => RunPowercfg("/setactive SCHEME_BALANCED");

        public static bool SetHighPerformancePlan() => RunPowercfg("/setactive SCHEME_MIN");

        public static void SetOfficeMode()
        {
            RunPowercfg("/setactive SCHEME_BALANCED");
            SetRegSafe(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2);
            SetRegSafe(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "ClearPageFileAtShutdown", 0);
            SetRegSafe(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", "RequireAdmin", RegistryValueKind.String);
            SetRegSafe(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0);
            SetRegSafe(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0);
        }

        public static void DisableOfficeMode()
        {
            SetRegSafe(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 0);
            SetRegSafe(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 1);
            SetRegSafe(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 1);
            RestartExplorer();
        }

        public static void SetGamingMode()
        {
            RunPowercfg("/setactive SCHEME_MIN");
            SetRegSafe(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2);
            SetRegSafe(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AllowAutoGameMode", 1);
            SetRegSafe(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0);
            SetRegSafe(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", 255);
            SetRegSafe(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0);
            SetRegSafe(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", 8);
            SetRegSafe(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", 6);
            SetRegSafe(Registry.CurrentUser, @"Control Panel\Desktop", "MenuShowDelay", "0", RegistryValueKind.String);
            SetRegSafe(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 38);
            SetRegSafe(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2);
            SetRegSafe(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0);
            SetRegSafe(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 0);
        }

        public static void DisableGamingMode()
        {
            RunPowercfg("/setactive SCHEME_BALANCED");
            SetRegSafe(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 1);
            SetRegSafe(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", 10);
            SetRegSafe(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 14);
            SetRegSafe(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 1);
            SetRegSafe(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 1);
            RestartExplorer();
        }

        // Ίδιο με το Stop-Process -Name explorer -Force του ps1 original - δεν επανεκκινεί χειροκίνητα
        // τον Explorer, βασίζεται στη δική του αυτόματη επανεκκίνηση από τα Windows (ίδια συμπεριφορά
        // με το αρχικό, δεν είναι bug εδώ).
        private static void RestartExplorer()
        {
            foreach (var p in Process.GetProcessesByName("explorer"))
            {
                try { p.Kill(); } catch { }
            }
        }

        private static void SetRegSafe(RegistryKey root, string path, string name, int value) =>
            SetRegSafe(root, path, name, value, RegistryValueKind.DWord);

        private static void SetRegSafe(RegistryKey root, string path, string name, object value, RegistryValueKind kind)
        {
            try
            {
                using var key = root.CreateSubKey(path, writable: true);
                key?.SetValue(name, value, kind);
            }
            catch
            {
                // Ίδια πολιτική ανοχής σφαλμάτων με το Set-RegSafe του Optimizer.ps1 - ένα μεμονωμένο
                // registry key (π.χ. λόγω έλλειψης elevation) δεν πρέπει να σταματήσει τα υπόλοιπα.
            }
        }

        private static bool RunPowercfg(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var process = Process.Start(psi);
                process?.WaitForExit(5000);
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
