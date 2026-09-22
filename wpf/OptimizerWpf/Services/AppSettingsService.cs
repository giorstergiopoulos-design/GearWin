using System;
using System.IO;
using System.Text.Json;

namespace OptimizerWpf.Services
{
    public class AppSettings
    {
        public bool SidebarEnabled { get; set; }
        public string SidebarPosition { get; set; } = "Right";
        public string ThemeName { get; set; } = "Windows 11 Fluent";
        public bool IsDarkMode { get; set; } = true;
        public bool AutoInstallDrivers { get; set; }
        // "Classic" | "Sidebar" | "HorizontalModern" - port του $global:appSettings.MenuMode
        // (Optimizer.ps1 ~792, προεπιλογή "HorizontalModern" εκεί επίσης).
        public string MenuMode { get; set; } = "HorizontalModern";
        public bool AnimatedBackgrounds { get; set; } = true;
        // "Theme" (κάθε θέμα το δικό του στυλ, βλ. ThemeBackgroundStyles) | "Gears" | "Orbit" |
        // "Particles" | "Waves" | "GridPulse" - καθολική αντικατάσταση του φόντου ΟΛΩΝ των θεμάτων.
        public string AnimationStyleOverride { get; set; } = "Theme";
        // "None" | "Light" | "Medium" - διαφάνεια κύριου παραθύρου.
        public string WindowOpacityMode { get; set; } = "None";
        // "el" | "en" | "de" | "fr" - βλ. Services/LanguageService.cs.
        public string Language { get; set; } = "el";
        // Θυμάται ποιο "πραγματικό" θέμα ίσχυε πριν επιλεγεί το skin "Microsoft PC Manager", ώστε η
        // επιστροφή σε "Classic" στο Skins tab να επαναφέρει ΤΟ ΙΔΙΟ θέμα αντί για μια σταθερή προεπιλογή.
        public string LastNonPcManagerTheme { get; set; } = "Windows 11 Fluent";
        // ΝΕΟ - ρητό αίτημα χρήστη: "κουρτίνα με tutorial στο πρώτο άνοιγμα της εφαρμογής". False στην
        // πρώτη εκκίνηση (δεν υπάρχει ακόμα αρχείο ρυθμίσεων) - το App.xaml.cs δείχνει το OnboardingWindow
        // μόνο όσο αυτό παραμένει false, γίνεται true μόλις ο χρήστης πατήσει "Ξεκινήστε"/Παράλειψη/κλείσιμο.
        public bool HasSeenOnboarding { get; set; }

        // ΝΕΟ - roadmap "Προγραμματισμένη συντήρηση" - βλ. AdvancedToolsService.SetScheduledMaintenanceAsync.
        public bool ScheduledMaintenanceEnabled { get; set; }

        // ΝΕΟ - roadmap "Αυτόματο Gaming Mode" - βλ. AutoGamingModeService.
        public bool AutoGamingModeEnabled { get; set; }

        // ΝΕΟ - roadmap "Καρφιτσωμένες συντομεύσεις" - κλειδιά μορφής "<λίστα>:<Label>" (βλ.
        // TweakRowVm.PinKey στο TweaksView.xaml.cs) για tweaks καρφιτσωμένα στην Αρχική. ΣΗΜΕΙΩΣΗ
        // ΕΙΛΙΚΡΙΝΕΙΑΣ: το Label είναι μεταφρασμένο κείμενο (LanguageService.T) - μια καρφίτσα δεν θα
        // ταιριάξει αν ο χρήστης αλλάξει γλώσσα εφαρμογής μετά το καρφίτσωμα (καμία σταθερή, γλωσσο-
        // ανεξάρτητη ταυτότητα υπάρχει ακόμα ανά tweak). Αποδεκτός συμβιβασμός για ένα "Μεσαίο" χαρακτηριστικό.
        public System.Collections.Generic.List<string> PinnedTweakKeys { get; set; } = new();

        // ΝΕΟ - roadmap "Προγραμματισμένος (background) Πλήρης Έλεγχος Υγείας" - απλούστερη, ασφαλέστερη
        // εκδοχή από πλήρες Windows Task Scheduler background scan: η Αρχική δείχνει μια ήπια υπενθύμιση
        // αν δεν έχει τρέξει Πλήρης Έλεγχος Υγείας εδώ και 7+ μέρες (ή ποτέ) - βλ. HomeView.xaml.cs's
        // CheckHealthCheckReminder. Καθαρά ενημερωτικό banner, ΚΑΜΙΑ αυτόματη σάρωση/ενέργεια στο
        // παρασκήνιο χωρίς να το ζητήσει ρητά ο χρήστης πατώντας το ίδιο το κουμπί.
        public DateTime? LastHealthCheckRunAt { get; set; }

        // ΝΕΟ - roadmap "Widget επιφάνειας εργασίας" - σε αντίθεση με το ήδη υπάρχον tray popup
        // (ανοίγει μόνο σε κλικ, κλείνει αυτόματα στο Deactivated), αυτό παραμένει ΠΑΝΤΑ ορατό στην
        // επιφάνεια εργασίας όσο είναι ενεργοποιημένο - βλ. Services/DesktopWidgetService.cs,
        // Views/DesktopWidgetWindow.xaml(.cs). Θέση null σημαίνει "δεν έχει μετακινηθεί ακόμα από τον
        // χρήστη" (προεπιλογή κάτω-δεξιά, ίδιο μοτίβο με το tray popup).
        public bool DesktopWidgetEnabled { get; set; }
        public double? DesktopWidgetX { get; set; }
        public double? DesktopWidgetY { get; set; }
    }

    // Port του $global:appSettings / Save-AppSettings του Optimizer.ps1 - JSON persisted ρυθμίσεις
    // που έπρεπε να επιβιώνουν επανεκκινήσεις (θέμα, πλευρικό μενού) αλλά δεν είχαν ακόμα δικό τους
    // αποθηκευτικό μηχανισμό στο WPF port - το θέμα π.χ. επανερχόταν πάντα σε Windows 11 Fluent/Dark
    // σε κάθε εκκίνηση.
    public static class AppSettingsService
    {
        private static readonly string StorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf", "AppSettings.json");

        private static AppSettings? _cache;

        // Οι 14 υποστηριζόμενοι κωδικοί γλώσσας (βλ. LanguageService.AllTranslations) - hardcoded εδώ
        // αντί για αναφορά στο LanguageService ώστε να αποφευχθεί κυκλική εξάρτηση μεταξύ των δύο
        // static classes.
        private static readonly string[] SupportedLanguages = { "el", "en", "de", "fr", "es", "it", "ru", "zh", "ja", "pt", "ko", "tr", "ar", "hi" };

        // ΝΕΟ - βελτίωση: στην ΠΡΩΤΗ πραγματική εκκίνηση (δεν υπάρχει ακόμα αρχείο ρυθμίσεων), αντί να
        // ξεκινάει πάντα σε Ελληνικά, ανιχνεύει τη γλώσσα διεπαφής χρήστη των Windows και τη χρησιμοποιεί
        // αν υποστηρίζεται - πιο φιλικό για μη-ελληνόφωνους χρήστες που δεν θα ήξεραν αμέσως πού είναι
        // το κουμπί γλώσσας. Εφαρμόζεται ΜΟΝΟ όταν δεν υπάρχει καθόλου αρχείο (πραγματική πρώτη φορά) -
        // ένα αρχείο που υπάρχει αλλά απέτυχε να αποσειριοποιηθεί (πχ κατεστραμμένο) παραμένει στο
        // ασφαλές προεπιλεγμένο "el" αντί να μαντεύει.
        private static string DetectSystemLanguage()
        {
            var code = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            return Array.IndexOf(SupportedLanguages, code) >= 0 ? code : "el";
        }

        public static AppSettings Current
        {
            get
            {
                if (_cache != null) return _cache;
                var fileExists = File.Exists(StorePath);
                try
                {
                    if (fileExists)
                    {
                        _cache = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(StorePath));
                    }
                }
                catch { }
                if (_cache == null)
                {
                    _cache = new AppSettings();
                    if (!fileExists) _cache.Language = DetectSystemLanguage();
                }
                return _cache;
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
                File.WriteAllText(StorePath, JsonSerializer.Serialize(Current));
            }
            catch { }
        }
    }
}
