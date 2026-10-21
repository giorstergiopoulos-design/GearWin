using System;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests
{
    public class SelfUpdateTests
    {
        private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private static string Release(string tag = "v6.5.0", string asset = "GearWin-Setup-6.5.0.exe",
            string? url = null, string digest = "sha256:" + Hash, bool prerelease = false)
        {
            var downloadUrl = url ?? "https://github.com/giorstergiopoulos-design/GearWin/releases/download/" + tag + "/" + asset;
            return "{\"tag_name\":\"" + tag + "\",\"draft\":false,\"prerelease\":" + prerelease.ToString().ToLowerInvariant()
                + ",\"body\":\"notes\",\"html_url\":\"https://github.com/giorstergiopoulos-design/GearWin/releases/tag/" + tag + "\","
                + "\"assets\":[{\"name\":\"" + asset + "\",\"size\":1234,\"digest\":\"" + digest + "\",\"browser_download_url\":\"" + downloadUrl + "\"}]}";
        }

        [Fact]
        public void ParsesAValidRelease()
        {
            var info = SelfUpdateService.ParseRelease(Release())!;
            Assert.Equal(new Version(6, 5, 0), info.Version);
            Assert.Equal("GearWin-Setup-6.5.0.exe", info.AssetName);
            Assert.Equal(Hash, info.Sha256);
        }

        [Theory]
        [InlineData("https://evil.example.com/GearWin-Setup-6.5.0.exe")]
        [InlineData("http://github.com/giorstergiopoulos-design/GearWin/releases/download/v6.5.0/GearWin-Setup-6.5.0.exe")]
        [InlineData("https://github.com/someone-else/GearWin/releases/download/v6.5.0/GearWin-Setup-6.5.0.exe")]
        [InlineData("https://user@github.com/giorstergiopoulos-design/GearWin/releases/download/v6.5.0/GearWin-Setup-6.5.0.exe")]
        public void RejectsUntrustedDownloadUrls(string url) => Assert.Null(SelfUpdateService.ParseRelease(Release(url: url)));

        [Theory]
        [InlineData("GearWin-Setup-6.5.0.zip")]
        [InlineData("malware.exe")]
        [InlineData("GearWin-Setup-latest.exe")]
        public void RejectsUnexpectedAssetNames(string name) => Assert.Null(SelfUpdateService.ParseRelease(Release(asset: name)));

        [Fact]
        public void IgnoresPrereleasesAndMissingOrMalformedDigest()
        {
            Assert.Null(SelfUpdateService.ParseRelease(Release(prerelease: true)));
            Assert.Null(SelfUpdateService.ParseRelease(Release(digest: "sha256:zz"))!.Sha256);
            Assert.Null(SelfUpdateService.ParseRelease(Release(digest: ""))!.Sha256);
        }

        [Theory]
        [InlineData("v6.4.1", "6.4.0", true)]
        [InlineData("v6.4.0", "6.4.0", false)]
        [InlineData("6.10.0", "6.9.9", true)]
        [InlineData("v6.3.9", "6.4.0", false)]
        public void ComparesVersionsNumerically(string tag, string current, bool newer)
        {
            Assert.True(SelfUpdateService.TryParseTag(tag, out var v));
            Assert.Equal(newer, SelfUpdateService.IsNewer(v, Version.Parse(current)));
        }

        [Theory]
        [InlineData("nightly")]
        [InlineData("v6")]
        [InlineData("")]
        public void RejectsNonVersionTags(string tag) => Assert.False(SelfUpdateService.TryParseTag(tag, out _));
    }
}
