using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    // Απλό DWORD/String registry toggle - "off" γυρνάει στην backed-up πραγματική προηγούμενη τιμή
    // (μέσω TweakBackupService), όχι hardcoded default - ίδια σημασιολογία με το ps1 original.
    public record RegTweak(string Label, string Description, RegistryHive Hive, string Path, string Name, object OnValue, RegistryValueKind Kind, string BackupKey);

    public record SimpleTweak(string Label, string Description, Action OnAction, Action OffAction);

    // Port του Optimizer.ps1's Επιπλέον Ρυθμίσεις καρτέλα (~16519-17573) - η πυκνότερη καρτέλα της
    // εφαρμογής. Βλ. HANDOFF.md §0.4ιβ για πλήρη τεκμηρίωση όλων των 25 tweaks.
    public static class TweakService
    {
        public static readonly IReadOnlyList<RegTweak> MainTweaks = new[]
        {
            new RegTweak("Storage Sense (Αυτόματος Καθαρισμός Δίσκου)", "Αυτόματος καθαρισμός προσωρινών αρχείων.",
                RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "01", 1, RegistryValueKind.DWord, "StorageSense"),
            new RegTweak("Fast Startup (Hiberboot)", "Ταχύτερη εκκίνηση - μπορεί να προκαλέσει προβλήματα σε dual-boot/VM.",
                RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 1, RegistryValueKind.DWord, "Hiberboot"),
            new RegTweak("Εμφάνιση Επεκτάσεων & Κρυφών Αρχείων", "Εμφανίζει επεκτάσεις αρχείων και κρυφά αρχεία στην Εξερεύνηση.",
                RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0, RegistryValueKind.DWord, "HideFileExt"),
            new RegTweak("Delivery Optimization (P2P Κοινή Χρήση Ενημερώσεων)", "Λήψη ενημερώσεων και από άλλους υπολογιστές στο δίκτυο/διαδίκτυο.",
                RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config", "DODownloadMode", 1, RegistryValueKind.DWord, "DODownloadMode"),
            new RegTweak("Αφαίρεση 'Προτεινόμενα' από Start Menu", "Αφαιρεί τις προτεινόμενες εφαρμογές/διαφημίσεις από το μενού Έναρξης.",
                RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations", 0, RegistryValueKind.DWord, "IrisRecommendations"),
            new RegTweak("Ιστορικό Πρόχειρου (Win+V)", "Ενεργοποιεί το ιστορικό clipboard των Windows.",
                RegistryHive.CurrentUser, @"Software\Microsoft\Clipboard", "EnableClipboardHistory", 1, RegistryValueKind.DWord, "ClipboardHistory"),
            new RegTweak("Απενεργοποίηση Network Throttling", "Αφαιρεί το όριο εύρους ζώνης πολυμέσων για χαμηλότερη καθυστέρηση.",
                RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord, "NetThrottle"),
            new RegTweak("Βελτιστοποίηση System Responsiveness", "Λιγότερος δεσμευμένος πόρος CPU για background υπηρεσίες.",
                RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0, RegistryValueKind.DWord, "SysResponsiveness"),
            new RegTweak("Τηλεμετρία στο Ελάχιστο Επίπεδο", "Μειώνει τη συλλογή διαγνωστικών δεδομένων στο ελάχιστο επιτρεπτό (Home/Pro).",
                RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 1, RegistryValueKind.DWord, "Telemetry"),
        };

        public static readonly IReadOnlyList<RegTweak> AiCopilotTweaks = new[]
        {
            new RegTweak("Απενεργοποίηση Windows Copilot", "'Μαλακή' απενεργοποίηση - το πακέτο παραμένει εγκατεστημένο.",
                RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 0, RegistryValueKind.DWord, "CopilotBtn"),
            new RegTweak("Απενεργοποίηση Windows Recall", "Απαιτεί Copilot+ PC.",
                RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1, RegistryValueKind.DWord, "RecallCU"),
            new RegTweak("Απενεργοποίηση Click To Do", "Windows 11 25H2 - ανάλυση στιγμιότυπων Recall.",
                RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableClickToDo", 1, RegistryValueKind.DWord, "ClickToDo"),
            new RegTweak("Απενεργοποίηση Αυτόματης Εκκίνησης AI Service", "",
                RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1, RegistryValueKind.DWord, "AiServiceLM"),
            new RegTweak("Απενεργοποίηση AI στο Edge", "Αφαιρεί Copilot sidebar/Bing Discover/AI writing assist.",
                RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "HubsSidebarEnabled", 0, RegistryValueKind.DWord, "EdgeAI"),
            new RegTweak("Απενεργοποίηση AI στο Paint", "Αφαιρεί το κουμπί Cocreator.",
                RegistryHive.CurrentUser, @"Software\Microsoft\Paint\App", "EnableCopilot", 0, RegistryValueKind.DWord, "PaintAI"),
            new RegTweak("Απενεργοποίηση AI στο Notepad", "Αφαιρεί Rewrite/Summarize.",
                RegistryHive.CurrentUser, @"Software\Microsoft\Notepad", "CocreatorEnabled", 0, RegistryValueKind.DWord, "NotepadAI"),
        };

        public static readonly IReadOnlyList<RegTweak> PerfRegTweaks = new[]
        {
            new RegTweak("Ενεργοποίηση HAGS (Hardware GPU Scheduling)", "Απαιτεί επανεκκίνηση + πρόσφατο GPU/driver.",
                RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, RegistryValueKind.DWord, "HAGS"),
        };

        public static readonly IReadOnlyList<SimpleTweak> PerfPowercfgTweaks = new[]
        {
            new SimpleTweak("Εξοικονόμηση Μπαταρίας Πάντα Ενεργή (Μέγιστη)", "Μόνο laptop.",
                () => RunPowercfg("/setdcvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD 0", "/S SCHEME_CURRENT"),
                () => RunPowercfg("/setdcvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD 20", "/S SCHEME_CURRENT")),
            new SimpleTweak("Αναστολή Αδρανών Θυρών USB", "Απενεργοποίηση = πάντα ενεργές (λιγότερες τυχαίες αποσυνδέσεις, περισσότερη κατανάλωση).",
                () => RunPowercfg("/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0",
                                   "/setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0"),
                () => RunPowercfg("/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 1",
                                   "/setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 1")),
            new SimpleTweak("Εξοικονόμηση Ενέργειας PCI Express", "Απενεργοποίηση μπορεί να διορθώσει κόψιμο ήχου/αποσυνδέσεις USB.",
                () => RunPowercfg("/setacvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 0",
                                   "/setdcvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 0"),
                () => RunPowercfg("/setacvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 1",
                                   "/setdcvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 1")),
        };

        // Ενιαία λίστα των 14 κύριων tweaks σε μια μορφή (RegTweak + ειδικές περιπτώσεις μαζί) -
        // απλοποιεί το UI σε ένα ItemsControl αντί για ανάμειξη δύο τύπων record.
        public static IReadOnlyList<SimpleTweak> AllMainTweaks()
        {
            var list = new List<SimpleTweak>();
            var m = MainTweaks;
            list.Add(ToSimple(m[0])); // Storage Sense
            list.Add(ToSimple(m[1])); // Fast Startup
            list.Add(new SimpleTweak("Κλασικό (Win10) Δεξί-Κλικ Μενού", "Επαναφέρει το παλιό, πλήρες μενού δεξιού κλικ.", EnableClassicContextMenu, DisableClassicContextMenu));
            list.Add(ToSimple(m[2])); // File extensions
            list.Add(ToSimple(m[3])); // Delivery Optimization
            list.Add(new SimpleTweak("Απενεργοποίηση Επιτάχυνσης Ποντικιού", "'Enhance pointer precision' off.", MouseAccelOff, MouseAccelOn));
            list.Add(new SimpleTweak("Αρχείο Αδρανοποίησης (Hibernation)", "", HibernateOn, HibernateOff));
            list.Add(new SimpleTweak("Ultimate Performance (Κρυφό Πλάνο Ενέργειας)", "", UltimatePerformanceOn, UltimatePerformanceOff));
            list.Add(ToSimple(m[4])); // Remove Recommended
            list.Add(ToSimple(m[5])); // Clipboard history
            list.Add(ToSimple(m[6])); // Network throttling
            list.Add(ToSimple(m[7])); // System responsiveness
            list.Add(new SimpleTweak("Απενεργοποίηση Game Bar / Game DVR", "Ξεχωριστό από το Gaming Mode - αγγίζει ΜΟΝΟ το Game DVR.", DisableGameBar, EnableGameBar));
            list.Add(ToSimple(m[8])); // Telemetry
            return list;
        }

        private static SimpleTweak ToSimple(RegTweak t) => new(t.Label, t.Description, () => ApplyOn(t), () => ApplyOff(t));

        public static IReadOnlyList<SimpleTweak> AiCopilotTweaksSimple() => AiCopilotTweaks.Select(ToSimple).ToList();

        public static IReadOnlyList<SimpleTweak> PerfTweaksSimple() =>
            PerfRegTweaks.Select(ToSimple).Concat(PerfPowercfgTweaks).ToList();

        private static void MouseAccelOff()
        {
            RegSet(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "0", RegistryValueKind.String);
            RegSet(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "0", RegistryValueKind.String);
            RegSet(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "0", RegistryValueKind.String);
        }

        private static void MouseAccelOn()
        {
            RegSet(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "1", RegistryValueKind.String);
            RegSet(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "6", RegistryValueKind.String);
            RegSet(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "10", RegistryValueKind.String);
        }

        public static void ApplyOn(RegTweak tweak)
        {
            using var key = OpenHive(tweak.Hive).CreateSubKey(tweak.Path, writable: true);
            var current = key?.GetValue(tweak.Name);
            if (current != null) TweakBackupService.BackupIfNeeded(tweak.BackupKey, current.ToString()!);
            key?.SetValue(tweak.Name, tweak.OnValue, tweak.Kind);
        }

        public static void ApplyOff(RegTweak tweak)
        {
            using var key = OpenHive(tweak.Hive).CreateSubKey(tweak.Path, writable: true);
            var backup = TweakBackupService.GetBackup(tweak.BackupKey);
            if (backup == null) { key?.DeleteValue(tweak.Name, throwOnMissingValue: false); return; }
            object value = tweak.Kind == RegistryValueKind.String ? backup : int.Parse(backup);
            key?.SetValue(tweak.Name, value, tweak.Kind);
        }

        private static RegistryKey OpenHive(RegistryHive hive) => hive == RegistryHive.LocalMachine ? Registry.LocalMachine : Registry.CurrentUser;

        // ===== Κλασικό (Win10) Δεξί-Κλικ Μενού - key create/delete, ΟΧΙ value backup =====

        public static void EnableClassicContextMenu()
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", writable: true);
            key?.SetValue("", "", RegistryValueKind.String);
            RestartExplorer();
        }

        public static void DisableClassicContextMenu()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}", throwOnMissingSubKey: false); } catch { }
            RestartExplorer();
        }

        // ===== Αρχείο Αδρανοποίησης / Ultimate Performance =====

        public static void HibernateOn() => RunProcess("powercfg.exe", "/hibernate on");
        public static void HibernateOff() => RunProcess("powercfg.exe", "/hibernate off");

        public static void UltimatePerformanceOn() => RunProcess("powercfg.exe", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61");

        public static void UltimatePerformanceOff()
        {
            RunProcess("powercfg.exe", "/setactive SCHEME_BALANCED");
            var list = RunProcessCapture("powercfg.exe", "/list");
            var match = System.Text.RegularExpressions.Regex.Match(list, @"([0-9a-fA-F\-]{36}).*Ultimate Performance");
            if (match.Success) RunProcess("powercfg.exe", $"-delete {match.Groups[1].Value}");
        }

        // ===== Απενεργοποίηση Game Bar / Game DVR (ξεχωριστό από Gaming Mode) =====

        public static void DisableGameBar()
        {
            RegSet(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0);
            RegSet(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0);
        }

        public static void EnableGameBar()
        {
            RegSet(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 1);
            try { Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", throwOnMissingSubKey: false); } catch { }
        }

        // ===== Οπτικά Εφέ - ΠΟΤΕ το undocumented UserPreferencesMask =====

        public static void SetVisualEffectsPreset(int visualFx, int onOff)
        {
            RegSet(Registry.CurrentUser, @"Control Panel\Desktop", "DragFullWindows", onOff.ToString(), RegistryValueKind.String);
            RegSet(Registry.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", onOff.ToString(), RegistryValueKind.String);
            RegSet(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect", onOff);
            RegSet(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", onOff);
            RegSet(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow", onOff);
            RegSet(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "EnableTransparency", onOff);
            RegSet(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", visualFx);
            RestartExplorer();
        }

        // ===== Κρυπτογράφηση Συσκευής - ΠΟΤΕ registry toggle, μόνο άνοιγμα ρυθμίσεων =====

        public static void OpenDeviceEncryptionSettings()
        {
            try { Process.Start(new ProcessStartInfo("control.exe", "/name Microsoft.BitLockerDriveEncryption") { UseShellExecute = true }); }
            catch { Process.Start(new ProcessStartInfo("ms-settings:deviceencryption") { UseShellExecute = true }); }
        }

        // ===== 2 hardcoded δεξί-κλικ επιλογές (Ιδιοκτησία/PowerShell εδώ) =====

        public static void InstallTakeOwnership()
        {
            foreach (var root in new[] { @"*\shell\OptimizerTakeOwnership", @"Directory\shell\OptimizerTakeOwnership", @"Drive\shell\OptimizerTakeOwnership" })
            {
                using var key = Registry.ClassesRoot.CreateSubKey(root, writable: true);
                key?.SetValue("", "Ιδιοκτησία");
                key?.SetValue("HasLUAShield", "");
                using var cmd = Registry.ClassesRoot.CreateSubKey(root + @"\command", writable: true);
                cmd?.SetValue("", "cmd.exe /c takeown /f \"%1\" && icacls \"%1\" /grant *S-1-3-4:F /c /l & pause");
            }
        }

        public static void RemoveTakeOwnership()
        {
            foreach (var root in new[] { @"*\shell\OptimizerTakeOwnership", @"Directory\shell\OptimizerTakeOwnership", @"Drive\shell\OptimizerTakeOwnership" })
                try { Registry.ClassesRoot.DeleteSubKeyTree(root, throwOnMissingSubKey: false); } catch { }
        }

        public static void InstallOpenPowerShellHere()
        {
            foreach (var root in new[] { @"Directory\shell\OptimizerOpenPS", @"Directory\Background\shell\OptimizerOpenPS" })
            {
                using var key = Registry.ClassesRoot.CreateSubKey(root, writable: true);
                key?.SetValue("", "Άνοιγμα PowerShell εδώ");
                using var cmd = Registry.ClassesRoot.CreateSubKey(root + @"\command", writable: true);
                cmd?.SetValue("", root.Contains("Background")
                    ? "powershell.exe -NoExit -Command \"Set-Location -LiteralPath '%V'\""
                    : "powershell.exe -NoExit -Command \"Set-Location -LiteralPath '%1'\"");
            }
        }

        public static void RemoveOpenPowerShellHere()
        {
            foreach (var root in new[] { @"Directory\shell\OptimizerOpenPS", @"Directory\Background\shell\OptimizerOpenPS" })
                try { Registry.ClassesRoot.DeleteSubKeyTree(root, throwOnMissingSubKey: false); } catch { }
        }

        // ===== helpers =====

        private static void RegSet(RegistryKey hive, string path, string name, object value, RegistryValueKind kind = RegistryValueKind.DWord)
        {
            using var key = hive.CreateSubKey(path, writable: true);
            key?.SetValue(name, value, kind);
        }

        private static void RestartExplorer()
        {
            foreach (var p in Process.GetProcessesByName("explorer")) { try { p.Kill(); } catch { } }
        }

        private static void RunProcess(string fileName, string arguments)
        {
            try { using var p = Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Minimized }); p?.WaitForExit(); }
            catch { }
        }

        private static string RunProcessCapture(string fileName, string arguments)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(fileName, arguments) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = System.Text.Encoding.UTF8 });
                var output = p?.StandardOutput.ReadToEnd() ?? "";
                p?.WaitForExit();
                return output;
            }
            catch { return ""; }
        }

        private static void RunPowercfg(params string[] argSets)
        {
            foreach (var args in argSets) RunProcess("powercfg.exe", args);
        }
    }
}
