using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using OptimizerWpf.Services;

namespace OptimizerWpf
{
    // Applies a ThemeColors palette directly onto the app's resource brushes (MainBgBrush,
    // CardBgBrush, etc. - the same keys every XAML file already binds to via DynamicResource).
    public static class ThemeManager
    {
        public static bool IsDarkMode { get; private set; } = true;
        public static ThemePair CurrentPair { get; private set; } = ThemeCatalog.Windows11Fluent;
        public static ThemeColors CurrentTheme => CurrentPair.Get(IsDarkMode);
        public static bool AnimatedBackgrounds { get; private set; } = true;

        // Πυροδοτείται όποτε αλλάζει το θέμα, το Light/Dark, Ή η ρύθμιση κινούμενων φόντων -
        // το ThemedBackgroundControl κάθε παραθύρου (κύριου + δευτερευόντων) εγγράφεται εδώ μέσω
        // AttachBackground αντί να χρειάζεται κάθε παράθυρο τη δική του σκόρπια συνδρομή.
        public static event Action? Changed;

        // Επανα-εφαρμόζει το τρέχον φόντο σε όλα τα εγγεγραμμένα ThemedBackgroundControl - καλείται
        // όταν αλλάζει κάτι που ΔΕΝ επηρεάζει το ίδιο το ThemeManager (π.χ. AnimationStyleOverride,
        // μια ρύθμιση του AppSettingsService), οπότε δεν υπάρχει άλλος λόγος να πυροδοτηθεί το Changed.
        public static void RefreshBackgrounds() => Changed?.Invoke();

        public static void SetAnimatedBackgrounds(bool value)
        {
            AnimatedBackgrounds = value;
            AppSettingsService.Current.AnimatedBackgrounds = value;
            AppSettingsService.Save();
            Changed?.Invoke();
        }

        // Βολικό σημείο σύνδεσης για ΚΑΘΕ ThemedBackgroundControl (κύριο + δευτερεύοντα παράθυρα) -
        // εφαρμόζει αμέσως το τρέχον θέμα/κατάσταση ΚΑΙ εγγράφεται για μελλοντικές αλλαγές, με αυτόματη
        // διαγραφή εγγραφής στο Unloaded του παραθύρου (αποφεύγει διαρροή μνήμης από στατικό event).
        // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε: "βγάλε από τα δευτερεύοντα παράθυρα το κινούμενο φόντο") - η
        // εφαρμογή τετράγωνων γωνιών (Windows Classic skin) χρειάζεται ΚΑΘΕ παράθυρο, ανεξάρτητα από
        // το αν έχει ΚΑΙ ThemedBackgroundControl - διαχωρίστηκε σε δικό του σημείο σύνδεσης ώστε τα
        // δευτερεύοντα παράθυρα (που έχασαν το animated background τους) να το καλούν απευθείας.
        public static void AttachWindow(Window window)
        {
            void ApplyCorners() => Services.WindowCornerService.SetSquareCorners(window, IsWindowsClassicSkin);
            window.SourceInitialized += (_, _) => ApplyCorners();
            void OnChanged() => ApplyCorners();
            Changed += OnChanged;
            window.Closed += (_, _) => Changed -= OnChanged;

            // ΝΕΟ - υποστήριξη RTL layout για τα Αραβικά (μόνο μετάφραση κειμένου δεν αρκεί για μια
            // αυθεντική εμπειρία - η ίδια η διάταξη πρέπει να "γυρίσει": σειρά κουμπιών, στοίχιση,
            // κατεύθυνση κύλισης). Το WPF's FlowDirection είναι κληρονομήσιμο σε όλο το visual tree,
            // οπότε αρκεί να οριστεί στη ρίζα (το ίδιο το Window) - ΟΛΑ τα παιδιά (StackPanel,
            // TextBlock, Button, κ.λπ.) το κληρονομούν αυτόματα χωρίς καμία αλλαγή σε XAML. Εφαρμόζεται
            // αμέσως ΚΑΙ ξανά σε κάθε αλλαγή γλώσσας (π.χ. αν ο χρήστης αλλάξει γλώσσα ενώ είναι ήδη
            // ανοιχτό ένα δευτερεύον παράθυρο).
            void ApplyFlowDirection() => window.FlowDirection = LanguageService.Current == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            ApplyFlowDirection();
            void OnLanguageChanged() => ApplyFlowDirection();
            LanguageService.Changed += OnLanguageChanged;
            window.Closed += (_, _) => LanguageService.Changed -= OnLanguageChanged;
        }

        public static void AttachBackground(Views.ThemedBackgroundControl control)
        {
            // Το πραγματικό HWND ενδέχεται να μην υπάρχει ακόμα στον constructor (πριν το Show()) -
            // το AttachWindow's SourceInitialized είναι η εγγύηση ότι θα ξαναπροσπαθήσει μόλις υπάρξει
            // πραγματικό handle.
            var ownerWindow = Window.GetWindow(control);
            if (ownerWindow != null) AttachWindow(ownerWindow);

            // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε: "δες αν βαραίνει η εφαρμογή πολύ") - το animation ΔΕΝ έχει
            // κανένα λόγο να τρέχει όποτε ο χρήστης δεν κοιτάζει το παράθυρο (ελαχιστοποιημένο, ή
            // κάποιο άλλο παράθυρο έχει focus) - καθαρό κέρδος CPU χωρίς καμία οπτική απώλεια, αφού
            // δεν είναι ορατό ούτως ή άλλως.
            if (ownerWindow != null)
            {
                ownerWindow.Deactivated += (_, _) => control.Pause();
                ownerWindow.Activated += (_, _) => control.Resume();
                ownerWindow.StateChanged += (_, _) =>
                {
                    if (ownerWindow.WindowState == WindowState.Minimized) control.Pause();
                    else if (ownerWindow.IsActive) control.Resume();
                };
                // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "ελαφρύτερη εφαρμογή... παύση φόντου όταν καλύπτεται") -
                // το Deactivated παραπάνω ήδη καλύπτει τη συνηθισμένη περίπτωση (ο χρήστης πέρασε σε
                // άλλη εφαρμογή) - αυτό εδώ πιάνει το σπανιότερο σενάριο όπου το παράθυρο γίνεται
                // ΤΕΛΕΙΩΣ αόρατο (π.χ. εναλλαγή virtual desktop) ΧΩΡΙΣ απαραίτητα να χάσει focus πρώτα.
                // Καμία πραγματική οπτική απώλεια αφού δεν είναι ορατό ούτως ή άλλως.
                ownerWindow.IsVisibleChanged += (_, e) =>
                {
                    if (!(bool)e.NewValue) control.Pause();
                    else if (ownerWindow.IsActive) control.Resume();
                };
            }

            // Ρητό αίτημα χρήστη: όλα τα θέματα έχουν σταθερό/κινούμενο φόντο ΕΚΤΟΣ από το
            // "Microsoft PC Manager" (το πραγματικό MS PC Manager app δεν έχει τέτοιο φόντο) -
            // κεντρικό σημείο ώστε ΚΑΘΕ παράθυρο (κύριο + δευτερεύοντα) να το σέβεται αυτόματα.
            void Apply()
            {
                var isPcManager = CurrentPair.DisplayName == "Microsoft PC Manager";
                control.Visibility = isPcManager ? Visibility.Collapsed : Visibility.Visible;
                if (isPcManager) return;
                // Ρητό αίτημα χρήστη (screenshot Ρυθμίσεων Εμφάνισης): "Κινούμενο φόντο" έχει καθολική
                // επιλογή στυλ (Γρανάζια/Τροχιές/Σωματίδια/Κύματα/Πλέγμα) που αντικαθιστά το ανά-θέμα
                // στυλ όταν δεν είναι "Theme" (=Εφέ θέματος).
                var overrideStyle = AppSettingsService.Current.AnimationStyleOverride;
                var style = overrideStyle == "Theme" ? ThemeBackgroundStyles.For(CurrentPair.DisplayName) : overrideStyle;
                control.ApplyTheme(style, IsDarkMode, AnimatedBackgrounds);
            }
            Apply();
            Changed += Apply;
            control.Unloaded += (_, _) => Changed -= Apply;
        }

        // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "ολοκλήρωσε τη μεταφορά") - το θέμα επανερχόταν πάντα σε
        // Windows 11 Fluent/Dark σε κάθε εκκίνηση, καμία επιμονή δεν υπήρχε πριν. Καλείται από
        // App.xaml.cs στο startup, ΠΡΙΝ την πρώτη Apply().
        public static void LoadPersisted()
        {
            var settings = AppSettingsService.Current;
            // ΔΙΟΡΘΩΣΗ - ThemeCatalog.All δεν περιλαμβάνει πια τα skins (Microsoft PC Manager/Windows
            // Classic, βλ. σχόλιο εκεί) - AllIncludingSkins εδώ ώστε ένα αποθηκευμένο skin ως
            // προεπιλογή εκκίνησης να συνεχίζει να αναγνωρίζεται σωστά.
            var pair = ThemeCatalog.AllIncludingSkins.FirstOrDefault(p => p.DisplayName == settings.ThemeName) ?? ThemeCatalog.Windows11Fluent;
            CurrentPair = pair;
            IsDarkMode = settings.IsDarkMode;
            AnimatedBackgrounds = settings.AnimatedBackgrounds;
            Apply();
        }

        // Selects a theme (keeps the current Light/Dark state, applying that variant of the newly
        // picked theme) - what the theme picker calls.
        public static void SelectTheme(ThemePair pair)
        {
            CurrentPair = pair;
            Apply();
            Persist();
        }

        // Ρητό αίτημα χρήστη (screenshot Ρυθμίσεων Εμφάνισης): "Ορισμός ως προεπιλεγμένο θέμα
        // εκκίνησης" - χωρίς αυτό το checkbox, η επιλογή θέματος εφαρμόζεται ζωντανά ΓΙΑ ΤΗΝ ΤΡΕΧΟΥΣΑ
        // συνεδρία μόνο, ΧΩΡΙΣ να γράφεται στις persisted ρυθμίσεις - στην επόμενη εκκίνηση θα
        // επανέλθει ό,τι ήταν πραγματικά αποθηκευμένο πριν.
        public static void PreviewTheme(ThemePair pair)
        {
            CurrentPair = pair;
            Apply();
            Changed?.Invoke();
        }

        // Every theme has both variants now (see ThemeCatalog) - toggling always re-applies the
        // SAME theme's other palette, matching Optimizer.ps1's Get-ThemeColors (branches on
        // isDarkMode first, theme name second - the theme selection itself never changes on toggle).
        public static void ToggleLightDark()
        {
            IsDarkMode = !IsDarkMode;
            Apply();
            Persist();
        }

        private static void Persist()
        {
            AppSettingsService.Current.ThemeName = CurrentPair.DisplayName;
            AppSettingsService.Current.IsDarkMode = IsDarkMode;
            if (CurrentPair.DisplayName != "Microsoft PC Manager" && CurrentPair.DisplayName != "Windows Classic")
                AppSettingsService.Current.LastNonPcManagerTheme = CurrentPair.DisplayName;
            AppSettingsService.Save();
            Changed?.Invoke();
        }

        // Ρητό αίτημα χρήστη: "Windows Classic skin - τετράγωνα παράθυρα, όλα τα χαρακτηριστικά όπως τα
        // αυθεντικά Windows Classic". True όταν είναι επιλεγμένο το θέμα "Windows Classic" - το MainWindow
        // το χρησιμοποιεί για να κρύψει sidebar/hamburger/tab-strip και να αναγκάσει το κλασικό μενού
        // πάντα ορατό (ίδιο μοτίβο με το ήδη υπάρχον "Microsoft PC Manager" skin παραπάνω).
        public static bool IsWindowsClassicSkin => CurrentPair.DisplayName == "Windows Classic";

        private static void Apply()
        {
            var theme = CurrentTheme;
            var res = Application.Current.Resources;
            res["MainBgBrush"] = new SolidColorBrush(theme.MainBg);
            res["TabBgBrush"] = new SolidColorBrush(theme.TabBg);
            res["TabSelectBrush"] = new SolidColorBrush(theme.TabSelect);
            res["TextBrush"] = new SolidColorBrush(theme.Text);
            res["SubTextBrush"] = new SolidColorBrush(theme.SubText);
            res["StatusTextBrush"] = new SolidColorBrush(theme.StatusText);
            res["CardBgBrush"] = new SolidColorBrush(theme.CardBg);
            res["CardBorderBrush"] = new SolidColorBrush(theme.CardBorder);
            res["BtnDefaultBrush"] = new SolidColorBrush(theme.BtnDefault);
            res["BtnHoverBrush"] = new SolidColorBrush(theme.BtnHover);
            res["AccentBrush"] = new SolidColorBrush(theme.Accent);

            // "Τετράγωνα" - μηδενική ακτίνα γωνίας παντού (κάρτες/κουμπιά/καρτέλες) ΜΟΝΟ σε αυτό το
            // skin· ορατό, συμπαγές περίγραμμα στα κουμπιά αντί για το σκόπιμα αόρατο (0px) που έχουν
            // όλα τα άλλα, μοντέρνα θέματα - απλοποιημένη προσέγγιση του κλασικού ανάγλυφου (3D bevel)
            // στυλ των Windows, ΧΩΡΙΣ πλήρη αναδημιουργία ξεχωριστού ControlTemplate ανά στοιχείο.
            var isClassic = IsWindowsClassicSkin;
            res["AppCornerRadius"] = new CornerRadius(isClassic ? 0 : 8);
            res["AppButtonBorderThickness"] = new Thickness(isClassic ? 1 : 0);
            res["AppButtonBorderBrush"] = new SolidColorBrush(theme.CardBorder);
        }
    }
}
