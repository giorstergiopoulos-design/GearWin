using OptimizerWpf.Services;

namespace OptimizerWpf.Tests;

// Πρόταση βελτίωσης που εγκρίθηκε ("κάνε τα όλα") - βασικά unit tests για την καθαρή λογική
// (χωρίς I/O/UI) του driver-engine, η οποία μέχρι τώρα δεν είχε καμία αυτοματοποιημένη επαλήθευση.
public class DriverServiceTests
{
    [Theory]
    [InlineData("2026-01-01", "2025-01-01", true)]   // νεότερο - πράγματι νεότερο
    [InlineData("2025-01-01", "2026-01-01", false)]  // "νεότερο" στην πραγματικότητα παλιότερο
    [InlineData("2025-01-01", "2025-01-01", false)]  // ίδια ημερομηνία - ΟΧΙ "νεότερο"
    [InlineData(null, "2025-01-01", false)]           // καμία διαθέσιμη ημερομηνία - δεν μπορεί να ισχυριστεί τίποτα
    [InlineData("2026-01-01", null, true)]            // άγνωστη εγκατεστημένη ημερομηνία, αλλά υπάρχει επίσημη - συντηρητικά "πιθανή ενημέρωση"
    [InlineData("not-a-date", "2025-01-01", false)]   // μη-έγκυρη ημερομηνία - ΠΟΤΕ crash, απλά false
    public void IsDateNewer_MatchesExpected(string? latest, string? installed, bool expected)
    {
        Assert.Equal(expected, DriverService.IsDateNewer(latest, installed));
    }

    [Fact]
    public void HasSourceOverlap_MatchesSubstringEitherDirection()
    {
        var confirmed = new[] { "NVIDIA GeForce RTX 4070" };
        Assert.True(DriverService.HasSourceOverlap("NVIDIA GeForce RTX 4070 Laptop GPU", confirmed));
        Assert.True(DriverService.HasSourceOverlap("GeForce RTX 4070", confirmed));
        Assert.False(DriverService.HasSourceOverlap("AMD Radeon RX 7900 GRE", confirmed));
    }

    [Fact]
    public void HasSourceOverlap_EmptyInputsNeverMatch()
    {
        Assert.False(DriverService.HasSourceOverlap(null, new[] { "Anything" }));
        Assert.False(DriverService.HasSourceOverlap("Some GPU", Array.Empty<string>()));
        Assert.False(DriverService.HasSourceOverlap("Some GPU", new[] { "", null! }));
    }
}
