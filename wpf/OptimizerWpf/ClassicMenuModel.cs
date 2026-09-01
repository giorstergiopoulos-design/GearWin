using System.Collections.Generic;
using System.Linq;

namespace OptimizerWpf
{
    // Είτε πλοήγηση σε καρτέλα (NavigateTag) είτε λειτουργία που δεν έχει μεταφερθεί ακόμα
    // (NotPortedLabel) - ακριβώς τα ίδια δύο "είδη" κλικ που ήδη υπήρχαν σκορπισμένα στο
    // MainWindow.xaml.cs (MenuViewTab_Click vs MenuNotPorted_Click).
    public record MenuAction(string? NavigateTag, string? NotPortedLabel);

    public record MenuLeaf(string Icon, string Label, MenuAction Action, string? Shortcut = null);

    public record MenuGroup(string Icon, string Header, IReadOnlyList<MenuLeaf> Items);

    // Ενιαίο μοντέλο για ΟΛΟ το περιεχόμενο του κλασικού μενού (Εργαλεία/Προβολή/Ρυθμίσεις/Βοήθεια) -
    // πλέον αντλείται ΚΑΙ από το ίδιο το κλασικό `Menu` (MainWindow.xaml) ΚΑΙ από το πλευρικό μενού
    // (Views/SidebarNav.xaml), ώστε τα δύο να ΜΗΝ μπορούν ποτέ να αποκλίνουν (χρήστης ζήτησε ρητά:
    // "το πλευρικό μενού έχει ουσιαστικά το περιεχόμενο του κλασικού μενού"). Το NavItems.All (μόνο
    // οι 8 καρτέλες, χωρίς Εργαλεία/Ρυθμίσεις/Βοήθεια) παραμένει η βάση για την ομάδα "Προβολή" εδώ
    // ΚΑΙ είναι ό,τι θα χρειαστεί από μόνο του ένα μελλοντικό, πιο minimal skin (π.χ. το σχεδιαζόμενο
    // PC Manager-equivalent - βλ. SidebarNav.ShowOnlyNavigationGroup).
    public static class ClassicMenuModel
    {
        public static readonly IReadOnlyList<MenuGroup> Groups = new[]
        {
            new MenuGroup("🛠️", "Εργαλεία", new[]
            {
                new MenuLeaf("", "Κρυφές Λειτουργίες (ViVeTool)...", new MenuAction(null, "Κρυφές Λειτουργίες (ViVeTool)")),
                new MenuLeaf("", "Διαχείριση UWP Εφαρμογών...", new MenuAction(null, "Διαχείριση UWP Εφαρμογών")),
            }),
            new MenuGroup("👁️", "Προβολή", NavItems.All
                .Select((n, i) => new MenuLeaf(n.Icon, n.Label, new MenuAction(n.Tag, null), i < 8 ? $"Ctrl+{i + 1}" : null))
                .ToList()),
            new MenuGroup("⚙️", "Ρυθμίσεις", new[]
            {
                new MenuLeaf("", "Εμφάνιση Εφαρμογής...", new MenuAction(null, "Εμφάνιση Εφαρμογής")),
            }),
            new MenuGroup("❓", "Βοήθεια", new[]
            {
                new MenuLeaf("", "Ιστορικό Εκδόσεων", new MenuAction(null, "Ιστορικό Εκδόσεων")),
                new MenuLeaf("", "Οδηγίες & Πληροφορίες", new MenuAction(null, "Οδηγίες & Πληροφορίες")),
            }),
        };
    }
}
