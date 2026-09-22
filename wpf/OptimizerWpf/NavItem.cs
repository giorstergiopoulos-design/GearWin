using System.Collections.Generic;
using OptimizerWpf.Services;
using OptimizerWpf.Views;

namespace OptimizerWpf
{
    // Ενιαία πηγή αλήθειας για τα 8 στοιχεία πλοήγησης (τις "καρτέλες") - πλέον αντλείται από το
    // κλασικό μενού ΚΑΙ το νέο πλευρικό μενού (Views/SidebarNav.xaml), αντί να είναι χειροκίνητα
    // αντιγραμμένη λίστα σε 2-3 ξεχωριστά σημεία XAML (ρίσκο: προσθήκη νέας καρτέλας ξεχνιέται σε
    // ένα από τα σημεία). Η λωρίδα καρτελών (TabStrip, MainWindow.xaml) παραμένει σκόπιμα
    // χειρόγραφη προς το παρόν - ήδη δοκιμασμένη/σταθερή, η μετατροπή της σε data-driven θα
    // απαιτούσε ξαναγράψιμο του IsChecked-timing fix (βλ. HANDOFF.md) χωρίς αντίστοιχο όφελος αφού
    // δεν είναι αυτή η πηγή του προβλήματος διπλότυπης κατηγοριοποίησης.
    //
    // Είναι επίσης η βάση που κάνει το πλευρικό μενού skin-agnostic (ρητό αίτημα χρήστη - βλ.
    // HANDOFF.md §0.4ζ): οποιοδήποτε μελλοντικό skin (Windows Classic, PC Manager-equivalent) μπορεί
    // να τοποθετήσει το ΙΔΙΟ SidebarNav (ή να αντλήσει απευθείας το NavItems.All) στο δικό του
    // window chrome χωρίς να ξαναορίσει κατηγορίες/tags.
    // ΔΙΟΡΘΩΣΗ (roadmap "Οπτικά": "emoji εμφανίζονται ως περιγράμματα") - το Icon (string emoji)
    // παραμένει για το κλασικό μενού/αποτελέσματα αναζήτησης (απλό κείμενο, δεν μπορεί να φιλοξενήσει
    // control) - το ΝΕΟ IconKind χρησιμοποιείται από το πλευρικό μενού (SidebarNav.xaml, ορατό surface,
    // FontSize 24 - το πιο εμφανές σημείο όπου το σπασμένο emoji rendering φαινόταν) για το ίδιο
    // διανυσματικό HeaderGlyphIcon που πλέον χρησιμοποιείται σε όλη την εφαρμογή.
    public record NavItem(string Tag, string Icon, string Label, GlyphKind IconKind);

    public static class NavItems
    {
        // Property (όχι readonly field) ώστε το πλευρικό μενού/PC Manager rail/κλασικό μενού να
        // ακολουθούν την τρέχουσα γλώσσα - ίδια κλειδιά μετάφρασης με τη λωρίδα καρτελών
        // (LblTabHome κ.λπ. στο MainWindow.xaml.cs), ώστε τα δύο να μη συγκλίνουν ποτέ σε
        // διαφορετικό κείμενο (ρητό αίτημα χρήστη: "μετάφρασε τα όλα").
        public static IReadOnlyList<NavItem> All => new[]
        {
            new NavItem("Home", "\U0001F3E0", LanguageService.T("TabHome"), GlyphKind.Home),
            new NavItem("Optimization", "⚡", LanguageService.T("TabOptimization"), GlyphKind.Bolt),
            new NavItem("Health", "❤", LanguageService.T("TabHealth"), GlyphKind.Heart),
            new NavItem("Network", "\U0001F310", LanguageService.T("TabNetwork"), GlyphKind.Globe),
            new NavItem("Tweaks", "\U0001F527", LanguageService.T("TabTweaks"), GlyphKind.Wrench),
            new NavItem("Bloatware", "\U0001F9F9", LanguageService.T("TabBloatware"), GlyphKind.Broom),
            new NavItem("Advanced", "\U0001F6E0", LanguageService.T("TabAdvanced"), GlyphKind.Tools),
            new NavItem("System", "\U0001F5A5", LanguageService.T("TabSystem"), GlyphKind.Monitor),
        };
    }
}
