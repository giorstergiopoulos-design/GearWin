using System.Collections.Generic;
using System.Linq;

namespace OptimizerWpf
{
    // Πώς πλοηγείται ο χρήστης σε ένα skin: κανονική λωρίδα καρτελών, στενή κάθετη μπάρα εικονιδίων,
    // πλατιά κάθετη μπάρα εικονιδίου+κειμένου (τύπου PC Manager / Ρυθμίσεις των Windows 11), ή μόνο κλασικό μενού.
    public enum SkinNav { Tabs, CompactRail, WideRail, MenuOnly }

    // 6.1.0 - ΕΝΑ σημείο ορισμού κάθε skin (πριν ήταν διάσπαρτα "if DisplayName == ..." σε 4 αρχεία).
    // Skin = παλέτα θέματος (ThemeCatalog) + διάταξη (Nav, μενού) + σχήμα (γωνίες/περίγραμμα κουμπιών).
    // FlatBackground: χωρίς κινούμενο/στατικό φόντο (τα πραγματικά Mica-στυλ UI δεν έχουν τέτοιο).
    // HeroHome: η Αρχική εμφανίζεται ως PC Manager "hero" (Ενίσχυση + Έλεγχος υγείας) αντί για την κανονική.
    public record SkinInfo(string ThemeName, SkinNav Nav, bool ShowMenuBar, double CornerRadius, double ButtonBorder,
        bool FlatBackground, bool HeroHome);

    public static class SkinCatalog
    {
        public static readonly IReadOnlyList<SkinInfo> All = new[]
        {
            new SkinInfo("Microsoft PC Manager", SkinNav.WideRail, false, 8, 0, true, true),
            new SkinInfo("Windows Classic", SkinNav.MenuOnly, true, 0, 1, false, false),
            new SkinInfo("Windows 11 Settings", SkinNav.WideRail, false, 8, 0, true, false),
            new SkinInfo("Gaming Hub", SkinNav.CompactRail, false, 14, 0, false, false),
            new SkinInfo("Office Ribbon", SkinNav.Tabs, true, 2, 1, true, false),
        };

        public static SkinInfo? For(string themeName) => All.FirstOrDefault(s => s.ThemeName == themeName);

        public static bool IsSkin(string themeName) => For(themeName) != null;
    }
}
