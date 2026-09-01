using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OptimizerWpf.Services;
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

        StatusService.Changed += OnStatusChanged;
        Closed += (_, _) => StatusService.Changed -= OnStatusChanged;
        StatusService.SetBusy("Εκκίνηση εφαρμογής...");
        StatusService.SetIdle("Έτοιμο για χρήση");
    }

    private readonly Storyboard _spinnerStoryboard = BuildSpinnerStoryboard();

    private static Storyboard BuildSpinnerStoryboard()
    {
        var anim = new DoubleAnimation(0, 360, new Duration(System.TimeSpan.FromSeconds(0.9)))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTargetName(anim, "SpinnerRotate");
        Storyboard.SetTargetProperty(anim, new PropertyPath(RotateTransform.AngleProperty));
        var sb = new Storyboard();
        sb.Children.Add(anim);
        return sb;
    }

    // Μοναδικός συνδρομητής του StatusService - κάθε προβολή/υπηρεσία απλώς καλεί
    // StatusService.SetBusy/SetIdle, χωρίς να ξέρει τίποτα για το status bar ή το spinner.
    private void OnStatusChanged(string message, bool busy)
    {
        TxtStatus.Text = $"Κατάσταση: {message}";
        if (busy)
        {
            ActivitySpinner.Visibility = Visibility.Visible;
            _spinnerStoryboard.Begin(this, true);
        }
        else
        {
            _spinnerStoryboard.Stop(this);
            ActivitySpinner.Visibility = Visibility.Collapsed;
        }
    }

    // Ctrl+1..6 (εναλλαγή καρτέλας) + Ctrl+M (εμφάνιση/απόκρυψη κλασικού μενού) - ρητό αίτημα
    // χρήστη. Το InputGestureText πάνω στα MenuItem του κλασικού μενού είναι μόνο ΚΕΙΜΕΝΟ (WPF δεν
    // το συνδέει αυτόματα με πραγματική συντόμευση εκτός αν υπάρχει ICommand+InputBinding) - αυτός
    // ο handler είναι η πραγματική υλοποίηση πίσω από αυτό το κείμενο.
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        if (e.Key == Key.M)
        {
            ClassicMenu.Visibility = ClassicMenu.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            e.Handled = true;
            return;
        }

        var tag = e.Key switch
        {
            Key.D1 or Key.NumPad1 => "Home",
            Key.D2 or Key.NumPad2 => "Optimization",
            Key.D3 or Key.NumPad3 => "Health",
            Key.D4 or Key.NumPad4 => "Network",
            Key.D5 or Key.NumPad5 => "Tweaks",
            Key.D6 or Key.NumPad6 => "Bloatware",
            Key.D7 or Key.NumPad7 => "Advanced",
            Key.D8 or Key.NumPad8 => "System",
            _ => null,
        };
        if (tag == null) return;

        foreach (var child in TabStrip.Children)
        {
            if (child is RadioButton rb && rb.Tag as string == tag) { rb.IsChecked = true; break; }
        }
        e.Handled = true;
    }

    private void TabButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;
        ShowTabContent(tag, GetTabLabel(rb) ?? tag);
    }

    // Tab pill Content is now an icon+text StackPanel (ρητό αίτημα χρήστη: εικονίδια στις καρτέλες),
    // όχι πλέον απλό string - rb.Content.ToString() θα επέστρεφε το όνομα του StackPanel type αντί
    // για το πραγματικό ελληνικό label, οπότε τραβάμε το κείμενο από το δεύτερο TextBlock παιδί.
    private static string? GetTabLabel(RadioButton rb) =>
        (rb.Content as StackPanel)?.Children.OfType<TextBlock>().LastOrDefault()?.Text;

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
