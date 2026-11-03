using System;
using System.Linq;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests
{
    public class PerformanceProfileTests
    {
        [Theory]
        [InlineData(7, 2, false, false, PerformanceTier.Low)]        // GitHub runner (2 vCPU, 7 GB)
        [InlineData(32, 2, false, false, PerformanceTier.Low)]       // few cores
        [InlineData(4, 8, false, false, PerformanceTier.Low)]        // little RAM
        [InlineData(16, 8, true, true, PerformanceTier.Low)]         // battery saver
        [InlineData(16, 8, true, false, PerformanceTier.Balanced)]   // on battery, no saver: not High
        [InlineData(16, 4, false, false, PerformanceTier.Balanced)]
        [InlineData(32, 16, false, false, PerformanceTier.High)]
        public void TierIsChosenFromTheSpecs(double ram, int cores, bool battery, bool saver, PerformanceTier expected) =>
            Assert.Equal(expected, PerformanceProfileService.Choose(new HardwareSpecs(ram, cores, battery, saver)));

        [Fact]
        public void EveryTierHasItsOwnRowAndLowIsTheGentlest()
        {
            var low = PerformanceProfileService.ProfileFor(PerformanceTier.Low);
            var bal = PerformanceProfileService.ProfileFor(PerformanceTier.Balanced);
            var high = PerformanceProfileService.ProfileFor(PerformanceTier.High);
            Assert.True(low.MaxParallelScans < bal.MaxParallelScans && bal.MaxParallelScans < high.MaxParallelScans);
            Assert.False(low.AnimationsEnabled); Assert.False(low.TabFadeEnabled);
            Assert.True(high.AnimationsEnabled && high.TabFadeEnabled);
            Assert.True(low.HomeRefreshSeconds > bal.HomeRefreshSeconds && bal.HomeRefreshSeconds > high.HomeRefreshSeconds);
        }

        [Fact]
        public void ForcedTierBeatsTheSettingAndTheSettingBeatsDetection()
        {
            var strong = new HardwareSpecs(32, 16, false, false);
            Assert.Equal(PerformanceTier.High, PerformanceProfileService.BaseTier(strong, "auto"));
            Assert.Equal(PerformanceTier.Low, PerformanceProfileService.BaseTier(strong, "low"));
            Assert.Equal(PerformanceTier.Balanced, PerformanceProfileService.BaseTier(strong, "low", "balanced"));
            Assert.Equal(PerformanceTier.High, PerformanceProfileService.BaseTier(strong, "garbage"));
        }

        [Fact]
        public void GovernorDowngradesAfterTenBusySecondsAndRecoversAfterThirtyCalmOnes()
        {
            var busy9 = Enumerable.Repeat(95.0, 9).ToList();
            Assert.Equal(0, PerformanceProfileService.NextDowngrade(0, busy9));                       // not yet 10 s
            Assert.Equal(1, PerformanceProfileService.NextDowngrade(0, Enumerable.Repeat(95.0, 10).ToList()));
            Assert.Equal(2, PerformanceProfileService.NextDowngrade(2, Enumerable.Repeat(99.0, 20).ToList()));   // never below two levels
            var mixed = Enumerable.Repeat(95.0, 9).Append(40.0).ToList();
            Assert.Equal(0, PerformanceProfileService.NextDowngrade(0, mixed));                       // one calm sample breaks the streak
            Assert.Equal(1, PerformanceProfileService.NextDowngrade(2, Enumerable.Repeat(20.0, 30).ToList()));    // recover one level
            Assert.Equal(2, PerformanceProfileService.NextDowngrade(2, Enumerable.Repeat(20.0, 29).ToList()));    // hysteresis
            Assert.Equal(0, PerformanceProfileService.NextDowngrade(0, Enumerable.Repeat(20.0, 60).ToList()));
        }

        [Fact]
        public void DetectionNeverThrowsAndGivesSaneNumbers()
        {
            var d = PerformanceProfileService.Detect();
            Assert.True(d.RamGb > 0 && d.LogicalCores >= 1);
            _ = PerformanceProfileService.Current;   // no exception even without a GPU/WMI
        }

        [Fact]
        public void ParseModeUnderstandsTheFourSettings()
        {
            Assert.Null(PerformanceProfileService.ParseMode("auto"));
            Assert.Null(PerformanceProfileService.ParseMode(null));
            Assert.Equal(PerformanceTier.Low, PerformanceProfileService.ParseMode(" Low "));
            Assert.Equal(PerformanceTier.High, PerformanceProfileService.ParseMode("HIGH"));
        }
    }
}
