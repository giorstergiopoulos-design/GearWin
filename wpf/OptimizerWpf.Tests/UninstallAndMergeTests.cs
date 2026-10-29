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
    }
}
