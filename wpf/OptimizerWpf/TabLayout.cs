using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using OptimizerWpf.Views;

namespace OptimizerWpf
{
    // Ένα τμήμα σελίδας: μια υπάρχουσα προβολή (View) από την οποία εμφανίζονται ΜΟΝΟ οι κάρτες των αναφερόμενων ενοτήτων.
    public sealed record TabPart(string View, params string[] Sections);

    // Μια υπο-σελίδα (chip) μέσα σε καρτέλα - TitleKey = κλειδί μετάφρασης (Sub_*).
    public sealed record TabSubPage(string TitleKey, params TabPart[] Parts);

    // 6.5.0 - ΝΕΑ ΔΟΜΗ ΚΑΡΤΕΛΩΝ (ρητό αίτημα χρήστη: "καλύτερη κατηγοριοποίηση/ομαδοποίηση, έλεγξε τις λειτουργίες αν υπάρχουν παρόμοιες").
    // Οι 9 καρτέλες διατηρούν τα ίδια tags (Home, Bloatware, Optimization, Health, Network, GamesMedia, Tweaks, Advanced, System) ώστε να
    // μη σπάσουν συντομεύσεις/tray/ρυθμίσεις, αλλά το περιεχόμενό τους ξανασυντίθεται από κάρτες των υπαρχουσών προβολών:
    //   Bloatware     = Ενημερώσεις & Εφαρμογές   (Ενημερώσεις | Οδηγοί | Εφαρμογές)
    //   Optimization  = Καθαρισμός & Απόδοση      (Απόδοση | Καθαρισμός | Εκκίνηση)
    //   Health        = Υγεία & Επισκευή
    //   Network       = Ασφάλεια & Απόρρητο       (Ασφάλεια | Δίκτυο)
    //   Advanced      = Αντίγραφα & Αναφορές      (Αναφορές | Αντίγραφα & Επαναφορά)
    //   System        = Σύστημα & Εργαλεία        (Πληροφορίες | Εργαλεία)
    // Κάθε προβολή δηλώνει ποια ΚΑΡΤΑ ανήκει σε ποια ενότητα (Sections): ο δείκτης είναι η θέση του παιδιού στη ρίζα της προβολής.
    // Το TabLayoutTests επιβεβαιώνει ότι ο πίνακας ταιριάζει με το XAML (προστέθηκε κάρτα χωρίς ενότητα = test αποτυγχάνει).
    public static class TabLayout
    {
        private static readonly IReadOnlyDictionary<string, TabSubPage[]> Pages = new Dictionary<string, TabSubPage[]>
        {
            ["Bloatware"] = new[]
            {
                new TabSubPage("Sub_Updates", new TabPart("Optimization", "Updates")),
                new TabSubPage("Sub_Drivers", new TabPart("Optimization", "Drivers"), new TabPart("Advanced", "Ghost")),
                new TabSubPage("Sub_Apps", new TabPart("Bloatware", "All")),
            },
            ["Optimization"] = new[]
            {
                new TabSubPage("Sub_Performance", new TabPart("Optimization", "Modes"), new TabPart("System", "Processes"), new TabPart("Advanced", "Benchmark")),
                new TabSubPage("Sub_Cleanup", new TabPart("Health", "Cleanup"), new TabPart("System", "Storage"), new TabPart("Advanced", "Duplicates")),
                new TabSubPage("Sub_Startup", new TabPart("Advanced", "Startup")),
            },
            ["Health"] = new[]
            {
                new TabSubPage("Sub_Repair", new TabPart("Health", "Repair")),
            },
            ["Network"] = new[]
            {
                new TabSubPage("Sub_Security", new TabPart("Network", "Security"), new TabPart("Advanced", "Security")),
                new TabSubPage("Sub_Network", new TabPart("Network", "Network")),
            },
            ["Advanced"] = new[]
            {
                new TabSubPage("Sub_Reports", new TabPart("Health", "Reports")),
                new TabSubPage("Sub_Backups", new TabPart("Health", "Backups"), new TabPart("Optimization", "DriverBackup")),
            },
            ["System"] = new[]
            {
                new TabSubPage("Sub_Info", new TabPart("System", "Info")),
                // 6.5.4: οι κάρτες συσκευών (οθόνες, ήχος, κάμερα/μικρόφωνο) ζούσαν στην καρτέλα "Παιχνίδια & Πολυμέσα" χωρίς σχέση με παιχνίδια.
                new TabSubPage("Sub_Devices", new TabPart("GamesMedia", "Devices")),
                new TabSubPage("Sub_Tools", new TabPart("Advanced", "Tools")),
            },
            // 6.5.4: Παιχνίδια | Πολυμέσα - η καρτέλα είχε 10 κάρτες σε μία σελίδα.
            ["GamesMedia"] = new[]
            {
                new TabSubPage("Sub_Games", new TabPart("GamesMedia", "Games")),
                new TabSubPage("Sub_Media", new TabPart("GamesMedia", "Media")),
            },
        };

        // Ενότητα ανά παιδί της ρίζας κάθε προβολής (null = κάρτα που δεν εμφανίζεται σε καμία σύνθετη σελίδα, π.χ. διπλότυπος τίτλος).
        private static readonly IReadOnlyDictionary<string, string?[]> RootSections = new Dictionary<string, string?[]>
        {
            ["Optimization"] = new[] { "Updates", "Updates", "Updates", "Drivers", "Drivers", "DriverBackup", "Drivers", "Modes", "Modes" },
            ["Health"] = new[] { "Repair", "Repair", "Backups", "Cleanup", "Repair", "Reports", "Repair", "@Recovery" },
            ["Network"] = new[] { "Network", "Network", "Network", "Network", "Network", "Security", "Security", "Security", "Security", "Network", "Network" },
            ["Advanced"] = new[] { null, "Tools", "Tools", "Ghost", "Tools", "Tools", "Tools", "Startup", "Startup", "Security", "@PowerTools" },
            ["System"] = new[] { "Info", "Info", "Info", "Info", "Processes", "Storage", "Info", "Info" },
            ["Bloatware"] = new[] { "All", "All", "All", "All", "All", "All" },
            // GamesMediaView: βιβλιοθήκη, ανά παιχνίδι, gaming, shader, save backup, σημείωση ενημερώσεων | διαχωριστής | οθόνες, ήχος, κάμερα/μικρόφωνο | κωδικοποιητές, μετατροπέας
            ["GamesMedia"] = new[] { "Games", "Games", "Games", "Games", "Games", "Games", null, "Devices", "Devices", "Devices", "Media", "Media" },
        };

        // Εσωτερικές κάρτες των UserControl που είναι ΜΙΑ θέση της ρίζας (@Recovery, @PowerTools).
        private static readonly IReadOnlyDictionary<string, string?[]> NestedSections = new Dictionary<string, string?[]>
        {
            ["@Recovery"] = new[] { "Backups", "Backups" },
            ["@PowerTools"] = new[] { "Startup", "Startup", "Duplicates", "Security", "Benchmark" },
        };

        public static IReadOnlyList<TabSubPage>? SubPages(string tag) => Pages.TryGetValue(tag, out var p) ? p : null;

        public static bool IsComposite(string tag) => Pages.ContainsKey(tag);

        public static IReadOnlyCollection<string> CompositeTags => Pages.Keys.ToList();

        public static UserControl CreateView(string view) => view switch
        {
            "Optimization" => new OptimizationView(),
            "Health" => new HealthView(),
            "Network" => new NetworkView(),
            "Advanced" => new AdvancedView(),
            "System" => new SystemView(),
            "Bloatware" => new BloatwareView(),
            "Tweaks" => new TweaksView(),
            "GamesMedia" => new GamesMediaView(),
            _ => throw new ArgumentException("Unknown view " + view),
        };

        public static IReadOnlyCollection<string> ViewsWithSections => RootSections.Keys.ToList();

        // Όλες οι ενότητες που δηλώνει μια προβολή (και οι εσωτερικές των @UserControl).
        public static IReadOnlyCollection<string> SectionsOf(string view)
        {
            var result = new HashSet<string>();
            foreach (var s in RootSections[view])
            {
                if (s == null) continue;
                if (s.StartsWith('@')) { foreach (var n in NestedSections[s]) if (n != null) result.Add(n); }
                else result.Add(s);
            }
            return result;
        }

        public static int RootChildCount(string view) => RootSections.TryGetValue(view, out var m) ? m.Length : -1;

        // Αφήνει στη ρίζα της προβολής ΜΟΝΟ τις κάρτες των ενοτήτων "keep" (οι υπόλοιπες αφαιρούνται από το δέντρο - ΔΕΝ απλώς
        // κρύβονται, ώστε ο κώδικας της προβολής να μην μπορεί να τις ξαναεμφανίσει αργότερα). Επιστρέφει false αν ο πίνακας δεν
        // ταιριάζει με το XAML (τότε η προβολή μένει ΑΝΕΠΑΦΗ - προτιμάται σωστό αλλά πλήρες περιεχόμενο από λάθος περιεχόμενο).
        public static bool ApplySections(UserControl view, string viewKey, ISet<string> keep)
        {
            if (!RootSections.TryGetValue(viewKey, out var map)) return false;
            var panel = RootPanel(view);
            if (panel == null || panel.Children.Count != map.Length) return false;

            for (var i = map.Length - 1; i >= 0; i--)
            {
                var section = map[i];
                var child = panel.Children[i];
                if (section != null && section.StartsWith('@'))
                {
                    var nested = NestedSections[section];
                    var inner = child is UserControl uc ? RootPanel(uc) : null;
                    if (inner == null || inner.Children.Count != nested.Length) continue; // ασυμφωνία: αφήνεται ανέπαφο
                    for (var j = nested.Length - 1; j >= 0; j--)
                        if (nested[j] == null || !keep.Contains(nested[j]!)) inner.Children.RemoveAt(j);
                    if (inner.Children.Count == 0) panel.Children.RemoveAt(i);
                    continue;
                }
                if (section == null || !keep.Contains(section)) panel.Children.RemoveAt(i);
            }
            return true;
        }

        // Ρίζα προβολής: UserControl -> ScrollViewer -> StackPanel (ή UserControl -> StackPanel για τις εσωτερικές κάρτες).
        public static StackPanel? RootPanel(UserControl view) => view.Content switch
        {
            ScrollViewer { Content: StackPanel sp } => sp,
            StackPanel sp => sp,
            _ => null,
        };
    }
}
