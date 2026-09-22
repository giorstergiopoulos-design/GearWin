using System;
using OptimizerWpf.Views;

namespace OptimizerWpf.Tests;

// ΝΕΟ - roadmap "Περισσότερα unit tests" (v4.3.5). Καλύπτει το "AI"-στυλ scoring (StorageResultRow's
// Score(), internal ειδικά για testability - ίδιο μοτίβο με το ήδη υπάρχον DriverService.IsDateNewer)
// που πρόσθεσε η v4.3.0 - ηλικία τελευταίας τροποποίησης + τύπος επέκτασης αρχείου, ρητά ΟΧΙ
// πραγματικό ML. Δοκιμάζεται το ΑΡΙΘΜΗΤΙΚΟ σκορ αντί για το μεταφρασμένο SafetyLabel, ώστε τα tests
// να μην εξαρτώνται από το καθολικό, mutable LanguageService.Current (που άλλα tests εναλλάσσουν).
public class StorageResultRowTests
{
    private static StorageResultRow Row(string path, int daysOld) =>
        new(path, 1.0, DateTime.Now.AddDays(-daysOld));

    [Fact]
    public void Score_OldTempFile_IsHigh()
    {
        // 400 μέρες (>=365 -> 55) + .tmp (+35) = 90
        Assert.Equal(90, Row(@"C:\Temp\old.tmp", 400).Score());
    }

    [Fact]
    public void Score_RecentExecutable_ClampsToZero()
    {
        // 1 μέρα (5) + .exe (-40) -> αρνητικό, clamp στο 0
        Assert.Equal(0, Row(@"C:\Program Files\app.exe", 1).Score());
    }

    [Fact]
    public void Score_OldExecutable_NeverReachesHighTier()
    {
        // Ακόμα κι αν είναι πολύ παλιό (>=365 -> 55), το .exe (-40) κρατά το σκορ στο 15 - ΠΟΤΕ
        // δεν φτάνει το όριο "Υψηλή" (>=65) για εκτελέσιμο αρχείο.
        Assert.Equal(15, Row(@"C:\Program Files\old.exe", 1000).Score());
        Assert.True(Row(@"C:\Program Files\old.exe", 1000).Score() < 65);
    }

    [Fact]
    public void Score_MidAgeUnknownExtension_IsBelowMediumThreshold()
    {
        // 90 μέρες (>=30 -> 20) χωρίς ειδική επέκταση -> 20, κάτω από το όριο "Μέτρια" (35)
        Assert.Equal(20, Row(@"C:\Users\test\notes.docx", 90).Score());
    }

    [Fact]
    public void Score_OldUnknownExtension_ReachesMediumTier()
    {
        // 200 μέρες (>=180 -> 40) χωρίς ειδική επέκταση -> 40, μέσα στο εύρος "Μέτρια" [35,65)
        var score = Row(@"C:\Users\test\report.pdf", 200).Score();
        Assert.InRange(score, 35, 64);
    }

    [Fact]
    public void SafetyColor_MatchesScoreTier()
    {
        Assert.Equal("#FF4CAF50", Row(@"C:\Temp\old.tmp", 400).SafetyColor);   // Υψηλή -> πράσινο
        Assert.Equal("#FF9E9E9E", Row(@"C:\Program Files\app.exe", 1).SafetyColor); // Χαμηλή -> γκρι
    }
}
