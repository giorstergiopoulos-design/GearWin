using System;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests;

// ΝΕΟ - roadmap "Περισσότερα unit tests" (v4.3.5). Καλύπτει το in-memory sparkline ιστορικό
// (ProcessHistoryService) - καθαρή λογική, μηδενικό OS access. Κάθε test χρησιμοποιεί ΜΟΝΑΔΙΚΟ
// όνομα διεργασίας (Guid) επειδή το History dictionary είναι static/κοινόχρηστο μεταξύ όλων των
// tests (ίδιο σχέδιο με το ίδιο το service - "static ώστε να επιβιώνει σε αλλαγές καρτέλας") -
// χωρίς αυτό, παράλληλα tests θα μπορούσαν να παρέμβουν το ένα στο ιστορικό του άλλου.
public class ProcessHistoryServiceTests
{
    private static string UniqueName() => "test_proc_" + Guid.NewGuid().ToString("N");

    [Fact]
    public void GetSparkline_ReturnsEmpty_WhenNoSamplesRecorded()
    {
        Assert.Equal("", ProcessHistoryService.GetSparkline(UniqueName()));
    }

    [Fact]
    public void GetSparkline_ReturnsEmpty_WithOnlyOneSample()
    {
        var name = UniqueName();
        ProcessHistoryService.RecordSample(name, 100.0);
        Assert.Equal("", ProcessHistoryService.GetSparkline(name));
    }

    [Fact]
    public void GetSparkline_ReturnsFlatMiddleBlock_WhenAllSamplesEqual()
    {
        var name = UniqueName();
        ProcessHistoryService.RecordSample(name, 50.0);
        ProcessHistoryService.RecordSample(name, 50.0);
        ProcessHistoryService.RecordSample(name, 50.0);
        Assert.Equal("▄▄▄", ProcessHistoryService.GetSparkline(name));
    }

    [Fact]
    public void GetSparkline_LowestAndHighestSamplesMapToEndBlocks()
    {
        var name = UniqueName();
        ProcessHistoryService.RecordSample(name, 10.0);
        ProcessHistoryService.RecordSample(name, 100.0);
        var spark = ProcessHistoryService.GetSparkline(name);
        Assert.Equal('▁', spark[0]);
        Assert.Equal('█', spark[1]);
    }

    [Fact]
    public void RecordSample_CapsHistoryAtTwentySamples()
    {
        var name = UniqueName();
        for (var i = 0; i < 25; i++) ProcessHistoryService.RecordSample(name, i);
        // Δεν υπάρχει δημόσιο μέγεθος ιστορικού, αλλά το sparkline output length αντιστοιχεί 1:1
        // στον αριθμό διατηρημένων δειγμάτων (max 20, βλ. MaxSamples στο service).
        Assert.Equal(20, ProcessHistoryService.GetSparkline(name).Length);
    }
}
