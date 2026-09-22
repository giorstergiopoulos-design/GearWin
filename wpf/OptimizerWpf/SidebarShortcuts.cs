using System.Collections.Generic;
using OptimizerWpf.Services;
using OptimizerWpf.Views;

namespace OptimizerWpf
{
    // Port του ΠΡΑΓΜΑΤΙΚΟΥ μοντέρνου πλευρικού μενού του Optimizer.ps1 (Add-SidebarItem/Show-Sidebar/
    // Hide-Sidebar, ~7937-8129) - ΔΙΟΡΘΩΣΗ μετά από ρητή παρατήρηση χρήστη ότι το πρώτο πέρασμα
    // (grouped, με τις 8 καρτέλες μέσα) ήταν λάθος σχεδιασμός δικής μου επινόησης αντί να ακολουθήσει
    // το πραγματικό ps1 ως reference. Το πραγματικό πλευρικό μενού είναι ΕΝΑ ΕΠΙΠΕΔΟ (χωρίς
    // ομάδες/submenus) σύνολο 6 συντομεύσεων προς δευτερεύοντα παράθυρα - ΔΕΝ περιλαμβάνει καθόλου
    // τις καρτέλες (αυτές έχουν ήδη τη δική τους οριζόντια λωρίδα). Κάθε στοιχείο: μεγάλο εικονίδιο
    // + πλήρης ετικέτα (έως 2 γραμμές) από κάτω, τετραγωνισμένο pill (~90x96, βλ. Add-SidebarItem
    // Size 86x90). Οι ετικέτες αντιγράφονται ΑΚΡΙΒΩΣ από τα δικά του "SidebarXxx" translation keys -
    // ΣΚΟΠΙΜΑ διαφορετικές από τις αντίστοιχες ετικέτες του κλασικού μενού για τον ΙΔΙΟ προορισμό
    // (το ίδιο το ps1 έχει ξεχωριστά $sbKeys/$hmKeys σύνολα ακριβώς γι' αυτό - όχι ασυνέπεια προς
    // διόρθωση).
    // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε μετά τη μετάφραση του πλευρικού μενού): το
    // DestinationKey (πρώην NotPortedLabel) ΠΡΕΠΕΙ να είναι σταθερό, ουδέτερο-ως-προς-γλώσσα
    // αναγνωριστικό - βλ. ίδιο σχόλιο στο ClassicMenuModel.cs/MenuAction.
    // ΔΙΟΡΘΩΣΗ (roadmap "Οπτικά": "emoji εμφανίζονται ως περιγράμματα") - IconKind προστέθηκε για το
    // ίδιο διανυσματικό HeaderGlyphIcon (βλ. SidebarNav.xaml) - το Icon (emoji string) παραμένει
    // αχρησιμοποίητο πλέον στο UI αλλά διατηρείται για να μη σπάσει τυχόν άλλη αναφορά.
    public record SidebarShortcut(string Icon, string Label, string DestinationKey, GlyphKind IconKind);

    public static class SidebarShortcuts
    {
        // Property (όχι readonly field) ώστε το πλευρικό μενού να ακολουθεί την τρέχουσα γλώσσα -
        // ρητό αίτημα χρήστη: "μετάφρασε τα όλα".
        public static IReadOnlyList<SidebarShortcut> All => new[]
        {
            // Ρητό σπάσιμο σε 2 γραμμές στο " & " (ίδιο με το ps1's $lbl.Text -replace ' & ', "`n") -
            // προβλέψιμο αποτέλεσμα αντί να βασίζεται στο αυτόματο word-wrap του WPF, που θα
            // μπορούσε να σπάσει αλλού (π.χ. "Οδηγίες &" / "Βοήθεια").
            new SidebarShortcut("❓", LanguageService.T("Sidebar_HelpLabel"), "Help_Title", GlyphKind.Question),
            // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη) - το "Ιστορικό Εκδόσεων" αντικαταστάθηκε: είναι πλέον
            // περιττό εδώ αφού είναι ήδη προσβάσιμο ως tab μέσα στο Βοήθεια/Οδηγίες (βλ. σχόλιο στο
            // MainWindow.xaml.cs's OpenDestination). Το Ιστορικό Πρόχειρου ήταν μέχρι τώρα προσβάσιμο
            // ΜΟΝΟ μέσω tray icon/Win+Shift+V - πραγματικό δευτερεύον παράθυρο που άξιζε θέση εδώ.
            new SidebarShortcut("📋", LanguageService.T("Clipboard_Title"), "Clipboard_Title", GlyphKind.Document),
            new SidebarShortcut("📄", LanguageService.T("ActionLog_Title"), "ActionLog_Title", GlyphKind.Document),
            new SidebarShortcut("🧩", LanguageService.T("Sidebar_ViveToolLabel"), "Vive_Title", GlyphKind.Puzzle),
            new SidebarShortcut("📱", LanguageService.T("Sidebar_UwpManagerLabel"), "Uwp_Title", GlyphKind.Phone),
            new SidebarShortcut("🎨", LanguageService.T("AppearanceSettingsTitle"), "AppearanceSettingsTitle", GlyphKind.Palette),
        };
    }
}
