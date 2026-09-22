using OptimizerWpf.Services;

namespace OptimizerWpf.Tests;

// ΝΕΟ - roadmap "Περισσότερα unit tests" (v4.3.5). Καλύπτει το GuessFromModelName fallback (καθαρή
// string/regex λογική, μηδενικό I/O) - χρησιμοποιείται μόνο όταν το WMI αποτυγχάνει εντελώς, βλ.
// σχόλιο ειλικρίνειας στο ίδιο το service ότι πρόκειται για ΕΚΤΙΜΗΣΗ, όχι σίγουρη ανίχνευση.
public class DriveTypeServiceTests
{
    [Theory]
    [InlineData("Samsung SSD 970 EVO Plus 1TB", PhysicalDriveKind.Nvme)]
    [InlineData("WD Blue SN580 1TB", PhysicalDriveKind.Nvme)]
    [InlineData("Crucial P3 Plus 2TB", PhysicalDriveKind.Nvme)]
    [InlineData("KINGSTON NV2 500GB", PhysicalDriveKind.Nvme)]
    [InlineData("Crucial MX500 SSD 1TB", PhysicalDriveKind.Ssd)]
    [InlineData("KINGSTON KC3000 1TB", PhysicalDriveKind.Ssd)]
    [InlineData("ST2000DM008-2FR102 (Barracuda)", PhysicalDriveKind.Hdd)]
    [InlineData("Seagate IronWolf 4TB", PhysicalDriveKind.Hdd)]
    [InlineData("WD40EZRZ", PhysicalDriveKind.Hdd)]
    [InlineData("TOSHIBA DT01ACA100", PhysicalDriveKind.Hdd)]
    [InlineData("Some Unrecognized Model X1", PhysicalDriveKind.Unknown)]
    [InlineData(null, PhysicalDriveKind.Unknown)]
    [InlineData("", PhysicalDriveKind.Unknown)]
    [InlineData("   ", PhysicalDriveKind.Unknown)]
    public void GuessFromModelName_MatchesExpectedKind(string? model, PhysicalDriveKind expected)
    {
        Assert.Equal(expected, DriveTypeService.GuessFromModelName(model));
    }

    [Fact]
    public void GuessFromModelName_NvmePatternWinsOverGenericSsd()
    {
        // Το "EVO" (NVMe pattern) πρέπει να ελεγχθεί ΠΡΙΝ το γενικό "SSD" branch - ένα μοντέλο που
        // περιέχει και τα δύο λέξη-κλειδιά πρέπει να ταξινομείται ως Nvme, όχι απλό Ssd.
        Assert.Equal(PhysicalDriveKind.Nvme, DriveTypeService.GuessFromModelName("Samsung SSD 990 EVO"));
    }

    [Fact]
    public void GuessFromModelName_IsCaseInsensitive()
    {
        Assert.Equal(PhysicalDriveKind.Nvme, DriveTypeService.GuessFromModelName("samsung nvme 970 evo"));
    }
}
