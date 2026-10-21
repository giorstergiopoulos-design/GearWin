using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public sealed record SelfUpdateInfo(Version Version, string Tag, string Notes, string ReleaseUrl, string AssetName, string AssetUrl, long Size, string? Sha256);

    // Ενημέρωση της ίδιας της εφαρμογής από τα GitHub Releases (ρητό αίτημα χρήστη: κουμπί ενημέρωσης από το GitHub, στις Ρυθμίσεις).
    // ΑΣΦΑΛΕΙΑ (η εφαρμογή τρέχει ως Administrator και θα εκτελέσει το αρχείο που κατεβάζει):
    //  - μόνο HTTPS και μόνο URL της μορφής https://github.com/<owner>/<repo>/releases/download/... (άλλος host/path απορρίπτεται)·
    //  - το installer πρέπει να έχει SHA-256 digest στο GitHub API και το κατεβασμένο αρχείο πρέπει να ταιριάζει ΑΚΡΙΒΩΣ - αλλιώς
    //    διαγράφεται και δεν εκτελείται ποτέ (χωρίς digest ανοίγει μόνο η σελίδα της έκδοσης στον browser)·
    //  - το αρχείο κατεβαίνει στο %ProgramData%\GearWin\Update (ACL μόνο-admin εγγραφής, βλ. SecureStorage) ώστε να μην μπορεί να
    //    αντικατασταθεί από μη-elevated κώδικα ανάμεσα στον έλεγχο και την εκτέλεση·
    //  - πάντα με ρητή επιβεβαίωση του χρήστη (βλ. AppearanceSettingsWindow).
    public static class SelfUpdateService
    {
        public const string Owner = "giorstergiopoulos-design";
        public const string Repo = "GearWin";
        private static readonly string DownloadPrefix = $"https://github.com/{Owner}/{Repo}/releases/download/";
        private static readonly Regex InstallerName = new(@"^GearWin-Setup-\d+\.\d+\.\d+(\.\d+)?\.exe$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("GearWin-SelfUpdate/1.0");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return c;
        }

        public static Version Current { get; } = ReadCurrent();

        private static Version ReadCurrent()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? new Version(0, 0, 0) : new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }

        public static bool TryParseTag(string? tag, out Version version)
        {
            version = new Version(0, 0, 0);
            if (string.IsNullOrWhiteSpace(tag)) return false;
            var t = tag.Trim().TrimStart('v', 'V');
            if (!Version.TryParse(t, out var v) || v.Build < 0) return false;
            version = new Version(v.Major, v.Minor, v.Build);
            return true;
        }

        public static bool IsNewer(Version candidate, Version current) => candidate > current;

        public static bool IsTrustedDownloadUrl(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
            && url!.StartsWith(DownloadPrefix, StringComparison.Ordinal) && u.Host == "github.com" && string.IsNullOrEmpty(u.UserInfo);

        // "sha256:ABC..." -> "abc..." (64 hex) ή null.
        public static string? ParseDigest(string? digest)
        {
            if (string.IsNullOrWhiteSpace(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return null;
            var hex = digest[7..].Trim().ToLowerInvariant();
            return Regex.IsMatch(hex, "^[0-9a-f]{64}$") ? hex : null;
        }

        // Καθαρή συνάρτηση (δοκιμάζεται χωρίς δίκτυο): JSON του releases/latest -> πληροφορίες ή null αν δεν υπάρχει έγκυρο installer.
        public static SelfUpdateInfo? ParseRelease(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("tag_name", out var tagEl) || !TryParseTag(tagEl.GetString(), out var version)) return null;
            if (root.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True) return null;
            if (root.TryGetProperty("prerelease", out var p) && p.ValueKind == JsonValueKind.True) return null;
            var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            var page = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : $"https://github.com/{Owner}/{Repo}/releases";
            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var url = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                if (!InstallerName.IsMatch(name) || !IsTrustedDownloadUrl(url)) continue;
                var size = a.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz) ? sz : 0;
                var digest = ParseDigest(a.TryGetProperty("digest", out var dg) ? dg.GetString() : null);
                return new SelfUpdateInfo(version, tagEl.GetString()!, notes, page, name, url!, size, digest);
            }
            return null;
        }

        // (Info, Error): Info = νεότερη έκδοση ή null αν είστε ενημερωμένοι· Error μη-κενό αν ο έλεγχος απέτυχε.
        public static async Task<(SelfUpdateInfo? Info, string? Error)> CheckAsync(CancellationToken ct = default)
        {
            try
            {
                using var resp = await Http.GetAsync($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest", ct);
                if (!resp.IsSuccessStatusCode) return (null, $"HTTP {(int)resp.StatusCode}");
                var info = ParseRelease(await resp.Content.ReadAsStringAsync(ct));
                return info != null && IsNewer(info.Version, Current) ? (info, null) : (null, null);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
            {
                return (null, ex.Message);
            }
        }

        public static string UpdateDir => Path.Combine(SecureStorage.RootDir, "Update");

        // Κατεβάζει στον προστατευμένο φάκελο και επιστρέφει τη διαδρομή ΜΟΝΟ αν το SHA-256 ταιριάζει. Εξαίρεση σε κάθε άλλη περίπτωση.
        public static async Task<string> DownloadAsync(SelfUpdateInfo info, IProgress<double>? progress = null, CancellationToken ct = default)
        {
            if (info.Sha256 == null) throw new InvalidOperationException("no-digest");
            if (!IsTrustedDownloadUrl(info.AssetUrl)) throw new InvalidOperationException("untrusted-url");
            SecureStorage.SecureDirectory(UpdateDir);
            foreach (var old in Directory.GetFiles(UpdateDir)) { try { File.Delete(old); } catch { } }
            var path = Path.Combine(UpdateDir, info.AssetName);

            using var resp = await Http.GetAsync(info.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? info.Size;
            using var sha = SHA256.Create();
            await using (var src = await resp.Content.ReadAsStreamAsync(ct))
            await using (var dst = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long read = 0;
                int n;
                while ((n = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                    sha.TransformBlock(buffer, 0, n, null, 0);
                    read += n;
                    if (total > 0) progress?.Report(Math.Min(1.0, read / (double)total));
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            }
            var actual = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
            if (actual != info.Sha256)
            {
                try { File.Delete(path); } catch { }
                throw new InvalidDataException("hash-mismatch");
            }
            return path;
        }

        // Ξεκινά κρυφό PowerShell που περιμένει να κλείσει η εφαρμογή, τρέχει σιωπηλά τον installer και μετά ξανανοίγει την εφαρμογή.
        // (Ο installer έχει AppMutex - δεν μπορεί να συνεχίσει όσο τρέχει η εφαρμογή.) Μετά το κάλεσμα η εφαρμογή πρέπει να τερματίσει.
        public static void LaunchInstallerAfterExit(string installerPath)
        {
            var exe = Environment.ProcessPath ?? "";
            static string Q(string s) => "'" + s.Replace("'", "''") + "'";
            var script = new StringBuilder()
                .AppendLine("$ErrorActionPreference = 'Stop'")
                .AppendLine($"try {{ Wait-Process -Id {Environment.ProcessId} -Timeout 90 -ErrorAction SilentlyContinue }} catch {{ }}")
                .AppendLine($"$p = Start-Process -FilePath {Q(installerPath)} -ArgumentList '/SILENT','/SUPPRESSMSGBOXES','/NORESTART','/CLOSEAPPLICATIONS' -Wait -PassThru")
                .AppendLine($"if ($p.ExitCode -eq 0 -and (Test-Path -LiteralPath {Q(exe)})) {{ Start-Process -FilePath {Q(exe)} }}")
                .ToString();
            Process.Start(new ProcessStartInfo("powershell.exe", PowerShellArgs.Command(script, hidden: true)) { UseShellExecute = false, CreateNoWindow = true });
        }
    }
}
