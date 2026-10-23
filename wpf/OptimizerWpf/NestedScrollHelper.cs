using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OptimizerWpf
{
    // Ρητό αίτημα χρήστη ("η κύλιση με το ποντίκι κολλάει, ειδικά στις καρτέλες Σύστημα &
    // Βελτιστοποίηση") - αυτές οι καρτέλες έχουν πολλαπλά "εσωτερικά" ScrollViewer (λίστες
    // Διεργασιών/Υπηρεσιών/Αποθηκευτικού Χώρου/Οδηγών κ.λπ.) μέσα στο ΕΝΑ "εξωτερικό" ScrollViewer
    // ολόκληρης της καρτέλας. Το WPF ΔΕΝ προωθεί αυτόματα τη ροδέλα του ποντικιού στο εξωτερικό όταν
    // το ποντίκι είναι πάνω από ένα εσωτερικό ScrollViewer που είτε δεν χρειάζεται κύλιση είτε έχει ήδη
    // φτάσει στο άκρο του - το γεγονός "καταναλώνεται" εκεί, δίνοντας αίσθηση κολλήματος. Αυτός ο
    // handler προωθεί χειροκίνητα την κύλιση στο πρώτο ScrollViewer-πρόγονο όταν συμβεί αυτό.
    //
    // ΔΙΟΡΘΩΣΗ 6.5.3 (χρήστης: "στους ενημερωτές, όταν είμαι πάνω στη λίστα να κυλά η λίστα και όχι το παράθυρο"):
    // από το 6.5.0 κάθε φιλοξενούμενη προβολή (CompositeTabView) έχει ΔΙΚΗ ΤΗΣ ScrollViewer με απενεργοποιημένη κάθετη
    // κύλιση ΚΑΙ αυτόν τον handler. Επειδή τα PreviewMouseWheel "κατεβαίνουν" από έξω προς τα μέσα, ο handler του
    // εξωτερικού (άχρηστου) ScrollViewer έτρεχε ΠΡΩΤΟΣ, έβλεπε "δεν έχω χώρο κύλισης" και μετέφερε ΠΑΝΤΑ την κίνηση στη σελίδα
    // με Handled=true - η εσωτερική λίστα δεν έφτανε ποτέ να τη δει. Τώρα: (1) αν το ποντίκι είναι πάνω από ένα βαθύτερο
    // ScrollViewer που μπορεί ακόμα να κυλήσει προς αυτή την κατεύθυνση, ο handler δεν κάνει τίποτα· (2) η προώθηση προς τα
    // πάνω πηγαίνει στον πρώτο πρόγονο που ΠΡΑΓΜΑΤΙΚΑ κυλά (όχι σε έναν με απενεργοποιημένη κύλιση, που αγνοούσε την εντολή).
    public static class NestedScrollHelper
    {
        public static void Forward(object sender, MouseWheelEventArgs e)
        {
            // ΝΕΟ - roadmap "Virtualization σε μεγάλες λίστες" - οι πλέον virtualized ListBox (βλ.
            // VirtualizedListStyle) έχουν το ΔΙΚΟ τους εσωτερικό ScrollViewer κρυμμένο μέσα στο
            // template τους, όχι έναν ρητό ScrollViewer γύρω τους σαν πριν - το sender εδώ είναι πλέον
            // το ίδιο το ListBox. Αναζήτηση προς τα ΚΑΤΩ (descendant) βρίσκει το εσωτερικό template
            // ScrollViewer· η υπόλοιπη λογική (πρόγονος-ScrollViewer προς τα πάνω) παραμένει ίδια.
            var inner = sender as ScrollViewer ?? FindDescendantScrollViewer(sender as DependencyObject);
            if (inner == null) return;

            // (1) ένα βαθύτερο ScrollViewer κάτω από το ποντίκι που μπορεί να κυλήσει: αφήνουμε το γεγονός να φτάσει εκεί.
            if (sender is ScrollViewer && e.OriginalSource is DependencyObject source && DeeperScrollerCanHandle(source, inner, e.Delta)) return;

            if (CanScroll(inner))
            {
                var atTop = e.Delta > 0 && inner.VerticalOffset <= 0;
                var atBottom = e.Delta < 0 && inner.VerticalOffset >= inner.ScrollableHeight;
                if (!atTop && !atBottom) return; // η εσωτερική κύλιση έχει ακόμα χώρο - προχωράει κανονικά
            }

            var outer = FindScrollableAncestor(inner);
            if (outer == null) return;
            outer.ScrollToVerticalOffset(outer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        /// <summary>True όταν το ScrollViewer έχει κάθετη κύλιση ενεργή και περιεχόμενο που δεν χωράει.</summary>
        internal static bool CanScroll(ScrollViewer sv) => sv.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled && sv.ScrollableHeight > 0;

        private static bool DeeperScrollerCanHandle(DependencyObject source, ScrollViewer stopAt, int delta)
        {
            for (var node = source; node != null && !ReferenceEquals(node, stopAt); node = ParentOf(node))
            {
                if (node is ScrollViewer sv && CanScroll(sv))
                {
                    var canUp = delta > 0 && sv.VerticalOffset > 0;
                    var canDown = delta < 0 && sv.VerticalOffset < sv.ScrollableHeight;
                    if (canUp || canDown) return true;
                }
            }
            return false;
        }

        // Τα Run/Hyperlink κ.λπ. είναι ContentElement (όχι Visual): ανεβαίνουμε από το λογικό δέντρο μέχρι να βρεθεί Visual.
        private static DependencyObject? ParentOf(DependencyObject node) =>
            node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);

        private static ScrollViewer? FindScrollableAncestor(DependencyObject child)
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is ScrollViewer sv && sv.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled) return sv;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private static ScrollViewer? FindDescendantScrollViewer(DependencyObject? parent)
        {
            if (parent == null) return null;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer sv) return sv;
                var found = FindDescendantScrollViewer(child);
                if (found != null) return found;
            }
            return null;
        }
    }
}
