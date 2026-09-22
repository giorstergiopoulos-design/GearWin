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
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
        private struct POINT { public int X; public int Y; }

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
            var area = SystemParameters.WorkArea;
            var x = 100.0; var y = 100.0;
            if (GetCursorPos(out var pt)) { x = pt.X; y = pt.Y; }
            Left = System.Math.Min(x, area.Right - Width - 8);
            Top = System.Math.Min(y, area.Bottom - Height - 8);
            Left = System.Math.Max(Left, area.Left + 8);
            Top = System.Math.Max(Top, area.Top + 8);

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

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Deactivated(object sender, System.EventArgs e) => Close();
    }
}
