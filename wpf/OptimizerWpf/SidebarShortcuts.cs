using System.Collections.Generic;

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
    public record SidebarShortcut(string Icon, string Label, string NotPortedLabel);

    public static class SidebarShortcuts
    {
        public static readonly IReadOnlyList<SidebarShortcut> All = new[]
        {
            // Ρητό σπάσιμο σε 2 γραμμές στο " & " (ίδιο με το ps1's $lbl.Text -replace ' & ', "`n") -
            // προβλέψιμο αποτέλεσμα αντί να βασίζεται στο αυτόματο word-wrap του WPF, που θα
            // μπορούσε να σπάσει αλλού (π.χ. "Οδηγίες &" / "Βοήθεια").
            new SidebarShortcut("❓", "Οδηγίες\nΒοήθεια", "Οδηγίες & Βοήθεια"),
            new SidebarShortcut("🕘", "Ιστορικό Εκδόσεων", "Ιστορικό Εκδόσεων"),
            new SidebarShortcut("📄", "Ιστορικό Ενεργειών", "Ιστορικό Ενεργειών"),
            new SidebarShortcut("🧩", "Κρυφές Λειτουργίες", "Κρυφές Λειτουργίες (ViVeTool)"),
            new SidebarShortcut("📱", "Διαχείριση UWP", "Διαχείριση UWP Εφαρμογών"),
            new SidebarShortcut("🎨", "Ρυθμίσεις Εμφάνισης", "Ρυθμίσεις Εμφάνισης"),
        };
    }
}
