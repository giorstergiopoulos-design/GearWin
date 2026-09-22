using System;
using System.IO;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests;

// ΝΕΟ - roadmap "Περισσότερα unit tests" (v4.3.5). Καλύπτει την καθαρή λογική του QuickCleanService
// που μέχρι τώρα δεν είχε καμία αυτοματοποιημένη επαλήθευση: το μέγεθος σε ανθρώπινα αναγνώσιμη
// μορφή (FormatSize) και τον αναδρομικό, ανά-φάκελο προστατευμένο περίπατο (DirSize) που διόρθωσε
// το πραγματικό bug με το lazy Directory.EnumerateFiles (βλ. HANDOFF.md/roadmap - "Γρήγορος
// Καθαρισμός κολλούσε σε σάρωση").
public class QuickCleanServiceTests
{
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(512L, "512 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1024L * 1024, "1 MB")]
    [InlineData(1024L * 1024 * 1024, "1 GB")]
    public void FormatSize_MatchesExpectedUnit(long bytes, string expected)
    {
        Assert.Equal(expected, QuickCleanService.FormatSize(bytes));
    }

    // Ξεχωριστό test για δεκαδικό μέγεθος - το δεκαδικό διαχωριστικό εξαρτάται από το τρέχον
    // culture (π.χ. "," σε el-GR, "." σε en-US), οπότε το αναμενόμενο κείμενο χτίζεται δυναμικά
    // αντί να είναι hardcoded, ώστε το test να περνάει ανεξάρτητα από το locale του μηχανήματος.
    [Fact]
    public void FormatSize_UsesCurrentCultureDecimalSeparator()
    {
        var sep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        Assert.Equal($"1{sep}5 KB", QuickCleanService.FormatSize(1536L));
    }

    [Fact]
    public void DirSize_SumsAllFilesRecursively()
    {
        var root = Directory.CreateTempSubdirectory("qc_test_").FullName;
        try
        {
            File.WriteAllBytes(Path.Combine(root, "a.txt"), new byte[100]);
            var sub = Directory.CreateDirectory(Path.Combine(root, "sub"));
            File.WriteAllBytes(Path.Combine(sub.FullName, "b.txt"), new byte[250]);
            var subsub = Directory.CreateDirectory(Path.Combine(sub.FullName, "deeper"));
            File.WriteAllBytes(Path.Combine(subsub.FullName, "c.txt"), new byte[10]);

            Assert.Equal(360L, QuickCleanService.DirSize(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DirSize_ReturnsZero_WhenDirectoryDoesNotExist()
    {
        var missing = Path.Combine(Path.GetTempPath(), "qc_test_does_not_exist_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(0L, QuickCleanService.DirSize(missing));
    }

    [Fact]
    public void DirSize_IgnoresEmptySubfolders()
    {
        var root = Directory.CreateTempSubdirectory("qc_test_").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "empty1"));
            Directory.CreateDirectory(Path.Combine(root, "empty2"));
            Assert.Equal(0L, QuickCleanService.DirSize(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
