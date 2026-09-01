using System.Windows;
using System.Windows.Controls;
using OptimizerWpf.Views;

namespace OptimizerWpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Set the initial selected tab AFTER InitializeComponent, not via IsChecked="True" in XAML -
        // XAML-set IsChecked fires the Checked event synchronously WHILE the rest of the window's
        // named elements (ContentHost, declared later in the document) are still being wired up by
        // the generated InitializeComponent, causing a NullReferenceException on ContentHost. Doing
        // it here guarantees the full object graph already exists when the event fires.
        TabHome.IsChecked = true;

        ListThemes.ItemsSource = ThemeCatalog.All;
        ListThemes.SelectedItem = ThemeManager.CurrentPair;
    }

    private void TabButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;
        ShowTabContent(tag, rb.Content?.ToString() ?? tag);
    }

    // Shared by the tab strip's RadioButtons AND the classic menu's "Προβολή" submenu (ρητό αίτημα
    // χρήστη) - keeps both navigation paths in sync instead of duplicating the switch logic.
    private void ShowTabContent(string tag, string label)
    {
        // Only the Home/Optimization tabs are fully ported so far - every other tab shows a labeled
        // placeholder until it's ported in a later session (see the staged migration plan).
        ContentHost.Content = tag switch
        {
            "Home" => new HomeView(),
            "Optimization" => new OptimizationView(),
            _ => new PlaceholderView(label)
        };
    }

    private void MenuViewTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || mi.Tag is not string tag) return;
        // Also checks the matching tab-strip pill so both navigation paths (menu and tab strip)
        // always agree on which tab is "active", rather than only updating the content host.
        foreach (var child in TabStrip.Children)
        {
            if (child is RadioButton rb && rb.Tag as string == tag) { rb.IsChecked = true; return; }
        }
        ShowTabContent(tag, mi.Header?.ToString() ?? tag);
    }

    // Classic menu items whose real windows (Βοήθεια/Ιστορικό/ViVeTool/UWP Manager/Ρυθμίσεις
    // Εμφάνισης) haven't been ported to WPF yet - honest placeholder instead of pretending they
    // work, per the staged migration plan (see HANDOFF.md).
    private void MenuNotPorted_Click(object sender, RoutedEventArgs e)
    {
        var label = (sender as MenuItem)?.Header?.ToString()?.TrimEnd('.') ?? "Αυτή η λειτουργία";
        MessageBox.Show($"«{label}» δεν έχει μεταφερθεί ακόμα από το Optimizer.ps1 σε αυτό το WPF preview.",
            "Δεν έχει υλοποιηθεί ακόμα", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.ToggleLightDark();
        TxtThemeToggle.Text = ThemeManager.IsDarkMode ? "☀ Light Mode" : "☽ Dark Mode";
    }

    private void BtnThemePicker_Click(object sender, RoutedEventArgs e)
    {
        ThemePopup.IsOpen = !ThemePopup.IsOpen;
    }

    private void ListThemes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListThemes.SelectedItem is not ThemePair pair) return;
        ThemeManager.SelectTheme(pair);
        ThemePopup.IsOpen = false;
    }
}
