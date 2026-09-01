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
        ThemeManager.ToggleTheme();
        TxtThemeToggle.Text = ThemeManager.IsDarkMode ? "☀ Light Mode" : "☽ Dark Mode";
    }
}
