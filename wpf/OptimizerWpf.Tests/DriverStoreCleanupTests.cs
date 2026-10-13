using System.Linq;
using OptimizerWpf.Services;
using Xunit;

namespace OptimizerWpf.Tests
{
    // WMT 6.6 "Reworked Clean Old Drivers": σε χρήση πρώτα, μετά υψηλότερη έκδοση, μετά ημερομηνία.
    public class DriverStoreCleanupTests
    {
        private static DriverStoreEntry E(string oem, string file, string ver, string date, bool inUse = false) =>
            new(oem, file, ver, date, "Net", inUse);

        [Fact]
        public void HigherVersionWinsEvenWithOlderDate()
        {
            var all = new[] { E("oem1.inf", "net.inf", "2.0.0.0", "2020-01-01"), E("oem2.inf", "net.inf", "1.0.0.0", "2023-01-01") };
            var old = DriverService.SelectOldDrivers(all);
            Assert.Single(old);
            Assert.Equal("oem2.inf", old[0].Driver);
        }

        [Fact]
        public void InUsePackageIsKeptAndNeverSelected()
        {
            var all = new[] { E("oem1.inf", "gpu.inf", "1.0.0.0", "2020-01-01", inUse: true), E("oem2.inf", "gpu.inf", "9.0.0.0", "2024-01-01") };
            var old = DriverService.SelectOldDrivers(all);
            Assert.Single(old);
            Assert.Equal("oem2.inf", old[0].Driver);
            Assert.DoesNotContain(old, d => d.InUse);
        }

        [Fact]
        public void EqualVersionFallsBackToNewerDate()
        {
            var all = new[] { E("oem1.inf", "a.inf", "1.0.0.0", "2020-01-01"), E("oem2.inf", "a.inf", "1.0.0.0", "2021-01-01") };
            Assert.Equal("oem1.inf", DriverService.SelectOldDrivers(all).Single().Driver);
        }

        [Fact]
        public void SinglePackagesAreNeverSelected()
        {
            var all = new[] { E("oem1.inf", "a.inf", "1.0", "2020-01-01"), E("oem2.inf", "b.inf", "1.0", "2020-01-01") };
            Assert.Empty(DriverService.SelectOldDrivers(all));
        }

        [Fact]
        public void KeptInfoNamesTheSurvivingPackage()
        {
            var all = new[] { E("oem1.inf", "a.inf", "1.0.0.0", "2020-01-01"), E("oem2.inf", "a.inf", "2.0.0.0", "2021-01-01") };
            var info = DriverService.SelectOldDrivers(all).Single().KeptInfo;
            Assert.Contains("oem2.inf", info);
        }
    }
}
