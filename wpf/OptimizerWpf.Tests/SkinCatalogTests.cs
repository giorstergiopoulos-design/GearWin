using System.Linq;
using OptimizerWpf;

namespace OptimizerWpf.Tests;

// Χρειάζεται WPF (System.Windows.Media.Color στο ThemeCatalog) - τρέχει μόνο σε Windows.
public class SkinCatalogTests
{
    [Fact]
    public void EverySkinHasUniqueNameAndAMatchingThemePair()
    {
        Assert.Equal(SkinCatalog.All.Count, SkinCatalog.All.Select(s => s.ThemeName).Distinct().Count());
        foreach (var skin in SkinCatalog.All)
            Assert.Contains(ThemeCatalog.AllIncludingSkins, p => p.DisplayName == skin.ThemeName);
    }

    [Fact]
    public void SkinsAreNotListedAsPlainThemes()
    {
        foreach (var skin in SkinCatalog.All)
            Assert.DoesNotContain(ThemeCatalog.All, p => p.DisplayName == skin.ThemeName);
    }

    [Fact]
    public void NavigationLayoutsMatchTheDesign()
    {
        Assert.Equal(SkinNav.WideRail, SkinCatalog.For("Microsoft PC Manager")!.Nav);
        Assert.True(SkinCatalog.For("Microsoft PC Manager")!.HeroHome);
        Assert.Equal(SkinNav.MenuOnly, SkinCatalog.For("Windows Classic")!.Nav);
        Assert.Equal(SkinNav.CompactRail, SkinCatalog.For("Gaming Hub")!.Nav);
        Assert.True(SkinCatalog.For("Office Ribbon")!.ShowMenuBar);
        Assert.Equal(SkinNav.Tabs, SkinCatalog.For("Office Ribbon")!.Nav);
        Assert.Null(SkinCatalog.For("Matrix"));
    }

    [Theory]
    [InlineData(0x00D47800, 0x00, 0x78, 0xD4)] // DWM AccentColor είναι ABGR: R=0x00 G=0x78 B=0xD4 (Windows μπλε)
    [InlineData(0x000000FF, 0xFF, 0x00, 0x00)]
    public void WindowsAccent_IsDecodedFromDwmAbgr(int abgr, int r, int g, int b)
    {
        var c = ThemeCatalog.AccentFromDwm(abgr);
        Assert.Equal(((byte)r, (byte)g, (byte)b), (c.R, c.G, c.B));
    }

    [Fact]
    public void WindowsAccent_FallsBackToDefaultBlue()
    {
        var c = ThemeCatalog.AccentFromDwm(null);
        Assert.Equal(((byte)0, (byte)120, (byte)212), (c.R, c.G, c.B));
    }
}
