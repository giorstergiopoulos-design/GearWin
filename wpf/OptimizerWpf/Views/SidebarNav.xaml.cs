using System;
using System.Windows;
using System.Windows.Controls;

namespace OptimizerWpf.Views
{
    public partial class SidebarNav : UserControl
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        public event Action<SidebarShortcut>? ShortcutClicked;

        public SidebarNav()
        {
            InitializeComponent();
            ListItems.ItemsSource = SidebarShortcuts.All;
        }

        // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "όταν αλλάζουμε γλώσσα κάποια elements... μένουν στην προηγούμενη
        // γλώσσα μέχρι το κλείσιμο και άνοιγμα ξανά") - σε αντίθεση με τις 8 καρτέλες περιεχομένου (που
        // ξαναδημιουργούνται εξ αρχής σε κάθε εναλλαγή, άρα διαβάζουν αυτόματα την τρέχουσα γλώσσα), το
        // SidebarNav είναι ΜΟΝΙΜΟ UserControl μέσα στο MainWindow.xaml (απλώς εμφανίζεται/κρύβεται) - το
        // ItemsSource οριζόταν ΜΙΑ φορά στον constructor και ποτέ ξανά. Καλείται από το MainWindow's
        // ApplyLanguage() (βλ. εκεί) σε κάθε αλλαγή γλώσσας.
        public void RefreshLanguage() => ListItems.ItemsSource = SidebarShortcuts.All;

        private void Shortcut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: SidebarShortcut shortcut }) ShortcutClicked?.Invoke(shortcut);
        }
    }
}
