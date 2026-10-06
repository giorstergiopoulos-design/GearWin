using System;
using System.Linq;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests;

// 6.1.0 - νέες καρτέλες Παιχνίδια / Πολυμέσα: καθαρή λογική (parsers, registry value builders, ορίσματα ffmpeg).
public class GameLibraryServiceTests
{
    private const string LibraryFolders = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t\t\"label\"\t\t\"\"\n\t\t\"apps\"\n\t\t{\n\t\t\t\"730\"\t\t\"123\"\n\t\t}\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\SteamLibrary\"\n\t}\n}";

    [Fact]
    public void Vdf_ParsesNestedBlocksAndEscapes()
    {
        var root = GameLibraryService.ParseVdf(LibraryFolders);
        var folders = (System.Collections.Generic.Dictionary<string, object>)root["libraryfolders"];
        Assert.Equal(2, folders.Count);
        var first = (System.Collections.Generic.Dictionary<string, object>)folders["0"];
        Assert.Equal(@"C:\Program Files (x86)\Steam", first["path"]);
    }

    [Fact]
    public void Steam_LibraryPathsAreExtracted()
    {
        var paths = GameLibraryService.ParseSteamLibraryPaths(LibraryFolders);
        Assert.Equal(new[] { @"C:\Program Files (x86)\Steam", @"D:\SteamLibrary" }, paths);
    }

    private static string Acf(int flags, long toDownload = 0, string name = "Counter-Strike 2") =>
        "\"AppState\"\n{\n\t\"appid\"\t\t\"730\"\n\t\"name\"\t\t\"" + name + "\"\n\t\"StateFlags\"\t\t\"" + flags + "\"\n\t\"installdir\"\t\t\"Counter-Strike Global Offensive\"\n" +
        "\t\"LastPlayed\"\t\t\"1700000000\"\n\t\"SizeOnDisk\"\t\t\"36000000000\"\n\t\"BytesToDownload\"\t\t\"" + toDownload + "\"\n}";

    [Fact]
    public void Steam_ManifestFieldsAndLaunchUris()
    {
        var g = GameLibraryService.ParseSteamManifest(Acf(4), @"D:\SteamLibrary");
        Assert.NotNull(g);
        Assert.Equal("730", g!.Id);
        Assert.Equal("Counter-Strike 2", g.Name);
        Assert.Equal(36_000_000_000, g.SizeBytes);
        Assert.Equal(@"D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive", g.InstallDir.Replace('/', '\\'));
        Assert.Equal("steam://rungameid/730", g.LaunchTarget);
        Assert.Equal("steam://validate/730", g.VerifyTarget);
        Assert.NotNull(g.LastPlayed);
        Assert.False(g.UpdatePending);
    }

    [Theory]
    [InlineData(4, 0, false)]     // πλήρως εγκατεστημένο
    [InlineData(6, 0, true)]      // 4 + UpdateRequired(2)
    [InlineData(1026, 5000, true)]
    [InlineData(260, 0, true)]    // 4 + 0x100 (ενημέρωση σε εξέλιξη)
    [InlineData(4, 1234, false)]  // flags==4 ⇒ τα bytes εκκρεμούν από DLC/ρυθμίσεις - όχι εκκρεμής ενημέρωση
    public void Steam_UpdatePendingFollowsStateFlags(int flags, long bytes, bool expected) =>
        Assert.Equal(expected, GameLibraryService.ParseSteamManifest(Acf(flags, bytes), @"C:\S")!.UpdatePending);

    [Fact]
    public void Steam_RedistributablesAreNotGames() =>
        Assert.Null(GameLibraryService.ParseSteamManifest(Acf(4, 0, "Steamworks Common Redistributables"), @"C:\S"));

    [Fact]
    public void Steam_GarbageReturnsNull() =>
        Assert.Null(GameLibraryService.ParseSteamManifest("not vdf at all", @"C:\S"));

    [Fact]
    public void Epic_ManifestParsedWithLaunchUri()
    {
        const string json = "{\"DisplayName\":\"Fortnite\",\"AppName\":\"Fortnite\",\"InstallLocation\":\"C:\\\\Epic\\\\Fortnite\",\"InstallSize\":1000,\"CatalogNamespace\":\"fn\",\"CatalogItemId\":\"4fe7\"}";
        var g = GameLibraryService.ParseEpicManifest(json);
        Assert.NotNull(g);
        Assert.Equal("Fortnite", g!.Name);
        Assert.Equal(@"C:\Epic\Fortnite", g.InstallDir);
        Assert.Equal("com.epicgames.launcher://apps/fn%3A4fe7%3AFortnite?action=launch&silent=true", g.LaunchTarget);
    }

    [Fact]
    public void Epic_IncompleteOrInvalidManifestsAreSkipped()
    {
        Assert.Null(GameLibraryService.ParseEpicManifest("{\"DisplayName\":\"X\",\"AppName\":\"X\",\"bIsIncompleteInstall\":true}"));
        Assert.Null(GameLibraryService.ParseEpicManifest("{}"));
        Assert.Null(GameLibraryService.ParseEpicManifest("nope"));
    }
}

public class GamingTweaksServiceTests
{
    [Theory]
    [InlineData(GpuPreference.Auto, "GpuPreference=0;")]
    [InlineData(GpuPreference.PowerSaving, "GpuPreference=1;")]
    [InlineData(GpuPreference.HighPerformance, "GpuPreference=2;")]
    public void GpuPreference_RoundTrips(GpuPreference p, string text)
    {
        Assert.Equal(text, GamingTweaksService.BuildGpuPreferenceValue(p));
        Assert.Equal(p, GamingTweaksService.ParseGpuPreference(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("GpuPreference=9;")]
    [InlineData("Foo=1;")]
    public void GpuPreference_InvalidValuesGiveNull(string? text) => Assert.Null(GamingTweaksService.ParseGpuPreference(text));

    [Fact]
    public void GpuPreference_ToleratesOtherSettingsInTheSameValue() =>
        Assert.Equal(GpuPreference.HighPerformance, GamingTweaksService.ParseGpuPreference("SwapEffectUpgradeEnable=1;GpuPreference=2;"));

    [Fact]
    public void LayerFlags_AddKeepOthersAndRemove()
    {
        var v = GamingTweaksService.SetLayerFlag(null, "DISABLEDXMAXIMIZEDWINDOWEDMODE", true);
        Assert.Equal("~ DISABLEDXMAXIMIZEDWINDOWEDMODE", v);
        v = GamingTweaksService.SetLayerFlag("~ RUNASADMIN", "DISABLEDXMAXIMIZEDWINDOWEDMODE", true);
        Assert.True(GamingTweaksService.HasLayerFlag(v, "RUNASADMIN"));
        Assert.True(GamingTweaksService.HasLayerFlag(v, "DISABLEDXMAXIMIZEDWINDOWEDMODE"));
        v = GamingTweaksService.SetLayerFlag(v, "DISABLEDXMAXIMIZEDWINDOWEDMODE", false);
        Assert.Equal("~ RUNASADMIN", v);
        Assert.Null(GamingTweaksService.SetLayerFlag(v, "RUNASADMIN", false)); // τελευταίο flag => διαγραφή τιμής
    }

    [Fact]
    public void DxGlobal_ParseAndBuildPreserveOtherSettings()
    {
        var d = GamingTweaksService.ParseDxGlobal("VRROptimizeEnable=0;SwapEffectUpgradeEnable=1;");
        Assert.False(d["VRROptimizeEnable"]);
        Assert.True(d["SwapEffectUpgradeEnable"]);
        d["VRROptimizeEnable"] = true;
        Assert.Equal("VRROptimizeEnable=1;SwapEffectUpgradeEnable=1;", GamingTweaksService.BuildDxGlobal(d));
    }
}

public class MultimediaServicesTests
{
    [Fact]
    public void Privacy_InterpretDetectsInUseAndConvertsFiletime()
    {
        var ft = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc).ToFileTimeUtc();
        var (start, stop, inUse) = PrivacyAccessService.Interpret(ft, 0);
        Assert.True(inUse);
        Assert.Null(stop);
        Assert.Equal(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc), start!.Value.ToUniversalTime());
        Assert.False(PrivacyAccessService.Interpret(ft, ft + 10_000_000).InUse);
        Assert.Null(PrivacyAccessService.Interpret(0, 0).Start);
    }

    [Theory]
    [InlineData("Microsoft.WindowsCamera_8wekyb3d8bbwe", "Microsoft.WindowsCamera")]
    [InlineData("C:#Program Files#Zoom#bin#Zoom.exe", "Zoom.exe")]
    [InlineData("Plain", "Plain")]
    public void Privacy_FriendlyName(string key, string expected) => Assert.Equal(expected, PrivacyAccessService.FriendlyName(key));

    [Fact]
    public void Codecs_InstalledMatchesPackagePrefixOnly()
    {
        var names = new[] { "Microsoft.HEVCVideoExtension_2.0.61931.0_x64__8wekyb3d8bbwe", "Microsoft.HEVCVideoExtensions2_1.0_x64__x" };
        Assert.True(MediaCodecService.IsInstalled(names, "Microsoft.HEVCVideoExtension"));
        Assert.False(MediaCodecService.IsInstalled(names, "Microsoft.AV1VideoExtension"));
        Assert.False(MediaCodecService.IsInstalled(new[] { "Microsoft.HEVCVideoExtensionsFoo_1_x" }, "Microsoft.HEVCVideoExtension"));
    }

    [Fact]
    public void Codecs_StoreLinkIsSearchUriWithEscapedQuery() =>
        Assert.Equal("ms-windows-store://search/?query=HEVC%20Video%20Extensions", MediaCodecService.StoreSearchUri(MediaCodecService.Extensions[0]));

    [Theory]
    [InlineData(MediaPreset.VideoToMp4H264, "libx264")]
    [InlineData(MediaPreset.VideoToMp4H265, "libx265")]
    [InlineData(MediaPreset.ExtractMp3, "libmp3lame")]
    [InlineData(MediaPreset.ShareSized720p, "scale=-2:720")]
    [InlineData(MediaPreset.VideoToGif, "paletteuse")]
    public void Ffmpeg_ArgsContainPresetEssentials(MediaPreset p, string expected)
    {
        var a = MediaConverterService.BuildArgs(p, MediaQuality.Medium, @"C:\in put.mov", @"C:\out.mp4");
        Assert.Contains(a, x => x.Contains(expected));
        Assert.Equal(@"C:\in put.mov", a[a.ToList().IndexOf("-i") + 1]);   // διαδρομή με κενό: ένα όρισμα, χωρίς quoting
        Assert.Equal(@"C:\out.mp4", a[^1]);
        Assert.Equal("-y", a[0]);
    }

    [Fact]
    public void Ffmpeg_OutputNeverOverwritesInputAndAvoidsExisting()
    {
        var path = MediaConverterService.SuggestOutputPath(@"C:\v\clip.mp4", MediaPreset.VideoToMp4H265, _ => false);
        Assert.Equal(@"C:\v\clip (GearWin).mp4", path.Replace('/', '\\'));
        var next = MediaConverterService.SuggestOutputPath(@"C:\v\clip.mp4", MediaPreset.VideoToMp4H265,
            p => p.Replace('/', '\\').EndsWith(@"clip (GearWin).mp4"));
        Assert.Contains("(GearWin 2)", next);
        Assert.EndsWith(".mp3", MediaConverterService.SuggestOutputPath(@"C:\v\clip.mp4", MediaPreset.ExtractMp3, _ => false));
    }

    [Fact]
    public void Ffmpeg_ProgressAndDurationParsing()
    {
        var total = MediaConverterService.ParseDuration("  Duration: 00:02:00.50, start: 0.000000, bitrate: 1000 kb/s");
        Assert.Equal(TimeSpan.FromSeconds(120.5), total);
        var p = MediaConverterService.ParseProgress("frame=  100 fps= 30 q=28.0 size=1024kB time=00:01:00.25 bitrate=...", total!.Value);
        Assert.NotNull(p);
        Assert.Equal(0.5, p!.Value, 2);
        Assert.Null(MediaConverterService.ParseProgress("no progress here", total.Value));
        Assert.Null(MediaConverterService.ParseProgress("time=00:00:01.00", TimeSpan.Zero));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(4, false)]
    public void Audio_OnlyStateOneIsActive(int state, bool active) => Assert.Equal(active, AudioDeviceService.IsActiveState(state));

    [Theory]
    [InlineData(60, true)]
    [InlineData(59, true)]
    [InlineData(144, false)]
    public void Display_SixtyHzDetection(int hz, bool expected) => Assert.Equal(expected, DisplayInfoService.LooksLikeHighRefreshAtSixty(hz));
}
