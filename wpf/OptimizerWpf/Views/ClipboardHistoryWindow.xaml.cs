using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class ClipboardHistoryWindow : Window
    {
        private List<string> _all = new();

        public ClipboardHistoryWindow()
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // ΝΕΟ - εμφανίζεται κοντά στη θέση του δρομέα (Win32 GetCursorPos) αντί για σταθερή γωνία -
            // ο χρήστης καλεί το hotkey ενώ γράφει οπουδήποτε, οπότε το popup πρέπει να εμφανιστεί εκεί.
            // Περιορίζεται μέσα στα όρια της διαθέσιμης οθόνης ώστε να μην "κόβεται" σε άκρη οθόνης.
            // Το GetCursorPos δίνει ΦΥΣΙΚΑ pixels - πριν ανατίθονταν απευθείας σε Left/Top (DIPs), λάθος σε
            // scaling > 100% (βλ. PopupPlacement). Πλέον όλα σε φυσικά pixels, στην οθόνη του δρομέα.
            PopupPlacement.PlaceNearCursor(this);

            RefreshList();
            TxtSearch.Focus();
            Activate();
        }

        private void RefreshList()
        {
            _all = ClipboardManagerService.History.ToList();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var term = TxtSearch.Text?.Trim();
            var filtered = string.IsNullOrEmpty(term)
                ? _all
                : _all.Where(x => x.Contains(term, System.StringComparison.OrdinalIgnoreCase)).ToList();
            ListHistory.ItemsSource = filtered;
            TxtEmpty.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyFilter();

        private void ItemBorder_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: string text }) return;
            ClipboardManagerService.CopyToClipboard(text);
            Close();
        }

        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string text }) return;
            ClipboardManagerService.RemoveItem(text);
            RefreshList();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            ClipboardManagerService.ClearHistory();
            RefreshList();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => CloseOnce();

        // ΔΙΟΡΘΩΣΗ (βρέθηκε από το live click-through): το Deactivated μπορεί να έρθει ΕΝΩ το παράθυρο ήδη κλείνει (π.χ. Close από
        // κουμπί/Esc → απώλεια focus → δεύτερο Close). Το WPF τότε ρίχνει InvalidOperationException ("...while a Window is
        // closing") από μέσα στο WmActivate - μη χειρίσιμο από τον καθολικό handler και σε ορισμένα σενάρια τερμάτιζε τη διεργασία.
        private bool _closeRequested;
        private void CloseOnce()
        {
            if (_closeRequested) return;
            _closeRequested = true;
            try { Close(); } catch (System.InvalidOperationException) { /* ήδη κλείνει */ }
        }

        private void Window_Deactivated(object sender, System.EventArgs e) => CloseOnce();
    }
}
