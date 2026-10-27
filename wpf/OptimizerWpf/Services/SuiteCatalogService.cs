using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public sealed record SuiteApp(
        string Id, string Name, string Repo, string InstallerAssetPattern, string? ChecksumAsset, string UninstallDisplayName,
        IReadOnlyList<string> Paths, IReadOnlyDictionary<string, string> Tagline, IReadOnlyDictionary<string, IReadOnlyList<string>> Capabilities)
    {
        public string RepoUrl => $"https://github.com/{SuiteCatalogService.Owner}/{Repo}";
        public string ReleasesUrl => RepoUrl + "/releases";

        public string TaglineFor(string lang) => Tagline.TryGetValue(lang, out var t) ? t : Tagline.TryGetValue("en", out var e) ? e : "";

        public IReadOnlyList<string> CapabilitiesFor(string lang) =>
            Capabilities.TryGetValue(lang, out var c) && c.Count > 0 ? c : Capabilities.TryGetValue("en", out var e) ? e : Array.Empty<string>();
    }

    public sealed record SuiteCatalog(int Schema, string Updated, IReadOnlyList<SuiteApp> Apps);

    // Κατάλογος της σουίτας εφαρμογών (suite/apps.json στο repo Opsis). Κάθε εφαρμογή κρατά ένα ενσωματωμένο αντίγραφο ως fallback,
    // κατεβάζει το ζωντανό αρχείο το πολύ μία φορά το 24ωρο (timeout 5 s, σιωπηλή αποτυχία), το επικυρώνει αυστηρά και κρατά cache στον
    // φάκελο δεδομένων. Επικύρωση: μόνο https, μόνο github.com / raw.githubusercontent.com / api.github.com, μέγιστο μέγεθος 256 KB,
    // άγνωστα πεδία αγνοούνται, ο owner πρέπει να είναι ο γνωστός λογαριασμός (ώστε ένας αλλοιωμένος κατάλογος να μην οδηγεί σε ξένο repo).
    public static class SuiteCatalogService
    {
        public const string Owner = "giorstergiopoulos-design";
        public const string CatalogUrl = "https://raw.githubusercontent.com/giorstergiopoulos-design/Opsis/master/suite/apps.json";
        public const int MaxBytes = 256 * 1024;
        public const int MaxApps = 24;
        public static readonly string[] AllowedHosts = { "github.com", "raw.githubusercontent.com", "api.github.com" };
        // τα GitHub release assets ανακατευθύνονται εδώ (ο έλεγχος SHA-256 ισχύει ούτως ή άλλως)
        private static readonly string[] RedirectHosts = { "objects.githubusercontent.com", "release-assets.githubusercontent.com" };

        private static readonly Regex IdRegex = new("^[a-z0-9][a-z0-9_-]{0,31}$", RegexOptions.Compiled);
        private static readonly Regex RepoRegex = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$", RegexOptions.Compiled);
        private static readonly Regex AssetNameRegex = new(@"^[A-Za-z0-9][A-Za-z0-9._ -]{0,119}$", RegexOptions.Compiled);

        public static bool IsAllowedUrl(string? url, bool allowRedirectHosts = false)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
            if (u.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(u.UserInfo) || !u.IsDefaultPort) return false;
            var host = u.Host.ToLowerInvariant();
            return AllowedHosts.Contains(host) || (allowRedirectHosts && RedirectHosts.Contains(host));
        }

        // Καθαρή συνάρτηση (δοκιμάζεται χωρίς δίκτυο). Επιστρέφει null + λόγο όταν το JSON δεν είναι αποδεκτό· εφαρμογές με
        // κακά πεδία παραλείπονται μία-μία (ένα κακό στοιχείο δεν ακυρώνει όλο τον κατάλογο), αλλά αν δεν μείνει καμία → απόρριψη.
        public static SuiteCatalog? Parse(string? json, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(json)) { error = "empty"; return null; }
            if (Encoding.UTF8.GetByteCount(json) > MaxBytes) { error = "too-large"; return null; }
            try
            {
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16, AllowTrailingCommas = true });
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) { error = "not-object"; return null; }
                if (!root.TryGetProperty("schema", out var sch) || sch.ValueKind != JsonValueKind.Number || !sch.TryGetInt32(out var schema) || schema != 1)
                { error = "schema"; return null; }
                var owner = Str(root, "owner");
                if (owner != null && !string.Equals(owner, Owner, StringComparison.Ordinal)) { error = "owner"; return null; }
                if (!root.TryGetProperty("apps", out var apps) || apps.ValueKind != JsonValueKind.Array) { error = "apps"; return null; }
                var list = new List<SuiteApp>();
                var ids = new HashSet<string>();
                foreach (var a in apps.EnumerateArray())
                {
                    if (list.Count >= MaxApps) break;
                    var app = ParseApp(a);
                    if (app != null && ids.Add(app.Id)) list.Add(app);
                }
                if (list.Count == 0) { error = "no-valid-apps"; return null; }
                return new SuiteCatalog(schema, Str(root, "updated") ?? "", list);
            }
            catch (JsonException) { error = "malformed"; return null; }
        }

        private static string? Str(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static SuiteApp? ParseApp(JsonElement a)
        {
            if (a.ValueKind != JsonValueKind.Object) return null;
            var id = Str(a, "id"); var name = Str(a, "name"); var repo = Str(a, "repo");
            var pattern = Str(a, "installerAsset"); var display = Str(a, "uninstallDisplayName");
            if (id == null || !IdRegex.IsMatch(id)) return null;
            if (string.IsNullOrWhiteSpace(name) || name.Length > 60) return null;
            if (repo == null || !RepoRegex.IsMatch(repo) || repo is "." or "..") return null;
            if (pattern == null || pattern.Length > 120) return null;
            try { _ = new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50)); } catch (ArgumentException) { return null; }
            var checksum = Str(a, "checksumAsset");
            if (checksum != null && !AssetNameRegex.IsMatch(checksum)) return null;
            var paths = new List<string>();
            if (a.TryGetProperty("paths", out var pa) && pa.ValueKind == JsonValueKind.Array)
                foreach (var p in pa.EnumerateArray())
                    if (p.ValueKind == JsonValueKind.String && p.GetString() is { Length: > 0 and <= 260 } s && !s.Contains("..")) paths.Add(s);
            return new SuiteApp(id, name.Trim(), repo, pattern, checksum, (display ?? name).Trim(), paths,
                LocalizedString(a, "tagline"), LocalizedList(a, "capabilities"));
        }

        private static Dictionary<string, string> LocalizedString(JsonElement a, string prop)
        {
            var d = new Dictionary<string, string>();
            if (a.TryGetProperty(prop, out var t) && t.ValueKind == JsonValueKind.Object)
                foreach (var kv in t.EnumerateObject())
                    if (kv.Value.ValueKind == JsonValueKind.String && kv.Name.Length <= 8 && kv.Value.GetString() is { Length: > 0 and <= 200 } s) d[kv.Name] = s;
            return d;
        }

        private static Dictionary<string, IReadOnlyList<string>> LocalizedList(JsonElement a, string prop)
        {
            var d = new Dictionary<string, IReadOnlyList<string>>();
            if (a.TryGetProperty(prop, out var t) && t.ValueKind == JsonValueKind.Object)
                foreach (var kv in t.EnumerateObject())
                {
                    if (kv.Value.ValueKind != JsonValueKind.Array || kv.Name.Length > 8) continue;
                    var items = kv.Value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!)
                        .Where(s => s.Length is > 0 and <= 240).Take(8).ToList();
                    if (items.Count > 0) d[kv.Name] = items;
                }
            return d;
        }

        public static SuiteCatalog LoadEmbedded()
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("suite-apps.json")
                ?? throw new InvalidOperationException("suite-apps.json resource missing");
            using var r = new StreamReader(s, Encoding.UTF8);
            return Parse(r.ReadToEnd(), out var err) ?? throw new InvalidOperationException("embedded catalogue invalid: " + err);
        }

        private static string CachePath(string dir) => Path.Combine(dir, "suite-apps.cache.json");

        // Επιστρέφει τον πιο πρόσφατο έγκυρο κατάλογο (ενσωματωμένος / cache / ζωντανός, με βάση το πεδίο "updated"· σε ισοπαλία κερδίζει ο
        // ενσωματωμένος που έχει ελεγχθεί κατά το build). `fetched` = true αν έγινε πραγματική λήψη τώρα.
        public static async Task<(SuiteCatalog Catalog, bool Live, string Source)> LoadAsync(
            string? cacheDir = null, HttpMessageHandler? handler = null, DateTime? now = null, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            cacheDir ??= OncePerVersionStore.AppDataDir();
            var clock = now ?? DateTime.UtcNow;
            var best = LoadEmbedded();
            var source = "embedded";

            SuiteCatalog? cached = null;
            var cachePath = CachePath(cacheDir);
            try
            {
                if (File.Exists(cachePath) && new FileInfo(cachePath).Length <= MaxBytes)
                    cached = Parse(File.ReadAllText(cachePath), out _);
            }
            catch { }
            if (cached != null && string.CompareOrdinal(cached.Updated, best.Updated) > 0) { best = cached; source = "cache"; }

            var fresh = false;
            try { fresh = File.Exists(cachePath) && clock - File.GetLastWriteTimeUtc(cachePath) < TimeSpan.FromHours(24); } catch { }
            if (fresh) return (best, false, source);

            var json = await FetchAsync(CatalogUrl, handler, timeout ?? TimeSpan.FromSeconds(5), ct);
            if (json != null)
            {
                var live = Parse(json, out _);
                if (live != null)
                {
                    try { Directory.CreateDirectory(cacheDir); File.WriteAllText(cachePath, json); } catch { }
                    if (string.CompareOrdinal(live.Updated, best.Updated) > 0) { best = live; source = "live"; }
                    return (best, true, source);
                }
            }
            // αποτυχία λήψης: ξανά προσπάθεια όχι νωρίτερα από την επόμενη ημέρα δεν χρειάζεται - η επόμενη εκκίνηση του παραθύρου ξαναδοκιμάζει
            return (best, false, source);
        }

        // Μικρό αρχείο κειμένου (π.χ. .sha256 ενός release) από github.com - ίδιο όριο: ως 4 KB.
        public static async Task<string?> FetchSmallTextAsync(string url, HttpMessageHandler? handler, CancellationToken ct = default)
        {
            if (!IsAllowedUrl(url)) return null;
            try
            {
                using var http = handler != null ? new HttpClient(handler, disposeHandler: false) : new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("GearWin-Suite/1.0");
                using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode || resp.Content.Headers.ContentLength is > 4096) return null;
                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                return bytes.Length > 4096 ? null : Encoding.UTF8.GetString(bytes);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException) { return null; }
        }

        // Ασφαλής λήψη: μόνο επιτρεπόμενος host, όριο μεγέθους ήδη κατά τη λήψη, timeout, σιωπηλή αποτυχία (null).
        public static async Task<string?> FetchAsync(string url, HttpMessageHandler? handler, TimeSpan timeout, CancellationToken ct = default)
        {
            if (!IsAllowedUrl(url)) return null;
            try
            {
                using var http = handler != null ? new HttpClient(handler, disposeHandler: false) : new HttpClient();
                http.Timeout = timeout;
                http.DefaultRequestHeaders.UserAgent.ParseAdd("GearWin-Suite/1.0");
                using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode) return null;
                if (resp.Content.Headers.ContentLength is > MaxBytes) return null;
                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var ms = new MemoryStream();
                var buf = new byte[8192];
                int n;
                while ((n = await stream.ReadAsync(buf, ct)) > 0)
                {
                    ms.Write(buf, 0, n);
                    if (ms.Length > MaxBytes) return null;
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException) { return null; }
        }
    }
}
