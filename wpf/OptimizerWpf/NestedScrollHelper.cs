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
            var atTop = e.Delta > 0 && inner.VerticalOffset <= 0;
            var atBottom = e.Delta < 0 && inner.VerticalOffset >= inner.ScrollableHeight;
            if (!atTop && !atBottom) return; // η εσωτερική κύλιση έχει ακόμα χώρο - προχωράει κανονικά

            var outer = FindAncestorScrollViewer(inner);
            if (outer == null) return;
            outer.ScrollToVerticalOffset(outer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private static ScrollViewer? FindAncestorScrollViewer(DependencyObject child)
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is ScrollViewer sv) return sv;
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
