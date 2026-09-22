using System.Text.Json;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests;

// ΝΕΟ - χρήστης έστειλε ζωντανό screenshot σφάλματος: "The requested operation requires an element
// of type 'String', but the target element has type 'Number'." - PowerShell's ConvertTo-Json δεν
// σειριοποιεί πάντα το Get-WindowsOptionalFeature's State (Dism enum) ως string· για τουλάχιστον
// μία λειτουργία σε πραγματικό σύστημα βγήκε ως ακέραιος, και το τυφλό JsonElement.GetString()
// πετούσε εξαίρεση που ακύρωνε ΟΛΟΚΛΗΡΗ τη σάρωση (εμφανιζόταν ως λανθασμένο μήνυμα "τρέξε ως
// Διαχειριστής"). Αυτά τα tests επαληθεύουν ότι το IsFeatureStateEnabled χειρίζεται και τις δύο
// πιθανές μορφές JSON χωρίς exception - δεν μπορεί να αναπαραχθεί ζωντανά χωρίς πραγματικό elevated
// Windows session, οπότε ο έλεγχος γίνεται απευθείας πάνω σε κατασκευασμένα JsonElement.
public class AdvancedToolsServiceTests
{
    private static JsonElement ParseFeature(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void IsFeatureStateEnabled_StringEnabled_ReturnsTrue()
    {
        var el = ParseFeature("""{"FeatureName":"Foo","State":"Enabled"}""");
        Assert.True(AdvancedToolsService.IsFeatureStateEnabled(el));
    }

    [Fact]
    public void IsFeatureStateEnabled_StringDisabled_ReturnsFalse()
    {
        var el = ParseFeature("""{"FeatureName":"Foo","State":"Disabled"}""");
        Assert.False(AdvancedToolsService.IsFeatureStateEnabled(el));
    }

    [Fact]
    public void IsFeatureStateEnabled_NumericOne_ReturnsTrue()
    {
        // Το πραγματικό, ζωντανά αναπαραγμένο σενάριο σφάλματος - State ως ακέραιος αντί για string.
        var el = ParseFeature("""{"FeatureName":"Foo","State":1}""");
        Assert.True(AdvancedToolsService.IsFeatureStateEnabled(el));
    }

    [Fact]
    public void IsFeatureStateEnabled_NumericOther_ReturnsFalseWithoutThrowing()
    {
        var el = ParseFeature("""{"FeatureName":"Foo","State":2}""");
        Assert.False(AdvancedToolsService.IsFeatureStateEnabled(el));
    }

    [Fact]
    public void IsFeatureStateEnabled_MissingState_ReturnsFalseWithoutThrowing()
    {
        var el = ParseFeature("""{"FeatureName":"Foo"}""");
        Assert.False(AdvancedToolsService.IsFeatureStateEnabled(el));
    }

    [Fact]
    public void IsFeatureStateEnabled_NullState_ReturnsFalseWithoutThrowing()
    {
        var el = ParseFeature("""{"FeatureName":"Foo","State":null}""");
        Assert.False(AdvancedToolsService.IsFeatureStateEnabled(el));
    }
}
