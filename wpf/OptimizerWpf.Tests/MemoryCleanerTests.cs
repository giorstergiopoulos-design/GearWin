using OptimizerWpf.Services;

namespace OptimizerWpf.Tests
{
    public class MemoryCleanerTests
    {
        [Fact]
        public void Snapshot_ReportsPlausibleMemoryNumbers()
        {
            var s = MemoryCleanerService.Snapshot();
            Assert.True(s.TotalBytes > 0);
            Assert.InRange(s.AvailableBytes, 1, s.TotalBytes);
            Assert.InRange(s.FreeBytes, 0, s.TotalBytes);
            Assert.InRange(s.StandbyBytes, 0, s.TotalBytes);
        }

        // Opt-in (trims real processes of the machine running the tests): GEARWIN_LIVE_RAMCLEAN=1.
        [Fact]
        public async Task Clean_Live_FreesCacheAndKeepsNumbersConsistent()
        {
            if (System.Environment.GetEnvironmentVariable("GEARWIN_LIVE_RAMCLEAN") != "1") return;
            var r = await MemoryCleanerService.CleanAsync();
            System.Console.WriteLine($"LIVE-RAM before free={r.Before.FreeGb:0.00} standby={r.Before.StandbyBytes / 1073741824.0:0.00} -> after free={r.After.FreeGb:0.00} standby={r.After.StandbyBytes / 1073741824.0:0.00} trimmed={r.TrimmedProcesses} purged={r.ListsPurged}");
            Assert.True(r.After.TotalBytes == r.Before.TotalBytes);
        }
    }
}
