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

    // ΔΙΟΡΘΩΣΗ (γνωστό κενό #01 του roadmap: "οι διακόπτες δεν διαβάζουν την πραγματική τρέχουσα τιμή
    // του μητρώου - ξεκινούν πάντα ανενεργοί") - το προαιρετικό DetectState διαβάζει την ΠΡΑΓΜΑΤΙΚΗ
    // τρέχουσα κατάσταση του συστήματος (μητρώο/powercfg), ώστε το TweaksView να δείχνει τον διακόπτη
    // ήδη ενεργό αν το tweak ισχύει ήδη - όχι πια πάντα false στο άνοιγμα. null σημαίνει "δεν υπάρχει
    // φθηνός/αξιόπιστος τρόπος ανίχνευσης" (π.χ. καμία τέτοια περίπτωση προς το παρόν - βλ. παρακάτω,
    // όλα τα 25 tweaks έχουν πλέον πραγματική ανίχνευση). Προαιρετική παράμετρος (=null προεπιλογή)
    // ώστε τα ήδη υπάρχοντα 4-args call sites να μη χρειάζονται αλλαγή.
    public record SimpleTweak(string Label, string Description, Action OnAction, Action OffAction, Func<bool?>? DetectState = null);

    // Port του Optimizer.ps1's Επιπλέον Ρυθμίσεις καρτέλα (~16519-17573) - η πυκνότερη καρτέλα της
    // εφαρμογής. Βλ. HANDOFF.md §0.4ιβ για πλήρη τεκμηρίωση όλων των 25 tweaks.
    public static class TweakService
    {
        // Properties (όχι readonly fields) ώστε να ξαναχτίζονται με την τρέχουσα γλώσσα κάθε φορά που
        // ανοίγει το TweaksView (ρητό αίτημα χρήστη: "μετάφρασε τα όλα").
        public static IReadOnlyList<RegTweak> MainTweaks => new[]
        {
            new RegTweak(LanguageService.T("Tweak_M0Label"), LanguageService.T("Tweak_M0Desc"),
                RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "01", 1, RegistryValueKind.DWord, "StorageSense"),
            new RegTweak(LanguageService.T("Tweak_M1Label"), LanguageService.T("Tweak_M1Desc"),
                RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 1, RegistryValueKind.DWord, "Hiberboot"),
            new RegTweak(LanguageService.T("Tweak_M2Label"), LanguageService.T("Tweak_M2Desc"),
                RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0, RegistryValueKind.DWord, "HideFileExt"),
            new RegTweak(LanguageService.T("Tweak_M3Label"), LanguageService.T("Tweak_M3Desc"),
                RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config", "DODownloadMode", 1, RegistryValueKind.DWord, "DODownloadMode"),
            new RegTweak(LanguageService.T("Tweak_M4Label"), LanguageService.T("Tweak_M4Desc"),
                RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations", 0, RegistryValueKind.DWord, "IrisRecommendations"),
            new RegTweak(LanguageService.T("Tweak_M5Label"), LanguageService.T("Tweak_M5Desc"),
                RegistryHive.CurrentUser, @"Software\Microsoft\Clipboard", "EnableClipboardHistory", 1, RegistryValueKind.DWord, "ClipboardHistory"),
            new RegTweak(LanguageService.T("Tweak_M6Label"), LanguageService.T("Tweak_M6Desc"),
                RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord, "NetThrottle"),
            new RegTweak(LanguageService.T("Tweak_M7Label"), LanguageService.T("Tweak_M7Desc"),
                RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0, RegistryValueKind.DWord, "SysResponsiveness"),
            new RegTweak(LanguageService.T("Tweak_M8Label"), LanguageService.T("Tweak_M8Desc"),
                RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 1, RegistryValueKind.DWord, "Telemetry"),
        };

        public static IReadOnlyList<RegTweak> AiCopilotTweaks => new[]
        {
            new RegTweak(LanguageService.T("Tweak_A0Label"), LanguageService.T("Tweak_A0Desc"),
                RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 0, RegistryValueKind.DWord, "CopilotBtn"),
            new RegTweak(LanguageService.T("Tweak_A1Label"), LanguageService.T("Tweak_A1Desc"),
                RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1, RegistryValueKind.DWord, "RecallCU"),
            new RegTweak(LanguageService.T("Tweak_A2Label"), LanguageService.T("Tweak_A2Desc"),
                RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableClickToDo", 1, RegistryValueKind.DWord, "ClickToDo"),
            new RegTweak(LanguageService.T("Tweak_A3Label"), "",
                RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1, RegistryValueKind.DWord, "AiServiceLM"),
            new RegTweak(LanguageService.T("Tweak_A4Label"), LanguageService.T("Tweak_A4Desc"),
                RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "HubsSidebarEnabled", 0, RegistryValueKind.DWord, "EdgeAI"),
            new RegTweak(LanguageService.T("Tweak_A5Label"), LanguageService.T("Tweak_A5Desc"),
                RegistryHive.CurrentUser, @"Software\Microsoft\Paint\App", "EnableCopilot", 0, RegistryValueKind.DWord, "PaintAI"),
            new RegTweak(LanguageService.T("Tweak_A6Label"), LanguageService.T("Tweak_A6Desc"),
                RegistryHive.CurrentUser, @"Software\Microsoft\Notepad", "CocreatorEnabled", 0, RegistryValueKind.DWord, "NotepadAI"),
        };

        public static IReadOnlyList<RegTweak> PerfRegTweaks => new[]
        {
            new RegTweak(LanguageService.T("Tweak_HagsLabel"), LanguageService.T("Tweak_HagsDesc"),
                RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, RegistryValueKind.DWord, "HAGS"),
        };

        public static IReadOnlyList<SimpleTweak> PerfPowercfgTweaks => new[]
        {
            new SimpleTweak(LanguageService.T("Tweak_P0Label"), LanguageService.T("Tweak_P0Desc"),
                () => RunPowercfg("/setdcvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD 0", "/S SCHEME_CURRENT"),
                () => RunPowercfg("/setdcvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD 20", "/S SCHEME_CURRENT"),
                DetectState: () => GetPowercfgIndex("SUB_ENERGYSAVER", "ESBATTTHRESHOLD", ac: false) is int i0 ? i0 == 0 : null),
            new SimpleTweak(LanguageService.T("Tweak_P1Label"), LanguageService.T("Tweak_P1Desc"),
                () => RunPowercfg("/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0",
                                   "/setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0"),
                () => RunPowercfg("/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 1",
                                   "/setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 1"),
                DetectState: () => GetPowercfgIndex("2a737441-1930-4402-8d77-b2bebba308a3", "48e6b7a6-50f5-4782-a5d4-53bb8f07e226", ac: true) is int i1 ? i1 == 0 : null),
            new SimpleTweak(LanguageService.T("Tweak_P2Label"), LanguageService.T("Tweak_P2Desc"),
                () => RunPowercfg("/setacvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 0",
                                   "/setdcvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 0"),
                () => RunPowercfg("/setacvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 1",
                                   "/setdcvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 1"),
                DetectState: () => GetPowercfgIndex("501a4d13-42af-4429-9fd1-a8218c268e20", "ee12f906-d277-404b-b6da-e5fa1a576df5", ac: true) is int i2 ? i2 == 0 : null),
        };

        // Ενιαία λίστα των 14 κύριων tweaks σε μια μορφή (RegTweak + ειδικές περιπτώσεις μαζί) -
        // απλοποιεί το UI σε ένα ItemsControl αντί για ανάμειξη δύο τύπων record.
        public static IReadOnlyList<SimpleTweak> AllMainTweaks()
        {
            var list = new List<SimpleTweak>();
            var m = MainTweaks;
            list.Add(ToSimple(m[0])); // Storage Sense
            list.Add(ToSimple(m[1])); // Fast Startup
            list.Add(new SimpleTweak(LanguageService.T("Tweak_ClassicMenuLabel"), LanguageService.T("Tweak_ClassicMenuDesc"), EnableClassicContextMenu, DisableClassicContextMenu,
                DetectState: () => { using var k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}"); return k != null; }));
            list.Add(ToSimple(m[2])); // File extensions
            list.Add(ToSimple(m[3])); // Delivery Optimization
            list.Add(new SimpleTweak(LanguageService.T("Tweak_MouseAccelLabel"), LanguageService.T("Tweak_MouseAccelDesc"), MouseAccelOff, MouseAccelOn,
                DetectState: () => { using var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse"); return k?.GetValue("MouseSpeed")?.ToString() == "0"; }));
            list.Add(new SimpleTweak(LanguageService.T("Tweak_HibernateLabel"), "", HibernateOn, HibernateOff,
                DetectState: () => { using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power"); return Convert.ToInt32(k?.GetValue("HibernateEnabled") ?? 0) == 1; }));
            list.Add(new SimpleTweak(LanguageService.T("Tweak_UltimatePerfLabel"), "", UltimatePerformanceOn, UltimatePerformanceOff,
                DetectState: () => RunProcessCapture("powercfg.exe", "/list").Contains("e9a42b02-d5df-448d-aa00-03f14749eb61", StringComparison.OrdinalIgnoreCase)));
            list.Add(ToSimple(m[4])); // Remove Recommended
            list.Add(ToSimple(m[5])); // Clipboard history
            list.Add(ToSimple(m[6])); // Network throttling
            list.Add(ToSimple(m[7])); // System responsiveness
            list.Add(new SimpleTweak(LanguageService.T("Tweak_GameBarLabel"), LanguageService.T("Tweak_GameBarDesc"), DisableGameBar, EnableGameBar,
                DetectState: () => { using var k = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore"); return Convert.ToInt32(k?.GetValue("GameDVR_Enabled") ?? 1) == 0; }));
            list.Add(ToSimple(m[8])); // Telemetry
            return list;
        }

        private static SimpleTweak ToSimple(RegTweak t) => new(t.Label, t.Description, () => ApplyOn(t), () => ApplyOff(t), DetectState: () => IsRegTweakOn(t));

        // ΔΙΟΡΘΩΣΗ (γνωστό κενό #01) - διαβάζει την ΠΡΑΓΜΑΤΙΚΗ τρέχουσα τιμή του κάθε RegTweak από το
        // μητρώο και τη συγκρίνει με το OnValue, αντί το UI να υποθέτει πάντα "ανενεργό". Απούσα τιμή
        // (καμία τιμή στο μητρώο ακόμα) μετράει ως "ανενεργό" - ίδια σημασιολογία με το ApplyOff's
        // DeleteValue όταν δεν υπάρχει backup.
        private static bool IsRegTweakOn(RegTweak t)
        {
            using var key = OpenHive(t.Hive).OpenSubKey(t.Path);
            var current = key?.GetValue(t.Name);
            if (current == null) return false;
            if (t.Kind == RegistryValueKind.String) return string.Equals(current.ToString(), t.OnValue.ToString(), StringComparison.OrdinalIgnoreCase);
            return Convert.ToInt64(current) == Convert.ToInt64(t.OnValue);
        }

        // ΔΙΟΡΘΩΣΗ (γνωστό κενό #01) - διαβάζει το τρέχον AC/DC Power Setting Index ενός powercfg
        // υπο-ρύθμισης (π.χ. USB Selective Suspend, PCIe Power Saving) μέσω "powercfg /q", για τα 3
        // tweaks που δεν έχουν καθόλου registry-based αναπαράσταση. null αν το parsing αποτύχει.
        private static int? GetPowercfgIndex(string subgroup, string setting, bool ac)
        {
            var output = RunProcessCapture("powercfg.exe", $"/q SCHEME_CURRENT {subgroup} {setting}");
            var pattern = ac ? @"Current AC Power Setting Index:\s*0x([0-9a-fA-F]+)" : @"Current DC Power Setting Index:\s*0x([0-9a-fA-F]+)";
            var match = System.Text.RegularExpressions.Regex.Match(output, pattern);
            return match.Success ? Convert.ToInt32(match.Groups[1].Value, 16) : (int?)null;
        }

        public static IReadOnlyList<SimpleTweak> AiCopilotTweaksSimple() => AiCopilotTweaks.Select(ToSimple).ToList();

        // ΝΕΟ - ρητό αίτημα χρήστη ("ενσωμάτωσε τα... ελαφρύτερα Windows") - 2 tweaks που ελαφρώνουν
        // τα ίδια τα Windows στο παρασκήνιο, ίδιο μοτίβο SimpleTweak/DetectState με τα υπόλοιπα.
        public static IReadOnlyList<SimpleTweak> LighterWindowsTweaksSimple() => new[]
        {
            new SimpleTweak(LanguageService.T("Tweak_BgAppsLabel"), LanguageService.T("Tweak_BgAppsDesc"),
                DisableBackgroundApps, EnableBackgroundApps, DetectState: DetectBackgroundAppsOff),
            new SimpleTweak(LanguageService.T("Tweak_SysMainLabel"), LanguageService.T("Tweak_SysMainDesc"),
                DisableSysMain, EnableSysMain, DetectState: DetectSysMainOff),
        };

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
                key?.SetValue("", LanguageService.T("Tweak_TakeOwnershipMenuLabel"));
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

        // ΔΙΟΡΘΩΣΗ (γνωστό κενό #01) - τα 2 hardcoded ToggleButton (TweaksView.xaml) δεν περνούν από
        // SimpleTweak/TweakRowVm, οπότε χρειάζονται τη δική τους δημόσια έκθεση κατάστασης.
        public static bool IsTakeOwnershipInstalled() =>
            new[] { @"*\shell\OptimizerTakeOwnership", @"Directory\shell\OptimizerTakeOwnership", @"Drive\shell\OptimizerTakeOwnership" }
                .Any(root => { using var k = Registry.ClassesRoot.OpenSubKey(root); return k != null; });

        public static void InstallOpenPowerShellHere()
        {
            foreach (var root in new[] { @"Directory\shell\OptimizerOpenPS", @"Directory\Background\shell\OptimizerOpenPS" })
            {
                using var key = Registry.ClassesRoot.CreateSubKey(root, writable: true);
                key?.SetValue("", LanguageService.T("Tweak_OpenPsHereMenuLabel"));
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

        public static bool IsOpenPowerShellHereInstalled() =>
            new[] { @"Directory\shell\OptimizerOpenPS", @"Directory\Background\shell\OptimizerOpenPS" }
                .Any(root => { using var k = Registry.ClassesRoot.OpenSubKey(root); return k != null; });

        // ===== Πλήρης επαναφορά (uninstall) =====

        // ΝΕΟ - καλείται ΑΠΟΚΛΕΙΣΤΙΚΑ από τον installer κατά την απεγκατάσταση, μέσω
        // "GearWin.exe --reset-tweaks" (βλ. App.xaml.cs, installer/OptimizerWpf.iss) -
        // ρητό αίτημα χρήστη: "έλεγξε
        // αν υπάρχει πρόβλεψη για... επαναφορά όλων των ρυθμίσεων των windows στα προεπιλεγμένα αν το
        // θέλει ο χρήστης". ΕΠΑΝΑΦΕΡΕΙ ΜΟΝΟ ό,τι η εφαρμογή έχει ΑΠΟΔΕΔΕΙΓΜΕΝΑ αλλάξει:
        //  - Τα RegTweak με πραγματική καταγεγραμμένη προηγούμενη τιμή στο TweakBackupService (μη-null
        //    GetBackup σημαίνει "η εφαρμογή το άλλαξε τουλάχιστον μία φορά, να η αληθινή προηγούμενη τιμή").
        //  - Τα 3 δικά της namespaced δεξιά-κλικ κλειδιά (Optimizer*) - ασφαλές να αφαιρεθούν αν
        //    υπάρχουν, αφού ΚΑΝΕΝΑ άλλο πρόγραμμα δεν τα δημιουργεί.
        //  - Το telemetry hosts-block (state-checkable μέσω IsTelemetryBlocked, όχι εικασία).
        // ΣΚΟΠΙΜΑ ΔΕΝ αγγίζει mouse acceleration / hibernate / ultimate performance / game bar / visual
        // effects - καμία από αυτές τις SimpleTweak δεν έχει καταγεγραμμένη "πριν" τιμή πουθενά (ούτε
        // καν το ίδιο το TweaksView τις παρακολουθεί - κάθε toggle ξεκινάει πάντα ΑΝΕΝΕΡΓΟ σε κάθε
        // άνοιγμα, βλ. TweaksView.xaml.cs's TweakRowVm), οπότε ένα blind "off" εδώ θα ήταν ΜΙΑ ΑΚΟΜΑ
        // εικασία (π.χ. θα ζόριζε mouse acceleration ΟΝ ακόμα κι αν ο χρήστης ποτέ δεν άγγιξε αυτό το
        // tweak μέσω της εφαρμογής) - προτιμότερο να παραλειφθεί παρά να ρισκάρει λάθος επαναφορά.
        public static int RestoreAllTrackedTweaks()
        {
            var reverted = 0;
            foreach (var t in MainTweaks.Concat(AiCopilotTweaks).Concat(PerfRegTweaks))
            {
                if (TweakBackupService.GetBackup(t.BackupKey) == null) continue;
                try { ApplyOff(t); reverted++; }
                catch { }
            }

            try
            {
                using var classicMenuKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}");
                if (classicMenuKey != null) { DisableClassicContextMenu(); reverted++; }
            }
            catch { }

            try
            {
                if (new[] { @"*\shell\OptimizerTakeOwnership", @"Directory\shell\OptimizerTakeOwnership", @"Drive\shell\OptimizerTakeOwnership" }
                    .Any(root => { using var k = Registry.ClassesRoot.OpenSubKey(root); return k != null; }))
                { RemoveTakeOwnership(); reverted++; }
            }
            catch { }

            try
            {
                if (new[] { @"Directory\shell\OptimizerOpenPS", @"Directory\Background\shell\OptimizerOpenPS" }
                    .Any(root => { using var k = Registry.ClassesRoot.OpenSubKey(root); return k != null; }))
                { RemoveOpenPowerShellHere(); reverted++; }
            }
            catch { }

            try { if (NetworkService.IsTelemetryBlocked()) { NetworkService.UnblockTelemetry(); reverted++; } }
            catch { }

            return reverted;
        }

        // ΝΕΟ - roadmap "Health Check" (ρητό αίτημα χρήστη, βλ. PC Manager's "X default settings can
        // be restored") - ΙΔΙΑ ακριβώς λογική ανίχνευσης με το RestoreAllTrackedTweaks, αλλά ΧΩΡΙΣ να
        // εφαρμόζει τίποτα (μόνο μέτρηση, για την κάρτα σύνοψης).
        public static int CountRestorableTweaks()
        {
            var count = 0;
            foreach (var t in MainTweaks.Concat(AiCopilotTweaks).Concat(PerfRegTweaks))
                if (TweakBackupService.GetBackup(t.BackupKey) != null) count++;

            try
            {
                using var classicMenuKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}");
                if (classicMenuKey != null) count++;
            }
            catch { }

            try
            {
                if (new[] { @"*\shell\OptimizerTakeOwnership", @"Directory\shell\OptimizerTakeOwnership", @"Drive\shell\OptimizerTakeOwnership" }
                    .Any(root => { using var k = Registry.ClassesRoot.OpenSubKey(root); return k != null; }))
                    count++;
            }
            catch { }

            try
            {
                if (new[] { @"Directory\shell\OptimizerOpenPS", @"Directory\Background\shell\OptimizerOpenPS" }
                    .Any(root => { using var k = Registry.ClassesRoot.OpenSubKey(root); return k != null; }))
                    count++;
            }
            catch { }

            try { if (NetworkService.IsTelemetryBlocked()) count++; }
            catch { }

            return count;
        }

        // ===== Ελαφρύτερα Windows (roadmap) =====

        // ΝΕΟ - μαζική απενεργοποίηση της άδειας "εκτέλεση στο παρασκήνιο" για ΟΛΕΣ τις εγκατεστημένες
        // εφαρμογές Store/UWP - ο ίδιος μηχανισμός με Ρυθμίσεις > Απόρρητο & Ασφάλεια > Εφαρμογές
        // Παρασκηνίου, απλά μαζικά αντί για μία-μία. Το registry key
        // HKCU\...\BackgroundAccessApplications\<PackageFamilyName> με Disabled/DisabledByUser=1 είναι
        // ο επίσημος, τεκμηριωμένος μηχανισμός (ο ίδιος που χρησιμοποιεί το ίδιο το Windows Settings).
        private const string BackgroundAppsKeyPath = @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications";

        public static void DisableBackgroundApps()
        {
            foreach (var pfn in GetInstalledPackageFamilyNames())
            {
                using var key = Registry.CurrentUser.CreateSubKey($@"{BackgroundAppsKeyPath}\{pfn}", writable: true);
                key?.SetValue("Disabled", 1, RegistryValueKind.DWord);
                key?.SetValue("DisabledByUser", 1, RegistryValueKind.DWord);
            }
        }

        public static void EnableBackgroundApps()
        {
            using var root = Registry.CurrentUser.OpenSubKey(BackgroundAppsKeyPath, writable: true);
            if (root == null) return;
            foreach (var pfn in root.GetSubKeyNames())
            {
                try { root.DeleteSubKeyTree(pfn); } catch { }
            }
        }

        // Φθηνός, ΤΟΠΙΚΟΣ έλεγχος μητρώου (καμία διεργασία PowerShell) - ώστε το άνοιγμα της καρτέλας
        // να μην επιβαρύνεται· "ενεργό" tweak σημαίνει ότι ΟΙ ΠΕΡΙΣΣΟΤΕΡΕΣ ήδη-καταγεγραμμένες εφαρμογές
        // είναι απενεργοποιημένες (η πλήρης απαρίθμηση εγκατεστημένων εφαρμογών μέσω PowerShell γίνεται
        // ΜΟΝΟ όταν ο χρήστης πραγματικά πατήσει τον διακόπτη, βλ. DisableBackgroundApps).
        private static bool? DetectBackgroundAppsOff()
        {
            using var root = Registry.CurrentUser.OpenSubKey(BackgroundAppsKeyPath);
            var names = root?.GetSubKeyNames();
            if (names == null || names.Length == 0) return false;
            var disabledCount = names.Count(n =>
            {
                using var sub = root!.OpenSubKey(n);
                return Convert.ToInt32(sub?.GetValue("Disabled") ?? 0) == 1;
            });
            return disabledCount > names.Length / 2;
        }

        private static IReadOnlyList<string> GetInstalledPackageFamilyNames()
        {
            const string script = "Get-AppxPackage | Where-Object { -not $_.IsFramework -and -not $_.IsResourcePackage } | " +
                                   "Select-Object -ExpandProperty PackageFamilyName";
            var output = RunProcessCapture("powershell.exe", $"-NoProfile -NonInteractive -Command \"{script}\"");
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // ΝΕΟ - SysMain (πρώην Superfetch) - η ίδια η Microsoft συστήνει απενεργοποίηση σε συστήματα με
        // SSD/NVMe (ο prefetch μηχανισμός στοχεύει σε αργούς περιστρεφόμενους δίσκους - σε γρήγορο
        // δίσκο δεν προσφέρει όφελος, μόνο συνεχή χρήση πόρων στο παρασκήνιο). sc.exe αντί για
        // ServiceController (καμία νέα εξάρτηση NuGet) - ίδιο πνεύμα με τα υπόλοιπα RunProcess.
        private const string SysMainServiceKey = @"SYSTEM\CurrentControlSet\Services\SysMain";

        public static void DisableSysMain()
        {
            RunProcess("sc.exe", "stop SysMain");
            RunProcess("sc.exe", "config SysMain start= disabled");
        }

        public static void EnableSysMain()
        {
            RunProcess("sc.exe", "config SysMain start= auto");
            RunProcess("sc.exe", "start SysMain");
        }

        private static bool? DetectSysMainOff()
        {
            using var key = Registry.LocalMachine.OpenSubKey(SysMainServiceKey);
            var start = key?.GetValue("Start");
            if (start == null) return null;
            return Convert.ToInt32(start) == 4; // 4 = Disabled
        }

        // ΝΕΟ - ΠΟΤΕ απευθείας χειρισμός του ευρετηρίου αναζήτησης (πολύπλοκο COM API, ρίσκο χαλασμένου
        // ευρετηρίου) - ίδια φιλοσοφία με το OpenDeviceEncryptionSettings, μόνο άνοιγμα του επίσημου
        // Windows dialog όπου ο χρήστης εξαιρεί μεγάλους φακέλους (πολυμέσα/ανάπτυξη) ο ίδιος.
        public static void OpenIndexingOptions()
        {
            try { Process.Start(new ProcessStartInfo("control.exe", "/name Microsoft.IndexingOptions") { UseShellExecute = true }); }
            catch { }
        }

        // ΝΕΟ - "Προφίλ Χαμηλής Χρήσης Πόρων" (ρητό αίτημα χρήστη) - συγκεντρώνει σε ΕΝΑ κουμπί 3 ήδη
        // υπάρχοντα, μεμονωμένα tweaks (Delivery Optimization, Εφαρμογές Παρασκηνίου, SysMain) αντί να
        // χρειάζεται ο χρήστης να τα βρει/ενεργοποιήσει ένα-ένα.
        public static void ApplyLowResourceProfile()
        {
            ApplyOn(MainTweaks[3]); // Delivery Optimization off (P2P sharing)
            DisableBackgroundApps();
            DisableSysMain();
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
