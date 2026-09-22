using OptimizerWpf.Services;

namespace OptimizerWpf.Tests;

// ΝΕΟ - roadmap "Περισσότερα unit tests" / "Κίνηση δικτύου ανά εφαρμογή" (v4.3.5). Καλύπτει την
// καθαρή μαθηματική λογική (BytesToKBps, internal ειδικά για testability) του νέου
// NetworkTrafficService - ΟΧΙ την ίδια την ETW συλλογή (απαιτεί πραγματικό kernel session/
// Administrator, εκτός εμβέλειας για unit test).
public class NetworkTrafficServiceTests
{
    [Fact]
    public void BytesToKBps_ConvertsCorrectly()
    {
        Assert.Equal(1.0, NetworkTrafficService.BytesToKBps(1024, 1.0));
        Assert.Equal(2.0, NetworkTrafficService.BytesToKBps(1024, 0.5));
        Assert.Equal(0.5, NetworkTrafficService.BytesToKBps(1024, 2.0));
    }

    [Fact]
    public void BytesToKBps_ReturnsZero_ForZeroOrNegativeWindow()
    {
        Assert.Equal(0, NetworkTrafficService.BytesToKBps(1024, 0));
        Assert.Equal(0, NetworkTrafficService.BytesToKBps(1024, -1));
    }

    [Fact]
    public void BytesToKBps_ZeroBytesIsZero()
    {
        Assert.Equal(0, NetworkTrafficService.BytesToKBps(0, 1.0));
    }
}
