using System.Collections.Generic;
using System.Linq;
using OptimizerWpf.Services;

namespace OptimizerWpf
{
    // Είτε πλοήγηση σε καρτέλα (NavigateTag) είτε άνοιγμα δευτερεύοντος παραθύρου (DestinationKey) -
    // ακριβώς τα ίδια δύο "είδη" κλικ που ήδη υπήρχαν σκορπισμένα στο MainWindow.xaml.cs
    // (MenuViewTab_Click vs MenuNotPorted_Click). ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε μετά τη
    // μετάφραση του μενού): το DestinationKey ΠΡΕΠΕΙ να είναι σταθερό, ουδέτερο-ως-προς-γλώσσα
    // αναγνωριστικό (π.χ. το ίδιο το LanguageService key name, ΟΧΙ η μεταφρασμένη τιμή του) - το
    // OpenDestination (MainWindow.xaml.cs) του κάνει match σε C# switch, οπότε αν εδώ περνούσε το
    // ήδη-μεταφρασμένο κείμενο, η δρομολόγηση θα έσπαγε σε κάθε γλώσσα εκτός Ελληνικών.
    public record MenuAction(string? NavigateTag, string? DestinationKey);

    public record MenuLeaf(string Icon, string Label, MenuAction Action, string? Shortcut = null);

    // Flat=true - port του ps1's $menuActionLog (~6671): top-level, μονό στοιχείο ΧΩΡΙΣ υπομενού
    // (κλικ απευθείας στο ίδιο το menu item), σε αντίθεση με τις υπόλοιπες ομάδες που ανοίγουν flyout.
    public record MenuGroup(string Icon, string Header, IReadOnlyList<MenuLeaf> Items, bool Flat = false);

    // Ενιαίο μοντέλο για ΟΛΟ το περιεχόμενο του κλασικού μενού (Εργαλεία/Προβολή/Ρυθμίσεις/Βοήθεια) -
    // πλέον αντλείται ΚΑΙ από το ίδιο το κλασικό `Menu` (MainWindow.xaml) ΚΑΙ από το πλευρικό μενού
    // (Views/SidebarNav.xaml), ώστε τα δύο να ΜΗΝ μπορούν ποτέ να αποκλίνουν (χρήστης ζήτησε ρητά:
    // "το πλευρικό μενού έχει ουσιαστικά το περιεχόμενο του κλασικού μενού"). Το NavItems.All (μόνο
    // οι 8 καρτέλες, χωρίς Εργαλεία/Ρυθμίσεις/Βοήθεια) παραμένει η βάση για την ομάδα "Προβολή" εδώ
    // ΚΑΙ είναι ό,τι θα χρειαστεί από μόνο του ένα μελλοντικό, πιο minimal skin (π.χ. το σχεδιαζόμενο
    // PC Manager-equivalent - βλ. SidebarNav.ShowOnlyNavigationGroup).
    public static class ClassicMenuModel
    {
        // Property (όχι readonly field) ώστε να ξαναχτίζεται με την τρέχουσα γλώσσα - βλ.
        // MainWindow.xaml.cs's ApplyLanguage(), που ξαναχτίζει το μενού/search index σε κάθε αλλαγή
        // γλώσσας (ρητό αίτημα χρήστη: "μετάφρασε τα όλα").
        public static IReadOnlyList<MenuGroup> Groups => new[]
        {
            new MenuGroup("🛠️", LanguageService.T("Menu_Tools"), new[]
            {
                new MenuLeaf("", $"{LanguageService.T("Vive_Title")}...", new MenuAction(null, "Vive_Title")),
                new MenuLeaf("", $"{LanguageService.T("Uwp_Title")}...", new MenuAction(null, "Uwp_Title")),
                // ΝΕΟ - πρόσθεσε πρόσβαση από το κλασικό/πλευρικό μενού, βλ. σχόλιο στο
                // MainWindow.xaml.cs's OpenDestination.
                new MenuLeaf("", $"{LanguageService.T("HealthCheck_Title")}...", new MenuAction(null, "HealthCheck_Title"), "Ctrl+H"),
            }),
            new MenuGroup("👁️", LanguageService.T("Menu_View"), NavItems.All
                .Select((n, i) => new MenuLeaf(n.Icon, n.Label, new MenuAction(n.Tag, null), i < 8 ? $"Ctrl+{i + 1}" : null))
                .ToList()),
            new MenuGroup("⚙️", LanguageService.T("Menu_Settings"), new[]
            {
                new MenuLeaf("", $"{LanguageService.T("AppearanceSettingsTitle")}...", new MenuAction(null, "AppearanceSettingsTitle")),
            }),
            new MenuGroup("📋", LanguageService.T("ActionLog_Title"), new[]
            {
                new MenuLeaf("", LanguageService.T("ActionLog_Title"), new MenuAction(null, "ActionLog_Title")),
            }, Flat: true),
            new MenuGroup("❓", LanguageService.T("Menu_Help"), new[]
            {
                new MenuLeaf("", LanguageService.T("VerHist_Title"), new MenuAction(null, "VerHist_Title")),
                new MenuLeaf("", LanguageService.T("Help_Title"), new MenuAction(null, "Help_Title")),
                // Ρητό αίτημα χρήστη: "με ξεχωριστό υπομενού στο κλασικό μενού άδεια χρήσης", μετά
                // "στην Βοήθεια/Οδηγίες να ανοίγει το παράθυρο με δύο tabs όπου το δεύτερο θα είναι η
                // άδεια χρήσης" - ξεχωριστό στοιχείο υπομενού εδώ, αλλά ανοίγει το ΙΔΙΟ HelpWindow
                // κατευθείαν στο 2ο tab (βλ. MainWindow.xaml.cs's OpenDestination), όχι πια δικό του
                // παράθυρο.
                new MenuLeaf("", LanguageService.T("License_Title"), new MenuAction(null, "License_Title")),
            }),
        };
    }
}
