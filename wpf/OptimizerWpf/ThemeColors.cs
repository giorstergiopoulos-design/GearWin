using System.Windows.Media;

namespace OptimizerWpf
{
    // One theme's full palette - mirrors the hashtable returned by Get-ThemeColors in Optimizer.ps1
    // (MainBg/TabBg/TabSelect/Text/SubText/StatusText/CardBg/CardBorder/BtnDefault/BtnHover/Accent).
    public record ThemeColors(
        string Name,
        Color MainBg,
        Color TabBg,
        Color TabSelect,
        Color Text,
        Color SubText,
        Color StatusText,
        Color CardBg,
        Color CardBorder,
        Color BtnDefault,
        Color BtnHover,
        Color Accent);

    // A theme's Dark AND Light palette together - every theme in Get-ThemeColors has both (the
    // function branches on $global:isDarkMode first, $currentThemeName second), so ThemeManager
    // toggling Light/Dark should always have real data to switch to, for every theme, not just
    // the default.
    public record ThemePair(string DisplayName, ThemeColors Dark, ThemeColors Light)
    {
        public ThemeColors Get(bool isDark) => isDark ? Dark : Light;

        // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε με screenshot: το dropdown θεμάτων έδειχνε
        // "ThemePair { DisplayName = Nordic Night, Dark = ... }" αντί για "Nordic Night") - records
        // παράγουν αυτόματο ToString() με ΟΛΑ τα πεδία τους· κάπου στο custom ComboBox template
        // (Themes/Styles.xaml) αυτό ξεπερνάει το DisplayMemberPath="DisplayName" για το κλειστό
        // πλαίσιο επιλογής. Το override εδώ εγγυάται σωστή εμφάνιση ΠΑΝΤΟΥ, ανεξάρτητα από ποιο
        // ακριβώς WPF μονοπάτι binding χρησιμοποιείται.
        public override string ToString() => DisplayName;
    }
}
