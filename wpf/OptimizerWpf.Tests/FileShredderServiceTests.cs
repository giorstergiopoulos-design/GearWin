using System;
using System.IO;
using System.Threading.Tasks;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests;

// ΝΕΟ - roadmap "Περισσότερα unit tests" (v4.3.5). Πραγματικό I/O test (όχι mock) - δημιουργεί ένα
// πραγματικό προσωρινό αρχείο και επαληθεύει ότι το ShredFileAsync πράγματι το αφαιρεί από το
// αρχικό του path, ίδιο πνεύμα με τα υπάρχοντα tests που προτιμούν πραγματική συμπεριφορά αντί για
// mocked OS calls όπου είναι εφικτό χωρίς μεγάλο refactor.
public class FileShredderServiceTests
{
    [Fact]
    public async Task ShredFileAsync_RemovesFile_WhenItExists()
    {
        var path = Path.Combine(Path.GetTempPath(), "shred_test_" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(path, "sensitive content");

        var ok = await FileShredderService.ShredFileAsync(path, passes: 1);

        Assert.True(ok);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task ShredFileAsync_ReturnsFalse_WhenFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), "shred_test_missing_" + Guid.NewGuid().ToString("N") + ".txt");
        var ok = await FileShredderService.ShredFileAsync(path);
        Assert.False(ok);
    }
}
