using System.Collections.Generic;

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
    public record NavItem(string Tag, string Icon, string Label);

    public static class NavItems
    {
        public static readonly IReadOnlyList<NavItem> All = new[]
        {
            new NavItem("Home", "\U0001F3E0", "Αρχική"),
            new NavItem("Optimization", "⚡", "Βελτιστοποίηση"),
            new NavItem("Health", "✚", "Υγεία & Συντήρηση"),
            new NavItem("Network", "\U0001F310", "Δίκτυο & Ασφάλεια"),
            new NavItem("Tweaks", "\U0001F527", "Επιπλέον Ρυθμίσεις"),
            new NavItem("Bloatware", "\U0001F9F9", "Εφαρμογές & Bloat"),
            new NavItem("Advanced", "\U0001F6E0", "Προηγμένα Εργαλεία"),
            new NavItem("System", "\U0001F5A5", "Σύστημα"),
        };
    }
}
