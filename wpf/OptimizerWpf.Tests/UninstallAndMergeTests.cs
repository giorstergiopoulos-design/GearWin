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
        public void ResultsAreSplitIntoAppsWindowsAndIgnored()
        {
            var results = new[]
            {
                new WingetUpdate("Tool", "Vendor.Tool", "1", "2", "winget"),
                new WingetUpdate("Other", "Vendor.Other", "1", "2", "winget"),
                new WingetUpdate("KB5001", "11111111-1111-1111-1111-111111111111|200", "", "Important", "windowsupdate"),
                new WingetUpdate("Other", "Vendor.Other", "1", "2", "winget"),   // duplicate
            };
            var (apps, windows, ignored) = OptimizerWpf.Views.OptimizationView.SplitUpdates(results, new[] { "winget|Vendor.Tool" });
            Assert.Equal(new[] { "Vendor.Other" }, apps.Select(u => u.Id).ToArray());
            Assert.Single(windows);
            Assert.Equal("windowsupdate", windows[0].Source);
            Assert.Equal(new[] { "Vendor.Tool" }, ignored.Select(u => u.Id).ToArray());
            // ignoring never hides Windows updates
            var (_, win2, _) = OptimizerWpf.Views.OptimizationView.SplitUpdates(results, new[] { "windowsupdate|11111111-1111-1111-1111-111111111111|200" });
            Assert.Single(win2);
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

        [Fact]
        public void UwpPackageNamesAreSplitIntoVersionAndArchitecture()
        {
            var a = new UwpAppInfo("Calculator", "Microsoft.WindowsCalculator_11.2405.1.0_x64__8wekyb3d8bbwe");
            Assert.Equal("11.2405.1.0", a.Version);
            Assert.Equal("x64", a.Architecture);
            Assert.Equal("11.2405.1.0 · x64", a.Details);
            Assert.Equal("C", a.Initial);
            Assert.Equal("", new UwpAppInfo("X", "weird").Details);
        }

        [Fact]
        public void SdioScriptsDownloadOnlyOrIntegrateAndAlwaysEnd()
        {
            var dl = SdioService.BuildScript(SdioAction.Download);
            Assert.Contains("checkupdates", dl);
            Assert.Contains("get indexes", dl);
            Assert.DoesNotContain("install", dl);
            Assert.DoesNotContain("enableinstall on", dl);
            var integrate = SdioService.BuildScript(SdioAction.Integrate);
            Assert.Contains("select missing better", integrate);
            Assert.True(integrate.IndexOf("enableinstall on") < integrate.IndexOf("\r\ninstall"));
            foreach (var script in new[] { dl, integrate }) Assert.EndsWith(":end\r\nend\r\n", script);
        }

        [Fact]
        public void UnifiedListFiltersByCategoryAndKeepsAllSourcesInOneList()
        {
            var apps = new[] { new OptimizerWpf.Views.WingetUpdateRow(new WingetUpdate("A", "a", "1", "2", "winget")) };
            var win = new[] { new OptimizerWpf.Views.WingetUpdateRow(new WingetUpdate("KB", "u|1", "", "x", "windowsupdate")) };
            var drv = new[] { new OptimizerWpf.Views.UnifiedDriverRow { Title = "GPU" } };
            Assert.Equal(6, OptimizerWpf.Views.OptimizationView.BuildUnified(apps, win, drv, "all").Count);   // 3 rows + 3 category headers
            Assert.Equal(2, OptimizerWpf.Views.OptimizationView.BuildUnified(apps, win, drv, "apps").Count);   // header + row
            Assert.IsType<OptimizerWpf.Views.UpdateGroupHeader>(OptimizerWpf.Views.OptimizationView.BuildUnified(apps, win, drv, "drivers")[0]);
            Assert.IsType<OptimizerWpf.Views.UnifiedDriverRow>(OptimizerWpf.Views.OptimizationView.BuildUnified(apps, win, drv, "drivers")[1]);
            Assert.False(win[0].ShowAppActions);
            Assert.True(apps[0].ShowAppActions);
        }

        [Fact]
        public void DriverHistoryKeepsTheLast100Entries()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gwdh_" + System.Guid.NewGuid().ToString("N"), "h.json");
            for (var i = 0; i < 105; i++) DriverHistoryService.Record("drv" + i, "Windows Update", "started", path, new System.DateTime(2026, 1, 1).AddMinutes(i));
            var list = DriverHistoryService.Load(path);
            Assert.Equal(100, list.Count);
            Assert.Equal("drv5", list[0].Title);
            Assert.Equal("drv104", list[^1].Title);
        }

        [Fact]
        public void StatusScopeEndsExactlyOnceAndReportsTheFinalMessage()
        {
            var before = StatusService.ActiveCount;
            var shown = new System.Collections.Generic.List<(string Text, bool Busy)>();
            void Handler(string t, bool b) => shown.Add((t, b));
            StatusService.Changed += Handler;
            try
            {
                var scope = StatusService.BeginScope("Scanning drivers...");
                scope.Update("Checking catalogue: GPU...");
                scope.Update("Checking catalogue: LAN...");
                Assert.Equal(before + 1, StatusService.ActiveCount);   // many updates, ONE entry (no leak)
                scope.End("3 driver updates found");
                scope.End("ignored second end");
                Assert.Equal(before, StatusService.ActiveCount);
                if (before == 0) Assert.Equal(("3 driver updates found", false), shown[^1]);
            }
            finally { StatusService.Changed -= Handler; }
        }

        [Fact]
        public void PasswordLockHashesWithSaltAndVerifies()
        {
            var salt = PasswordVaultLock.NewSalt();
            var hash = PasswordVaultLock.Hash("correct horse", salt);
            Assert.DoesNotContain("correct", hash);
            Assert.True(PasswordVaultLock.VerifyAgainst("correct horse", salt, hash));
            Assert.False(PasswordVaultLock.VerifyAgainst("wrong", salt, hash));
            Assert.False(PasswordVaultLock.VerifyAgainst("correct horse", PasswordVaultLock.NewSalt(), hash));
            Assert.False(PasswordVaultLock.VerifyAgainst("x", "###", hash));
        }

        [Fact]
        public void PasswordExportIsPlainTextWithWarningAndAllFields()
        {
            var text = PasswordVaultLock.FormatExport(new[] { new PasswordVaultService.SavedCredential("Edge", "https://example.com", "me", "s3cret") }, new System.DateTime(2026, 10, 30, 12, 0, 0));
            Assert.Contains("WARNING", text);
            Assert.Contains("Site:     https://example.com", text);
            Assert.Contains("Password: s3cret", text);
        }
    }
}
