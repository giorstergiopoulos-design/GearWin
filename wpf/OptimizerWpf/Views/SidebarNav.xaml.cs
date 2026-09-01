using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OptimizerWpf.Views
{
    public partial class SidebarNav : UserControl
    {
        public event Action<MenuLeaf>? LeafClicked;
        public event Action? PositionToggleRequested;

        // Έτοιμο για το μελλοντικό PC Manager-equivalent skin (βλ. σχόλιο στο .xaml) - όταν true,
        // δείχνει ΜΟΝΟ την ομάδα με τις καρτέλες (χωρίς Εργαλεία/Ρυθμίσεις/Βοήθεια). Αναγνωρίζεται
        // δομικά (όλα τα leaves της ομάδας έχουν NavigateTag), όχι με σύγκριση τίτλου - ανθεκτικό σε
        // μελλοντική μετονομασία της ομάδας "Προβολή".
        public bool ShowOnlyNavigationGroup { get; set; }

        private readonly Dictionary<string, RadioButton> _navButtons = new();

        public SidebarNav()
        {
            InitializeComponent();
            BuildItems();
        }

        private void BuildItems()
        {
            ItemsHost.Children.Clear();
            _navButtons.Clear();

            var groups = ShowOnlyNavigationGroup
                ? ClassicMenuModel.Groups.Where(g => g.Items.All(i => i.Action.NavigateTag != null))
                : ClassicMenuModel.Groups;

            var first = true;
            foreach (var group in groups)
            {
                ItemsHost.Children.Add(new TextBlock
                {
                    Text = $"{group.Icon} {group.Header}".ToUpperInvariant(),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("SubTextBrush"),
                    Margin = new Thickness(6, first ? 0 : 14, 0, 6),
                });
                first = false;

                foreach (var leaf in group.Items)
                {
                    ItemsHost.Children.Add(leaf.Action.NavigateTag != null ? BuildNavRow(leaf) : BuildActionRow(leaf));
                }
            }
        }

        private RadioButton BuildNavRow(MenuLeaf leaf)
        {
            var rb = new RadioButton
            {
                GroupName = "SidebarNav",
                Style = (Style)FindResource("TabPillStyle"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 4),
                Content = BuildLeafContent(leaf),
            };
            // Click (ΟΧΙ Checked) - το Click δεν πυροδοτείται όταν το IsChecked τίθεται
            // προγραμματιστικά (π.χ. από SetActiveTag), οπότε δεν υπάρχει κίνδυνος βρόχου
            // MainWindow -> SetActiveTag -> Checked -> LeafClicked -> MainWindow -> SetActiveTag...
            rb.Click += (_, _) => LeafClicked?.Invoke(leaf);
            _navButtons[leaf.Action.NavigateTag!] = rb;
            return rb;
        }

        private Button BuildActionRow(MenuLeaf leaf)
        {
            var btn = new Button
            {
                Style = (Style)FindResource("FlatButtonStyle"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 4),
                Content = BuildLeafContent(leaf),
            };
            btn.Click += (_, _) => LeafClicked?.Invoke(leaf);
            return btn;
        }

        private static StackPanel BuildLeafContent(MenuLeaf leaf)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            if (!string.IsNullOrEmpty(leaf.Icon))
                sp.Children.Add(new TextBlock { Text = leaf.Icon, FontSize = 14, Margin = new Thickness(0, 0, 8, 0) });
            sp.Children.Add(new TextBlock { Text = leaf.Label, TextTrimming = TextTrimming.CharacterEllipsis });
            return sp;
        }

        // Καλείται από το MainWindow όποτε αλλάζει η ενεργή καρτέλα από ΟΠΟΙΑΔΗΠΟΤΕ άλλη πλοήγηση
        // (λωρίδα καρτελών, κλασικό μενού, Ctrl+1..8) - ώστε το πλευρικό μενού να παραμένει πάντα σε
        // συμφωνία με τις υπόλοιπες, χωρίς να χρειάζεται να ξέρει τίποτα γι' αυτές.
        public void SetActiveTag(string tag)
        {
            if (_navButtons.TryGetValue(tag, out var rb)) rb.IsChecked = true;
        }

        private void BtnFlip_Click(object sender, RoutedEventArgs e) => PositionToggleRequested?.Invoke();
    }
}
