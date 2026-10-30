using System.Linq;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests
{
    public class UninstallAndMergeTests
    {
        [Theory]
        [InlineData("winget", "uninstall --id")]
        [InlineData("pip", "uninstall -y")]
        [InlineData("npm (global)", "npm uninstall -g")]
        [InlineData("chocolatey", "uninstall")]
        public void UninstallCommandsAreBuiltPerSource(string source, string expectedPart)
        {
            var cmd = WingetService.GetUninstallCommandForSource(source, "pkg.id");
            Assert.NotNull(cmd);
            Assert.Contains(expectedPart, cmd!.Value.Arguments);
            Assert.Contains("\"pkg.id\"", cmd.Value.Arguments);
        }

        [Theory]
        [InlineData("windowsupdate")]
        [InlineData("msstore")]
        [InlineData("unknown")]
        public void UnsupportedSourcesCannotBeUninstalled(string source) =>
            Assert.Null(WingetService.GetUninstallCommandForSource(source, "x"));

        [Fact]
        public async System.Threading.Tasks.Task UnsafeIdsAreRejectedBeforeAnyProcessStarts()
        {
            var (ok, reason) = await WingetService.UninstallAsync("x\" & calc & \"", "npm (global)");
            Assert.False(ok);
            Assert.Contains("Unsafe", reason);
        }

        [Fact]
        public void HealthTabNowHostsPerformanceCleanupAndStartup()
        {
            var pages = TabLayout.SubPages("Health")!;
            Assert.Equal(new[] { "Sub_Repair", "Sub_Performance", "Sub_Cleanup", "Sub_Startup" }, pages.Select(p => p.TitleKey).ToArray());
            Assert.DoesNotContain(NavItems.All, n => n.Tag == "Optimization");
        }

        [Fact]
        public void SuggestedFontsAreUniqueHttpsLinksOnKnownFontSources()
        {
            var hosts = new[] { "fonts.google.com", "fontsource.org", "fonts.bunny.net", "github.com", "www.brailleinstitute.org" };
            var fonts = FontSuggestionService.SuggestedFonts;
            Assert.True(fonts.Count >= 20);
            Assert.Equal(fonts.Count, fonts.Select(f => f.Name).Distinct().Count());
            Assert.All(fonts, f => { var u = new System.Uri(f.Url); Assert.Equal("https", u.Scheme); Assert.Contains(u.Host, hosts); });
        }

        [Fact]
        public void IgnoredUpdatesAreHiddenFromTheListAndCancellationKillsTheScan()
        {
            var settings = AppSettingsService.Current;
            var saved = settings.IgnoredUpdates.ToList();
            try
            {
                settings.IgnoredUpdates.Clear();
                settings.IgnoredUpdates.Add("winget|Vendor.Tool");
                OptimizerWpf.Views.OptimizationView.ReplaceUpdates(new[]
                {
                    new WingetUpdate("Tool", "Vendor.Tool", "1", "2", "winget"),
                    new WingetUpdate("Other", "Vendor.Other", "1", "2", "winget"),
                });
                Assert.Equal(1, OptimizerWpf.Views.OptimizationView.UpdateCountForTests);
                Assert.Equal("winget|Vendor.Tool", OptimizerWpf.Views.OptimizationView.IgnoreKey(new WingetUpdate("Tool", "Vendor.Tool", "1", "2", "winget")));
            }
            finally { settings.IgnoredUpdates.Clear(); settings.IgnoredUpdates.AddRange(saved); OptimizerWpf.Views.OptimizationView.ReplaceUpdates(System.Array.Empty<WingetUpdate>()); }
        }

        [Fact]
        public async System.Threading.Tasks.Task ACancelledScanTokenStopsTheScanBeforeTheNextProvider()
        {
            using var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();
            WingetService.ScanToken = cts.Token;
            try { await Assert.ThrowsAnyAsync<System.OperationCanceledException>(() => WingetService.ScanAsync()); }
            finally { WingetService.ScanToken = System.Threading.CancellationToken.None; }
        }
    }
}
