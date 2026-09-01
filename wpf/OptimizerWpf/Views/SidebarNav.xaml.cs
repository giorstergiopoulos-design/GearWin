using System;
using System.Windows;
using System.Windows.Controls;

namespace OptimizerWpf.Views
{
    public partial class SidebarNav : UserControl
    {
        public event Action<SidebarShortcut>? ShortcutClicked;

        public SidebarNav()
        {
            InitializeComponent();
            ListItems.ItemsSource = SidebarShortcuts.All;
        }

        private void Shortcut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: SidebarShortcut shortcut }) ShortcutClicked?.Invoke(shortcut);
        }
    }
}
