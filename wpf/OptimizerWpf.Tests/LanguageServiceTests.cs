using OptimizerWpf.Services;

namespace OptimizerWpf.Tests;

// Πρόταση βελτίωσης που εγκρίθηκε ("κάνε τα όλα") - επαληθεύει το ασφαλές fallback του i18n μηχανισμού
// (πάντα επιστρέφει κάτι λογικό, ποτέ crash/κενό string για ένα άγνωστο κλειδί ή γλώσσα).
public class LanguageServiceTests
{
    [Fact]
    public void T_ReturnsGreekByDefault()
    {
        Assert.Equal("Κλείσιμο", LanguageService.T("Close"));
    }

    [Fact]
    public void T_UnknownKeyReturnsTheKeyItself_NeverThrows()
    {
        var result = LanguageService.T("ThisKeyDoesNotExist");
        Assert.Equal("ThisKeyDoesNotExist", result);
    }

    [Fact]
    public void SetLanguage_SwitchesTranslationsAndPersists()
    {
        var original = LanguageService.Current;
        try
        {
            LanguageService.SetLanguage("en");
            Assert.Equal("en", LanguageService.Current);
            Assert.Equal("Close", LanguageService.T("Close"));

            LanguageService.SetLanguage("de");
            Assert.Equal("Schließen", LanguageService.T("Close"));
        }
        finally
        {
            LanguageService.SetLanguage(original); // δεν πρέπει να επηρεάζει άλλα tests που τρέχουν μετά
        }
    }

    [Fact]
    public void SetLanguage_UnknownCodeIsIgnored()
    {
        var original = LanguageService.Current;
        LanguageService.SetLanguage("xx-not-a-real-language");
        Assert.Equal(original, LanguageService.Current);
    }
}
