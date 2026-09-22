using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    public record WindowsFeatureInfo(string FeatureName, string DisplayName, bool Enabled);
    public record GhostDeviceInfo(string InstanceId, string FriendlyName, string Class);
    public record ScheduledTaskInfo(string TaskName, string TaskPath, string State, string Author);

    // Port του Optimizer.ps1's Προηγμένα Εργαλεία καρτέλα (~15001-15621). Βλ. HANDOFF.md §0.4ιβ.
    public static class AdvancedToolsService
    {
        // ===== Συντήρηση & Διάγνωση Συστήματος (10 ενέργειες) =====

        public static void FullMaintenance() => RunPsDetached(
            "Remove-Item \\\"$env:TEMP\\*\\\" -Recurse -Force -ErrorAction SilentlyContinue; sfc /scannow; DISM /Online /Cleanup-Image /RestoreHealth");

        public static bool EnablePrivilege(string privilege) => TokenPrivilege.Enable(privilege);

        public static void NetworkReset() => RunPsDetached("netsh winsock reset; netsh int ip reset; ipconfig /flushdns");

        public static int ClearEventLogs()
        {
            var count = 0;
            try
            {
                using var session = new EventLogSession();
                foreach (var logName in session.GetLogNames().ToList())
                {
                    try { session.ClearLog(logName); count++; } catch { }
                }
            }
            catch { }
            return count;
        }

        public static void CollectGarbage() { GC.Collect(); GC.WaitForPendingFinalizers(); }

        public static string WriteDeepDiagnosticsReport()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Deep_Diagnostics.txt");
            var text = $"OS: {Environment.OSVersion}\nMachine: {Environment.MachineName}\nCPU count: {Environment.ProcessorCount}\n" +
                       $"64-bit OS: {Environment.Is64BitOperatingSystem}\n.NET: {Environment.Version}\nDate: {DateTime.Now}\n";
            File.WriteAllText(path, text);
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
            return path;
        }

        public static void OpenDeviceManager() => Process.Start(new ProcessStartInfo("devmgmt.msc") { UseShellExecute = true });
        public static void OpenWindowsUpdateTroubleshooter() => Process.Start(new ProcessStartInfo("ms-settings:troubleshoot") { UseShellExecute = true });

        public static void CreateSystemReport()
        {
            var path = Path.Combine(Path.GetTempPath(), "OptimizerWpfSystemReport.txt");
            try
            {
                using var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c systeminfo > \"{path}\"") { UseShellExecute = false, CreateNoWindow = true });
                p?.WaitForExit();
            }
            catch { }
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
        }

        public static void ResetWindowsUpdateServices() => RunPsDetached("net stop wuauserv; net stop bits; net start wuauserv; net start bits");

        public static void SetDotNetRollForward() =>
            Environment.SetEnvironmentVariable("DOTNET_ROLL_FORWARD", "LatestMajor", EnvironmentVariableTarget.Machine);

        public static int CleanXboxCredentials()
        {
            var count = 0;
            try
            {
                using var p = Process.Start(new ProcessStartInfo("cmdkey.exe", "/list") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
                var output = p?.StandardOutput.ReadToEnd() ?? "";
                p?.WaitForExit();
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(output, @"(Xbl\S+)"))
                {
                    using var del = Process.Start(new ProcessStartInfo("cmdkey.exe", $"/delete:{m.Groups[1].Value}") { UseShellExecute = false, CreateNoWindow = true });
                    del?.WaitForExit();
                    count++;
                }
            }
            catch { }
            return count;
        }

        public static void InstallGroupPolicyEditor() => RunPsDetached(
            "Get-ChildItem \\\"$env:SystemRoot\\servicing\\Packages\\\" -Filter '*GroupPolicy*' | ForEach-Object { dism /online /norestart /add-package:$($_.FullName) }");

        // ===== Εργαλειοθήκη (launchers) =====

        public static void OpenSnippingTool() => Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true });
        public static void OpenNotepad() => Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true });
        public static void OpenCalculator() => Process.Start(new ProcessStartInfo("calc.exe") { UseShellExecute = true });
        public static void OpenControlPanel() => Process.Start(new ProcessStartInfo("control.exe") { UseShellExecute = true });
        public static void OpenTaskManager() => Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
        public static void OpenRegistryEditor() => Process.Start(new ProcessStartInfo("regedit.exe") { UseShellExecute = true });

        [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, System.UIntPtr dwExtraInfo);
        private const int VK_LWIN = 0x5B, VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_B = 0x42, KEYEVENTF_KEYUP = 0x2;

        // Προσομοίωση Win+Ctrl+Shift+B - επίσημη συντόμευση Windows για GPU driver soft reset.
        public static void GpuDriverSoftReset()
        {
            keybd_event(VK_LWIN, 0, 0, System.UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, 0, System.UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, 0, System.UIntPtr.Zero);
            keybd_event(VK_B, 0, 0, System.UIntPtr.Zero);
            keybd_event(VK_B, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
        }

        // ===== Windows Optional Features =====

        // ΝΕΟ - roadmap "Βοηθός ρύθμισης Autologon" - port του WinUtil's autologon helper. ΣΗΜΕΙΩΣΗ
        // ΕΙΛΙΚΡΙΝΕΙΑΣ: ο μηχανισμός AutoAdminLogon των ίδιων των Windows αποθηκεύει το DefaultPassword
        // σε ΚΑΘΑΡΟ ΚΕΙΜΕΝΟ στο μητρώο (HKLM\...\Winlogon) - γνωστός, τεκμηριωμένος περιορισμός, ΟΧΙ
        // κάτι που η εφαρμογή μπορεί να αποφύγει· το UI δείχνει ρητή προειδοποίηση πριν την εφαρμογή.
        private const string WinlogonKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";

        public static bool IsAutologonEnabled()
        {
            using var key = Registry.LocalMachine.OpenSubKey(WinlogonKeyPath);
            return key?.GetValue("AutoAdminLogon")?.ToString() == "1";
        }

        public static bool EnableAutologon(string username, string password, string? domain)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(WinlogonKeyPath, writable: true);
                if (key == null) return false;
                key.SetValue("AutoAdminLogon", "1", RegistryValueKind.String);
                key.SetValue("DefaultUserName", username, RegistryValueKind.String);
                key.SetValue("DefaultPassword", password, RegistryValueKind.String);
                if (!string.IsNullOrWhiteSpace(domain)) key.SetValue("DefaultDomainName", domain, RegistryValueKind.String);
                return true;
            }
            catch { return false; }
        }

        public static bool DisableAutologon()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(WinlogonKeyPath, writable: true);
                if (key == null) return false;
                key.SetValue("AutoAdminLogon", "0", RegistryValueKind.String);
                key.DeleteValue("DefaultPassword", throwOnMissingValue: false);
                return true;
            }
            catch { return false; }
        }

        public static readonly (string FeatureName, string DisplayName)[] CuratedFeatures =
        {
            ("NetFx3", ".NET Framework 3.5"),
            ("Containers-DisposableClientVM", "Windows Sandbox"),
            ("Microsoft-Hyper-V-All", "Hyper-V"),
            ("Microsoft-Windows-Subsystem-Linux", "WSL"),
            ("TelnetClient", "Telnet Client"),
            ("SMB1Protocol", "SMB 1.0/CIFS (legacy, ανασφαλές)"),
        };

        public static Task<bool> SetFeatureEnabledAsync(string featureName, bool enabled) => RunPsForSuccessAsync(
            enabled
                ? $"Enable-WindowsOptionalFeature -Online -FeatureName {featureName} -All -NoRestart"
                : $"Disable-WindowsOptionalFeature -Online -FeatureName {featureName} -NoRestart");

        // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "η λίστα και η από κάτω λειτουργία που είναι ακριβώς το ίδιο
        // να καταργηθεί, ενοποίησε") - πριν υπήρχαν 2 ξεχωριστές κάρτες (μια curated λίστα 6
        // λειτουργιών + μια "όλες εκτός από αυτές τις 6" λίστα) - ενοποιήθηκαν σε ΜΙΑ πλήρη,
        // αναζητήσιμη λίστα (χωρίς εξαίρεση) - βλ. AdvancedView.xaml.cs's AllFeatureRow.DisplayName
        // που χρησιμοποιεί το CuratedFeatures dictionary για φιλικό όνομα όπου υπάρχει.
        // ΔΙΟΡΘΩΣΗ (bug εντοπίστηκε κατά την ενοποίηση): το Enabled ήταν ΠΑΝΤΑ hardcoded false εδώ -
        // δεν διάβαζε καθόλου το πραγματικό "State" από το PowerShell αποτέλεσμα, οπότε ΚΑΘΕ
        // λειτουργία εμφανιζόταν σαν απενεργοποιημένη ανεξαρτήτως πραγματικής κατάστασης.
        // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "στη σάρωση των πρόσθετων λειτουργιών η εφαρμογή κρασάρει και
        // κλείνει") - το try/catch κάλυπτε ΜΟΝΟ το parsing του JSON, ΟΧΙ την ίδια την εκτέλεση
        // PowerShell (RunPsCaptureAsync) - μια εξαίρεση εκεί (π.χ. αν το DISM/component store έχει
        // πρόβλημα, ή γενικά οποιοδήποτε μη αναμενόμενο σφάλμα) περνούσε ΑΠΡΟΣΤΑΤΕΥΤΗ μέσα από το
        // async void event handler του κουμπιού και κατέρρευε ΟΛΗ την εφαρμογή (WPF προεπιλογή για
        // μη-χειρισμένες εξαιρέσεις σε async void χωρίς global handler - βλ. και App.xaml.cs). Τώρα
        // ΟΛΟΚΛΗΡΗ η μέθοδος προστατεύεται.
        // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "το κουμπί της ανανέωσης της λίστας δεν παράγει κανένα
        // αποτέλεσμα") - επιβεβαιώθηκε ότι το `Get-WindowsOptionalFeature -Online` πετάει
        // COMException("The requested operation requires elevation") όποτε η καλούσα διεργασία δεν
        // είναι πραγματικά elevated - το προηγούμενο catch{} το κατάπινε ΣΙΩΠΗΛΑ και επέστρεφε άδεια
        // λίστα, πανομοιότυπο οπτικά με "καμία λειτουργία δεν βρέθηκε" - ο χρήστης δεν είχε ΚΑΝΕΝΑ
        // τρόπο να ξέρει ότι η σάρωση απέτυχε αντί να επέστρεψε γνήσια 0 αποτελέσματα (αδύνατο στην
        // πράξη - κάθε Windows εγκατάσταση έχει δεκάδες προαιρετικές λειτουργίες). Επιστρέφει τώρα
        // null σε αποτυχία (διακριτό από μια πραγματική άδεια λίστα) ώστε το UI layer να δείξει
        // ρητό μήνυμα σφάλματος αντί να μείνει σιωπηλά κενό.
        // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "η ανανέωση βγάζει ότι δεν τρέχει η εφαρμογή ως Διαχειριστής ΕΝΩ
        // τρέχει") - επιστρέφει τώρα ΚΑΙ το πραγματικό κείμενο σφάλματος (Error) όταν αποτυγχάνει,
        // αντί μόνο για null - το UI layer (AdvancedView) μπορεί έτσι να δείξει την ΠΡΑΓΜΑΤΙΚΗ αιτία
        // αντί για μια σταθερή, πιθανώς λανθασμένη εικασία περί elevation.
        public static async Task<(System.Collections.Generic.IReadOnlyList<WindowsFeatureInfo>? Features, string? Error)> ScanAllFeaturesAsync()
        {
            try
            {
                // Εξαιρεί ΜΟΝΟ το MediaPlayback/WindowsMediaPlayer - έχει τη δική του, ειδική λογική
                // στο Bloatware tab (πραγματική διαφορετική καρτέλα, όχι εσωτερικός διπλότυπος κατάλογος).
                var excluded = new[] { "MediaPlayback", "WindowsMediaPlayer" }.ToHashSet();
                const string script = "Get-WindowsOptionalFeature -Online | Select-Object FeatureName, State | ConvertTo-Json -Compress";
                var (output, error) = await RunPsCaptureWithErrorAsync(script);
                if (string.IsNullOrWhiteSpace(output)) return (null, string.IsNullOrWhiteSpace(error) ? null : error.Trim());

                var root = JsonDocument.Parse(output).RootElement;
                var elements = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : new[] { root }.AsEnumerable();
                var list = elements
                    .Select(e => (Name: e.GetProperty("FeatureName").GetString() ?? "", Enabled: IsFeatureStateEnabled(e)))
                    .Where(x => x.Name != "" && !excluded.Contains(x.Name))
                    .Select(x => new WindowsFeatureInfo(x.Name, x.Name, x.Enabled))
                    .ToList();
                return (list, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        // ΔΙΟΡΘΩΣΗ (χρήστης έστειλε screenshot του πραγματικού σφάλματος - ίδιο έλεγχος αποκάλυψε την
        // ΠΡΑΓΜΑΤΙΚΗ αιτία πίσω από το "τρέξε ως Διαχειριστής" ψευδο-μήνυμα): "The requested operation
        // requires an element of type 'String', but the target element has type 'Number'." - το
        // PowerShell's ConvertTo-Json δεν σειριοποιεί ΠΑΝΤΑ το State (Microsoft.Dism.Commands.
        // FeatureState enum) ως το όνομά του (π.χ. "Enabled") - για ΤΟΥΛΑΧΙΣΤΟΝ μία λειτουργία σε αυτό
        // το σύστημα βγήκε ως ΑΚΕΡΑΙΟΣ αριθμός αντί για string, και το ΤΥΦΛΟ s.GetString() πετούσε
        // JsonException που ΑΚΥΡΩΝΕ ΟΛΟΚΛΗΡΗ τη σάρωση (ΟΛΕΣ οι εκατοντάδες λειτουργίες, όχι μόνο η
        // προβληματική) - ο χρήστης έβλεπε το γενικό "elevation" μήνυμα ΕΝΩ το πραγματικό πρόβλημα δεν
        // είχε ΚΑΜΙΑ σχέση με δικαιώματα. Τώρα ελέγχεται το ValueKind πριν την ανάγνωση - String
        // συγκρίνεται με "Enabled", Number συγκρίνεται με 1 (FeatureState.Enabled) ως καλύτερη δυνατή
        // εκτίμηση αφού δεν υπάρχει επίσημα τεκμηριωμένη αντιστοίχιση αριθμού->ονόματος. Σε κάθε άλλη
        // περίπτωση (απροσδόκητο ValueKind) η λειτουργία δείχνεται συντηρητικά ως ΑΠΕΝΕΡΓΟΠΟΙΗΜΕΝΗ -
        // λάθος προς τα "ασφαλέστερα" (ένα ήδη ενεργό feature που δείχνεται λάθος ως off οδηγεί ΤΟ ΠΟΛΥ
        // σε ένα αχρείαστο extra κλικ "ενεργοποίηση", ενώ το αντίστροφο θα μπορούσε να οδηγήσει τον
        // χρήστη να απενεργοποιήσει κάτι που δεν σκόπευε).
        internal static bool IsFeatureStateEnabled(JsonElement featureElement)
        {
            if (!featureElement.TryGetProperty("State", out var s)) return false;
            return s.ValueKind switch
            {
                JsonValueKind.String => s.GetString() == "Enabled",
                JsonValueKind.Number => s.TryGetInt32(out var n) && n == 1,
                _ => false,
            };
        }

        // ===== Συσκευές-Φαντάσματα =====

        public static async Task<System.Collections.Generic.IReadOnlyList<GhostDeviceInfo>> ScanGhostDevicesAsync()
        {
            const string script = "Get-PnpDevice -PresentOnly:$false | Where-Object { -not $_.Present -and $_.FriendlyName } | " +
                                   "Select-Object InstanceId, FriendlyName, Class | ConvertTo-Json -Compress";
            var output = await RunPsCaptureAsync(script);
            if (string.IsNullOrWhiteSpace(output)) return Array.Empty<GhostDeviceInfo>();
            try
            {
                var root = JsonDocument.Parse(output).RootElement;
                var elements = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : new[] { root }.AsEnumerable();
                return elements.Select(e => new GhostDeviceInfo(
                    e.GetProperty("InstanceId").GetString() ?? "",
                    e.GetProperty("FriendlyName").GetString() ?? "",
                    e.TryGetProperty("Class", out var c) ? c.GetString() ?? "" : "")).ToList();
            }
            catch (JsonException) { return Array.Empty<GhostDeviceInfo>(); }
        }

        // ΔΙΟΡΘΩΣΗ (βλ. HANDOFF.md §0.4ιβ - ο ps1 original ΔΕΝ έχει confirm dialog εδώ, το πιο
        // επικίνδυνο, ανεπιβεβαίωτο σημείο όλης της καρτέλας) - το confirm dialog είναι στο UI layer.
        public static Task<bool> RemoveGhostDeviceAsync(string instanceId) =>
            RunPsForSuccessAsync($"Remove-PnpDevice -InstanceId '{instanceId}' -Confirm:$false -ErrorAction Stop");

        // ===== Χρονοδιάγραμμα Εκκίνησης (roadmap "Χρονοδιάγραμμα εκκίνησης") =====
        // Event ID 100 στο "Microsoft-Windows-Diagnostics-Performance/Operational" log είναι το
        // επίσημο, τεκμηριωμένο Windows boot-performance event (το ΙΔΙΟ που τροφοδοτεί το Reliability
        // Monitor) - BootTime σε ms μέσα στο EventData. ΣΚΟΠΙΜΑ ΧΩΡΙΣ πλήρες οπτικό Gantt-timeline ανά
        // εφαρμογή εκκίνησης (δεν υπάρχει αξιόπιστη, τεκμηριωμένη ανά-εφαρμογή στιγμή εκκίνησης σε αυτό
        // το event - θα ήταν εικασία) - δείχνει ΜΟΝΟ πραγματικά δεδομένα: συνολικός χρόνος εκκίνησης
        // ανά εκκίνηση, τελευταίες N φορές, ώστε να φαίνεται τάση (πιο αργή εκκίνηση με τον καιρό).
        public record BootTimeEntry(DateTime When, double Seconds);

        public static System.Collections.Generic.IReadOnlyList<BootTimeEntry> GetRecentBootTimes(int max = 10)
        {
            var results = new System.Collections.Generic.List<BootTimeEntry>();
            try
            {
                var query = new EventLogQuery("Microsoft-Windows-Diagnostics-Performance/Operational", PathType.LogName, "*[System[(EventID=100)]]");
                using var reader = new EventLogReader(query);
                for (var e = reader.ReadEvent(); e != null && results.Count < max; e = reader.ReadEvent())
                {
                    try
                    {
                        var xml = System.Xml.Linq.XDocument.Parse(e.ToXml());
                        var ns = xml.Root!.GetDefaultNamespace();
                        var bootMs = xml.Descendants(ns + "Data").FirstOrDefault(d => (string?)d.Attribute("Name") == "BootTime")?.Value;
                        if (bootMs != null && double.TryParse(bootMs, out var ms) && e.TimeCreated.HasValue)
                            results.Add(new BootTimeEntry(e.TimeCreated.Value, Math.Round(ms / 1000.0, 1)));
                    }
                    catch { }
                    finally { e.Dispose(); }
                }
            }
            catch { /* Χρειάζεται elevation (η εφαρμογή τρέχει ήδη elevated) ή το log μπορεί να μην υπάρχει σε πολύ παλιά Windows - honest empty αντί για crash. */ }
            return results;
        }

        // ===== Προγραμματισμένη Συντήρηση (roadmap) =====
        // Εβδομαδιαία, μέσω μιας πραγματικής εργασίας του Windows Task Scheduler (ΟΧΙ ένα background
        // timer μέσα στην ίδια την εφαρμογή - θα χρειαζόταν η εφαρμογή να τρέχει συνέχεια, αντίθετο με
        // τη στόχευση "Ελαφρύτερη εφαρμογή" αλλού σε αυτό το project) - καλεί το ίδιο exe με
        // "--auto-maintenance" (βλ. App.xaml.cs), /rl highest ώστε να τρέχει elevated χωρίς προτροπή
        // UAC (ο ίδιος μηχανισμός που χρησιμοποιεί ήδη η ίδια η εφαρμογή μέσω app.manifest όταν την
        // ανοίγει ο χρήστης).
        private const string ScheduledMaintenanceTaskName = "OptimizerWpf_WeeklyMaintenance";

        public static async Task<bool> SetScheduledMaintenanceAsync(bool enabled)
        {
            // Απευθείας κλήση του schtasks.exe (ΟΧΙ μέσω PowerShell -Command) - το ήδη υπάρχον
            // RunPsForSuccessAsync εφαρμόζει ένα γενικό .Replace("\"","\\\"") σε ΟΛΗ την εντολή, που θα
            // σπάσει εδώ (πολλαπλά, εμφωλευμένα ζεύγη εισαγωγικών στο /TN και /TR) - το ProcessStartInfo.
            // ArgumentList παρακάτω περνάει κάθε όρισμα ως ξεχωριστό πίνακα, χωρίς κανένα quoting/escaping.
            string fileName; string[] args;
            if (!enabled)
            {
                fileName = "schtasks.exe";
                args = new[] { "/Delete", "/TN", ScheduledMaintenanceTaskName, "/F" };
            }
            else
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(exePath)) return false;
                fileName = "schtasks.exe";
                args = new[] { "/Create", "/TN", ScheduledMaintenanceTaskName, "/TR", $"\"{exePath}\" --auto-maintenance",
                    "/SC", "WEEKLY", "/D", "SUN", "/ST", "03:00", "/RL", "HIGHEST", "/F" };
            }

            var psi = new ProcessStartInfo(fileName) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var process = Process.Start(psi);
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }

        // Καλείται ΜΟΝΟ από το App.xaml.cs όταν η εφαρμογή ξεκινά με --auto-maintenance (headless, καμία
        // οθόνη) - ίδια λογική με το "Διόρθωση Όλων" κουμπί της Αρχικής (ΜΟΝΟ ο ασφαλής, αναστρέψιμος
        // καθαρισμός temp - ΠΟΤΕ αυτόματο SFC/DISM/registry σε μια απρόσεκτη, χωρίς επίβλεψη εκτέλεση),
        // με απλό αρχείο καταγραφής αφού δεν υπάρχει UI να δείξει τι έγινε.
        public static void RunScheduledMaintenance()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine($"=== OptimizerWpf scheduled maintenance - {DateTime.Now:yyyy-MM-dd HH:mm} ===");
            try
            {
                var result = HealthScoreService.Compute();
                log.AppendLine($"Health score before: {result.Score}");
                var cleanedTemp = false;
                foreach (var issue in result.Issues)
                {
                    if (issue.FixType != "Storage") continue;
                    var temp = Environment.GetEnvironmentVariable("TEMP");
                    if (string.IsNullOrEmpty(temp)) continue;
                    foreach (var f in Directory.EnumerateFileSystemEntries(temp))
                    {
                        try { if (Directory.Exists(f)) Directory.Delete(f, true); else File.Delete(f); } catch { }
                    }
                    cleanedTemp = true;
                }
                log.AppendLine(cleanedTemp ? "Temp files cleaned." : "No storage issue found - nothing to clean.");
            }
            catch (Exception ex) { log.AppendLine($"Error: {ex.Message}"); }

            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "ScheduledMaintenanceLog.txt"), log + "\n");
            }
            catch { }
        }

        // ===== Προγραμματισμένες Εργασίες (roadmap "Προβολή Προγραμματισμένων Εργασιών") =====
        // ΣΚΟΠΙΜΑ φιλτράρει ΕΞΩ τις εργασίες κάτω από \Microsoft\Windows\ (εκατοντάδες, όλες επίσημες
        // εργασίες των ίδιων των Windows - θόρυβος, όχι το "γνωστό σημείο επιμονής ανεπιθύμητου
        // λογισμικού" που περιγράφει το roadmap item). Δείχνει μόνο τις εργασίες τρίτων (root \ και
        // custom φακέλους) - εκεί βρίσκονται σχεδόν πάντα οι ύποπτες/ανεπιθύμητες καταχωρήσεις.
        public static async Task<System.Collections.Generic.IReadOnlyList<ScheduledTaskInfo>> ScanScheduledTasksAsync()
        {
            const string script = "Get-ScheduledTask | Where-Object { $_.TaskPath -notlike '\\Microsoft\\Windows\\*' } | " +
                                   "Select-Object TaskName, TaskPath, State, @{N='Author';E={$_.Principal.UserId}} | ConvertTo-Json -Compress";
            var output = await RunPsCaptureAsync(script);
            if (string.IsNullOrWhiteSpace(output)) return Array.Empty<ScheduledTaskInfo>();
            try
            {
                var root = JsonDocument.Parse(output).RootElement;
                var elements = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : new[] { root }.AsEnumerable();
                return elements.Select(e => new ScheduledTaskInfo(
                    e.GetProperty("TaskName").GetString() ?? "",
                    e.GetProperty("TaskPath").GetString() ?? "\\",
                    e.TryGetProperty("State", out var s) ? TaskStateToString(s) : "",
                    e.TryGetProperty("Author", out var a) ? a.GetString() ?? "" : "")).ToList();
            }
            // ΔΙΟΡΘΩΣΗ - βλ. σχόλιο στο TaskStateToString: InvalidOperationException (GetString() σε
            // Number element) ΔΕΝ είναι JsonException - χρειάζεται ρητά εδώ ώστε ΟΠΟΙΑΔΗΠΟΤΕ παρόμοια
            // απρόσμενη μορφή τιμής (όχι μόνο State) να επιστρέφει honest άδεια λίστα αντί να ρίξει την
            // εφαρμογή σε μη διαχειρίσιμη εξαίρεση.
            catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return Array.Empty<ScheduledTaskInfo>(); }
        }

        // ΔΙΟΡΘΩΣΗ - ίδια ρίζα με το IsFeatureStateEnabled παραπάνω: το PowerShell's ConvertTo-Json
        // δεν σειριοποιεί πάντα το TaskState enum ως string - ζωντανά επιβεβαιωμένο ότι "The requested
        // operation requires an element of type 'String', but the target element has type 'Number'"
        // εμφανιζόταν εδώ (μη διαχειρίσιμο InvalidOperationException, ΟΧΙ JsonException - το catch
        // παραπάνω δεν το έπιανε), σκάζοντας ΟΛΟΚΛΗΡΗ τη σάρωση Προγραμματισμένων Εργασιών. Οι αριθμητικές
        // τιμές του TaskState enum (Microsoft.PowerShell.ScheduledTasks) είναι τεκμηριωμένες: 0=Unknown,
        // 1=Disabled, 2=Queued, 3=Ready, 4=Running.
        private static string TaskStateToString(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.String => el.GetString() ?? "",
            JsonValueKind.Number when el.TryGetInt32(out var n) => n switch
            {
                0 => "Unknown", 1 => "Disabled", 2 => "Queued", 3 => "Ready", 4 => "Running", _ => n.ToString(),
            },
            _ => "",
        };

        public static Task<bool> SetScheduledTaskEnabledAsync(string taskName, string taskPath, bool enabled) =>
            RunPsForSuccessAsync($"{(enabled ? "Enable" : "Disable")}-ScheduledTask -TaskName '{taskName.Replace("'", "''")}' -TaskPath '{taskPath.Replace("'", "''")}'");

        // ===== helpers =====

        private static void RunPsDetached(string command)
        {
            try { Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"{command}\"") { UseShellExecute = false, CreateNoWindow = true }); }
            catch { }
        }

        private static async Task<string> RunPsCaptureAsync(string command)
        {
            using var process = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = System.Text.Encoding.UTF8 });
            if (process == null) return "";
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }

        // ΝΕΟ - χρήστης ανέφερε: "η ανανέωση της λίστας προσθέτων λειτουργιών βγάζει ότι δεν τρέχει η
        // εφαρμογή ως Διαχειριστής ΕΝΩ τρέχει". Το ΠΑΡΑΠΑΝΩ RunPsCaptureAsync διαβάζει ΜΟΝΟ
        // StandardOutput - όποτε το PowerShell γράφει ένα σφάλμα στο StandardError (π.χ. πραγματικό
        // πρόβλημα με το DISM/component store, ΟΧΙ πραγματικά elevation), το stdout μένει κενό, το
        // JSON parsing αποτυγχάνει, και το UI έδειχνε ΠΑΝΤΑ το ίδιο ΕΙΚΑΣΤΙΚΟ μήνυμα "τρέξε ως
        // Διαχειριστής" - ΛΑΘΟΣ διάγνωση όταν η εφαρμογή ΕΙΝΑΙ ήδη elevated. Αυτή η εκδοχή διαβάζει
        // ΚΑΙ το StandardError, ώστε το ScanAllFeaturesAsync να μπορεί να δείξει το ΠΡΑΓΜΑΤΙΚΟ μήνυμα
        // σφάλματος αντί για μια σταθερή εικασία.
        // ΔΙΟΡΘΩΣΗ #2 (εντοπίστηκε ζωντανά κατά τον έλεγχο της παραπάνω διόρθωσης): το πραγματικό
        // κείμενο σφάλματος εμφανιζόταν ΑΛΛΟΙΩΜΕΝΟ (τυχαίοι χαρακτήρες ◆) αντί για αναγνώσιμο - το
        // Windows PowerShell 5.1 γράφει το StandardError/Output στην ΚΩΔΙΚΟΣΕΛΙΔΑ κονσόλας (OEM/ANSI)
        // από προεπιλογή, ΟΧΙ σε UTF-8, όποτε γίνεται redirect - το .NET πλευρά διάβαζε τα ΙΔΙΑ bytes
        // σαν UTF-8 (StandardErrorEncoding=UTF8 παρακάτω), παράγοντας mojibake για ΚΑΘΕ μη-ASCII
        // χαρακτήρα (π.χ. γερμανικά umlauts στο μήνυμα του DISM). Λύση: το ίδιο το PowerShell script
        // θέτει ρητά [Console]::OutputEncoding=UTF8 ΠΡΙΝ εκτελέσει την πραγματική εντολή, ώστε τα
        // bytes που γράφει να ταιριάζουν ΠΡΑΓΜΑΤΙΚΑ με αυτό που περιμένει να διαβάσει η πλευρά .NET.
        private static async Task<(string Output, string Error)> RunPsCaptureWithErrorAsync(string command)
        {
            command = "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " + command;
            using var process = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
            });
            if (process == null) return ("", "");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync();
            return (outputTask.Result, errorTask.Result);
        }

        private static async Task<bool> RunPsForSuccessAsync(string command)
        {
            using var process = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"")
            { UseShellExecute = false, CreateNoWindow = true });
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
    }

    // Ελάχιστο P/Invoke wrapper για AdjustTokenPrivileges - port του .NET custom interop type που
    // χρησιμοποιεί το ps1 original (NativeSysInterop.TokenManipulator).
    internal static class TokenPrivilege
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES { public LUID Luid; public uint Attributes; }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES { public uint PrivilegeCount; public LUID_AND_ATTRIBUTES Privilege; }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr h, uint access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool LookupPrivilegeValue(string? host, string name, out LUID luid);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES newState, uint bufLen, IntPtr prev, IntPtr ret);

        private const uint TOKEN_ADJUST_PRIVILEGES = 0x20, TOKEN_QUERY = 0x8, SE_PRIVILEGE_ENABLED = 0x2;

        public static bool Enable(string privilege)
        {
            try
            {
                if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var token)) return false;
                if (!LookupPrivilegeValue(null, privilege, out var luid)) return false;
                var tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Privilege = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED } };
                return AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            catch { return false; }
        }
    }
}
