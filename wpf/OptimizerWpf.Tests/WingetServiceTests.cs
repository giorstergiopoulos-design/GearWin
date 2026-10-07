using System.Linq;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests;

// ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "polish 6.3.5 με automated tests" - inspect winget's update/balloon
// mechanism): καλύπτει το πραγματικό bug που βρέθηκε - ParseWingetTable διάβαζε τη στήλη "Source" του
// πίνακα του winget και μετά την πετούσε, αντικαθιστώντας ΠΑΝΤΑ με το literal "winget" (βλ.
// WingetService.cs's ScanAsync/ParseWingetTable) - γραμμές με πραγματική πηγή "msstore" χάνονταν στο
// ήδη σωστό "άνοιξε το Store, μην το μέτρησες ως αποτυχία" μονοπάτι (OptimizationView.xaml.cs) και
// κατέληγαν σε ένα plain "winget upgrade --id" που το ίδιο το winget μπορεί να αποτύχει/να ανοίξει
// μόνο του το Store γι' αυτή την πηγή πακέτων.
public class WingetServiceTests
{
    // Ίδια λογική στοίχισης στηλών με το πραγματικό winget upgrade output - κάθε στήλη με σταθερό
    // πλάτος ώστε οι θέσεις να είναι προβλέψιμες (το ParseWingetTable διαβάζει με βάση τη θέση
    // χαρακτήρα κάθε επικεφαλίδας μέσα στη γραμμή τίτλου, όχι διαχωριστικά tab/κόμμα).
    private static string Row(string name, string id, string version, string available, string? source = null)
    {
        var row = name.PadRight(28) + id.PadRight(32) + version.PadRight(16) + available.PadRight(16);
        return source == null ? row.TrimEnd() : row + source;
    }

    [Fact]
    public void ParseWingetTable_PreservesRealSourceColumn()
    {
        var header = Row("Name", "Id", "Version", "Available", "Source");
        var sep = new string('-', header.Length);
        var row1 = Row("7-Zip 23.01", "7zip.7zip", "23.00", "23.01", "winget");
        var row2 = Row("Discord", "Discord.Discord", "1.0.9", "1.0.9013", "msstore");
        var text = string.Join("\n", header, sep, row1, row2, "2 upgrades available.");

        var results = WingetService.ParseWingetTable(text);

        Assert.Equal(2, results.Count);
        Assert.Equal("7zip.7zip", results[0].Id);
        Assert.Equal("winget", results[0].Source);
        Assert.Equal("Discord.Discord", results[1].Id);
        // ΔΙΟΡΘΩΣΗ: πριν αυτό θα ήταν ΠΑΝΤΑ "winget", ανεξάρτητα από τι έλεγε ο πραγματικός πίνακας.
        Assert.Equal("msstore", results[1].Source);
    }

    [Fact]
    public void ParseWingetTable_MissingSourceColumnDefaultsToWinget()
    {
        // Παλιότερες/διαφορετικά ρυθμισμένες εκδόσεις winget μπορεί να μην έχουν καν στήλη Source -
        // το fallback πρέπει να παραμένει ασφαλές ("winget"), όχι κενό/null.
        var header = "Name".PadRight(28) + "Id".PadRight(32) + "Version".PadRight(16) + "Available";
        var sep = new string('-', header.Length);
        var row = "7-Zip 23.01".PadRight(28) + "7zip.7zip".PadRight(32) + "23.00".PadRight(16) + "23.01";
        var text = string.Join("\n", header, sep, row);

        var results = WingetService.ParseWingetTable(text);

        Assert.Single(results);
        Assert.Equal("winget", results[0].Source);
    }

    [Fact]
    public void ParseWingetTable_SkipsSeparatorAndSummaryLines()
    {
        var header = Row("Name", "Id", "Version", "Available", "Source");
        var sep = new string('-', header.Length);
        var row = Row("7-Zip 23.01", "7zip.7zip", "23.00", "23.01", "winget");
        var text = string.Join("\n", header, sep, row, "1 upgrades available.");

        var results = WingetService.ParseWingetTable(text);

        Assert.Single(results);
        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.Id)));
    }

    [Fact]
    public void ParseWingetTable_NoHeaderReturnsEmpty() =>
        Assert.Empty(WingetService.ParseWingetTable("No installed package found matching input criteria."));
}
