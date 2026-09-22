using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

namespace OptimizerWpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "η εφαρμογή εξακολουθεί να εμφανίζεται ως 4.6.0") - το MainWindow.xaml
    // και το SplashWindow.xaml είχαν το version string ΚΥΡΙΟΛΕΚΤΙΚΑ γραμμένο ("v4.6.0") αντί να
    // διαβάζεται από το OptimizerWpf.csproj's <Version> - κάθε version bump ξεχνιόταν εύκολα σε ένα από
    // τα δύο σημεία (ο installer/VerHist ενημερώνονταν σωστά, αυτά όχι). Τώρα διαβάζεται μία φορά εδώ,
    // από το πραγματικό AssemblyVersion (που ΠΑΝΤΑ ταιριάζει με το <Version>, βλ. csproj) - αδύνατο να
    // ξαναμείνει stale σε μελλοντικά version bumps.
    public static string DisplayVersion { get; } = $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!.ToString(3)}";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ΝΕΟ - headless λειτουργία, καλείται ΜΟΝΟ από τον installer's [UninstallRun] (βλ.
        // installer/OptimizerWpf.iss, TweakService.RestoreAllTrackedTweaks) - ρητό αίτημα χρήστη:
        // "επαναφορά όλων των ρυθμίσεων των windows στα προεπιλεγμένα αν το θέλει ο χρήστης" κατά την
        // απεγκατάσταση. Καμία οθόνη/παράθυρο - επαναφέρει ό,τι έχει καταγεγραμμένη προηγούμενη τιμή
        // και τερματίζει αμέσως, ΠΡΙΝ φορτωθεί οτιδήποτε άλλο (θέμα/γλώσσα/action log).
        if (e.Args.Contains("--reset-tweaks"))
        {
            try { Services.TweakService.RestoreAllTrackedTweaks(); } catch { }
            Shutdown();
            return;
        }

        // ΝΕΟ - roadmap "Προγραμματισμένη συντήρηση" - καλείται ΜΟΝΟ από την εβδομαδιαία εργασία του
        // Task Scheduler που δημιουργεί/αφαιρεί το AdvancedToolsService.SetScheduledMaintenanceAsync
        // (βλ. OptimizationView) - ίδιο μοτίβο headless-run με το --reset-tweaks παραπάνω, καμία οθόνη.
        if (e.Args.Contains("--auto-maintenance"))
        {
            try { Services.AdvancedToolsService.RunScheduledMaintenance(); } catch { }
            Shutdown();
            return;
        }

        // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "η εφαρμογή κρασάρει και κλείνει" σε μια σάρωση) - χωρίς αυτόν
        // τον global handler, ΚΑΘΕ μη-χειρισμένη εξαίρεση σε async void event handler (το μοτίβο που
        // χρησιμοποιείται σχεδόν παντού στην εφαρμογή για Click handlers) τερματίζει ΣΙΩΠΗΛΑ ολόκληρη
        // την εφαρμογή - προεπιλεγμένη συμπεριφορά του WPF. Τώρα δείχνεται ένα μήνυμα σφάλματος και η
        // εφαρμογή συνεχίζει να τρέχει αντί να κλείνει απότομα.
        DispatcherUnhandledException += (_, args) =>
        {
            ThemedMessageBox.Show($"{Services.LanguageService.T("App_UnhandledErrorPrefix")}{args.Exception.Message}{Services.LanguageService.T("App_UnhandledErrorSuffix")}",
                Services.LanguageService.T("App_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        // Applies the persisted (or default) theme's brushes BEFORE MainWindow is constructed, so
        // every DynamicResource binding in its XAML already has a real value on the very first
        // frame instead of resolving to nothing until the first theme change.
        ThemeManager.LoadPersisted();
        Services.LanguageService.LoadPersisted();
        Services.ActionLogService.EnsureStarted();

        // ΝΕΟ - roadmap "Αυτόματο Gaming Mode" - ξεκινά ΜΟΝΟ αν ο χρήστης το έχει ενεργοποιήσει
        // ρητά (Βελτιστοποίηση), σταματά καθαρά στο κλείσιμο της εφαρμογής (επαναφέρει Gaming Mode
        // αν ήταν ενεργό αυτόματα, ώστε να μη μείνει "κολλημένο" μετά το κλείσιμο).
        if (Services.AppSettingsService.Current.AutoGamingModeEnabled) Services.AutoGamingModeService.Start();
        Exit += (_, _) => Services.AutoGamingModeService.Stop();

        // ΝΕΟ - roadmap "Κίνηση δικτύου ανά εφαρμογή" (v4.3.5) - δίχτυ ασφαλείας. Το NetworkView ήδη
        // σταματά το ETW session στο Unloaded (αλλαγή καρτέλας), αλλά αν ο χρήστης κλείσει ΟΛΟΚΛΗΡΗ
        // την εφαρμογή ενώ βρίσκεται ακόμα στην καρτέλα Δίκτυο με ενεργή παρακολούθηση, αυτό εδώ
        // εγγυάται ότι το system-wide "NT Kernel Logger" session ΔΕΝ θα μείνει "κολλημένο" ανοιχτό
        // μετά το κλείσιμο (θα χρειαζόταν χειροκίνητο `logman stop "NT Kernel Logger" -ets`).
        Exit += (_, _) => Services.NetworkTrafficService.Stop();

        // ΝΕΟ - "πρώτη κουρτίνα" οδηγού χρήσης (ρητό αίτημα χρήστη: "κουρτίνα με tutorial στο πρώτο
        // ανοιγμα της εφαρμογης... πριν το γραφικό με το λογότυπο") - εμφανίζεται ΠΡΙΝ καν δημιουργηθεί
        // το MainWindow/SplashWindow, ΜΟΝΟ όσο δεν έχει ξαναδειχθεί (AppSettings.HasSeenOnboarding).
        // ShowDialog() μπλοκάρει εδώ μέχρι ο χρήστης να πατήσει Ξεκινήστε/Παράλειψη/κλείσιμο - βλ.
        // Views/OnboardingWindow.xaml(.cs). Ο χρήστης μπορεί να το ξαναδεί αργότερα από τη Βοήθεια.
        // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε, με screenshot: "Cannot set Owner property to a Window that has
        // not been shown previously") - το ΠΡΟΕΠΙΛΕΓΜΕΝΟ ShutdownMode (OnLastWindowClose) σημαίνει ότι
        // το κλείσιμο του OnboardingWindow (μέσω Παράλειψη/Ξεκινήστε) πυροδοτεί ΑΥΤΟΜΑΤΟ Application
        // shutdown ΑΚΡΙΒΩΣ επειδή, σε πρώτη εκκίνηση, είναι το ΜΟΝΑΔΙΚΟ ανοιχτό παράθυρο τη στιγμή που
        // κλείνει (το MainWindow δεν έχει δημιουργηθεί ακόμα) - ο εσωτερικός μηχανισμός shutdown του
        // WPF παρεμβαίνει στη μέση της αμέσως επόμενης δημιουργίας MainWindow/SplashWindow, με
        // αποτέλεσμα το Owner=main του SplashWindow να αποτυγχάνει. OnExplicitShutdown ΜΟΝΟ γύρω από
        // αυτό το ένα ShowDialog() εμποδίζει το "τελευταίο παράθυρο έκλεισε" να μετρήσει καθόλου εδώ -
        // επαναφέρεται αμέσως μετά, πριν δημιουργηθεί το πραγματικό MainWindow.
        if (!Services.AppSettingsService.Current.HasSeenOnboarding)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            new Views.OnboardingWindow().ShowDialog();
            ShutdownMode = ShutdownMode.OnLastWindowClose;
        }

        // Χειροκίνητη εκκίνηση (αντί για StartupUri) - χρειάζεται το MainWindow ήδη Show()-αρισμένο
        // (με πραγματικό Left/Top/ActualWidth/ActualHeight) ΠΡΙΝ δημιουργηθεί η οθόνη εκκίνησης, ώστε
        // η SplashWindow να μπορεί να καλύψει ΑΚΡΙΒΩΣ τα ίδια όρια και αργότερα να διαβάσει τη θέση
        // του πραγματικού γραναζιού (TitleGearBorder) για το εφέ "προσγείωσης" - βλ. SplashWindow.
        var main = new MainWindow();
        MainWindow = main;
        main.Show();

        var splash = new Views.SplashWindow(main) { Owner = main };
        splash.Show();

        // ΝΕΟ - roadmap "Mini widget / system tray live view" - ενεργό όσο τρέχει η εφαρμογή.
        Services.TrayIconService.Start(main);
        Exit += (_, _) => Services.TrayIconService.Stop();

        // ΝΕΟ - roadmap "Widget επιφάνειας εργασίας" - ξεκινά ΜΟΝΟ αν ο χρήστης το έχει ενεργοποιήσει
        // ρητά (Ρυθμίσεις Εμφάνισης), ίδιο μοτίβο με το AutoGamingModeEnabled παραπάνω.
        if (Services.AppSettingsService.Current.DesktopWidgetEnabled) Services.DesktopWidgetService.Start();
        Exit += (_, _) => Services.DesktopWidgetService.Stop();

        // ΝΕΟ - roadmap "Αναζητήσιμο ιστορικό clipboard" (ρητό αίτημα χρήστη) - ενεργό ΠΑΝΤΑ όσο τρέχει
        // η εφαρμογή (ίδιο μοτίβο με το TrayIconService παραπάνω, ΟΧΙ προαιρετικό όπως το Desktop
        // Widget) - το global hotkey (Win+Shift+V) πρέπει να δουλεύει από την πρώτη εκκίνηση χωρίς να
        // χρειάζεται ρητή ενεργοποίηση, ίδιο πνεύμα με τα υπόλοιπα "πάντα διαθέσιμα" tray/system
        // χαρακτηριστικά.
        Services.ClipboardManagerService.Start(main);
        Services.ClipboardManagerService.HotkeyPressed += () =>
        {
            main.Dispatcher.Invoke(() => new Views.ClipboardHistoryWindow().Show());
        };
        Exit += (_, _) => Services.ClipboardManagerService.Stop();
    }
}

