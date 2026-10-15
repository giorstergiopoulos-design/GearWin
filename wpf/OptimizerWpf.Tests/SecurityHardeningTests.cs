using System;
using System.Text;
using OptimizerWpf.Services;
using Xunit;

namespace OptimizerWpf.Tests
{
    // Έλεγχος ευπαθειών: η εφαρμογή τρέχει elevated, άρα κάθε εντολή που χτίζεται από εξωτερικά δεδομένα
    // πρέπει να μην επιτρέπει injection.
    public class SecurityHardeningTests
    {
        [Theory]
        [InlineData("left-pad")]
        [InlineData("@angular/cli")]
        [InlineData("laravel/installer")]
        [InlineData("Microsoft.PowerToys")]
        [InlineData("requests")]
        public void LegitimatePackageIdsAreAcceptedForShellSources(string id) =>
            Assert.True(WingetService.IsSafePackageId(id, "npm"));

        [Theory]
        [InlineData("x\" & calc & \"")]
        [InlineData("a&b")]
        [InlineData("a|b")]
        [InlineData("a b")]
        [InlineData("a;b")]
        [InlineData("a`b")]
        [InlineData("$(whoami)")]
        [InlineData("a\r\nb")]
        [InlineData("")]
        public void InjectionPayloadsAreRejectedForShellSources(string id)
        {
            foreach (var source in new[] { "npm", "npm (global)", "pnpm", "scoop", "gem", "composer", "pip", "chocolatey", "cargo", "dotnet" })
                Assert.False(WingetService.IsSafePackageId(id, source), $"{source}: {id}");
        }

        [Fact]
        public void WingetIdsMayContainBackslashAndBracesButNeverQuotes()
        {
            Assert.True(WingetService.IsSafePackageId(@"ARP\Machine\X64\{1234-ABCD}", "winget"));
            Assert.False(WingetService.IsSafePackageId("a\"b", "winget"));
            Assert.False(WingetService.IsSafePackageId(new string('a', 500), "winget"));
        }

        [Fact]
        public void EncodedCommandRoundTripsAnyCharacters()
        {
            const string script = "Write-Host 'a\"b' ; $x = \"`\"\\\"& calc\" # Ελληνικά ✓";
            var args = PowerShellArgs.Command(script);
            Assert.StartsWith("-NoProfile -ExecutionPolicy Bypass -EncodedCommand ", args);
            var b64 = args[(args.LastIndexOf(' ') + 1)..];
            Assert.Equal(script, Encoding.Unicode.GetString(Convert.FromBase64String(b64)));
            Assert.DoesNotContain("calc", args); // τίποτα από το περιεχόμενο δεν φτάνει σκέτο στη γραμμή εντολών
        }

        [Fact]
        public void HiddenFlagIsPlacedBeforeEncodedCommand()
        {
            var args = PowerShellArgs.Command("1", hidden: true);
            Assert.True(args.IndexOf("-WindowStyle Hidden", StringComparison.Ordinal) < args.IndexOf("-EncodedCommand", StringComparison.Ordinal));
        }

        [Fact]
        public void OversizedScriptsDoNotFitCommandLine() =>
            Assert.False(PowerShellArgs.Fits(new string('x', 20000)));

        [Fact]
        public void GamingModeSourceNoLongerTouchesVbsOrHvci()
        {
            var src = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..", "wpf", "OptimizerWpf", "Services", "PowerModeService.cs")));
            Assert.DoesNotContain("HypervisorEnforcedCodeIntegrity", RemoveComments(src));
            Assert.DoesNotContain("EnableVirtualizationBasedSecurity", RemoveComments(src));
        }

        private static string RemoveComments(string src)
        {
            var sb = new StringBuilder();
            foreach (var line in src.Split('\n'))
                if (!line.TrimStart().StartsWith("//")) sb.AppendLine(line);
            return sb.ToString();
        }
    }
}

namespace OptimizerWpf.Tests
{
    public class SecureStorageTests
    {
        [Fact]
        public void SystemDirectoryIsAdminOnlyWritable() =>
            Assert.True(SecureStorage.IsDirectoryAdminOnlyWritable(Environment.GetFolderPath(Environment.SpecialFolder.Windows)));

        [Fact]
        public void UserTempIsNotConsideredSafe() =>
            Assert.False(SecureStorage.IsDirectoryAdminOnlyWritable(System.IO.Path.GetTempPath()));

        [Fact]
        public void MissingDirectoryIsNotSafe() =>
            Assert.False(SecureStorage.IsDirectoryAdminOnlyWritable(@"C:\this\does\not\exist"));
    }
}

namespace OptimizerWpf.Tests
{
    public class WindowsUpdateIdTests
    {
        [Fact]
        public void WindowsUpdateRowIdsAreAcceptedOnlyInWuaFormat()
        {
            Assert.True(WingetService.IsSafePackageId("4dbb6451-7253-47b0-af26-ebc863e1ecec|200", "windowsupdate"));
            Assert.False(WingetService.IsSafePackageId("4dbb6451-7253-47b0-af26-ebc863e1ecec|200 & calc", "windowsupdate"));
            Assert.False(WingetService.IsSafePackageId("not-a-guid|1", "windowsupdate"));
        }

        [Fact]
        public void RowIdMatchesWmtFormat() =>
            Assert.Equal("abc|5", new WindowsUpdateItem("t", null, false, "abc", 5).RowId);

        [Fact]
        public void TestsNeverUseTheRealUserDataFolder() =>
            Assert.StartsWith(System.IO.Path.GetTempPath(), Environment.GetEnvironmentVariable("GEARWIN_DATA_DIR"));
    }
}
