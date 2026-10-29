using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public record WingetUpdate(string Name, string Id, string CurrentVersion, string AvailableVersion, string Source);

    // Πλήρης C# port του Optimizer.ps1's Complete-WingetListLoad/Get-UpgradeCommandForSource
    // (~11112-11760): πέρα από το βασικό "winget upgrade", σαρώνει ΚΑΙ το επίσημο Microsoft Store CLI
    // ("store updates") ΚΑΙ 10 επιπλέον package managers (pip/npm/pnpm/chocolatey/scoop/gem/cargo/
    // dotnet/psmodule/composer) - κάθε provider ανεξάρτητος (best-effort, ένας που λείπει/αποτυγχάνει
    // δεν εμποδίζει τους υπόλοιπους), με συγχώνευση/αφαίρεση διπλότυπων ίδια με το ps1 original.
    public static class WingetService
    {
        public static async Task<IReadOnlyList<WingetUpdate>> ScanAsync()
        {
            var results = new List<WingetUpdate>();
            var seenIds = new HashSet<string>();

            // Windows Update (μηχανισμός WMT 6.7, βλ. WindowsUpdateService): ανεξάρτητο από το winget - πριν, οι ενημερώσεις
            // Windows δεν εμφανίζονταν ΠΟΥΘΕΝΑ στη λίστα (μόνο ένας αριθμός στην Αρχική από το background check).
            foreach (var wu in await ScanWindowsUpdatesAsync())
                if (seenIds.Add($"windowsupdate:{wu.Id}")) results.Add(wu);

            var (wingetOutput, _, wingetStarted) = await RunToolAsync("winget.exe",
                "upgrade --include-unknown --accept-source-agreements --disable-interactivity");
            if (!wingetStarted)
            {
                // Ίδιο με το Get-Command winget.exe έλεγχο του ps1 - χωρίς winget, δεν έχει νόημα να
                // προσπαθήσουμε τίποτα άλλο (τα άλλα 10 providers είναι ανεξάρτητα του winget, αλλά
                // αυτό εδώ διατηρεί το ίδιο honest μήνυμα "δεν βρέθηκε" ως το πρωτεύον αποτέλεσμα).
                return results;
            }

            foreach (var u in ParseWingetTable(wingetOutput))
                if (seenIds.Add(u.Id)) results.Add(u);

            // Microsoft Store CLI ("store updates") - αντικατέστησε το παλιό "winget upgrade --source
            // msstore" στο ps1 (v1.9.6): τεκμηριωμένα πιο αξιόπιστο, το ΙΔΙΟ εργαλείο που χρησιμοποιεί
            // το Store εσωτερικά.
            foreach (var u in await ScanMsStoreAsync())
            {
                if (seenIds.Add($"{u.Source}:{u.Id}")) results.Add(u);
            }

            // 10 επιπλέον package managers - καθένας best-effort, ανεξάρτητος.
            var otherScans = new (string Source, Func<Task<List<WingetUpdate>>>)[]
            {
                ("pip", ScanPipAsync),
                ("npm", ScanNpmAsync),
                ("pnpm", ScanPnpmAsync),
                ("chocolatey", ScanChocolateyAsync),
                ("scoop", ScanScoopAsync),
                ("gem", ScanGemAsync),
                ("cargo", ScanCargoAsync),
                ("dotnet", ScanDotnetToolsAsync),
                ("psmodule", ScanPsModuleAsync),
                ("composer", ScanComposerAsync),
            };
            foreach (var (_, scan) in otherScans)
            {
                List<WingetUpdate> found;
                try { found = await scan(); } catch { continue; }
                foreach (var u in found)
                {
                    if (seenIds.Add($"{u.Source}:{u.Id}")) results.Add(u);
                }
            }

            // 6.9.0: repos git στους φακέλους που δήλωσε ο χρήστης (μόνο fast-forward pull στην ενημέρωση)
            try
            {
                var roots = AppSettingsService.Current.GitRepoRoots;
                if (roots.Count > 0)
                    foreach (var u in await GitRepoUpdateService.ScanAsync(roots))
                        if (seenIds.Add($"git:{u.Id}")) results.Add(u);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("git repo scan failed: " + ex.Message); }

            return results;
        }

        private static async Task<List<WingetUpdate>> ScanWindowsUpdatesAsync()
        {
            var list = new List<WingetUpdate>();
            try
            {
                var scan = await WindowsUpdateService.ScanDetailedAsync();
                if (!scan.Success) return list;
                foreach (var u in scan.Updates)
                {
                    var name = u.IsOptional ? $"[{LanguageService.T("WU_Optional")}] {u.Title}" : u.Title;
                    if (!string.IsNullOrEmpty(u.KbArticleId) && !name.Contains(u.KbArticleId, StringComparison.OrdinalIgnoreCase)) name += $" ({u.KbArticleId})";
                    var kind = u.IsOptional ? LanguageService.T("WU_Optional")
                        : u.Severity != null ? $"{LanguageService.T("WU_Security")}: {u.Severity}"
                        : LanguageService.T("WU_Important");
                    list.Add(new WingetUpdate(name, u.RowId, string.IsNullOrEmpty(u.Category) ? "Windows Update" : u.Category.Split(',')[0].Trim(), kind, "windowsupdate"));
                }
            }
            catch { /* best-effort όπως οι υπόλοιποι πάροχοι */ }
            return list;
        }

        // Port του $global:btnCheckMsStore του ps1 (~11240): το "store updates" CLI ΔΕΝ πιάνει πάντα
        // ΟΛΕΣ τις εκκρεμείς ενημερώσεις Metro/UWP εφαρμογών (επιβεβαιωμένο, τεκμηριωμένο όριο -
        // παραμένει "Preview" tool). Αυτό ενεργοποιεί ΠΡΑΓΜΑΤΙΚΗ σάρωση μέσω του επίσημου
        // MDM_EnterpriseModernAppManagement_AppManagement01 CIM provider (το ΙΔΙΟ μηχανισμό που
        // χρησιμοποιεί το ίδιο το Store όταν ο χρήστης πατά "Check for updates" εκεί) και ανοίγει τη
        // σελίδα Downloads & Updates του Store για επιβεβαίωση/εγκατάσταση - η σάρωση ΔΕΝ επιστρέφει
        // λίστα (μόνο ενεργοποιεί σάρωση στο παρασκήνιο), τα αποτελέσματα εμφανίζονται ΜΕΣΑ στο Store.
        public static void TriggerMsStoreUpdateScanAndOpen()
        {
            TriggerMsStoreUpdateScan();
            try
            {
                Process.Start(new ProcessStartInfo("ms-windows-store://downloadsandupdates") { UseShellExecute = true });
            }
            catch { }
        }

        // Μόνο το MDM UpdateScanMethod (Invoke-WmtStoreAppUpdateScan του WMT) - χωρίς άνοιγμα Store.
        public static void TriggerMsStoreUpdateScan()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\cimv2\mdm\dmmap",
                    "SELECT * FROM MDM_EnterpriseModernAppManagement_AppManagement01");
                foreach (ManagementObject obj in searcher.Get())
                {
                    obj.InvokeMethod("UpdateScanMethod", null);
                    break;
                }
            }
            catch
            {
                // Ίδια ανοχή με το ps1 original - αν ο MDM provider δεν είναι διαθέσιμος σε αυτό το
                // build Windows, απλά παραλείπεται το ενεργό trigger.
            }
        }

        // Ορίζεται όταν μια ενημέρωση Windows ολοκληρώθηκε αλλά απαιτεί επανεκκίνηση - το UI το δείχνει στη σύνοψη.
        public static bool RestartRequiredAfterUpdates { get; set; }

        // Για την προγραμματισμένη νυχτερινή ενημέρωση (AutoUpdateService): μόνο πηγές winget, σιωπηλά, χωρίς αλληλεπίδραση. Το winget
        // σέβεται τα pins (winget pin) και παραλείπει πακέτα χωρίς γνωστή έκδοση (χωρίς --include-unknown) - ασφαλές για μη επιβλεπόμενη εκτέλεση.
        public static async Task<(bool Started, int ExitCode, string Output)> UpgradeAllSilentAsync()
        {
            var (output, exitCode, started) = await RunToolAsync("winget.exe",
                "upgrade --all --silent --accept-source-agreements --accept-package-agreements --disable-interactivity", TimeSpan.FromMinutes(90));
            return (started, exitCode, output);
        }

        public static async Task<bool> UpgradeAsync(string id, string source) => (await UpgradeDetailedAsync(id, source)).Ok;

        // Όπως το WMT 6.7 (Winget failure logs / ErrorCodes): επιστρέφει ΚΑΙ τον πραγματικό λόγο αποτυχίας
        // από τον exit code (HRESULT του winget), ώστε το UI να λέει π.χ. "άλλη τεχνολογία εγκατάστασης"
        // αντί για μια αόριστη αποτυχία.
        public static async Task<(bool Ok, string? Reason)> UpgradeDetailedAsync(string id, string source)
        {
            if (!IsSafePackageId(id, source)) return (false, "Unsafe package id rejected");
            if (source == "windowsupdate")
            {
                var (ok, reboot, error) = await WindowsUpdateService.InstallAsync(id);
                if (ok && reboot) RestartRequiredAfterUpdates = true;
                return (ok, ok ? null : error);
            }
            if (source == GitRepoUpdateService.Source) return await GitRepoUpdateService.PullAsync(id);
            var (fileName, arguments) = GetUpgradeCommandForSource(source, id);
            var (_, exitCode, started) = await RunToolAsync(fileName, arguments, TimeSpan.FromMinutes(5));
            if (!started) return (false, null);
            return exitCode == 0 ? (true, null) : (false, DescribeExitCode(exitCode));
        }

        // ΑΣΦΑΛΕΙΑ: τα ids των npm/pnpm/scoop/gem/composer/pip/cargo/dotnet/choco προέρχονται από την ΕΞΟΔΟ εξωτερικών
        // εργαλείων (όνομα πακέτου που ελέγχει ο συγγραφέας του πακέτου) και μπαίνουν σε γραμμή εντολών μέσω
        // `cmd.exe /c ...` ΜΕ ΥΨΗΛΑ ΔΙΚΑΙΩΜΑΤΑ - ένα id όπως `x" & calc & "` θα εκτελούσε αυθαίρετο κώδικα ως Administrator.
        // Άρα: αυστηρό whitelist (γράμματα, ψηφία, . _ - @ / + ~) για κάθε πηγή που περνά από cmd. Για winget/msstore
        // (κλήση χωρίς shell, ids όπως "ARP\Machine\X64\{GUID}" ή με κενά) αρκεί να μην υπάρχουν εισαγωγικά/χαρακτήρες ελέγχου.
        public static bool IsSafePackageId(string? id, string source)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 200) return false;
            if (source == "windowsupdate") // "UpdateID|Revision" όπως το επιστρέφει το WUA
                return System.Text.RegularExpressions.Regex.IsMatch(id, @"^[0-9a-fA-F-]{36}\|\d{1,6}$");
            if (source == GitRepoUpdateService.Source) return GitRepoUpdateService.IsSafeRepoPath(id);
            if (source is "winget" or "msstore")
                return !id.Any(c => c == '"' || char.IsControl(c));
            return System.Text.RegularExpressions.Regex.IsMatch(id, @"^[A-Za-z0-9._@/+~\-]+$");
        }

        // Υποσύνολο του $ErrorCodes του WMT 6.7 (winget AppInstallerErrors.h + συνήθεις MSI/Windows κωδικοί).
        public static string? DescribeExitCode(int exitCode)
        {
            var hex = "0x" + unchecked((uint)exitCode).ToString("x");
            return hex switch
            {
                "0x8a150001" => "WinGet internal error",
                "0x8a150002" => "Invalid WinGet arguments",
                "0x8a150003" => "WinGet command failed",
                "0x8a150004" => "Opening manifest failed",
                "0x8a150005" => "Cancelled",
                "0x8a150006" => "Running installer failed",
                "0x8a150008" => "Downloading installer failed",
                "0x8a15000b" => "WinGet source information is corrupt",
                "0x8a150010" => "No applicable installer for this system",
                "0x8a150011" => "Installer hash does not match the manifest",
                "0x8a150014" => "No packages found",
                "0x8a15008e" => "Different install technology than the current installation (uninstall and reinstall the app)",
                "0x80070005" => "Access denied",
                "0x80072ee7" => "DNS lookup failed",
                "0x80072f8f" => "SSL certificate error",
                "0x80073d02" => "Resources currently in use (close the app and retry)",
                _ => exitCode switch
                {
                    1602 => "Cancelled by user",
                    1603 => "Fatal MSI error",
                    1618 => "Another installation is in progress",
                    3010 => "Reboot required",
                    _ => null,
                },
            };
        }

        // ΝΕΟ - roadmap "νεότερες δυνατότητες winget στο UI" (pin/export/import). Το "winget pin"
        // αποτρέπει το ΙΔΙΟ το winget από το να προτείνει/εφαρμόζει μελλοντικές αναβαθμίσεις για ένα
        // συγκεκριμένο πακέτο (χρήσιμο για εφαρμογές που ο χρήστης θέλει σκόπιμα σε παλαιότερη
        // έκδοση, π.χ. συμβατότητα). Λειτουργεί ΜΟΝΟ για πακέτα από την πηγή winget (--id).
        public static async Task<bool> PinAsync(string id)
        {
            var (_, exitCode, started) = await RunToolAsync("winget.exe", $"pin add --id \"{id}\" --accept-source-agreements", TimeSpan.FromSeconds(30));
            return started && exitCode == 0;
        }

        // "winget export" γράφει ΟΛΑ τα εγκατεστημένα πακέτα winget (ID + έκδοση + πηγή) σε ένα JSON -
        // μαζί με το ImportAsync παρακάτω, επιτρέπει "αντιγραφή λίστας εφαρμογών" σε νέο υπολογιστή,
        // ίδιο πνεύμα με το ήδη υπάρχον Named Profiles/tweak export αλλά για ΕΓΚΑΤΕΣΤΗΜΕΝΕΣ εφαρμογές
        // αντί για ρυθμίσεις.
        public static async Task<bool> ExportAsync(string filePath)
        {
            var (_, exitCode, started) = await RunToolAsync("winget.exe", $"export -o \"{filePath}\" --accept-source-agreements", TimeSpan.FromSeconds(60));
            return started && exitCode == 0;
        }

        // "winget import" εγκαθιστά/επαναφέρει όλα τα πακέτα ενός τέτοιου αρχείου - πολλαπλές
        // εγκαταστάσεις στη σειρά, οπότε ΠΟΛΥ μεγαλύτερο timeout από τα υπόλοιπα εργαλεία εδώ.
        public static async Task<bool> ImportAsync(string filePath)
        {
            var (_, exitCode, started) = await RunToolAsync("winget.exe",
                $"import -i \"{filePath}\" --accept-package-agreements --accept-source-agreements --ignore-versions", TimeSpan.FromMinutes(20));
            return started && exitCode == 0;
        }

        // Ίδια αντιστοίχιση με το Get-UpgradeCommandForSource του ps1 - τα script-wrapper εργαλεία
        // (npm/pnpm/scoop/gem/composer) περνάνε από cmd.exe /c, ΟΧΙ απευθείας exe (Process.Start με
        // UseShellExecute=false δεν ψάχνει .cmd/.ps1 wrappers στο PATH όπως κάνει ένα πραγματικό shell).
        // 6.8.0: απεγκατάσταση πακέτου από τη λίστα (winget, pip, npm/pnpm global, choco, scoop, gem, cargo, dotnet tool). null = δεν υποστηρίζεται
        // (π.χ. Windows Update, Microsoft Store). Ο id ελέγχεται ΠΡΙΝ με IsSafePackageId (ίδιο whitelist με τις αναβαθμίσεις).
        public static (string FileName, string Arguments)? GetUninstallCommandForSource(string source, string id) => source switch
        {
            "winget" => ("winget.exe", $"uninstall --id \"{id}\" --exact --silent --accept-source-agreements --disable-interactivity"),
            "pip" => ("python.exe", $"-m pip --disable-pip-version-check --no-input uninstall -y \"{id}\""),
            "npm (global)" => ("cmd.exe", $"/c npm uninstall -g \"{id}\""),
            "pnpm (global)" => ("cmd.exe", $"/c pnpm remove -g \"{id}\""),
            "chocolatey" => ("choco.exe", $"uninstall \"{id}\" -y"),
            "scoop" => ("cmd.exe", $"/c scoop uninstall \"{id}\""),
            "gem" => ("cmd.exe", $"/c gem uninstall \"{id}\" -x -a"),
            "cargo" => ("cargo.exe", $"uninstall \"{id}\""),
            "dotnet" => ("dotnet.exe", $"tool uninstall --global \"{id}\""),
            _ => null,
        };

        // 6.9.0: εγκατάσταση του SDI core (Snappy Driver Installer Origin) για τον ενημερωτή οδηγών - σταθερό id, ποτέ από είσοδο χρήστη.
        public const string SdioPackageId = "GlennDelahoy.SnappyDriverInstallerOrigin";

        public static async Task<(bool Ok, string? Reason)> InstallSdioAsync()
        {
            var (_, exitCode, started) = await RunToolAsync("winget.exe",
                "install --id " + SdioPackageId + " --exact --silent --accept-source-agreements --accept-package-agreements --disable-interactivity", TimeSpan.FromMinutes(10));
            if (!started) return (false, null);
            return exitCode == 0 ? (true, null) : (false, DescribeExitCode(exitCode));
        }

        public static async Task<(bool Ok, string? Reason)> UninstallAsync(string id, string source)
        {
            if (!IsSafePackageId(id, source)) return (false, "Unsafe package id rejected");
            var cmd = GetUninstallCommandForSource(source, id);
            if (cmd == null) return (false, null);
            var (_, exitCode, started) = await RunToolAsync(cmd.Value.FileName, cmd.Value.Arguments, TimeSpan.FromMinutes(5));
            if (!started) return (false, null);
            return exitCode == 0 ? (true, null) : (false, DescribeExitCode(exitCode));
        }

        private static (string FileName, string Arguments) GetUpgradeCommandForSource(string source, string id) => source switch
        {
            "winget" => ("winget.exe", $"upgrade --id \"{id}\" --exact --silent --accept-source-agreements --accept-package-agreements --disable-interactivity"),
            "msstore" => ("cmd.exe", "/c start ms-windows-store://downloadsandupdates"),
            "pip" => ("python.exe", $"-m pip --disable-pip-version-check --no-input install --upgrade \"{id}\""),
            "npm (global)" => ("cmd.exe", $"/c npm install -g \"{id}@latest\""),
            "npm" => ("cmd.exe", $"/c npm update \"{id}\""),
            "pnpm (global)" => ("cmd.exe", $"/c pnpm update -g \"{id}\""),
            "pnpm" => ("cmd.exe", $"/c pnpm update \"{id}\""),
            "chocolatey" => ("choco.exe", $"upgrade \"{id}\" -y"),
            "scoop" => ("cmd.exe", $"/c scoop update \"{id}\""),
            "gem" => ("cmd.exe", $"/c gem update \"{id}\""),
            "cargo" => ("cargo.exe", $"install \"{id}\" --force"),
            "dotnet" => ("dotnet.exe", $"tool update --global \"{id}\""),
            "composer" => ("cmd.exe", $"/c composer global update \"{id}\""),
            _ => ("winget.exe", $"upgrade --id \"{id}\" --exact --silent --accept-source-agreements --accept-package-agreements --disable-interactivity"),
        };

        // ===== Microsoft Store CLI ("store updates") =====
        private static async Task<List<WingetUpdate>> ScanMsStoreAsync()
        {
            var list = new List<WingetUpdate>();
            var psi = new ProcessStartInfo
            {
                FileName = "store.exe",
                Arguments = "updates",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };

            Process? process;
            try { process = Process.Start(psi); }
            catch (Win32Exception) { return list; } // "store" CLI δεν υπάρχει σε αυτό το build Windows

            // Ανάγνωση stdout/stderr ΤΑΥΤΟΧΡΟΝΑ με την αναμονή: πριν περιμέναμε πρώτα το exit και διαβάζαμε μετά —
            // αν η έξοδος ξεπερνούσε το buffer του pipe το store.exe μπλόκαρε και το scan κατέληγε σε timeout.
            Task<string> stdoutTask, stderrTask;
            try
            {
                stdoutTask = process!.StandardOutput.ReadToEndAsync();
                stderrTask = process.StandardError.ReadToEndAsync();
                try { await process.StandardInput.WriteLineAsync("n"); process.StandardInput.Close(); } catch { }
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process!.Kill(true); } catch { }
                return list;
            }

            var raw = await stdoutTask;
            _ = stderrTask;
            var clean = Regex.Replace(raw, @"\x1B\[[0-9;]*[a-zA-Z]", "");
            const char vertical = '│';
            string[]? headers = null;

            foreach (var rawLine in clean.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r').Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (Regex.IsMatch(line, @"^(checking|usage:|commands?:|options?:|examples?:|store cli)", RegexOptions.IgnoreCase)) continue;
                if (Regex.IsMatch(line, @"^(no updates|all apps are up to date|your apps are up to date)", RegexOptions.IgnoreCase)) continue;

                string[] cols;
                if (line.Contains(vertical))
                {
                    var raw2 = line.Split(vertical);
                    cols = raw2.Length > 2 ? raw2[1..^1].Select(c => c.Trim()).Where(c => c != "").ToArray() : Array.Empty<string>();
                }
                else if (Regex.IsMatch(line, @"\s{2,}"))
                {
                    cols = Regex.Split(line, @"\s{2,}").Select(c => c.Trim()).Where(c => c != "").ToArray();
                }
                else continue;

                if (cols.Length < 2) continue;

                var lower = cols.Select(c => c.ToLowerInvariant()).ToArray();
                if (lower.Contains("name") || lower.Contains("app") || lower.Contains("title"))
                {
                    headers = cols;
                    continue;
                }

                var nameIdx = 0;
                var verIdx = -1;
                var availIdx = -1;
                if (headers != null)
                {
                    for (var h = 0; h < headers.Length; h++)
                    {
                        if (Regex.IsMatch(headers[h], "^(name|app|title)$", RegexOptions.IgnoreCase)) nameIdx = h;
                        else if (Regex.IsMatch(headers[h], "installed|current|^version$", RegexOptions.IgnoreCase)) verIdx = h;
                        else if (Regex.IsMatch(headers[h], "available|latest|new\\s+version|update", RegexOptions.IgnoreCase)) availIdx = h;
                    }
                }
                if (availIdx == verIdx) availIdx = -1;

                var name = nameIdx < cols.Length ? cols[nameIdx] : "";
                name = Regex.Replace(name, "[─-╿]", "");
                name = Regex.Replace(name, @"^[^\p{L}\p{Nd}]+", "").Trim();
                if (string.IsNullOrWhiteSpace(name) || Regex.IsMatch(name, "^(name|app|title)$", RegexOptions.IgnoreCase)) continue;

                var ver = verIdx >= 0 && verIdx < cols.Length ? cols[verIdx] : "?";
                var avail = availIdx >= 0 && availIdx < cols.Length ? cols[availIdx] : "Update available";
                list.Add(new WingetUpdate(name, name, ver, avail, "msstore"));
            }

            return list;
        }

        // ===== 10 άλλοι πάροχοι πακέτων - καθένας best-effort, ίδια λογική ανίχνευσης/parsing με το
        // αντίστοιχο block μέσα στο $wingetScriptContent του ps1 (~11439-11585). =====

        private static async Task<List<WingetUpdate>> ScanPipAsync()
        {
            var list = new List<WingetUpdate>();
            var (output, _, started) = await RunToolAsync("python.exe",
                "-m pip --disable-pip-version-check --no-input --timeout 20 --retries 1 list --outdated --format=json");
            if (!started || !output.TrimStart().StartsWith("[")) return list;
            foreach (var el in JsonDocument.Parse(output).RootElement.EnumerateArray())
            {
                var name = el.GetProperty("name").GetString() ?? "";
                list.Add(new WingetUpdate(name, name, el.GetProperty("version").GetString() ?? "", el.GetProperty("latest_version").GetString() ?? "", "pip"));
            }
            return list;
        }

        private static async Task<List<WingetUpdate>> ScanNpmAsync()
        {
            var list = new List<WingetUpdate>();
            list.AddRange(await ParseNpmOutdatedAsync("/c npm outdated --json", "npm"));
            list.AddRange(await ParseNpmOutdatedAsync("/c npm outdated -g --json", "npm (global)"));
            return list;
        }

        private static async Task<List<WingetUpdate>> ParseNpmOutdatedAsync(string args, string source)
        {
            var list = new List<WingetUpdate>();
            var (output, _, started) = await RunToolAsync("cmd.exe", args);
            if (!started || !output.TrimStart().StartsWith("{")) return list;
            foreach (var prop in JsonDocument.Parse(output).RootElement.EnumerateObject())
            {
                var current = prop.Value.TryGetProperty("current", out var c) ? c.GetString() ?? "" : "";
                var latest = prop.Value.TryGetProperty("latest", out var l) ? l.GetString() ?? "" : "";
                list.Add(new WingetUpdate(prop.Name, prop.Name, current, latest, source));
            }
            return list;
        }

        private static async Task<List<WingetUpdate>> ScanPnpmAsync()
        {
            var list = new List<WingetUpdate>();
            list.AddRange(await ParsePnpmOutdatedAsync("/c pnpm outdated --format json", "pnpm"));
            list.AddRange(await ParsePnpmOutdatedAsync("/c pnpm outdated -g --format json", "pnpm (global)"));
            return list;
        }

        private static async Task<List<WingetUpdate>> ParsePnpmOutdatedAsync(string args, string source)
        {
            var list = new List<WingetUpdate>();
            var (output, _, started) = await RunToolAsync("cmd.exe", args);
            if (!started || !output.TrimStart().StartsWith("[")) return list;
            foreach (var el in JsonDocument.Parse(output).RootElement.EnumerateArray())
            {
                var name = el.GetProperty("name").GetString() ?? "";
                var current = el.TryGetProperty("current", out var c) ? c.GetString() ?? "" : "";
                var latest = el.TryGetProperty("latest", out var l) ? l.GetString() ?? "" : "";
                list.Add(new WingetUpdate(name, name, current, latest, source));
            }
            return list;
        }

        private static async Task<List<WingetUpdate>> ScanChocolateyAsync()
        {
            var list = new List<WingetUpdate>();
            var (output, _, started) = await RunToolAsync("choco.exe", "outdated -r");
            if (!started) return list;
            foreach (var line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('|');
                if (parts.Length >= 4) list.Add(new WingetUpdate(parts[0], parts[0], parts[1], parts[2], "chocolatey"));
            }
            return list;
        }

        private static async Task<List<WingetUpdate>> ScanScoopAsync()
        {
            var list = new List<WingetUpdate>();
            var (output, _, started) = await RunToolAsync("cmd.exe", "/c scoop status");
            if (!started) return list;
            foreach (var rawLine in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || Regex.IsMatch(line, @"^Name\s+Version") ||
                    line.StartsWith("----") || line.Contains("up to date")) continue;
                var m = Regex.Match(line, @"^(\S+)\s+(\S+)\s+(\S+)");
                if (m.Success) list.Add(new WingetUpdate(m.Groups[1].Value, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, "scoop"));
            }
            return list;
        }

        private static async Task<List<WingetUpdate>> ScanGemAsync()
        {
            var list = new List<WingetUpdate>();
            var (output, _, started) = await RunToolAsync("cmd.exe", "/c gem outdated");
            if (!started) return list;
            foreach (var line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var m = Regex.Match(line, @"^(\S+)\s+\(([\d\.]+)\s+<\s+([\d\.]+)\)");
                if (m.Success) list.Add(new WingetUpdate(m.Groups[1].Value, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, "gem"));
            }
            return list;
        }

        private static async Task<List<WingetUpdate>> ScanCargoAsync()
        {
            var list = new List<WingetUpdate>();
            var (output, _, started) = await RunToolAsync("cargo.exe", "install --list");
            if (!started) return list;
            foreach (var line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var m = Regex.Match(line, @"^([a-zA-Z0-9_\-]+)\s+v([\d\.]+):");
                if (m.Success) list.Add(new WingetUpdate(m.Groups[1].Value, m.Groups[1].Value, m.Groups[2].Value, "?", "cargo"));
            }
            return list;
        }

        private static async Task<List<WingetUpdate>> ScanDotnetToolsAsync()
        {
            var list = new List<WingetUpdate>();
            var (output, _, started) = await RunToolAsync("dotnet.exe", "tool list --global");
            if (!started) return list;
            foreach (var rawLine in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || Regex.IsMatch(line, @"^Package\s+Id", RegexOptions.IgnoreCase) || line.StartsWith("-")) continue;
                var parts = Regex.Split(line, @"\s+").Where(p => p != "").ToArray();
                if (parts.Length >= 2) list.Add(new WingetUpdate(parts[0], parts[0], parts[1], "?", "dotnet"));
            }
            return list;
        }

        // PSGallery version lookup (Find-Module) είναι εγγενώς PowerShell tooling - όπως και το
        // GetUpgradeCommandForSource ήδη περνάει από powershell.exe για το ίδιο provider, η σάρωση
        // κάνει το ίδιο αντί να ξαναϋλοποιήσει τη λογική σύγκρισης εκδόσεων του PSGallery σε C#.
        private static async Task<List<WingetUpdate>> ScanPsModuleAsync()
        {
            var list = new List<WingetUpdate>();
            const string script = @"
$ErrorActionPreference = 'SilentlyContinue'
$out = New-Object System.Collections.ArrayList
if (Get-Command Get-InstalledModule -ErrorAction SilentlyContinue) {
    foreach ($mod in @(Get-InstalledModule -ErrorAction SilentlyContinue)) {
        try {
            $found = Find-Module -Name $mod.Name -Repository PSGallery -ErrorAction SilentlyContinue
            if ($found -and ([version]$found.Version -gt [version]$mod.Version)) {
                [void]$out.Add([PSCustomObject]@{ Name = $mod.Name; Version = [string]$mod.Version; Available = [string]$found.Version })
            }
        } catch {}
    }
}
$out | ConvertTo-Json -Compress";
            var (output, _, started) = await RunToolAsync("powershell.exe",
                $"-NoProfile -Command \"{script.Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ")}\"",
                TimeSpan.FromSeconds(45));
            if (!started) return list;
            var trimmed = output.Trim();
            if (trimmed.Length == 0) return list;
            try
            {
                var root = JsonDocument.Parse(trimmed).RootElement;
                IEnumerable<JsonElement> elements = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : new[] { root };
                foreach (var el in elements)
                {
                    var name = el.GetProperty("Name").GetString() ?? "";
                    var version = el.GetProperty("Version").GetString() ?? "";
                    var available = el.GetProperty("Available").GetString() ?? "";
                    list.Add(new WingetUpdate(name, name, version, available, "psmodule"));
                }
            }
            catch (JsonException) { }
            return list;
        }

        private static async Task<List<WingetUpdate>> ScanComposerAsync()
        {
            var list = new List<WingetUpdate>();
            var (output, _, started) = await RunToolAsync("cmd.exe", "/c composer global outdated --format=json --no-interaction");
            if (!started || !output.TrimStart().StartsWith("{")) return list;
            var root = JsonDocument.Parse(output).RootElement;
            if (!root.TryGetProperty("installed", out var installed)) return list;
            foreach (var pkg in installed.EnumerateArray())
            {
                var name = pkg.GetProperty("name").GetString() ?? "";
                var version = pkg.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
                var latest = pkg.TryGetProperty("latest", out var l) ? l.GetString() ?? "" : "";
                list.Add(new WingetUpdate(name, name, version, latest, "composer"));
            }
            return list;
        }

        // ===== Κοινές βοηθητικές μέθοδοι =====

        private static Task<(string Output, int ExitCode, bool Started)> RunToolAsync(string fileName, string arguments) =>
            RunToolAsync(fileName, arguments, TimeSpan.FromSeconds(30));

        // started=false σημαίνει "το εργαλείο δεν βρέθηκε στο σύστημα" (Win32Exception ERROR_FILE_NOT_FOUND) -
        // ακριβώς η ίδια σημασιολογία με το "Get-Command X -ErrorAction SilentlyContinue" του ps1 original,
        // ώστε ο έλεγχος ύπαρξης κάθε προαιρετικού package manager να μη χρειάζεται ξεχωριστό βήμα.
        private static async Task<(string Output, int ExitCode, bool Started)> RunToolAsync(string fileName, string arguments, TimeSpan timeout)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };

            Process? process;
            try { process = Process.Start(psi); }
            catch (Win32Exception)
            {
                // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "όταν ξεκινούν τα windows δεν γίνεται έλεγχος ενημερώσεων,
                // ούτε balloon tip") - το winget.exe είναι ένα App Execution Alias (reparse-point stub
                // στο %LOCALAPPDATA%\Microsoft\WindowsApps) - η καταχώρηση αυτού του φακέλου στο PATH
                // της διεργασίας μπορεί να μην έχει προλάβει να διαδοθεί ΤΟΣΟ νωρίς μετά την είσοδο,
                // ειδικά όταν η εφαρμογή ξεκινά μέσω Task Scheduler LogonTrigger (--tray αυτόματη
                // εκκίνηση) αντί μέσω explorer.exe - το bare "winget.exe" απέτυχε σιωπηλά (Win32Exception,
                // "δεν βρέθηκε"), το UpdateNotificationService.StartupCheckAsync() το διάβαζε ως "0
                // ενημερώσεις" (αφού wingetStarted=false -> ScanAsync επιστρέφει άδεια λίστα, βλ. εκεί) -
                // καμία ειδοποίηση, καμία ένδειξη σφάλματος πουθενά. Fallback ΜΟΝΟ για το winget.exe,
                // στην απόλυτη διαδρομή του alias stub (σταθερή θέση, ανεξάρτητη από το PATH).
                process = null;
                if (string.Equals(fileName, "winget.exe", StringComparison.OrdinalIgnoreCase))
                {
                    var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
                    if (File.Exists(fallback))
                    {
                        psi.FileName = fallback;
                        try { process = Process.Start(psi); } catch (Win32Exception) { /* παραμένει null */ }
                    }
                }
                if (process == null) return ("", -1, false);
            }

            using (process)
            {
                var proc = process!; // μη-nullable alias - η null περίπτωση έχει ήδη επιστρέψει παραπάνω (το using(process) από μόνο του δεν στενεύει τη nullability ανάλυση)
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                _ = proc.StandardError.ReadToEndAsync(); // το stderr δεν διαβαζόταν ποτέ → block αν γέμιζε το pipe
                try
                {
                    using var cts = new CancellationTokenSource(timeout);
                    await proc.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { proc.Kill(true); } catch { } // ολόκληρο το δέντρο (winget/powershell παιδιά)
                    return ("", -1, true);
                }
                var stdout = await stdoutTask;
                return (stdout, proc.ExitCode, true);
            }
        }

        // internal (όχι private) - ίδιο μοτίβο InternalsVisibleTo με το ThemedMessageBox.TxtMessage
        // (βλ. OptimizerWpf.csproj), ώστε το OptimizerWpf.Tests να μπορεί να το δοκιμάσει απευθείας
        // χωρίς πραγματική κλήση winget.exe (βλ. WingetServiceTests.cs).
        internal static IReadOnlyList<WingetUpdate> ParseWingetTable(string rawText)
        {
            var result = new List<WingetUpdate>();
            // winget draws its table with ANSI escape/progress codes mixed in on some terminals -
            // strip them so column-position math below isn't thrown off (same reasoning as
            // Optimizer.ps1's ANSI-stripping fix mentioned in its changelog).
            var cleaned = Regex.Replace(rawText, @"\x1B\[[0-9;]*[a-zA-Z]", "");
            var lines = cleaned.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

            var headerIndex = Array.FindIndex(lines, l => Regex.IsMatch(l, @"^\s*Name\s+Id\s+Version"));
            if (headerIndex < 0 || headerIndex + 2 >= lines.Length) return result;

            var headerLine = lines[headerIndex];
            var columns = new[] { "Name", "Id", "Version", "Available", "Source" }
                .Select(name => (Name: name, Start: headerLine.IndexOf(name, StringComparison.Ordinal)))
                .Where(c => c.Start >= 0)
                .OrderBy(c => c.Start)
                .ToList();

            for (var i = headerIndex + 2; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (Regex.IsMatch(line, @"^\d+ upgrades? available") ||
                    line.StartsWith("No installed package found") ||
                    Regex.IsMatch(line, @"^-+$"))
                    continue;

                var fields = new Dictionary<string, string>();
                for (var c = 0; c < columns.Count; c++)
                {
                    var start = columns[c].Start;
                    if (start >= line.Length) continue;
                    var len = c + 1 < columns.Count ? columns[c + 1].Start - start : line.Length - start;
                    if (start + len > line.Length) len = line.Length - start;
                    if (len <= 0) continue;
                    fields[columns[c].Name] = line.Substring(start, len).Trim();
                }

                if (fields.TryGetValue("Id", out var id) && !string.IsNullOrEmpty(id))
                {
                    // ΔΙΟΡΘΩΣΗ (GEARWIN.MD: "κάποιες ενημερώσεις αποτυγχάνουν και ανοίγει το παράθυρο
                    // του msstore") - η στήλη "Source" διαβαζόταν από τον πίνακα και μετά πετιόταν,
                    // αντικαθιστώντας ΠΑΝΤΑ με "winget" - γραμμές με πραγματική πηγή "msstore" (το
                    // "winget upgrade" περιλαμβάνει και τα Store apps) παρουσιάζονταν ως απλό winget,
                    // προσπερνώντας τον ήδη υπάρχοντα χειρισμό msstore (άνοιγμα Store, όχι αποτυχία) και
                    // καταλήγοντας σε "winget upgrade --id" που το ίδιο το winget αποτυγχάνει/ανοίγει
                    // μόνο του το Store για πακέτα αυτής της πηγής.
                    result.Add(new WingetUpdate(
                        fields.GetValueOrDefault("Name", ""),
                        id,
                        fields.GetValueOrDefault("Version", ""),
                        fields.GetValueOrDefault("Available", ""),
                        fields.GetValueOrDefault("Source", "winget")));
                }
            }

            return result;
        }
    }
}
