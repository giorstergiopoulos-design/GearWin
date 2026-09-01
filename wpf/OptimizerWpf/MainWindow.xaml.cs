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

        // Same reasoning as TabHome above: populate + select AFTER InitializeComponent so
        // CboTheme_SelectionChanged doesn't fire against a half-built window.
        CboTheme.ItemsSource = ThemeCatalog.All;
        CboTheme.SelectedItem = ThemeManager.CurrentTheme;
    }

    private void TabButton_Checked(object sender, RoutedEventArgs e)
    {
        // Only the Home tab is fully ported (see HomeView) - every other tab shows a labeled
        // placeholder until it's ported in a later session (see the staged migration plan).
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;

        ContentHost.Content = tag switch
        {
            "Home" => new HomeView(),
            _ => new PlaceholderView(rb.Content?.ToString() ?? tag)
        };
    }

    private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.ToggleLightDark();
        TxtThemeToggle.Text = ThemeManager.IsDarkMode ? "☀ Light Mode" : "☽ Dark Mode";
        // Toggling Light/Dark can change which theme object is "current" (Fluent Dark <-> Fluent
        // Light are two distinct ThemeColors entries) - keep the dropdown's selection in sync so it
        // doesn't silently show the wrong theme name after a toggle.
        CboTheme.SelectionChanged -= CboTheme_SelectionChanged;
        CboTheme.SelectedItem = ThemeManager.CurrentTheme;
        CboTheme.SelectionChanged += CboTheme_SelectionChanged;
    }

    private void CboTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboTheme.SelectedItem is not ThemeColors theme) return;
        // Every catalog entry except the explicit Light variant is a dark palette (see ThemeCatalog) -
        // force IsDarkMode to match so the Light/Dark toggle button's label doesn't go stale (e.g.
        // still reading "switch to Dark" after picking a dark-only theme while Light Mode was on).
        ThemeManager.ApplyTheme(theme, forceDarkMode: theme != ThemeCatalog.Windows11FluentLight);
        TxtThemeToggle.Text = ThemeManager.IsDarkMode ? "☀ Light Mode" : "☽ Dark Mode";
    }
}
