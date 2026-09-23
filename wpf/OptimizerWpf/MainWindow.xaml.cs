using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
        // ΔΙΟΡΘΩΣΗ - ρητό αίτημα χρήστη: ο υπότιτλος "Complete PC Care" να εμφανίζεται στα απαραίτητα
        // πεδία - εδώ, δίπλα στην έκδοση, κάτω από το όνομα "GearWin".
        TxtAppVersion.Text = $"Complete PC Care  ·  {App.DisplayVersion}";
        RefreshThemeToggleLabel();

        PopulateClassicMenu();

        ListThemes.ItemsSource = ThemeCatalog.All;
        ListThemes.SelectedItem = ThemeManager.CurrentPair;

        _searchIndex = ClassicMenuModel.Groups
            .SelectMany(g => g.Items.Select(leaf => new SearchResultRow(leaf.Icon, leaf.Label, g.Header, leaf)))
            .ToList();

        StatusService.Changed += OnStatusChanged;
        Closed += (_, _) => StatusService.Changed -= OnStatusChanged;
        StatusService.SetBusy(LanguageService.T("Main_Starting"));
        StatusService.SetIdle(LanguageService.T("Ready"));

        ThemeManager.AttachBackground(AppBackground);
        ListPcManagerRail.ItemsSource = NavItems.All;
        ThemeManager.Changed += ApplyPcManagerSkinLayout;
        Closed += (_, _) => ThemeManager.Changed -= ApplyPcManagerSkinLayout;
        ApplyMenuModeVisibility();

        Opacity = AppSettingsService.Current.WindowOpacityMode switch { "Light" => 0.94, "Medium" => 0.85, _ => 1.0 };

        ApplyLanguage();
        LanguageService.Changed += ApplyLanguage;
        Closed += (_, _) => LanguageService.Changed -= ApplyLanguage;

        // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε μέτρηση: "δες αν βαραίνει η εφαρμογή πολύ") - επιβεβαιώθηκε
        // ζωντανά ότι το Pause/Resume του ThemedBackgroundControl (βλ. ThemeManager.AttachBackground)
        // ΔΕΝ αρκούσε από μόνο του - το CPU έμενε στο ~4% ακόμα και ελαχιστοποιημένο. Το γρανάζι του
        // τίτλου έχει ΔΙΚΟ ΤΟΥ, ανεξάρτητο DoubleAnimation με RepeatBehavior=Forever (βλ.
        // ThreeGearIcon.StartSpin) - ίδια λογική εδώ, ίδιος λόγος (δεν έχει νόημα να περιστρέφεται
        // κάτι που δεν είναι καν ορατό).
        Deactivated += (_, _) => TitleGear.StopSpin();
        Activated += (_, _) => TitleGear.StartSpin();
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized) TitleGear.StopSpin();
            else if (IsActive) TitleGear.StartSpin();
        };

        _ = CheckHealthAttentionAsync();
    }

    // ΝΕΟ (roadmap: "ένδειξη προσοχής στο tab strip") - υπολογίζεται ΜΙΑ φορά στην εκκίνηση (ίδιο
    // HealthScoreService.Compute() που ήδη τρέχει η Αρχική) ώστε η κουκκίδα να είναι ήδη ορατή πριν
    // καν ανοίξει ο χρήστης την καρτέλα Υγεία - χωρίς δικό της περιοδικό timer (ελάφρυνση εφαρμογής).
    private async System.Threading.Tasks.Task CheckHealthAttentionAsync()
    {
        try
        {
            var result = await System.Threading.Tasks.Task.Run(Services.HealthScoreService.Compute);
            var needsAttention = result.Score < 70;
            DotHealthAttention.Visibility = needsAttention ? Visibility.Visible : Visibility.Collapsed;
            var pulse = (System.Windows.Media.Animation.Storyboard)Resources["PulseAttentionDotStoryboard"];
            if (needsAttention) pulse.Begin(this, true);
            else pulse.Stop(this);
        }
        catch { }
    }

    // ΝΕΟ - roadmap "Toast/snackbar για ολοκλήρωση εργασιών παρασκηνίου" - καλείται από όποια
    // καρτέλα/παράθυρο το χρειάζεται μέσω (Window.GetWindow(this) as MainWindow)?.ShowToast(...).
    // Fade-in + ελαφριά ανύψωση (16px -> 0), παραμένει ~3.5s, μετά fade-out.
    private DispatcherTimer? _toastTimer;

    // ΝΕΟ - βελτίωση: πριν, ένα δεύτερο toast κατά τη διάρκεια του πρώτου έσβηνε σιωπηλά το πρώτο
    // μήνυμα (απλή επανεκκίνηση του ίδιου timer με το νέο κείμενο) - αν έτρεχαν πολλαπλές background
    // εργασίες σχεδόν ταυτόχρονα (π.χ. Full Maintenance + driver scan), ο χρήστης έχανε την πρώτη
    // ειδοποίηση χωρίς να το καταλάβει. Τώρα μπαίνει σε ουρά και εμφανίζεται αμέσως μετά το τρέχον -
    // ΑΚΟΜΑ ένα toast τη φορά στην οθόνη (καμία αλλαγή στο οπτικό layout/μέγεθος), απλά καμία απώλεια
    // μηνύματος πια.
    private readonly Queue<string> _toastQueue = new();
    private bool _toastShowing;

    public void ShowToast(string message)
    {
        if (_toastShowing) { _toastQueue.Enqueue(message); return; }
        DisplayToast(message);
    }

    private void DisplayToast(string message)
    {
        _toastShowing = true;
        TxtToastMessage.Text = message;

        var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(200));
        ToastHost.BeginAnimation(OpacityProperty, fadeIn);
        var slideIn = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
        ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer!.Stop();
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
            fadeOut.Completed += (_, _) =>
            {
                _toastShowing = false;
                if (_toastQueue.Count > 0) DisplayToast(_toastQueue.Dequeue());
            };
            ToastHost.BeginAnimation(OpacityProperty, fadeOut);
        };
        _toastTimer.Start();
    }

    // Επεκτάθηκε πέρα από το αρχικό μονο-παράθυρο proof-of-concept (ρητό αίτημα χρήστη) - οι τίτλοι
    // καρτελών του κύριου παραθύρου μεταφράζονται τώρα ζωντανά, μαζί με το κλασικό μενού/search
    // index (ClassicMenuModel.Groups είναι πλέον property, όχι readonly field - ξαναχτίζεται εδώ σε
    // κάθε αλλαγή γλώσσας, ίδιο μοτίβο με τις άλλες λίστες tweaks/features που έγιναν properties σε
    // αυτό το πέρασμα - ρητό αίτημα χρήστη: "μετάφρασε τα όλα").
    private void ApplyLanguage()
    {
        LblTabHome.Text = LanguageService.T("TabHome");
        LblTabOptimization.Text = LanguageService.T("TabOptimization");
        LblTabHealth.Text = LanguageService.T("TabHealth");
        LblTabNetwork.Text = LanguageService.T("TabNetwork");
        LblTabTweaks.Text = LanguageService.T("TabTweaks");
        LblTabBloatware.Text = LanguageService.T("TabBloatware");
        LblTabAdvanced.Text = LanguageService.T("TabAdvanced");
        LblTabSystem.Text = LanguageService.T("TabSystem");

        PopulateClassicMenu();
        _searchIndex = ClassicMenuModel.Groups
            .SelectMany(g => g.Items.Select(leaf => new SearchResultRow(leaf.Icon, leaf.Label, g.Header, leaf)))
            .ToList();
        ListPcManagerRail.ItemsSource = NavItems.All;

        // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "κάποια elements μένουν στην προηγούμενη γλώσσα μέχρι το
        // κλείσιμο και άνοιγμα ξανά") - το SidebarNav είναι μόνιμο UserControl (μόνο Visibility
        // toggle, ποτέ δεν ξαναδημιουργείται), βλ. σχόλιο στο RefreshLanguage() εκεί.
        Sidebar.RefreshLanguage();

        // Ίδιο bug class - η οριζόντια "μοντέρνα λωρίδα μενού" (MenuMode="HorizontalModern",
        // εναλλακτική του πλευρικού μενού) έδειχνε τα ίδια 6 shortcuts με το SidebarNav αλλά μέσω
        // {x:Static local:SidebarShortcuts.All} στο XAML - αξιολογείται ΜΙΑ φορά στο InitializeComponent
        // και ποτέ ξανά. Τώρα έχει x:Name ώστε να μπορεί να ξαναοριστεί εδώ, ίδιο μοτίβο με το
        // ListPcManagerRail ακριβώς από πάνω.
        ListHorizModernShortcuts.ItemsSource = SidebarShortcuts.All;

        // Ίδιο bug class - το status bar δείχνει ΗΔΗ-μεταφρασμένο κείμενο από το StatusService
        // (business event, όχι language event) - βλ. σχόλιο στο _statusIsIdle παραπάνω. Ξαναστέλνει
        // ένα φρέσκο "Έτοιμο" ΜΟΝΟ αν η γραμμή είναι αυτή τη στιγμή σε ανάπαυση (δεν πειράζει ένα
        // πραγματικά ενεργό busy-μήνυμα άλλης καρτέλας).
        if (_statusIsIdle) StatusService.SetIdle(LanguageService.T("Ready"));

        RefreshThemeToggleLabel();
    }

    // Port του $isPCManagerSkin κλάδου του Update-SidebarDockLayout (Optimizer.ps1 ~7426-7451) - όταν
    // το θέμα "Microsoft PC Manager" είναι ενεργό, η οριζόντια λωρίδα καρτελών/hamburger/πλευρικό
    // μενού κρύβονται και αντικαθίστανται από την κάθετη μπάρα εικονιδίων (PcManagerRail).
    private void ApplyPcManagerSkinLayout()
    {
        var isPcManagerSkin = ThemeManager.CurrentPair.DisplayName == "Microsoft PC Manager";
        var isWindowsClassicSkin = ThemeManager.IsWindowsClassicSkin;
        TabStripScroll.Visibility = (isPcManagerSkin || isWindowsClassicSkin) ? Visibility.Collapsed : Visibility.Visible;
        PcManagerRail.Visibility = isPcManagerSkin ? Visibility.Visible : Visibility.Collapsed;
        ColRail.Width = isPcManagerSkin ? GridLength.Auto : new GridLength(0);
        BtnHamburger.Visibility = (!isPcManagerSkin && !isWindowsClassicSkin && AppSettingsService.Current.SidebarEnabled) ? Visibility.Visible : Visibility.Collapsed;
        if (isPcManagerSkin || isWindowsClassicSkin)
        {
            _sidebarVisible = false;
            HideHorizModernStrip();
            ApplySidebarLayout();
        }

        // Ρητό αίτημα χρήστη: "Windows Classic skin - δεν θα υπάρχει πλευρικό μενού ούτε το κουμπί
        // μενού, θα λειτουργεί μόνο η γραμμή μενού η κλασική, που θα είναι πάντα εμφανής σε αυτό το
        // σκιν" - το κλασικό μενού γίνεται μόνιμα ορατό (όχι toggle-able πλέον μέσω Ctrl+M/κουμπιού) σε
        // αυτό το skin, ίδιο πνεύμα με το ήδη υπάρχον PC Manager rail replace-the-nav pattern.
        if (isWindowsClassicSkin) ClassicMenu.Visibility = Visibility.Visible;
    }

    private void PcManagerRailItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: NavItem item }) SelectTab(item.Tag);
    }

    private readonly Storyboard _spinnerStoryboard = BuildSpinnerStoryboard();
    private List<SearchResultRow> _searchIndex = new();

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
    // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "κάποια elements μένουν στην προηγούμενη γλώσσα μέχρι το κλείσιμο
    // και άνοιγμα ξανά") - το StatusService.Changed μεταδίδει ΗΔΗ-μεταφρασμένο κείμενο (business
    // event, όχι language event), οπότε το TxtStatus έμενε παγωμένο στην παλιά γλώσσα μέχρι το
    // επόμενο άσχετο SetBusy/SetIdle από κάποια καρτέλα. Το _statusIsIdle παρακολουθεί αν η γραμμή
    // κατάστασης είναι αυτή τη στιγμή σε ανάπαυση, ώστε το ApplyLanguage() να μπορεί να ξαναστείλει
    // ένα φρέσκο "Έτοιμο" στη νέα γλώσσα - καλύπτει την κοινή, μόνιμη κατάσταση ηρεμίας· ένα σπάνιο
    // "παγωμένο" αποτέλεσμα συγκεκριμένης ενέργειας (π.χ. "Ο λειτουργία Office ενεργοποιήθηκε") αυτο-
    // επουλώνεται μόλις γίνει η επόμενη πραγματική ενέργεια, ίδιο πνεύμα με άλλες μικρές μεταβατικές
    // περιπτώσεις σε αυτή την εφαρμογή.
    private bool _statusIsIdle = true;

    private void OnStatusChanged(string message, bool busy)
    {
        _statusIsIdle = !busy;
        TxtStatus.Text = $"{LanguageService.T("StatusPrefix")}{message}";
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
            // Στο "Windows Classic" skin η κλασική γραμμή μενού είναι ΠΑΝΤΑ ορατή (ρητό αίτημα χρήστη) -
            // το Ctrl+M δεν πρέπει να μπορεί να την κρύψει εκεί.
            if (!ThemeManager.IsWindowsClassicSkin)
                ClassicMenu.Visibility = ClassicMenu.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            e.Handled = true;
            return;
        }

        // ΝΕΟ - roadmap "έλεγξε αν χρειάζονται νέες συντομεύσεις πληκτρολογίου" - Ctrl+F για εστίαση
        // στο πεδίο αναζήτησης είναι μία από τις πιο καθιερωμένες συμβάσεις πληκτρολογίου (browsers,
        // IDEs, κ.λπ.) και έλειπε εντελώς - πριν ο χρήστης έπρεπε πάντα να κάνει κλικ με το ποντίκι
        // στο πεδίο. SelectAll ώστε μια ήδη υπάρχουσα αναζήτηση να αντικαθίσταται αμέσως πληκτρολογώντας,
        // όχι να προστίθεται στο τέλος της.
        if (e.Key == Key.F)
        {
            TxtSearch.Focus();
            TxtSearch.SelectAll();
            e.Handled = true;
            return;
        }

        // ΝΕΟ - Ctrl+H ανοίγει απευθείας τον Πλήρη Έλεγχο Υγείας (ίδιο παράθυρο με το κουμπί στην
        // Αρχική/tray) - "H" για Health, χωρίς σύγκρουση με καμία άλλη ήδη καθιερωμένη συντόμευση.
        if (e.Key == Key.H)
        {
            new Views.HealthCheckWindow { Owner = this }.ShowDialog();
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

    // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "όταν κλείνω το παράθυρο κλείνει και το tray") - δεν υπήρχε ΚΑΝΕΝΑΣ
    // Closing handler εδώ, οπότε το X του παραθύρου έκλεινε το MainWindow κανονικά, και επειδή το
    // ShutdownMode είναι OnLastWindowClose (βλ. App.xaml.cs) αυτό πυροδοτούσε πλήρες Application
    // shutdown - μαζί του TrayIconService.Stop() (το tray icon εξαφανιζόταν) ΚΑΙ όλες οι άλλες
    // "πάντα ενεργές" υπηρεσίες παρασκηνίου (Clipboard hotkey, Desktop Widget, Auto Gaming Mode). Αυτό
    // αναιρούσε το νόημα του ήδη υπάρχοντος tray μενού ("Άνοιγμα κύριου παραθύρου"/"Έξοδος" - βλ.
    // TrayIconService._openMainItem/_exitItem) - αν το X έκλεινε ήδη τα πάντα, το "Έξοδος" θα ήταν
    // περιττό. Τώρα το X απλώς ΚΡΥΒΕΙ το παράθυρο (minimize-to-tray, ίδιο μοτίβο με το
    // RestoreMainWindow's .Show()) εκτός αν η έξοδος είναι πραγματικά σκόπιμη
    // (TrayIconService.IsExiting=true, οριζόμενο ΑΠΟΚΛΕΙΣΤΙΚΑ από το tray's "Έξοδος" πριν καλέσει
    // Application.Current.Shutdown() - διαφορετικά η εφαρμογή δεν θα μπορούσε ΠΟΤΕ να κλείσει πραγματικά,
    // αφού το Shutdown() κλείνει ΚΑΙ αυτό το ίδιο το MainWindow, πυροδοτώντας ξανά αυτό το Closing).
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (TrayIconService.IsExiting) return;
        e.Cancel = true;
        Hide();
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
            "Health" => new HealthView(),
            "System" => new SystemView(),
            "Network" => new NetworkView(),
            "Bloatware" => new BloatwareView(),
            "Tweaks" => new TweaksView(),
            "Advanced" => new AdvancedView(),
            _ => new PlaceholderView(label)
        };

        // ΝΕΟ (roadmap: "ομαλή μετάβαση καρτελών") - απαλό fade-in αντί για ακαριαία εναλλαγή
        // περιεχομένου· μικρή διάρκεια (180ms) ώστε να μην καθυστερεί αισθητά την πλοήγηση.
        ContentHost.Opacity = 0;
        ContentHost.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1,
            new Duration(TimeSpan.FromMilliseconds(180))) { EasingFunction = new System.Windows.Media.Animation.QuadraticEase() });
    }

    // Γεμίζει ΟΛΟΚΛΗΡΟ το κλασικό μενού (4 ομάδες: Εργαλεία/Προβολή/Ρυθμίσεις/Βοήθεια) από το κοινό
    // ClassicMenuModel.Groups αντί για χειρόγραφα MenuItem στο XAML (βλ. ClassicMenuModel.cs) - το
    // ΙΔΙΟ μοντέλο τροφοδοτεί και το πλευρικό μενού (SidebarNav), ώστε τα δύο να μην αποκλίνουν ποτέ.
    private void PopulateClassicMenu()
    {
        // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε με screenshot: "διπλότυπα" στη γραμμή μενού) - η μέθοδος καλείται
        // ΚΑΙ απευθείας στον constructor ΚΑΙ μέσα από την ApplyLanguage() (η οποία επίσης καλείται
        // στον constructor, στην εκκίνηση) - χωρίς Clear() εδώ, κάθε ομάδα προστίθεται στο
        // ClassicMenu.Items ΔΥΟ φορές ήδη από την πρώτη εκκίνηση της εφαρμογής.
        ClassicMenu.Items.Clear();
        foreach (var group in ClassicMenuModel.Groups)
        {
            if (group.Flat)
            {
                var flatLeaf = group.Items[0];
                var flatItem = new MenuItem { Header = $"{group.Icon} {group.Header}" };
                flatItem.Click += (_, _) => HandleLeafClick(flatLeaf);
                ClassicMenu.Items.Add(flatItem);
                continue;
            }

            var groupItem = new MenuItem { Header = $"{group.Icon} {group.Header}" };
            foreach (var leaf in group.Items)
            {
                var mi = new MenuItem
                {
                    Header = BuildLeafHeader(leaf),
                    Tag = leaf,
                    InputGestureText = leaf.Shortcut ?? "",
                };
                mi.Click += (_, _) => HandleLeafClick(leaf);
                groupItem.Items.Add(mi);
            }
            ClassicMenu.Items.Add(groupItem);
        }
    }

    private static object BuildLeafHeader(MenuLeaf leaf)
    {
        if (string.IsNullOrEmpty(leaf.Icon)) return leaf.Label;
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new TextBlock { Text = leaf.Icon, Margin = new Thickness(0, 0, 6, 0) },
                new TextBlock { Text = leaf.Label },
            },
        };
    }

    // Κοινή δρομολόγηση για ΚΑΘΕ leaf είτε κλασικού μενού είτε πλευρικού μενού: είτε πλοήγηση σε
    // καρτέλα, είτε "δεν έχει μεταφερθεί ακόμα" μήνυμα - ίδια σημασιολογία με το πρώην
    // MenuViewTab_Click/MenuNotPorted_Click, τώρα ενοποιημένη σε ένα σημείο.
    private void HandleLeafClick(MenuLeaf leaf)
    {
        if (leaf.Action.NavigateTag != null) SelectTab(leaf.Action.NavigateTag);
        else if (leaf.Action.DestinationKey != null) OpenDestination(leaf.Action.DestinationKey);
    }

    // Κοινή δρομολόγηση: βρίσκει το αντίστοιχο RadioButton στη λωρίδα καρτελών και το τσεκάρει - το
    // TabButton_Checked αναλαμβάνει από εκεί (ShowTabContent), οπότε το κλασικό μενού/Ctrl+1..8 ΔΕΝ
    // χρειάζεται να ξέρουν τίποτα το ένα για το άλλο.
    // internal (όχι private) - v3.2.0: το HomeView's νέο κουμπί κατάστασης δικτύου το καλεί
    // απευθείας για να πηδήξει στην καρτέλα Δίκτυο & Ασφάλεια (βλ. HomeView.xaml.cs's
    // BtnOpenNetworkTab_Click) - ίδιο assembly, ασφαλές χωρίς να γίνει πλήρως public API.
    internal void SelectTab(string tag)
    {
        foreach (var child in TabStrip.Children)
        {
            if (child is RadioButton rb && rb.Tag as string == tag) { rb.IsChecked = true; return; }
        }
    }

    // Το πλευρικό μενού (SidebarNav) είναι ΜΟΝΟ 6 συντομεύσεις προς δευτερεύοντα παράθυρα (βλ.
    // SidebarShortcuts.cs) - πλέον ΟΛΑ ανοίγουν το πραγματικό τους παράθυρο (ολοκλήρωση μεταφοράς).
    private void Sidebar_ShortcutClicked(SidebarShortcut shortcut) => OpenDestination(shortcut.DestinationKey);

    private bool _sidebarVisible;

    // Port του $btnHamburger.Add_Click (Optimizer.ps1 ~8147) - το "SidebarEnabled" είναι ο ΜΟΝΙΜΟΣ
    // (persisted) γενικός διακόπτης του χαρακτηριστικού (Ρυθμίσεις Εμφάνισης > Μενού) - το ίδιο το
    // κλικ στο ☰ αλλάζει ΜΟΝΟ την προσωρινή, κατά τη διάρκεια της συνεδρίας ορατότητα, ΔΕΝ γράφει
    // ξανά στη ρύθμιση (ίδια σημασιολογία με το ps1's Show/Hide-Sidebar - παροδικό, όχι ρύθμιση).
    // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "δεν βρίσκω πουθενά το πλευρικό και σύγχρονο μενού με το αντίστοιχο
    // κουμπί") - όσο το "Ενεργοποίηση πλευρικού μενού" είναι απενεργοποιημένο (Ρυθμίσεις Εμφάνισης >
    // Μενού), το ☰ έκανε ΑΠΟΛΥΤΑ ΤΙΠΟΤΑ (silent no-op, χωρίς ΚΑΝΕΝΑ οπτικό feedback) - από την οπτική
    // γωνία του χρήστη το χαρακτηριστικό απλά "εξαφανίζεται", χωρίς κανένα ίχνος για το πού να ψάξει.
    // Τώρα ανοίγει ΚΑΤΕΥΘΕΙΑΝ τις Ρυθμίσεις Εμφάνισης στην καρτέλα "Μενού" (initialTabIndex: 1) αντί
    // να μην κάνει τίποτα.
    private void BtnHamburger_Click(object sender, RoutedEventArgs e)
    {
        if (!AppSettingsService.Current.SidebarEnabled)
        {
            new AppearanceSettingsWindow(initialTabIndex: 1) { Owner = this }.ShowDialog();
            return;
        }

        if (AppSettingsService.Current.MenuMode == "HorizontalModern")
        {
            if (HorizModernStrip.Visibility == Visibility.Visible) HideHorizModernStrip();
            else ShowHorizModernStrip();
            return;
        }

        _sidebarVisible = !_sidebarVisible;
        ApplySidebarLayout();
    }

    // Ρητό αίτημα χρήστη: "να ξετυλίγεται από το κινούμενο εικονίδιο" + "να καλύπτει και το search" -
    // ξεκινά με πλάτος 0 (δίπλα στο γρανάζι, ίδια θέση με τον τίτλο) και ανοίγει προς το φυσικό του
    // πλάτος· το TxtSearch/SearchPopup κρύβονται όσο είναι ανοιχτό ώστε να μην υπάρχει επικάλυψη.
    private void ShowHorizModernStrip()
    {
        TitleTextPanel.Visibility = Visibility.Collapsed;
        TxtSearch.Visibility = Visibility.Collapsed;
        SearchPopup.IsOpen = false;
        HorizModernStrip.Visibility = Visibility.Visible;

        HorizModernStripInner.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var targetWidth = HorizModernStripInner.DesiredSize.Width;
        HorizModernStrip.Width = 0;
        var anim = new DoubleAnimation(0, targetWidth, new Duration(System.TimeSpan.FromMilliseconds(400))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        HorizModernStrip.BeginAnimation(WidthProperty, anim);
    }

    private void HideHorizModernStrip()
    {
        HorizModernStrip.BeginAnimation(WidthProperty, null);
        HorizModernStrip.Visibility = Visibility.Collapsed;
        TitleTextPanel.Visibility = Visibility.Visible;
        TxtSearch.Visibility = Visibility.Visible;
    }

    private void HorizModernItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SidebarShortcut shortcut }) return;
        OpenDestination(shortcut.DestinationKey);
    }

    // Port του Apply-MenuModeSettings (Optimizer.ps1 ~6347) - επαναφέρει ΚΑΙ τα δύο εναλλακτικά UI
    // πλοήγησης (πλευρικό/οριζόντιο) σε καθαρή, κλειστή κατάσταση, μετά επανεφαρμόζει ό,τι ΘΑ έπρεπε
    // να είναι αυτόματα ορατό για τις τρέχουσες ρυθμίσεις (μόνιμα ενσωματωμένο πλευρικό μενού σε
    // MenuMode="Sidebar" όταν SidebarEnabled - ps1's startupSidebarShowTimer). Καλείται στο startup ΚΑΙ
    // από το AppearanceSettingsWindow όποτε αλλάζει MenuMode/SidebarEnabled/SidebarPosition, ώστε η
    // αλλαγή να φανεί άμεσα χωρίς επανεκκίνηση.
    public void ApplyMenuModeVisibility()
    {
        HideHorizModernStrip();

        var settings = AppSettingsService.Current;
        _sidebarVisible = settings.MenuMode == "Sidebar" && settings.SidebarEnabled;
        ApplySidebarLayout();
        ApplyPcManagerSkinLayout();
    }

    // Υποστηρίζει ΚΑΙ τις δύο θέσεις (Δεξιά/Αριστερά, ρύθμιση "Μενού" του AppearanceSettingsWindow) -
    // εναλλάσσει ποια στήλη έχει το πλάτος του πλευρικού μενού και σε ποια στήλη βρίσκεται το καθένα
    // από τα δύο στοιχεία (Sidebar/ContentHostBorder), αντί να χρειάζεται ξεχωριστό Grid layout ανά θέση.
    private void ApplySidebarLayout()
    {
        var sidebarSize = _sidebarVisible ? GridLength.Auto : new GridLength(0);
        ColGap.Width = _sidebarVisible ? new GridLength(12) : new GridLength(0);

        if (AppSettingsService.Current.SidebarPosition == "Left")
        {
            ColA.Width = sidebarSize;
            ColC.Width = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(Sidebar, 1);
            Grid.SetColumn(ContentHostBorder, 3);
        }
        else
        {
            ColA.Width = new GridLength(1, GridUnitType.Star);
            ColC.Width = sidebarSize;
            Grid.SetColumn(Sidebar, 3);
            Grid.SetColumn(ContentHostBorder, 1);
        }
        Sidebar.Visibility = _sidebarVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    // Ανοίγει το πραγματικό δευτερεύον παράθυρο για κάθε προορισμό μενού/πλευρικού μενού - πλέον ΟΛΑ
    // είναι υλοποιημένα (ολοκλήρωση μεταφοράς, ρητό αίτημα χρήστη). Οι ίδιες ετικέτες χρησιμοποιούνται
    // σκόπιμα διαφορετικές ανάμεσα σε κλασικό/πλευρικό μενού (βλ. SidebarShortcuts.cs), οπότε ελέγχονται
    // και οι δύο παραλλαγές εδώ.
    // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε μετά τη μετάφραση του μενού): το `key` εδώ ΠΡΕΠΕΙ να
    // είναι σταθερό LanguageService key name (π.χ. "Vive_Title"), ΟΧΙ μεταφρασμένο κείμενο - πριν
    // γινόταν match σε literal ελληνικό κείμενο, που θα έσπαγε τη δρομολόγηση σε κάθε άλλη γλώσσα
    // μετά τη μετάφραση των μενού (ClassicMenuModel.cs/SidebarShortcuts.cs).
    private void OpenDestination(string key)
    {
        Window? window = key switch
        {
            "Vive_Title" => new ViveToolWindow { Owner = this },
            "Uwp_Title" => new UwpAppManagerWindow { Owner = this },
            // ΝΕΟ - roadmap "έλεγξε αν χρειάζονται ανανεώσεις σε δευτερεύοντα παράθυρα/μενού" - ο
            // Πλήρης Έλεγχος Υγείας ήταν προσβάσιμος ΜΟΝΟ από κουμπί στην Αρχική (+ το tray, +το νέο
            // Ctrl+H) - πρόσθεσε εδώ ώστε να είναι προσβάσιμος ΚΑΙ από το κλασικό μενού/πλευρικό
            // μενού, ίδιο μοτίβο με τα υπόλοιπα δευτερεύοντα παράθυρα.
            "HealthCheck_Title" => new HealthCheckWindow { Owner = this },
            "AppearanceSettingsTitle" => new AppearanceSettingsWindow { Owner = this },
            // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "ενσωμάτωσε το ιστορικό εκδόσεων στο ίδιο δευτερεύον
            // παράθυρο σε tab") - δεν υπάρχει πια ξεχωριστό VersionHistoryWindow, ανοίγει το ίδιο
            // HelpWindow κατευθείαν στο 3ο tab (index 2, βλ. HelpWindow.xaml), ίδιο μοτίβο με το
            // "License_Title" -> HelpWindow(initialTabIndex: 1) παρακάτω.
            "VerHist_Title" => new HelpWindow(initialTabIndex: 2) { Owner = this },
            "Help_Title" => new HelpWindow { Owner = this },
            "ActionLog_Title" => new ActionLogWindow { Owner = this },
            // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε: "στην Βοήθεια/Οδηγίες να ανοίγει το παράθυρο με δύο tabs όπου
            // το δεύτερο θα είναι η άδεια χρήσης") - δεν υπάρχει πια ξεχωριστό LicenseWindow, ανοίγει
            // το ίδιο HelpWindow κατευθείαν στο 2ο tab (index 1).
            "License_Title" => new HelpWindow(initialTabIndex: 1) { Owner = this },
            _ => null,
        };

        // ΔΙΟΡΘΩΣΗ (bug εντοπίστηκε - χρήστης ανέφερε: "ανοίγοντας το ιστορικό clipboard το παράθυρο
        // φαίνεται να κολλάει και βγάζει κάποιο μήνυμα λάθους") - το ClipboardHistoryWindow είναι
        // σχεδιασμένο ως ελαφρύ, αυτο-κλειόμενο popup (Window_Deactivated -> Close(), βλ. εκεί) για
        // την αρχική του χρήση μέσω tray icon/Win+Shift+V (TrayIconService: .Show(), ΧΩΡΙΣ Owner). Η
        // δρομολόγηση εδώ (πλευρικό μενού/κλασικό μενού) το άνοιγε με το ΓΕΝΙΚΟ .ShowDialog() παρακάτω -
        // ένα modal ShowDialog() πυροδοτεί Deactivated (άρα Close()) ΜΕΣΑ στο δικό του Window_Loaded,
        // ΠΡΙΝ ολοκληρωθεί η εσωτερική διαδικασία εμφάνισης του ShowDialog - ακριβώς το σφάλμα WPF
        // "Cannot set Visibility to Visible or call Show, ShowDialog, or WindowInteropHelper.
        // EnsureHandle while a Window is closing." Το ίδιο το παράθυρο ΔΕΝ έγινε ποτέ modal dialog -
        // το μη-αποκλειστικό .Show() (ίδιο με το tray icon) δουλεύει σωστά από ΚΑΘΕ σημείο εισόδου.
        if (key == "Clipboard_Title")
        {
            new ClipboardHistoryWindow { Owner = this }.Show();
            return;
        }
        if (window != null) window.ShowDialog();
        else ThemedMessageBox.Show($"{LanguageService.T("Main_NotPortedPrefix")}{LanguageService.T(key)}{LanguageService.T("Main_NotPortedSuffix")}",
            LanguageService.T("Main_NotPortedTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "το κουμπί γλώσσα δεν βγάζει ξεχωριστό dropdown") - βλ. σχόλιο
    // στο LanguagePopup στο XAML. Λίστα φτιάχνεται ΚΑΘΕ φορά που ανοίγει (όχι μια φορά στον
    // constructor) ώστε το "Name" της τρέχουσας γλώσσας να ενημερώνεται αν άλλαξε από αλλού
    // (π.χ. από το πλήρες dropdown στις Ρυθμίσεις Εμφάνισης) πριν ξανανοίξει αυτό το popup.
    private void BtnLanguage_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in ListLanguages.Items)
        {
            if (item is ListBoxItem lbi && lbi.Tag as string == LanguageService.Current)
            {
                ListLanguages.SelectedItem = lbi;
                break;
            }
        }
        LanguagePopup.IsOpen = !LanguagePopup.IsOpen;
    }

    private void ListLanguages_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListLanguages.SelectedItem is not ListBoxItem { Tag: string code }) return;
        LanguageService.SetLanguage(code);
        LanguagePopup.IsOpen = false;
    }

    // Ζωντανή προεπισκόπηση - ταιριάζει ΚΑΘΕ λέξη του query ως substring στην ετικέτα (μερικό/κοινών
    // λέξεων ταίριασμα, ρητό αίτημα χρήστη), όχι μόνο ταίριασμα με την αρχή της ετικέτας.
    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = TxtSearch.Text.Trim();
        if (query.Length == 0) { SearchPopup.IsOpen = false; return; }

        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var results = _searchIndex
            .Where(r => words.All(w => r.Label.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Take(8)
            .ToList();

        // ΝΕΟ - βελτίωση: fallback σε fuzzy (Levenshtein) matching ΜΟΝΟ όταν το ακριβές substring
        // matching παραπάνω δεν βρίσκει τίποτα - πιάνει μικρά τυπογραφικά λάθη (π.χ. "netwrok" ->
        // "network") χωρίς να αλλοιώνει τα ήδη γρήγορα/ακριβή αποτελέσματα του συνηθισμένου path.
        if (results.Count == 0)
        {
            results = _searchIndex
                .Select(r => (Row: r, Score: FuzzyScore(words, r.Label)))
                .Where(x => x.Score < int.MaxValue)
                .OrderBy(x => x.Score)
                .Select(x => x.Row)
                .Take(8)
                .ToList();
        }

        ListSearchResults.ItemsSource = results;
        SearchPopup.IsOpen = results.Count > 0;
    }

    // Κάθε λέξη του query πρέπει να ταιριάζει (κατά προσέγγιση) με ΤΟΥΛΑΧΙΣΤΟΝ μία λέξη της ετικέτας -
    // ίδια λογική AND με το ακριβές matching παραπάνω. Το κατώφλι απόστασης κλιμακώνεται με το μήκος
    // της λέξης (1 για κοντές, 2 για μεγαλύτερες) ώστε ένα τυπογραφικό λάθος να περάσει αλλά όχι μια
    // εντελώς άσχετη λέξη. Επιστρέφει int.MaxValue αν δεν ταιριάζει καμία λέξη του query.
    private static int FuzzyScore(string[] queryWords, string label)
    {
        var labelWords = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var total = 0;
        foreach (var qw in queryWords)
        {
            var threshold = qw.Length <= 4 ? 1 : 2;
            var best = int.MaxValue;
            foreach (var lw in labelWords)
            {
                var dist = Levenshtein(qw, lw);
                if (dist < best) best = dist;
            }
            if (best > threshold) return int.MaxValue;
            total += best;
        }
        return total;
    }

    private static int Levenshtein(string a, string b)
    {
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();
        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
            }
        }
        return dp[a.Length, b.Length];
    }

    private void ListSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListSearchResults.SelectedItem is not SearchResultRow row) return;
        SearchPopup.IsOpen = false;
        TxtSearch.Text = "";
        HandleLeafClick(row.Leaf);
    }

    private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.ToggleLightDark();
        RefreshThemeToggleLabel();
    }

    // ΔΙΟΡΘΩΣΗ (βρέθηκε κατά τον έλεγχο για "elements μένουν στην προηγούμενη γλώσσα") - το κείμενο
    // αυτού του κουμπιού ήταν ΚΥΡΙΟΛΕΚΤΙΚΑ hardcoded αγγλικά ("Light Mode"/"Dark Mode"), ποτέ δεν
    // περνούσε καν από το LanguageService - όχι μόνο δεν ανανεωνόταν σε αλλαγή γλώσσας, ήταν πάντα
    // στα αγγλικά ανεξαρτήτως γλώσσας. Επαναχρησιμοποιεί τα ήδη υπάρχοντα Onb_SwitchToLight/
    // Onb_SwitchToDark κλειδιά (ίδιο νόημα, ίδιο ☀/☽ σύμβολο - του onboarding) αντί να προστεθούν
    // δύο νέα σχεδόν πανομοιότυπα κλειδιά σε 14 γλώσσες. Καλείται από τον constructor, από το
    // BtnThemeToggle_Click, ΚΑΙ από το ApplyLanguage() ώστε να μένει σωστό σε κάθε σενάριο.
    private void RefreshThemeToggleLabel() =>
        TxtThemeToggle.Text = ThemeManager.IsDarkMode ? LanguageService.T("Onb_SwitchToLight") : LanguageService.T("Onb_SwitchToDark");

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

// Μία γραμμή αποτελέσματος αναζήτησης - "τυλίγει" ένα MenuLeaf (ίδιο μοντέλο με το κλασικό/πλευρικό
// μενού) μαζί με την ομάδα του, για εμφάνιση στο popup προεπισκόπησης.
public record SearchResultRow(string Icon, string Label, string GroupHeader, MenuLeaf Leaf);
