using System;
using OptimizerWpf.Services;
using Xunit;

namespace OptimizerWpf.Tests;

public class HealthTimelineTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0);

    [Fact]
    public void Bsod_IsLinkedToInstallWithin48h()
    {
        var install = new TimelineEntry(T0.AddHours(-5), TimelineKind.Install, "App X");
        var bsod = new TimelineEntry(T0, TimelineKind.Bsod, "");
        var res = HealthTimelineService.Correlate(new[] { install, bsod });
        Assert.Equal(TimelineKind.Bsod, res[0].Kind);
        Assert.Equal("App X", res[0].Suspect?.Text);
    }

    [Fact]
    public void Install_OlderThan48h_IsNotASuspect()
    {
        var install = new TimelineEntry(T0.AddHours(-49), TimelineKind.Driver, "Drv");
        var crash = new TimelineEntry(T0, TimelineKind.Unexpected, "");
        Assert.Null(HealthTimelineService.Correlate(new[] { install, crash })[0].Suspect);
    }

    [Fact]
    public void Result_IsNewestFirst_AndInstallAfterProblemIsIgnored()
    {
        var crash = new TimelineEntry(T0, TimelineKind.Bsod, "");
        var later = new TimelineEntry(T0.AddHours(2), TimelineKind.Install, "Later");
        var res = HealthTimelineService.Correlate(new[] { crash, later });
        Assert.Equal("Later", res[0].Text);
        Assert.Null(res[1].Suspect);
    }
}
