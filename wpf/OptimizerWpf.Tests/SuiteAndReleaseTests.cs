using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests
{
    internal sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _f;
        public List<HttpRequestMessage> Requests { get; } = new();
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> f) => _f = f;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            var r = _f(request);
            r.RequestMessage = request;
            return Task.FromResult(r);
        }
    }

    internal sealed class FakeProbe : IInstallProbe
    {
        public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Versions { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<UninstallEntry> Entries { get; } = new();
        public string Expand(string path) => path.Replace("%LocalAppData%", @"C:\U\AppData\Local").Replace("%ProgramFiles%", @"C:\Program Files").Replace("%AppData%", @"C:\U\AppData\Roaming");
        public bool FileExists(string path) => Files.Contains(path);
        public string? FileVersion(string path) => Versions.TryGetValue(path, out var v) ? v : null;
        public IEnumerable<UninstallEntry> UninstallEntries() => Entries;
    }

    public class ReleaseVersionTests
    {
        [Theory]
        [InlineData("v6.10.0", "v6.9.9", true)]
        [InlineData("6.6.0", "6.5.5", true)]
        [InlineData("6.6.0", "6.6.0-beta.1", true)]
        [InlineData("6.6.0-beta.2", "6.6.0-beta.1", true)]
        [InlineData("6.6.0-beta.10", "6.6.0-beta.9", true)]
        [InlineData("1.2.3.4", "1.2.3", true)]
        [InlineData("6.6", "6.6.0", false)]
        [InlineData("v6.6.0+build5", "6.6.0", false)]
        [InlineData("6.5.5", "6.6.0", false)]
        public void Comparison(string a, string b, bool aIsNewer) =>
            Assert.Equal(aIsNewer, ReleaseVersion.IsNewer(a, b));

        [Theory]
        [InlineData("")] [InlineData("latest")] [InlineData("v")] [InlineData("1..2")] [InlineData("1.2.3.4.5")] [InlineData("1.2.x")] [InlineData("6.6.0-")]
        public void InvalidTagsAreNeverNewer(string tag)
        {
            Assert.False(ReleaseVersion.TryParse(tag, out _));
            Assert.False(ReleaseVersion.IsNewer(tag, "0.0.1"));
        }

        [Fact]
        public void EqualityAndMajorMinor()
        {
            Assert.Equal(ReleaseVersion.ParseOrNull("6.6"), ReleaseVersion.ParseOrNull("v6.6.0"));
            Assert.Equal(ReleaseVersion.ParseOrNull("6.6.9")!.MajorMinor, ReleaseVersion.ParseOrNull("6.6.0")!.MajorMinor);
            Assert.True(ReleaseVersion.ParseOrNull("6.6.0-rc.1")!.IsPrerelease);
        }
    }

    public class OncePerVersionStoreTests
    {
        private static string Temp() => Path.Combine(Path.GetTempPath(), "gwstore_" + Guid.NewGuid().ToString("N"), "s.json");

        [Fact]
        public void ShowsOncePerMajorMinorAndSurvivesRestart()
        {
            var p = Temp();
            var v660 = ReleaseVersion.ParseOrNull("6.6.0")!;
            var s1 = new OncePerVersionStore(p);
            Assert.True(s1.ShouldShow("guide", v660));
            s1.MarkSeen("guide", v660);
            Assert.False(s1.ShouldShow("guide", v660));
            var s2 = new OncePerVersionStore(p);   // "restart"
            Assert.False(s2.ShouldShow("guide", ReleaseVersion.ParseOrNull("6.6.3")!));   // patch: not new
            Assert.True(s2.ShouldShow("guide", ReleaseVersion.ParseOrNull("6.7.0")!));
            Assert.True(s2.ShouldShow("guide", ReleaseVersion.ParseOrNull("7.0.0")!));
        }

        [Fact]
        public void ExactModeTreatsPatchAsNew_AndNeverGoesBackwards()
        {
            var s = new OncePerVersionStore(Temp());
            s.MarkSeen("b", ReleaseVersion.ParseOrNull("6.6.1")!);
            Assert.True(s.ShouldShow("b", ReleaseVersion.ParseOrNull("6.6.2")!, majorMinorOnly: false));
            s.MarkSeen("b", ReleaseVersion.ParseOrNull("6.5.0")!);
            Assert.Equal("6.6.1", s.Get("b"));
        }

        [Fact]
        public void CorruptFileStartsEmpty_AndRawValuesRoundTrip()
        {
            var p = Temp();
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, "{ not json");
            var s = new OncePerVersionStore(p);
            Assert.Null(s.Get("x"));
            s.SetRaw("etag", "W/\"abc\"");
            Assert.Equal("W/\"abc\"", new OncePerVersionStore(p).Get("etag"));
            s.SetRaw("etag", null);
            Assert.Null(new OncePerVersionStore(p).Get("etag"));
        }
    }

    public class SuiteCatalogTests
    {
        private const string Good = """
        { "schema": 1, "updated": "2026-10-23", "owner": "giorstergiopoulos-design", "future": 1, "apps": [
          { "id": "a", "name": "A", "repo": "Aapp", "installerAsset": "^A\\.exe$", "checksumAsset": "A.exe.sha256", "uninstallDisplayName": "A", "paths": ["%LocalAppData%\\A\\A.exe"],
            "unknown": 5, "tagline": {"en":"t","el":"τ"}, "capabilities": {"en":["x","y"]} } ] }
        """;

        [Fact]
        public void ParsesGoodCatalogueAndIgnoresUnknownFields()
        {
            var c = SuiteCatalogService.Parse(Good, out var err);
            Assert.Null(err);
            Assert.Equal("A", c!.Apps.Single().Name);
            Assert.Equal("τ", c.Apps[0].TaglineFor("el"));
            Assert.Equal("t", c.Apps[0].TaglineFor("de"));   // fallback en
            Assert.Equal(2, c.Apps[0].CapabilitiesFor("ja").Count);
        }

        [Fact]
        public void EmbeddedCatalogueIsValidAndComplete()
        {
            var c = SuiteCatalogService.LoadEmbedded();
            Assert.Equal(7, c.Apps.Count);
            Assert.Contains(c.Apps, a => a.Id == "gearwin");
            Assert.All(c.Apps, a => { Assert.NotEmpty(a.Paths); Assert.NotEmpty(a.CapabilitiesFor("en")); Assert.NotEmpty(a.CapabilitiesFor("el")); });
        }

        [Theory]
        [InlineData("")]
        [InlineData("{ broken")]
        [InlineData("[]")]
        [InlineData("{\"schema\":2,\"apps\":[]}")]
        [InlineData("{\"schema\":1,\"apps\":[]}")]
        [InlineData("{\"schema\":1,\"owner\":\"evil\",\"apps\":[{\"id\":\"a\",\"name\":\"A\",\"repo\":\"r\",\"installerAsset\":\"x\"}]}")]
        public void RejectsMalformedOrForeign(string json)
        {
            Assert.Null(SuiteCatalogService.Parse(json, out var err));
            Assert.NotNull(err);
        }

        [Fact]
        public void RejectsOversize()
        {
            var big = Good.Replace("\"t\"", "\"" + new string('x', SuiteCatalogService.MaxBytes + 10) + "\"");
            Assert.Null(SuiteCatalogService.Parse(big, out var err));
            Assert.Equal("too-large", err);
        }

        [Theory]
        [InlineData("../evil")] [InlineData("a/b")] [InlineData("a b")] [InlineData("..")] [InlineData("")]
        public void SkipsAppsWithBadRepo(string repo)
        {
            var json = Good.Replace("\"Aapp\"", System.Text.Json.JsonSerializer.Serialize(repo));
            Assert.Null(SuiteCatalogService.Parse(json, out var err));
            Assert.Equal("no-valid-apps", err);
        }

        [Fact]
        public void SkipsAppWithBadRegexAndKeepsOthers()
        {
            var json = """{ "schema":1, "apps":[ {"id":"bad","name":"B","repo":"r","installerAsset":"([unclosed"}, {"id":"ok","name":"O","repo":"r2","installerAsset":"^o$"} ] }""";
            var c = SuiteCatalogService.Parse(json, out _);
            Assert.Equal("ok", c!.Apps.Single().Id);
        }

        [Theory]
        [InlineData("https://raw.githubusercontent.com/giorstergiopoulos-design/Opsis/master/suite/apps.json", true)]
        [InlineData("https://api.github.com/repos/o/r/releases/latest", true)]
        [InlineData("https://github.com/o/r/releases", true)]
        [InlineData("http://github.com/o/r", false)]
        [InlineData("https://evil.com/github.com", false)]
        [InlineData("https://github.com.evil.com/x", false)]
        [InlineData("https://user:pw@github.com/x", false)]
        [InlineData("https://github.com:8443/x", false)]
        [InlineData("file:///c:/x", false)]
        [InlineData("javascript:alert(1)", false)]
        [InlineData("", false)]
        public void UrlAllowList(string url, bool ok) => Assert.Equal(ok, SuiteCatalogService.IsAllowedUrl(url));

        [Fact]
        public async Task FetchRefusesBadHostAndOversizeAndHttpErrors()
        {
            var h = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', SuiteCatalogService.MaxBytes + 1)) });
            Assert.Null(await SuiteCatalogService.FetchAsync("https://evil.example/apps.json", h, TimeSpan.FromSeconds(2)));
            Assert.Empty(h.Requests);   // no request is even made to a bad host
            Assert.Null(await SuiteCatalogService.FetchAsync(SuiteCatalogService.CatalogUrl, h, TimeSpan.FromSeconds(2)));
            var h404 = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            Assert.Null(await SuiteCatalogService.FetchAsync(SuiteCatalogService.CatalogUrl, h404, TimeSpan.FromSeconds(2)));
        }

        [Fact]
        public async Task LoadPrefersNewerLiveFileCachesIt_AndFallsBackSilently()
        {
            var dir = Path.Combine(Path.GetTempPath(), "gwcat_" + Guid.NewGuid().ToString("N"));
            var live = Good.Replace("2026-10-23", "2099-01-01");
            var h = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(live) });
            var (cat, isLive, source) = await SuiteCatalogService.LoadAsync(dir, h);
            Assert.True(isLive);
            Assert.Equal("live", source);
            Assert.Equal("A", cat.Apps.Single().Name);
            // second call within 24 h: no new request, uses cache
            var h2 = new FakeHandler(_ => throw new InvalidOperationException("must not be called"));
            var (cat2, isLive2, src2) = await SuiteCatalogService.LoadAsync(dir, h2);
            Assert.False(isLive2);
            Assert.Equal("cache", src2);
            Assert.Equal("A", cat2.Apps.Single().Name);
            // failing network and empty cache: embedded
            var (cat3, _, src3) = await SuiteCatalogService.LoadAsync(Path.Combine(dir, "other"), new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));
            Assert.Equal("embedded", src3);
            Assert.Equal(7, cat3.Apps.Count);
            // an older live file never replaces the embedded one
            var old = Good.Replace("2026-10-23", "2020-01-01");
            var (cat4, _, src4) = await SuiteCatalogService.LoadAsync(Path.Combine(dir, "third"), new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(old) }));
            Assert.Equal("embedded", src4);
            Assert.Equal(7, cat4.Apps.Count);
        }
    }

    public class InstalledAppDetectorTests
    {
        private static SuiteApp App(string id, string display, params string[] paths) =>
            new(id, id, id, "^x$", null, display, paths, new Dictionary<string, string>(), new Dictionary<string, IReadOnlyList<string>>());

        [Fact]
        public void DetectsByKnownPathWithFileVersion()
        {
            var p = new FakeProbe();
            p.Files.Add(@"C:\U\AppData\Local\Programs\Kombos\Kombos.exe");
            p.Versions[@"C:\U\AppData\Local\Programs\Kombos\Kombos.exe"] = "1.4.2 (abc)".Split(' ')[0];
            var info = InstalledAppDetector.Detect(App("kombos", "Kombos", @"%LocalAppData%\Programs\Kombos\Kombos.exe"), p);
            Assert.True(info.Installed);
            Assert.Equal(ReleaseVersion.ParseOrNull("1.4.2"), info.Version);
            Assert.EndsWith("Kombos.exe", info.ExePath);
        }

        [Fact]
        public void FallsBackToUninstallEntryAndUsesExactNameOnly()
        {
            var p = new FakeProbe();
            p.Entries.Add(new UninstallEntry("Opsis News Crawler", "0.14.3", null));
            Assert.False(InstalledAppDetector.Detect(App("opsis", "Opsis", @"%ProgramFiles%\Opsis\Opsis.exe"), p).Installed);   // no prefix match
            var crawler = InstalledAppDetector.Detect(App("nc", "Opsis News Crawler"), p);
            Assert.True(crawler.Installed);
            Assert.Equal(ReleaseVersion.ParseOrNull("0.14.3"), crawler.Version);
        }

        [Fact]
        public void NothingInstalled() => Assert.False(InstalledAppDetector.Detect(App("x", "X", @"%ProgramFiles%\X\X.exe"), new FakeProbe()).Installed);

        [Fact]
        public void UnexpandedVariablesAreIgnored()
        {
            var p = new FakeProbe();
            p.Files.Add(@"%UnknownVar%\X.exe");
            Assert.False(InstalledAppDetector.Detect(App("x", "X", @"%UnknownVar%\X.exe"), p).Installed);
        }

        [Fact]
        public void StateMapping()
        {
            var v1 = ReleaseVersion.ParseOrNull("1.0.0"); var v2 = ReleaseVersion.ParseOrNull("1.1.0");
            Assert.Equal(SuiteInstallState.ThisApp, InstalledAppDetector.StateFor(true, new InstalledInfo(false, null, null), v2));
            Assert.Equal(SuiteInstallState.NotInstalled, InstalledAppDetector.StateFor(false, new InstalledInfo(false, null, null), v2));
            Assert.Equal(SuiteInstallState.Installed, InstalledAppDetector.StateFor(false, new InstalledInfo(true, v2, "x"), v2));
            Assert.Equal(SuiteInstallState.Installed, InstalledAppDetector.StateFor(false, new InstalledInfo(true, null, "x"), v2));
            Assert.Equal(SuiteInstallState.UpdateAvailable, InstalledAppDetector.StateFor(false, new InstalledInfo(true, v1, "x"), v2));
        }
    }

    public class ReleaseAndInstallerTests
    {
        private const string Owner = "giorstergiopoulos-design";

        private static string ReleaseJson(string tag, string sha = "", bool prerelease = false, string assetUrl = "", string assetName = "GearWin-Setup-9.9.9.exe")
        {
            var url = assetUrl.Length > 0 ? assetUrl : $"https://github.com/{Owner}/GearWin/releases/download/{tag}/{assetName}";
            var digest = sha.Length > 0 ? $"\"digest\":\"sha256:{sha}\"," : "";
            return $$"""{"tag_name":"{{tag}}","prerelease":{{prerelease.ToString().ToLowerInvariant()}},"draft":false,"html_url":"https://github.com/{{Owner}}/GearWin/releases/tag/{{tag}}","body":"notes","assets":[{"name":"{{assetName}}",{{digest}}"size":5,"browser_download_url":"{{url}}"}]}""";
        }

        [Fact]
        public void ParseIgnoresPrereleasesDraftsAndForeignAssetUrls()
        {
            Assert.Null(GitHubReleaseClient.Parse(ReleaseJson("v9.9.9", prerelease: true), Owner, "GearWin"));
            var r = GitHubReleaseClient.Parse(ReleaseJson("v9.9.9", assetUrl: "https://evil.com/x.exe"), Owner, "GearWin")!;
            Assert.Empty(r.Assets);
            var ok = GitHubReleaseClient.Parse(ReleaseJson("v9.9.9", new string('a', 64)), Owner, "GearWin")!;
            Assert.Equal(new string('a', 64), ok.Assets.Single().Sha256);
            Assert.Null(GitHubReleaseClient.Parse("{\"tag_name\":\"nightly\"}", Owner, "GearWin"));
            Assert.Null(GitHubReleaseClient.Parse("not json", Owner, "GearWin"));
        }

        [Fact]
        public async Task EtagIsSentAndNotModifiedIsHandled()
        {
            var h = new FakeHandler(req =>
            {
                if (req.Headers.TryGetValues("If-None-Match", out var v) && v.Contains("\"e1\"")) return new HttpResponseMessage(HttpStatusCode.NotModified);
                var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ReleaseJson("v9.9.9", new string('b', 64))) };
                resp.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"e1\"");
                return resp;
            });
            var first = await GitHubReleaseClient.GetLatestAsync(Owner, "GearWin", null, h);
            Assert.Equal(ReleaseFetchStatus.Ok, first.Status);
            Assert.Equal("\"e1\"", first.ETag);
            var second = await GitHubReleaseClient.GetLatestAsync(Owner, "GearWin", first.ETag, h);
            Assert.Equal(ReleaseFetchStatus.NotModified, second.Status);
            var fail = await GitHubReleaseClient.GetLatestAsync(Owner, "GearWin", null, new FakeHandler(_ => throw new HttpRequestException("offline")));
            Assert.Equal(ReleaseFetchStatus.Failed, fail.Status);
        }

        [Theory]
        [InlineData("9.9.9", "6.6.0", null, true)]
        [InlineData("9.9.9", "6.6.0", "9.9.9", false)]      // already told about this version
        [InlineData("9.9.9", "6.6.0", "9.0.0", true)]       // told about an older one -> tell again
        [InlineData("6.6.0", "6.6.0", null, false)]
        [InlineData("6.5.5", "6.6.0", null, false)]
        public void OncePerVersionRule(string latest, string current, string? notified, bool expected) =>
            Assert.Equal(expected, ReleaseCheckService.ShouldNotify(ReleaseVersion.ParseOrNull(current)!, ReleaseVersion.ParseOrNull(latest)!, notified));

        [Fact]
        public async Task CheckAsyncRemembersEtagAndUsesStoredTagOn304()
        {
            var store = new OncePerVersionStore(Path.Combine(Path.GetTempPath(), "gwrc_" + Guid.NewGuid().ToString("N"), "s.json"));
            var h = new FakeHandler(req =>
            {
                if (req.Headers.Contains("If-None-Match")) return new HttpResponseMessage(HttpStatusCode.NotModified);
                var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ReleaseJson("v9.9.9", new string('c', 64))) };
                resp.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"zz\"");
                return resp;
            });
            var cur = ReleaseVersion.ParseOrNull("6.6.0")!;
            var a = await ReleaseCheckService.CheckAsync(store, cur, h);
            Assert.True(a.ShouldNotify);
            Assert.True(a.CanInstallInApp);
            var b = await ReleaseCheckService.CheckAsync(store, cur, h);   // 304
            Assert.Equal(ReleaseFetchStatus.NotModified, b.Status);
            Assert.Equal("v9.9.9", b.Tag);
            Assert.True(b.ShouldNotify);   // not marked as notified until the balloon is really shown
            store.MarkSeen("gearwin.balloon", b.Latest!);
            Assert.False((await ReleaseCheckService.CheckAsync(store, cur, h)).ShouldNotify);
        }

        [Fact]
        public async Task ReleaseWithoutDigestCannotBeInstalledInApp()
        {
            var store = new OncePerVersionStore(Path.Combine(Path.GetTempPath(), "gwrc_" + Guid.NewGuid().ToString("N"), "s.json"));
            var h = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ReleaseJson("v9.9.9")) });
            var o = await ReleaseCheckService.CheckAsync(store, ReleaseVersion.ParseOrNull("6.6.0")!, h);
            Assert.True(o.ShouldNotify);
            Assert.False(o.CanInstallInApp);   // click opens the release page instead of the updater
        }

        [Fact]
        public void Sha256VerifyMatchAndMismatch()
        {
            var f = Path.GetTempFileName();
            File.WriteAllBytes(f, Encoding.ASCII.GetBytes("hello"));
            var good = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes("hello"))).ToLowerInvariant();
            Assert.True(SuiteInstallerService.VerifySha256(f, good));
            Assert.True(SuiteInstallerService.VerifySha256(f, good.ToUpperInvariant()));
            Assert.False(SuiteInstallerService.VerifySha256(f, new string('0', 64)));
            Assert.False(SuiteInstallerService.VerifySha256(f, "abc"));
            File.Delete(f);
        }

        [Fact]
        public void ChecksumFileParsing()
        {
            var h = new string('a', 64);
            Assert.Equal(h, SuiteInstallerService.ParseChecksumFile(h + "  Setup.exe\n"));
            Assert.Equal(h, SuiteInstallerService.ParseChecksumFile("SHA256 (Setup.exe) = " + h.ToUpperInvariant()));
            Assert.Null(SuiteInstallerService.ParseChecksumFile("deadbeef"));
            Assert.Null(SuiteInstallerService.ParseChecksumFile(new string('a', 65)));
            Assert.Null(SuiteInstallerService.ParseChecksumFile(null));
        }

        private static (SuiteApp App, ReleaseData Rel, byte[] Bytes) Fixture(string? sha = null)
        {
            var bytes = Encoding.ASCII.GetBytes("installer-bytes");
            var real = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var app = new SuiteApp("kombos", "Kombos", "Kombos", @"^KombosSetup-.*\.exe$", null, "Kombos", Array.Empty<string>(),
                new Dictionary<string, string>(), new Dictionary<string, IReadOnlyList<string>>());
            var json = ReleaseJson("v1.0.0", sha ?? real, assetUrl: $"https://github.com/{Owner}/Kombos/releases/download/v1.0.0/KombosSetup-1.0.0.exe", assetName: "KombosSetup-1.0.0.exe");
            return (app, GitHubReleaseClient.Parse(json, Owner, "Kombos")!, bytes);
        }

        [Fact]
        public async Task DownloadWithMatchingHashSucceedsAndMismatchDeletesFile()
        {
            var dir = Path.Combine(Path.GetTempPath(), "gwdl_" + Guid.NewGuid().ToString("N"));
            var (app, rel, bytes) = Fixture();
            var h = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
            var item = await SuiteInstallerService.ResolveAsync(app, rel, h);
            Assert.NotNull(item);
            var path = await SuiteInstallerService.DownloadAsync(item!, dir, h);
            Assert.True(File.Exists(path));
            Assert.Empty(Directory.GetFiles(dir, "*.part"));

            var (app2, rel2, _) = Fixture(sha: new string('f', 64));
            var item2 = await SuiteInstallerService.ResolveAsync(app2, rel2, h);
            var dir2 = Path.Combine(Path.GetTempPath(), "gwdl_" + Guid.NewGuid().ToString("N"));
            await Assert.ThrowsAsync<InvalidDataException>(() => SuiteInstallerService.DownloadAsync(item2!, dir2, h));
            Assert.Empty(Directory.GetFiles(dir2));   // neither the final file nor .part survives
        }

        [Fact]
        public async Task NoHashMeansNoDownload_AndChecksumAssetIsUsed()
        {
            var bytes = Encoding.ASCII.GetBytes("abc");
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var app = new SuiteApp("p", "P", "P", @"^P-Setup\.exe$", "P-Setup.exe.sha256", "P", Array.Empty<string>(), new Dictionary<string, string>(), new Dictionary<string, IReadOnlyList<string>>());
            string Rel(bool withSum) => $$"""{"tag_name":"v1.0.0","assets":[{"name":"P-Setup.exe","size":3,"browser_download_url":"https://github.com/{{Owner}}/P/releases/download/v1.0.0/P-Setup.exe"}{{(withSum ? $",{{\"name\":\"P-Setup.exe.sha256\",\"size\":70,\"browser_download_url\":\"https://github.com/{Owner}/P/releases/download/v1.0.0/P-Setup.exe.sha256\"}}" : "")}}]}""";
            var h = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(hash + "  P-Setup.exe") });
            Assert.Null(await SuiteInstallerService.ResolveAsync(app, GitHubReleaseClient.Parse(Rel(false), Owner, "P")!, h));
            var item = await SuiteInstallerService.ResolveAsync(app, GitHubReleaseClient.Parse(Rel(true), Owner, "P")!, h);
            Assert.Equal(hash, item!.ExpectedSha256);
        }

        [Fact]
        public async Task DownloadRefusesUntrustedUrlAndBadFileName()
        {
            var item = new VerifiedInstaller(new ReleaseAsset("x.exe", "https://evil.com/x.exe", 1, null), new string('a', 64));
            await Assert.ThrowsAsync<InvalidOperationException>(() => SuiteInstallerService.DownloadAsync(item, Path.GetTempPath(), new FakeHandler(_ => new HttpResponseMessage())));
            var bad = new VerifiedInstaller(new ReleaseAsset("..\\x.exe", "https://github.com/o/r/releases/download/v1/x.exe", 1, null), new string('a', 64));
            await Assert.ThrowsAsync<InvalidOperationException>(() => SuiteInstallerService.DownloadAsync(bad, Path.GetTempPath(), new FakeHandler(_ => new HttpResponseMessage())));
        }
    }
}
