using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public record DriverUpdate(string UpdateId, string Title, string? DriverClass, string? Manufacturer, string? Model, string? Date);
    public record DriverScanResult(bool PolicyBlocked, IReadOnlyList<DriverUpdate> Updates);
    public record DriverStoreEntry(string Driver, string OriginalFileName, string? Version, string? Date, string? Class);
    public record PnpDeviceInfo(string DeviceName, string HardwareId, Version CurrentVersion, DateTime CurrentDate)
    {
        public string PnpClass { get; init; } = "";
    }

    public record CatalogCandidate(string DeviceName, string HardwareId, string UpdateId, string Title, Version Version, DateTime ReleaseDate, bool IsWhql)
    {
        public string ResultClass { get; init; } = "";
    }

    // Port του Optimizer.ps1's driver-ενημερώσεων συστήματος (~11847-13411) + ενσωμάτωση της τεχνικής
    // αναζήτησης στο Microsoft Update Catalog ανά Hardware ID (χρήστης παρείχε reference script
    // Driver_Updater.ps1) ως ΔΕΥΤΕΡΗ, συμπληρωματική πηγή - βλ. HANDOFF.md §0.4ιε. AMD/NVIDIA/Dell
    // ΠΑΡΑΜΕΝΟΥΝ εκτός εμβέλειας: narrow, εύθραυστο web scraping συγκεκριμένων vendor ιστοσελίδων
    // (ανά GPU/OEM), δεν καλύπτουν τη γενική περίπτωση όπως το Catalog - ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ.
    public static class DriverService
    {
        private static readonly HttpClient Http = BuildHttpClient();

        // Τελευταίο SHA256 hash που υπολογίστηκε σε εγκατάσταση Catalog driver - διαθέσιμο στο UI
        // για εμφάνιση/log, ίδια λογική με το "κράτα ίχνος τι πραγματικά εγκαταστάθηκε".
        public static string? LastInstallLog { get; private set; }

        private static HttpClient BuildHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            client.Timeout = TimeSpan.FromSeconds(20);
            return client;
        }

        // ===== Microsoft Update Catalog (ανά Hardware ID) =====

        public static Task<IReadOnlyList<PnpDeviceInfo>> GetSystemDevicesAsync() => Task.Run(() =>
        {
            var devices = new List<PnpDeviceInfo>();
            using var searcher = new ManagementObjectSearcher("SELECT Name, DeviceID, HardwareID, PNPClass FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0");
            foreach (ManagementObject entity in searcher.Get())
            {
                var name = entity["Name"] as string;
                var hwIds = entity["HardwareID"] as string[];
                var deviceId = entity["DeviceID"] as string;
                if (string.IsNullOrEmpty(name) || hwIds == null || hwIds.Length == 0 || deviceId == null) continue;
                var pnpClass = entity["PNPClass"] as string ?? "";

                var version = new Version(0, 0, 0, 0);
                var date = DateTime.MinValue;
                try
                {
                    using var driverSearcher = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_PnPEntity.DeviceID=\"{deviceId.Replace("\\", "\\\\")}\"}} WHERE ResultClass = Win32_PnPSignedDriver");
                    var signed = driverSearcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                    if (signed != null)
                    {
                        if (signed["DriverVersion"] is string vs) Version.TryParse(vs, out version!);
                        if (signed["DriverDate"] is string ds) date = ManagementDateTimeConverter.ToDateTime(ds);
                    }
                }
                catch { }

                devices.Add(new PnpDeviceInfo(name, hwIds[0], version, date) { PnpClass = pnpClass });
            }
            return (IReadOnlyList<PnpDeviceInfo>)devices;
        });

        // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "για τα ρίσκα βρες λύσεις και ενσωμάτωσε") - 3 άμυνες πάνω από
        // την απλή έκδοση:
        // 1. Throttled + jittered delay ανάμεσα σε requests (ρητή σχεδιαστική επιλογή, ΔΕΝ υπήρχε στο
        //    reference script) - το catalog.update.microsoft.com είναι τεκμηριωμένα ευαίσθητο σε
        //    rate-limiting για αυτοματοποιημένη πρόσβαση, ένα πλήρες σύστημα έχει 20-60 συσκευές.
        // 2. Retry-with-backoff (1 επανάληψη) σε μεμονωμένο αποτυχημένο request πριν το εγκαταλείψει.
        // 3. Ανίχνευση "ύποπτου μηδενικού αποτελέσματος": αν σαρωθούν ≥5 συσκευές και ΚΑΜΙΑ δεν
        //    επιστρέψει έστω ένα αποτέλεσμα (πριν καν το φιλτράρισμα stable/newer), είναι πολύ πιθανό
        //    να άλλαξε η μορφή της σελίδας (σπασμένο regex) ή να μπλοκαρίστηκε η πρόσβαση - ΔΕΝ πρέπει
        //    να αναφερθεί σιωπηλά ως "όλα ενημερωμένα" (ψευδώς καθησυχαστικό), αναφέρεται ρητά.
        public static async Task<(IReadOnlyList<CatalogCandidate> Candidates, bool LooksBlocked)> ScanCatalogAsync(
            IReadOnlyList<PnpDeviceInfo> devices, IProgress<string>? progress = null, CancellationToken ct = default)
        {
            var results = new List<CatalogCandidate>();
            var devicesWithAnyResult = 0;
            var devicesScanned = 0;
            var rnd = new Random();

            foreach (var device in devices)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(device.DeviceName);
                devicesScanned++;
                try
                {
                    var candidates = await SearchCatalogForDeviceWithRetryAsync(device);
                    if (candidates.Count > 0) devicesWithAnyResult++;
                    var best = PickBest(device, candidates);
                    if (best != null) results.Add(best);
                }
                catch { /* ένα αποτυχημένο device δεν πρέπει να σταματήσει τα υπόλοιπα */ }
                await Task.Delay(350 + rnd.Next(0, 250), ct);
            }

            var looksBlocked = devicesScanned >= 5 && devicesWithAnyResult == 0;
            return (results, looksBlocked);
        }

        private static async Task<List<CatalogCandidate>> SearchCatalogForDeviceWithRetryAsync(PnpDeviceInfo device)
        {
            try { return await SearchCatalogForDeviceAsync(device); }
            catch
            {
                await Task.Delay(1500);
                try { return await SearchCatalogForDeviceAsync(device); }
                catch { return new List<CatalogCandidate>(); }
            }
        }

        private static async Task<List<CatalogCandidate>> SearchCatalogForDeviceAsync(PnpDeviceInfo device)
        {
            var url = $"https://www.catalog.update.microsoft.com/Search.aspx?q={Uri.EscapeDataString(device.HardwareId)}";
            var html = await Http.GetStringAsync(url);

            // Ίδιο regex με το reference script - parsing του πίνακα αποτελεσμάτων του Catalog.
            const string pattern = "(?s)<tr id=\"(?<UpdateId>[a-f0-9\\-]+)_row\".*?class=\"tableRow\">.*?<a.*?>(?<Title>.*?)</a>.*?<td>(?<Class>.*?)</td>.*?<td>(?<Version>.*?)</td>.*?<td>(?<Date>.*?)</td>";
            var list = new List<CatalogCandidate>();
            foreach (Match m in Regex.Matches(html, pattern))
            {
                var title = m.Groups["Title"].Value.Trim();
                if (title.Contains("Beta", StringComparison.OrdinalIgnoreCase) || title.Contains("Preview", StringComparison.OrdinalIgnoreCase)) continue;
                if (!Version.TryParse(m.Groups["Version"].Value.Trim(), out var version)) continue;
                if (!DateTime.TryParse(m.Groups["Date"].Value.Trim(), out var date)) continue;

                list.Add(new CatalogCandidate(device.DeviceName, device.HardwareId, m.Groups["UpdateId"].Value, title, version, date, IsWhql: true)
                { ResultClass = m.Groups["Class"].Value.Trim() });
            }
            return list;
        }

        // ΔΙΟΡΘΩΣΗ (ρίσκο #3 - "η αναζήτηση Catalog είναι ελεύθερο κείμενο, όχι αυστηρό Hardware ID
        // φιλτράρισμα, μπορεί να επιστρέψει άσχετα αποτελέσματα"): πρόσθετος έλεγχος λογικότητας -
        // αν η κλάση συσκευής (π.χ. "Net"/"Display"/"HDC") είναι γνωστή ΚΑΙ το αποτέλεσμα του Catalog
        // έχει ΔΙΑΦΟΡΕΤΙΚΗ, μη-κενή κλάση, απορρίπτεται ως πιθανό ψευδές ταίριασμα - όχι τέλεια
        // επικύρωση, αλλά αρκετά καλή για να πιάσει τα πιο προφανή άσχετα αποτελέσματα.
        private static CatalogCandidate? PickBest(PnpDeviceInfo device, List<CatalogCandidate> candidates)
        {
            CatalogCandidate? best = null;
            var bestScore = -1;
            foreach (var c in candidates)
            {
                if (c.Version <= device.CurrentVersion) continue;
                if (!string.IsNullOrEmpty(device.PnpClass) && !string.IsNullOrEmpty(c.ResultClass) &&
                    !c.ResultClass.Contains(device.PnpClass, StringComparison.OrdinalIgnoreCase) &&
                    !device.PnpClass.Contains(c.ResultClass, StringComparison.OrdinalIgnoreCase))
                    continue;

                var score = (c.IsWhql ? 40 : 0) + 30 + (c.ReleaseDate > device.CurrentDate ? 20 : 0);
                if (score > bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        // ΔΙΟΡΘΩΣΗ σε σχέση με το reference script: το `DownloadDialog.aspx?updateIDs=<guid>` ως απλό
        // GET link ΔΕΝ επιστρέφει το πραγματικό αρχείο - επιστρέφει HTML σελίδα διαλόγου. Η πραγματική,
        // λειτουργική τεχνική (επιβεβαιώθηκε μέσω έρευνας σε καθιερωμένα open-source εργαλεία σαν το
        // MSCatalog) απαιτεί POST στο ίδιο endpoint με συγκεκριμένο JSON body, μετά εξαγωγή του
        // πραγματικού CDN link από την απάντηση.
        public static async Task<bool> InstallCatalogDriverAsync(CatalogCandidate candidate)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"OptimizerWpfDriverCatalog_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var downloadUrl = await ResolveCatalogDownloadUrlAsync(candidate.UpdateId);
                if (downloadUrl == null) return false;

                var filePath = Path.Combine(tempDir, Path.GetFileName(new Uri(downloadUrl).LocalPath));
                var bytes = await Http.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(filePath, bytes);

                // SHA256 - καταγράφεται στο log ως πρόσθετο, ελέγξιμο ίχνος του τι πραγματικά
                // κατέβηκε/εγκαταστάθηκε (defense-in-depth πλάι στο Authenticode, δεν αντικαθιστά την
                // υπογραφή - δεν υπάρχει "γνωστό καλό" hash να συγκριθεί εκ των προτέρων, το Catalog
                // δεν εκθέτει hashes δημόσια).
                var sha256 = Convert.ToHexString(SHA256.HashData(bytes));
                LastInstallLog = $"{candidate.Title} · SHA256: {sha256}";

                // Επαλήθευση Authenticode ΠΡΙΝ την εγκατάσταση - το .NET δεν έχει καθαρό, ενσωματωμένο
                // API ισοδύναμο του Get-AuthenticodeSignature (θα χρειαζόταν P/Invoke WinVerifyTrust) -
                // shell out στο ΙΔΙΟ cmdlet που χρησιμοποιεί το PowerShell, ίδιο μοτίβο με τα υπόλοιπα
                // σημεία της εφαρμογής που στηρίζονται σε PowerShell-only λειτουργικότητα.
                if (!await VerifyAuthenticodeAsync(filePath)) return false;

                if (filePath.EndsWith(".cab", StringComparison.OrdinalIgnoreCase))
                {
                    var extractPath = Path.Combine(tempDir, "extracted");
                    Directory.CreateDirectory(extractPath);
                    using var expand = Process.Start(new ProcessStartInfo("expand.exe", $"\"{filePath}\" -F:* \"{extractPath}\"")
                    { UseShellExecute = false, CreateNoWindow = true });
                    expand?.WaitForExit();
                    if (expand?.ExitCode != 0) return false;

                    using var pnputil = Process.Start(new ProcessStartInfo("pnputil.exe", $"/add-driver \"{extractPath}\\*.inf\" /subdirs /install")
                    { UseShellExecute = false, CreateNoWindow = true });
                    pnputil?.WaitForExit();
                    return pnputil?.ExitCode == 0;
                }

                // .msu ή άλλος τύπος πακέτου - wusa.exe είναι ο επίσημος εγκαταστάτης για .msu.
                using var wusa = Process.Start(new ProcessStartInfo("wusa.exe", $"\"{filePath}\" /quiet /norestart") { UseShellExecute = false, CreateNoWindow = true });
                wusa?.WaitForExit();
                return wusa?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        private static async Task<string?> ResolveCatalogDownloadUrlAsync(string updateId)
        {
            var body = $"updateIDs=[{{\"size\":0,\"updateID\":\"{updateId}\",\"uidInfo\":\"{updateId}\"}}]";
            var content = new StringContent(Uri.UnescapeDataString(body));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
            using var response = await Http.PostAsync("https://www.catalog.update.microsoft.com/DownloadDialog.aspx", content);
            var html = await response.Content.ReadAsStringAsync();
            html = html.Replace("www.download.windowsupdate", "download.windowsupdate");
            var match = Regex.Match(html, @"(https?://(?:dl\.delivery\.mp\.microsoft\.com|(?:catalog\.s\.)?download\.windowsupdate\.com)/[^'""]*)");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static async Task<bool> VerifyAuthenticodeAsync(string filePath)
        {
            var psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -Command \"(Get-AuthenticodeSignature -LiteralPath '{filePath}').Status\"")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var process = Process.Start(psi);
            if (process == null) return false;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output.Trim() == "Valid";
        }
        public static async Task<DriverScanResult> ScanAsync()
        {
            const string script = @"
$ErrorActionPreference = 'Continue'
$policyBlocked = $false
foreach ($p in @(
    'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate',
    'HKLM:\SOFTWARE\Microsoft\PolicyManager\current\device\Update',
    'HKLM:\SOFTWARE\Microsoft\PolicyManager\default\Update',
    'HKLM:\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings'
)) {
    try {
        $v = (Get-ItemProperty -Path $p -Name 'ExcludeWUDriversInQualityUpdate' -ErrorAction SilentlyContinue).ExcludeWUDriversInQualityUpdate
        if ($v -eq 1) { $policyBlocked = $true }
    } catch {}
}

$updates = @()
if (-not $policyBlocked) {
    try {
        $session = New-Object -ComObject Microsoft.Update.Session
        $searcher = $session.CreateUpdateSearcher()
        $result = $searcher.Search(""IsInstalled=0 and Type='Driver'"")
        foreach ($u in $result.Updates) {
            $updates += [PSCustomObject]@{
                UpdateId     = $u.Identity.UpdateID
                Title        = $u.Title
                DriverClass  = $u.DriverClass
                Manufacturer = $u.DriverManufacturer
                Model        = $u.DriverModel
                Date         = if ($u.DriverVerDate) { $u.DriverVerDate.ToString('yyyy-MM-dd') } else { $null }
            }
        }
    } catch {}
}

[PSCustomObject]@{ PolicyBlocked = $policyBlocked; Updates = @($updates) } | ConvertTo-Json -Depth 4 -Compress
";
            var output = await RunPowerShellScriptAsync(script, TimeSpan.FromMinutes(3));
            if (string.IsNullOrWhiteSpace(output)) return new DriverScanResult(false, Array.Empty<DriverUpdate>());

            try
            {
                var root = JsonDocument.Parse(output).RootElement;
                var policyBlocked = root.GetProperty("PolicyBlocked").GetBoolean();
                var updates = new List<DriverUpdate>();
                foreach (var u in root.GetProperty("Updates").EnumerateArray())
                {
                    updates.Add(new DriverUpdate(
                        u.GetProperty("UpdateId").GetString() ?? "",
                        u.GetProperty("Title").GetString() ?? "",
                        u.TryGetProperty("DriverClass", out var dc) ? dc.GetString() : null,
                        u.TryGetProperty("Manufacturer", out var mf) ? mf.GetString() : null,
                        u.TryGetProperty("Model", out var md) ? md.GetString() : null,
                        u.TryGetProperty("Date", out var dt) ? dt.GetString() : null));
                }
                return new DriverScanResult(policyBlocked, updates);
            }
            catch (JsonException)
            {
                return new DriverScanResult(false, Array.Empty<DriverUpdate>());
            }
        }

        // Ξεχωριστή background διεργασία ανά driver, ίδιο με το ps1 original - δεν περιμένει να
        // ολοκληρωθεί (η λήψη+εγκατάσταση μέσω Windows Update μπορεί να πάρει αρκετά λεπτά).
        public static void InstallInBackground(string updateId)
        {
            var script = $@"
$ErrorActionPreference = 'Continue'
try {{
    $session = New-Object -ComObject Microsoft.Update.Session
    $searcher = $session.CreateUpdateSearcher()
    $result = $searcher.Search(""UpdateID='{updateId}'"")
    if ($result.Updates.Count -eq 0) {{ exit 1 }}
    $toInstall = New-Object -ComObject Microsoft.Update.UpdateColl
    $toInstall.Add($result.Updates.Item(0)) | Out-Null
    $downloader = $session.CreateUpdateDownloader()
    $downloader.Updates = $toInstall
    $downloader.Download() | Out-Null
    $installer = $session.CreateUpdateInstaller()
    $installer.Updates = $toInstall
    $installer.Install() | Out-Null
}} catch {{}}
";
            RunPowerShellScriptDetached(script);
        }

        // DISM Export-Driver - εξάγει ΟΛΟΥΣ τους οδηγούς τρίτων κατασκευαστών (ΟΧΙ τους εργοστασιακούς
        // Windows) σε φάκελο. Ίδιος μηχανισμός με καθιερωμένα εργαλεία δημιουργίας αντιγράφων οδηγών.
        public static Task<bool> BackupAsync(string destinationPath) =>
            RunProcessAsync("dism.exe", $"/Online /Export-Driver /Destination:\"{destinationPath}\"", TimeSpan.FromMinutes(10));

        // pnputil - εγκαθιστά ξανά όλους τους οδηγούς από φάκελο αντιγράφου. Απαιτεί δικαιώματα
        // διαχειριστή (ήδη διαθέσιμα - βλ. app.manifest).
        public static Task<bool> RestoreAsync(string sourcePath) =>
            RunProcessAsync("pnputil.exe", $"/add-driver \"{sourcePath}\\*.inf\" /subdirs /install", TimeSpan.FromMinutes(10));

        // Get-WindowsDriver (DISM cmdlet, ΟΧΙ ανάλυση κειμένου pnputil - το τελευταίο είναι localized
        // ανά γλώσσα Windows, ήδη προκάλεσε bug αλλού στο ps1 original). Φιλτράρει Inbox=false (ΜΟΝΟ
        // 3rd-party, ΠΟΤΕ εργοστασιακούς οδηγούς Windows).
        public static async Task<IReadOnlyList<DriverStoreEntry>> ScanStoreAsync()
        {
            const string script = @"
$ErrorActionPreference = 'Continue'
$drivers = Get-WindowsDriver -Online -ErrorAction SilentlyContinue | Where-Object { -not $_.Inbox }
$result = @($drivers | ForEach-Object {
    [PSCustomObject]@{
        Driver           = $_.Driver
        OriginalFileName = [System.IO.Path]::GetFileName($_.OriginalFileName)
        Version          = $_.Version
        Date             = if ($_.Date) { $_.Date.ToString('yyyy-MM-dd') } else { $null }
        ClassName        = $_.ClassName
    }
})
$result | ConvertTo-Json -Depth 4 -Compress
";
            var output = await RunPowerShellScriptAsync(script, TimeSpan.FromMinutes(3));
            if (string.IsNullOrWhiteSpace(output)) return Array.Empty<DriverStoreEntry>();

            try
            {
                var root = JsonDocument.Parse(output).RootElement;
                var entries = new List<DriverStoreEntry>();
                var elements = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : new[] { root }.AsEnumerable();
                foreach (var d in elements)
                {
                    entries.Add(new DriverStoreEntry(
                        d.GetProperty("Driver").GetString() ?? "",
                        d.GetProperty("OriginalFileName").GetString() ?? "",
                        d.TryGetProperty("Version", out var v) ? v.GetString() : null,
                        d.TryGetProperty("Date", out var dt) ? dt.GetString() : null,
                        d.TryGetProperty("ClassName", out var c) ? c.GetString() : null));
                }
                return entries;
            }
            catch (JsonException)
            {
                return Array.Empty<DriverStoreEntry>();
            }
        }

        // pnputil αρνείται από μόνο του να διαγράψει driver που βρίσκεται ενεργά σε χρήση - καμία
        // πρόσθετη προστασία χρειάζεται εδώ (ίδια λογική με το ps1 original).
        public static Task<bool> DeleteStoreDriverAsync(string driverOemName) =>
            RunProcessAsync("pnputil.exe", $"/delete-driver {driverOemName} /uninstall /force", TimeSpan.FromMinutes(2));

        private static async Task<string> RunPowerShellScriptAsync(string script, TimeSpan timeout)
        {
            var scriptFile = Path.Combine(Path.GetTempPath(), $"OptimizerWpfDriver_{Guid.NewGuid():N}.ps1");
            File.WriteAllText(scriptFile, script);
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                };
                using var process = Process.Start(psi);
                if (process == null) return "";
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                using var cts = new System.Threading.CancellationTokenSource(timeout);
                try { await process.WaitForExitAsync(cts.Token); }
                catch (OperationCanceledException) { try { process.Kill(); } catch { } return ""; }
                return await stdoutTask;
            }
            finally
            {
                try { File.Delete(scriptFile); } catch { }
            }
        }

        // Fire-and-forget - χρησιμοποιείται μόνο για την εγκατάσταση driver (μπορεί να πάρει πολλά
        // λεπτά, δεν χρειάζεται να την περιμένει η εφαρμογή· ο χρήστης βλέπει μόνο ότι ξεκίνησε).
        private static void RunPowerShellScriptDetached(string script)
        {
            var scriptFile = Path.Combine(Path.GetTempPath(), $"OptimizerWpfDriverInstall_{Guid.NewGuid():N}.ps1");
            File.WriteAllText(scriptFile, script);
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"& '{scriptFile}'; Remove-Item '{scriptFile}' -Force\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            }
            catch { }
        }

        private static async Task<bool> RunProcessAsync(string fileName, string arguments, TimeSpan timeout)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var process = Process.Start(psi);
                if (process == null) return false;
                using var cts = new System.Threading.CancellationTokenSource(timeout);
                try { await process.WaitForExitAsync(cts.Token); }
                catch (OperationCanceledException) { try { process.Kill(); } catch { } return false; }
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
