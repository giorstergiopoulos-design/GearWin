using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public sealed record ReleaseAsset(string Name, string Url, long Size, string? Sha256);

    public sealed record ReleaseData(string Tag, ReleaseVersion Version, string PageUrl, string Notes, IReadOnlyList<ReleaseAsset> Assets, string Json);

    public enum ReleaseFetchStatus { Ok, NotModified, NoStableRelease, Failed }

    public sealed record ReleaseFetchResult(ReleaseFetchStatus Status, ReleaseData? Release, string? ETag);

    // Ελάχιστος πελάτης για GET https://api.github.com/repos/<owner>/<repo>/releases/latest - χωρίς token, με ETag/If-None-Match (το 304
    // δεν μετράει στο όριο του API), timeout, αγνόηση draft/pre-release, και ΜΟΝΟ links που ανήκουν στο συγκεκριμένο repo.
    // Η μόνη αποστολή δεδομένων είναι αυτό το GET (καμία τηλεμετρία).
    public static class GitHubReleaseClient
    {
        public static string ApiUrl(string owner, string repo) => $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

        public static string DownloadPrefix(string owner, string repo) => $"https://github.com/{owner}/{repo}/releases/download/";

        public static bool IsRepoDownloadUrl(string? url, string owner, string repo) =>
            Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.Host == "github.com"
            && string.IsNullOrEmpty(u.UserInfo) && url!.StartsWith(DownloadPrefix(owner, repo), StringComparison.Ordinal);

        // Καθαρή συνάρτηση (δοκιμάζεται χωρίς δίκτυο).
        public static ReleaseData? Parse(string json, string owner, string repo)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return null;
                if (!root.TryGetProperty("tag_name", out var tagEl) || tagEl.ValueKind != JsonValueKind.String) return null;
                var tag = tagEl.GetString()!;
                if (!ReleaseVersion.TryParse(tag, out var version) || version.IsPrerelease) return null;
                if (root.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True) return null;
                if (root.TryGetProperty("prerelease", out var p) && p.ValueKind == JsonValueKind.True) return null;
                var notes = root.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() ?? "" : "";
                var fallbackPage = $"https://github.com/{owner}/{repo}/releases/tag/{Uri.EscapeDataString(tag)}";
                var page = root.TryGetProperty("html_url", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null;
                if (!SuiteCatalogService.IsAllowedUrl(page) || !page!.StartsWith($"https://github.com/{owner}/{repo}/", StringComparison.Ordinal)) page = fallbackPage;
                var assets = new List<ReleaseAsset>();
                if (root.TryGetProperty("assets", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var a in arr.EnumerateArray())
                    {
                        var name = a.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
                        var url = a.TryGetProperty("browser_download_url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
                        if (name.Length == 0 || !IsRepoDownloadUrl(url, owner, repo)) continue;
                        var size = a.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz) ? sz : 0;
                        var digest = SelfUpdateService.ParseDigest(a.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String ? dg.GetString() : null);
                        assets.Add(new ReleaseAsset(name, url!, size, digest));
                    }
                }
                return new ReleaseData(tag, version, page, notes, assets, json);
            }
            catch (JsonException) { return null; }
        }

        public static async Task<ReleaseFetchResult> GetLatestAsync(string owner, string repo, string? etag = null,
            HttpMessageHandler? handler = null, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            try
            {
                using var http = handler != null ? new HttpClient(handler, disposeHandler: false) : new HttpClient();
                http.Timeout = timeout ?? TimeSpan.FromSeconds(8);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("GearWin-Suite/1.0");
                http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                using var req = new HttpRequestMessage(HttpMethod.Get, ApiUrl(owner, repo));
                if (!string.IsNullOrEmpty(etag)) req.Headers.TryAddWithoutValidation("If-None-Match", etag);
                using var resp = await http.SendAsync(req, ct);
                if (resp.StatusCode == HttpStatusCode.NotModified) return new ReleaseFetchResult(ReleaseFetchStatus.NotModified, null, etag);
                if (resp.StatusCode == HttpStatusCode.NotFound) return new ReleaseFetchResult(ReleaseFetchStatus.NoStableRelease, null, null);
                if (!resp.IsSuccessStatusCode) return new ReleaseFetchResult(ReleaseFetchStatus.Failed, null, etag);
                var json = await resp.Content.ReadAsStringAsync(ct);
                if (json.Length > 2 * 1024 * 1024) return new ReleaseFetchResult(ReleaseFetchStatus.Failed, null, etag);
                var data = Parse(json, owner, repo);
                var newTag = resp.Headers.ETag?.ToString();
                return data == null ? new ReleaseFetchResult(ReleaseFetchStatus.NoStableRelease, null, newTag) : new ReleaseFetchResult(ReleaseFetchStatus.Ok, data, newTag);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or InvalidOperationException)
            {
                return new ReleaseFetchResult(ReleaseFetchStatus.Failed, null, etag);
            }
        }
    }
}
