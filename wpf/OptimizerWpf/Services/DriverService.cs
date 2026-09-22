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

    // Port των 4 πρόσθετων πηγών του Optimizer.ps1's Start-DriverScan (~11977-12289, βλ. HANDOFF.md
    // ενότητα 0/§Α.3) - AMD/NVIDIA ζωντανά επιβεβαιωμένα σε πραγματικό hardware, Dell/SDIO μόνο μέσω
    // τεκμηρίωσης+headless flags. Η σύγκριση "νεότερο" γίνεται ΠΑΝΤΑ με ΗΜΕΡΟΜΗΝΙΕΣ, ΠΟΤΕ version
    // strings (το AMD Adrenalin/NVIDIA marketing versioning δεν αντιστοιχεί στο εσωτερικό Windows
    // DriverVersion) - ίδια σχεδιαστική επιλογή με το ps1 original.
    public record AmdDriverInfo(string GpuName, string LatestVersion, string? LatestReleaseDate, string? InstalledVersion, string? InstalledDate, string DownloadUrl)
    {
        public bool IsNewer => DriverService.IsDateNewer(LatestReleaseDate, InstalledDate);
    }
    public record NvidiaDriverInfo(string GpuName, string LatestVersion, string? LatestReleaseDate, string? InstalledVersion, string? InstalledDate, string DownloadUrl)
    {
        public bool IsNewer => DriverService.IsDateNewer(LatestReleaseDate, InstalledDate);
    }
    public record DellUpdateInfo(string? Name, string? Version, string? Date, string? Urgency, string? Type, string? Category);
    public record DellScanResult(bool Installed, IReadOnlyList<DellUpdateInfo> Updates, string? Error);
    public record SdioScanResult(bool Installed, bool TimedOut, bool ReportFound, bool HasDriverPacks, string? ReportPath, string? Error);
    public record OemInfo(string OemName, int? LenovoPackageCount = null);
    public record VendorScanResult(IReadOnlyList<AmdDriverInfo> Amd, IReadOnlyList<NvidiaDriverInfo> Nvidia, DellScanResult? Dell, SdioScanResult? Sdio, OemInfo? Oem);

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
        // ΝΕΟ - βελτίωση απόδοσης: τοπική cache ανά Hardware ID στο δίσκο (24ωρη διάρκεια ζωής) -
        // η σάρωση αυτή είναι σκόπιμα αργή (throttled delay 350-600ms/συσκευή παραπάνω, για να μην
        // μπλοκαριστεί από το catalog.update.microsoft.com) και ένα πλήρες σύστημα έχει 20-60
        // συσκευές, άρα μπορεί να πάρει αρκετά λεπτά. Αν ο χρήστης ξανατρέξει τη σάρωση μέσα στο ίδιο
        // 24ωρο (π.χ. μετά από ένα Full Maintenance), δεν έχει νόημα να ξαναπεριμένει το ίδιο διάστημα
        // για ΤΟ ΙΔΙΟ αποτέλεσμα - το hardware δεν άλλαξε. ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: ένα νέο driver που
        // δημοσιεύτηκε ΜΕΣΑ σε αυτό το 24ωρο δεν θα φανεί μέχρι να λήξει η cache - αποδεκτός συμβιβασμός
        // δεδομένου του πόσο σπάνια αλλάζει το διαθέσιμο driver ενός συγκεκριμένου hardware ID.
        private static readonly string CatalogCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf", "DriverCatalogCache.json");
        private static readonly TimeSpan CatalogCacheTtl = TimeSpan.FromHours(24);
        private record CachedCandidate(string DeviceName, string HardwareId, string UpdateId, string Title, string Version, string ReleaseDate, bool IsWhql, string ResultClass);
        private record CatalogCacheEntry(DateTime CachedAtUtc, List<CachedCandidate> Candidates);
        private static Dictionary<string, CatalogCacheEntry>? _catalogCache;

        private static Dictionary<string, CatalogCacheEntry> LoadCatalogCache()
        {
            if (_catalogCache != null) return _catalogCache;
            try
            {
                if (File.Exists(CatalogCachePath))
                    _catalogCache = JsonSerializer.Deserialize<Dictionary<string, CatalogCacheEntry>>(File.ReadAllText(CatalogCachePath));
            }
            catch { }
            _catalogCache ??= new Dictionary<string, CatalogCacheEntry>();
            return _catalogCache;
        }

        private static void SaveCatalogCache()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogCachePath)!);
                File.WriteAllText(CatalogCachePath, JsonSerializer.Serialize(_catalogCache));
            }
            catch { }
        }

        private static CachedCandidate ToCached(CatalogCandidate c) =>
            new(c.DeviceName, c.HardwareId, c.UpdateId, c.Title, c.Version.ToString(), c.ReleaseDate.ToString("O"), c.IsWhql, c.ResultClass);

        private static CatalogCandidate? FromCached(CachedCandidate c)
        {
            try
            {
                return new CatalogCandidate(c.DeviceName, c.HardwareId, c.UpdateId, c.Title, Version.Parse(c.Version), DateTime.Parse(c.ReleaseDate), c.IsWhql) { ResultClass = c.ResultClass };
            }
            catch { return null; }
        }

        public static async Task<(IReadOnlyList<CatalogCandidate> Candidates, bool LooksBlocked)> ScanCatalogAsync(
            IReadOnlyList<PnpDeviceInfo> devices, IProgress<string>? progress = null, CancellationToken ct = default)
        {
            var results = new List<CatalogCandidate>();
            var devicesWithAnyResult = 0;
            var devicesScanned = 0;
            var rnd = new Random();
            var cache = LoadCatalogCache();
            var cacheChanged = false;

            foreach (var device in devices)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(device.DeviceName);
                devicesScanned++;

                if (cache.TryGetValue(device.HardwareId, out var cached) && DateTime.UtcNow - cached.CachedAtUtc < CatalogCacheTtl)
                {
                    var cachedCandidates = cached.Candidates.Select(FromCached).Where(c => c != null).Select(c => c!).ToList();
                    if (cachedCandidates.Count > 0) devicesWithAnyResult++;
                    var bestCached = PickBest(device, cachedCandidates);
                    if (bestCached != null) results.Add(bestCached);
                    continue;
                }

                try
                {
                    var candidates = await SearchCatalogForDeviceWithRetryAsync(device);
                    if (candidates.Count > 0) devicesWithAnyResult++;
                    cache[device.HardwareId] = new CatalogCacheEntry(DateTime.UtcNow, candidates.Select(ToCached).ToList());
                    cacheChanged = true;
                    var best = PickBest(device, candidates);
                    if (best != null) results.Add(best);
                }
                catch { /* ένα αποτυχημένο device δεν πρέπει να σταματήσει τα υπόλοιπα */ }
                await Task.Delay(350 + rnd.Next(0, 250), ct);
            }

            if (cacheChanged) SaveCatalogCache();

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

        // "Νεότερο" πάντα με ημερομηνίες (yyyy-MM-dd) - ίδια λογική με το Complete-DriverScan του ps1
        // original (Test-DriverSourceOverlap/amdNewer/nvNewer). Αν λείπει η εγκατεστημένη ημερομηνία
        // αλλά υπάρχει η επίσημη release date, θεωρείται συντηρητικά "πιθανή ενημέρωση" (ίδιο με ps1).
        internal static bool IsDateNewer(string? latest, string? installed)
        {
            if (string.IsNullOrEmpty(latest)) return false;
            if (!DateTime.TryParse(latest, out var latestDate)) return false;
            if (string.IsNullOrEmpty(installed)) return true;
            return DateTime.TryParse(installed, out var installedDate) && latestDate > installedDate;
        }

        // Port του Test-DriverSourceOverlap (ίδιο πνεύμα best-effort string overlap, ΟΧΙ Hardware ID -
        // όταν 2 ανεξάρτητες πηγές συμφωνούν για το ΙΔΙΟ φυσικό GPU, η "πιθανή" ενημέρωση γίνεται
        // "επιβεβαιωμένη" υψηλότερης εμπιστοσύνης στο UI).
        public static bool HasSourceOverlap(string? deviceName, IEnumerable<string> confirmedNames)
        {
            if (string.IsNullOrEmpty(deviceName)) return false;
            foreach (var confirmed in confirmedNames)
            {
                if (string.IsNullOrEmpty(confirmed)) continue;
                if (deviceName.Contains(confirmed, StringComparison.OrdinalIgnoreCase) ||
                    confirmed.Contains(deviceName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // Port των 4 πρόσθετων πηγών (AMD/NVIDIA/Dell/SDIO) + OEM ανίχνευση (Lenovo/HP) από το
        // Start-DriverScan (~11977-12289) - ΕΝΑ combined script, ίδια δομή/λογική με το ps1 original,
        // ΚΑΝΕΝΑ αυτόματο install (μόνο σύνδεσμοι λήψης/κουμπιά - ο χρήστης αποφασίζει). Το SDIO
        // τρέχει ΠΑΝΤΑ headless (-nogui, βλ. σχόλιο ps1 ~12187) - ΠΟΤΕ δεν εμφανίζει το δικό του
        // παράθυρο· ΣΚΟΠΙΜΑ ΔΕΝ κάνει parsing μεμονωμένων driver γραμμών από το log του (η ακριβής
        // μορφή δεν έχει ζωντανά επιβεβαιωθεί - ίδια ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ με το ps1), μόνο αναφέρει αν
        // βρέθηκαν driverpacks (Sum>0) με κουμπί να ανοίξει τον φάκελο αναφοράς για χειροκίνητο έλεγχο.
        // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "η ενημέρωση των οδηγών συστήματος να μην γίνεται αυτόματα") -
        // αφαιρέθηκε το -autoupdate flag που το ps1 original είχε προσθέσει (βλ. σχόλιο ps1 ~12203):
        // η επίσημη τεκμηρίωση του SDI το περιγράφει ως "starts downloading automatically" και σε
        // περαιτέρω έρευνα ενδέχεται να κατεβάζει/εγκαθιστά ΚΑΙ τα ίδια τα driverpacks, όχι μόνο τον
        // τοπικό index - αντίκειτο στο "ΚΑΝΕΝΑ αυτόματο install" που ισχυριζόταν το UI. Χωρίς το flag,
        // το SDIO τρέχει αυστηρά scan-only (συγκρίνει έναντι ΚΕΝΟΥ τοπικού index, άρα πάντα Sum:0/
        // HasDriverPacks=false) - καμία λήψη/εγκατάσταση καμίας μορφής χωρίς ρητή ενέργεια χρήστη.
        public static async Task<VendorScanResult> ScanVendorSourcesAsync()
        {
            const string script = @"
$ErrorActionPreference = 'Continue'
$installed = @()
try {
    $installed = Get-CimInstance Win32_PnPSignedDriver -ErrorAction Stop |
        Where-Object { $_.DeviceName -and $_.DriverVersion } |
        Select-Object DeviceName, DriverVersion, DriverDate
} catch {}

$amdResults = @()
try {
    $amdGpus = Get-CimInstance Win32_VideoController -ErrorAction Stop | Where-Object { $_.Name -match 'AMD|Radeon' }
    foreach ($gpu in $amdGpus) {
        $gpuName = $gpu.Name
        $slugFamily = $null; $slugSeries = $null
        if ($gpuName -match 'RX\s*9\d{3}') { $slugFamily = 'radeon-rx'; $slugSeries = 'radeon-rx-9000-series' }
        elseif ($gpuName -match 'RX\s*7\d{3}') { $slugFamily = 'radeon-rx'; $slugSeries = 'radeon-rx-7000-series' }
        elseif ($gpuName -match 'RX\s*6\d{3}') { $slugFamily = 'radeon-rx'; $slugSeries = 'radeon-rx-6000-series' }
        elseif ($gpuName -match 'RX\s*5\d{3}') { $slugFamily = 'radeon-rx'; $slugSeries = 'radeon-rx-5000-series' }
        elseif ($gpuName -match 'RX\s*Vega') { $slugFamily = 'radeon-rx'; $slugSeries = 'radeon-rx-vega-series' }
        if (-not $slugFamily) { continue }
        $modelSlug = ($gpuName -replace '[™®()]', '').Trim()
        $modelSlug = ($modelSlug -replace '\s+', '-').ToLower()
        if ($modelSlug -notmatch '^amd-') { $modelSlug = ""amd-$modelSlug"" }
        $amdUrl = ""https://www.amd.com/en/support/downloads/drivers.html/graphics/$slugFamily/$slugSeries/$modelSlug.html""
        try {
            $amdResp = Invoke-WebRequest -Uri $amdUrl -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
            $amdHtml = $amdResp.Content
            $verMatch = [regex]::Match($amdHtml, 'amd-software-adrenalin-edition-([\d.]+)-[^""]*\.exe')
            $dateMatch = [regex]::Match($amdHtml, '<strong>\s*Release Date\s*</strong>\s*<p>\s*([\d-]+)\s*</p>')
            if ($verMatch.Success) {
                $installedGpuDrv = $installed | Where-Object { $_.DeviceName -eq $gpuName } | Select-Object -First 1
                $installedDateStr = $null
                if ($installedGpuDrv -and $installedGpuDrv.DriverDate) {
                    try { $installedDateStr = ([datetime]$installedGpuDrv.DriverDate).ToString('yyyy-MM-dd') } catch {}
                }
                $amdResults += [PSCustomObject]@{
                    GpuName = $gpuName
                    LatestVersion = $verMatch.Groups[1].Value
                    LatestReleaseDate = if ($dateMatch.Success) { $dateMatch.Groups[1].Value } else { $null }
                    InstalledVersion = if ($installedGpuDrv) { $installedGpuDrv.DriverVersion } else { $null }
                    InstalledDate = $installedDateStr
                    DownloadUrl = $amdUrl
                }
            }
        } catch {}
    }
} catch {}

$nvidiaResults = @()
try {
    $nvidiaGpus = Get-CimInstance Win32_VideoController -ErrorAction Stop | Where-Object { $_.Name -match 'NVIDIA|GeForce|Quadro|RTX|GTX' }
    if ($nvidiaGpus) {
        $osId = if ([Environment]::OSVersion.Version.Build -ge 22000) { 135 } else { 57 }
        $gpuListResp = Invoke-WebRequest -Uri 'https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=3' -UseBasicParsing -TimeoutSec 20 -ErrorAction Stop
        [xml]$gpuListXml = $gpuListResp.Content
        $allGpuEntries = @($gpuListXml.LookupValueSearch.LookupValues.LookupValue)
        foreach ($gpu in $nvidiaGpus) {
            $gpuName = $gpu.Name
            $cleanName = $gpuName -replace '^NVIDIA\s+', ''
            $cleanName = $cleanName -replace '\s+with Max-Q Design', ''
            $cleanName = $cleanName -replace '\s*\(OEM\)', ''
            $cleanName = $cleanName -replace '\s+\d+GB$', ''
            $cleanName = $cleanName -replace 'Super', 'SUPER'
            $cleanName = $cleanName.Trim()
            $gpuEntry = $allGpuEntries | Where-Object { $_.Name -eq $cleanName } | Select-Object -First 1
            if (-not $gpuEntry) { continue }
            $pfid = $gpuEntry.Value
            try {
                $lookupUrl = ""https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&pfid=$pfid&osID=$osId&upCRD=0&dch=1""
                $nvResp = Invoke-WebRequest -Uri $lookupUrl -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
                $nvJson = $nvResp.Content | ConvertFrom-Json -ErrorAction Stop
                if ($nvJson.Success -eq 1 -and $nvJson.IDS -and $nvJson.IDS.Count -gt 0) {
                    $dlInfo = $nvJson.IDS[0].downloadInfo
                    $latestDateStr = $null
                    try { $latestDateStr = ([datetime]::Parse($dlInfo.ReleaseDateTime)).ToString('yyyy-MM-dd') } catch {}
                    $installedGpuDrvN = $installed | Where-Object { $_.DeviceName -eq $gpuName } | Select-Object -First 1
                    $installedDateStrN = $null
                    if ($installedGpuDrvN -and $installedGpuDrvN.DriverDate) {
                        try { $installedDateStrN = ([datetime]$installedGpuDrvN.DriverDate).ToString('yyyy-MM-dd') } catch {}
                    }
                    $nvidiaResults += [PSCustomObject]@{
                        GpuName = $gpuName
                        LatestVersion = $dlInfo.Version
                        LatestReleaseDate = $latestDateStr
                        InstalledVersion = if ($installedGpuDrvN) { $installedGpuDrvN.DriverVersion } else { $null }
                        InstalledDate = $installedDateStrN
                        DownloadUrl = $dlInfo.DownloadURL
                    }
                }
            } catch {}
        }
    }
} catch {}

$dellResult = $null
try {
    $cs = Get-CimInstance Win32_ComputerSystem -ErrorAction Stop
    if ($cs.Manufacturer -match 'Dell') {
        $progFilesBase = if ([Environment]::Is64BitOperatingSystem) { ${env:ProgramFiles(x86)} } else { $env:ProgramFiles }
        $dcuPath = Join-Path -Path $progFilesBase -ChildPath 'Dell\CommandUpdate\dcu-cli.exe'
        if (Test-Path $dcuPath) {
            $dcuReportDir = Join-Path $env:TEMP ""OptimizerDCU_$([guid]::NewGuid().ToString('N'))""
            $dcuReportPath = Join-Path $dcuReportDir 'DCUApplicableUpdates.xml'
            try {
                Start-Process -FilePath $dcuPath -ArgumentList ""/scan -report=`""$dcuReportDir`"""" -Wait -WindowStyle Hidden -ErrorAction Stop
                if (Test-Path $dcuReportPath) {
                    [xml]$dcuXml = Get-Content -Path $dcuReportPath -Raw -ErrorAction Stop
                    $dcuUpdates = @($dcuXml.updates.update | Select-Object -Property name, version, date, urgency, type, category)
                    $dellResult = [PSCustomObject]@{ Installed = $true; Updates = $dcuUpdates; Error = $null }
                } else {
                    $dellResult = [PSCustomObject]@{ Installed = $true; Updates = @(); Error = $null }
                }
            } catch {
                $dellResult = [PSCustomObject]@{ Installed = $true; Updates = @(); Error = $_.Exception.Message }
            } finally {
                if (Test-Path $dcuReportDir) { Remove-Item -Path $dcuReportDir -Recurse -Force -ErrorAction SilentlyContinue }
            }
        } else {
            $dellResult = [PSCustomObject]@{ Installed = $false; Updates = @(); Error = $null }
        }
    }
} catch {}

$sdioResult = $null
try {
    $sdioLinksPath = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Links\sdio.exe'
    $sdioPath = $null
    if (Test-Path $sdioLinksPath) { $sdioPath = $sdioLinksPath }
    else { $sdioCmd = Get-Command 'sdio.exe' -ErrorAction SilentlyContinue; if ($sdioCmd) { $sdioPath = $sdioCmd.Source } }
    if ($sdioPath) {
        $sdioOutDir = Join-Path $env:TEMP ""OptimizerSDIO_$([guid]::NewGuid().ToString('N'))""
        New-Item -ItemType Directory -Path $sdioOutDir -Force | Out-Null
        try {
            $sdioArgs = ""-nogui -autoclose -nostamp -output_dir:`""$sdioOutDir`"" -log_dir:`""$sdioOutDir`""""
            $sdioProc = Start-Process -FilePath $sdioPath -ArgumentList $sdioArgs -WindowStyle Hidden -PassThru -ErrorAction Stop
            $sdioFinished = $sdioProc.WaitForExit(600000)
            if (-not $sdioFinished) { try { $sdioProc.Kill() } catch {} }
            $sdioReportFiles = @(Get-ChildItem -Path $sdioOutDir -Filter '*.txt' -Recurse -ErrorAction SilentlyContinue)
            $sdioHasDriverPacks = $false
            $sdioLogFile = $sdioReportFiles | Where-Object { $_.Name -eq 'log.txt' } | Select-Object -First 1
            if ($sdioLogFile) {
                try {
                    $sdioLogText = [System.IO.File]::ReadAllText($sdioLogFile.FullName, [System.Text.Encoding]::Default)
                    $sdioSumMatch = [regex]::Match($sdioLogText, 'Driverpacks[\s\S]{0,200}?Sum:\s*(\d+)')
                    if ($sdioSumMatch.Success -and [int]$sdioSumMatch.Groups[1].Value -gt 0) { $sdioHasDriverPacks = $true }
                } catch {}
            }
            $sdioResult = [PSCustomObject]@{
                Installed = $true
                TimedOut = (-not $sdioFinished)
                ReportFound = ($sdioReportFiles.Count -gt 0)
                HasDriverPacks = $sdioHasDriverPacks
                ReportPath = if ($sdioReportFiles.Count -gt 0) { $sdioOutDir } else { $null }
                Error = $null
            }
            if ($sdioReportFiles.Count -eq 0) { Remove-Item -Path $sdioOutDir -Recurse -Force -ErrorAction SilentlyContinue }
        } catch {
            $sdioResult = [PSCustomObject]@{ Installed = $true; TimedOut = $false; ReportFound = $false; HasDriverPacks = $false; ReportPath = $null; Error = $_.Exception.Message }
        }
    } else {
        $sdioResult = [PSCustomObject]@{ Installed = $false; TimedOut = $false; ReportFound = $false; HasDriverPacks = $false; ReportPath = $null; Error = $null }
    }
} catch {}

$oemResult = $null
try {
    $csOem = Get-CimInstance Win32_ComputerSystem -ErrorAction Stop
    if ($csOem.Manufacturer -match 'Lenovo') {
        # ΝΕΟ (πρόσθετη πηγή - βλ. HANDOFF.md, jantari/LSUClient): ο επίσημος κατάλογος Lenovo ανά
        # μοντέλο (MTM code, τα πρώτα 4 χαρακτήρες του Model) - ΜΟΝΟ το ΠΡΩΤΟ, ελαφρύ XML ευρετήριο
        # (μετράει πακέτα), ΧΩΡΙΣ τις δεκάδες επιπλέον HTTP κλήσεις ανά πακέτο (τεκμηριωμένα αργό/
        # εύθραυστο αν γίνει πλήρες parsing - ΣΚΟΠΙΜΑ εκτός εμβέλειας εδώ, ίδια σύνεση με το ps1 original).
        $lenovoPkgCount = $null
        try {
            $mtm = $csOem.Model.Substring(0, 4)
            $lenovoResp = Invoke-WebRequest -Uri ""https://download.lenovo.com/catalog/${mtm}_Win11.xml"" -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
            $bom = [System.Text.Encoding]::UTF8.GetString(@(239,187,191))
            $lenovoContent = $lenovoResp.Content -replace ""^$bom"", ''
            [xml]$lenovoXml = $lenovoContent
            $lenovoPkgCount = @($lenovoXml.packages.package).Count
        } catch {}
        $oemResult = [PSCustomObject]@{ OemName = 'Lenovo'; LenovoPackageCount = $lenovoPkgCount }
    }
    elseif ($csOem.Manufacturer -match 'HP|Hewlett-Packard') { $oemResult = [PSCustomObject]@{ OemName = 'HP'; LenovoPackageCount = $null } }
} catch {}

[PSCustomObject]@{ AmdResults = @($amdResults); NvidiaResults = @($nvidiaResults); DellResult = $dellResult; SdioResult = $sdioResult; OemResult = $oemResult } | ConvertTo-Json -Depth 6 -Compress
";
            var output = await RunPowerShellScriptAsync(script, TimeSpan.FromMinutes(12));
            var empty = new VendorScanResult(Array.Empty<AmdDriverInfo>(), Array.Empty<NvidiaDriverInfo>(), null, null, null);
            if (string.IsNullOrWhiteSpace(output)) return empty;

            try
            {
                var root = JsonDocument.Parse(output).RootElement;

                var amd = new List<AmdDriverInfo>();
                foreach (var a in root.GetProperty("AmdResults").EnumerateArray())
                    amd.Add(new AmdDriverInfo(
                        a.GetProperty("GpuName").GetString() ?? "", a.GetProperty("LatestVersion").GetString() ?? "",
                        a.TryGetProperty("LatestReleaseDate", out var lrd) ? lrd.GetString() : null,
                        a.TryGetProperty("InstalledVersion", out var iv) ? iv.GetString() : null,
                        a.TryGetProperty("InstalledDate", out var id) ? id.GetString() : null,
                        a.GetProperty("DownloadUrl").GetString() ?? ""));

                var nvidia = new List<NvidiaDriverInfo>();
                foreach (var n in root.GetProperty("NvidiaResults").EnumerateArray())
                    nvidia.Add(new NvidiaDriverInfo(
                        n.GetProperty("GpuName").GetString() ?? "", n.GetProperty("LatestVersion").GetString() ?? "",
                        n.TryGetProperty("LatestReleaseDate", out var nlrd) ? nlrd.GetString() : null,
                        n.TryGetProperty("InstalledVersion", out var niv) ? niv.GetString() : null,
                        n.TryGetProperty("InstalledDate", out var nid) ? nid.GetString() : null,
                        n.GetProperty("DownloadUrl").GetString() ?? ""));

                DellScanResult? dell = null;
                if (root.TryGetProperty("DellResult", out var dr) && dr.ValueKind != JsonValueKind.Null)
                {
                    var dellUpdates = new List<DellUpdateInfo>();
                    if (dr.TryGetProperty("Updates", out var du) && du.ValueKind == JsonValueKind.Array)
                        foreach (var u in du.EnumerateArray())
                            dellUpdates.Add(new DellUpdateInfo(
                                u.TryGetProperty("name", out var n2) ? n2.GetString() : null,
                                u.TryGetProperty("version", out var v2) ? v2.GetString() : null,
                                u.TryGetProperty("date", out var d2) ? d2.GetString() : null,
                                u.TryGetProperty("urgency", out var ur2) ? ur2.GetString() : null,
                                u.TryGetProperty("type", out var t2) ? t2.GetString() : null,
                                u.TryGetProperty("category", out var c2) ? c2.GetString() : null));
                    dell = new DellScanResult(dr.GetProperty("Installed").GetBoolean(), dellUpdates,
                        dr.TryGetProperty("Error", out var de) ? de.GetString() : null);
                }

                SdioScanResult? sdio = null;
                if (root.TryGetProperty("SdioResult", out var sr) && sr.ValueKind != JsonValueKind.Null)
                    sdio = new SdioScanResult(
                        sr.GetProperty("Installed").GetBoolean(),
                        sr.TryGetProperty("TimedOut", out var to) && to.GetBoolean(),
                        sr.TryGetProperty("ReportFound", out var rf) && rf.GetBoolean(),
                        sr.TryGetProperty("HasDriverPacks", out var hdp) && hdp.GetBoolean(),
                        sr.TryGetProperty("ReportPath", out var rp) ? rp.GetString() : null,
                        sr.TryGetProperty("Error", out var se) ? se.GetString() : null);

                OemInfo? oem = null;
                if (root.TryGetProperty("OemResult", out var or) && or.ValueKind != JsonValueKind.Null)
                    oem = new OemInfo(
                        or.GetProperty("OemName").GetString() ?? "",
                        or.TryGetProperty("LenovoPackageCount", out var lpc) && lpc.ValueKind == JsonValueKind.Number ? lpc.GetInt32() : null);

                return new VendorScanResult(amd, nvidia, dell, sdio, oem);
            }
            catch (JsonException)
            {
                return empty;
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
