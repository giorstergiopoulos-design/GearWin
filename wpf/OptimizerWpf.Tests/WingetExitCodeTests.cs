using OptimizerWpf.Services;
using Xunit;

namespace OptimizerWpf.Tests
{
    public class WingetExitCodeTests
    {
        [Fact]
        public void InstallerTechnologyChangeIsExplained() =>
            // 0x8A15008E == signed exit code winget returns for "different install technology"
            Assert.Contains("install technology", WingetService.DescribeExitCode(unchecked((int)0x8A15008E)));

        [Fact]
        public void UnknownCodeReturnsNull() => Assert.Null(WingetService.DescribeExitCode(12345));

        [Fact]
        public void MsiCodesAreMapped() => Assert.Equal("Another installation is in progress", WingetService.DescribeExitCode(1618));
    }
}
