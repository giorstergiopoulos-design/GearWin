using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    // Βλ. σχόλιο στο OnboardingWindow.xaml. Ίδιο παράθυρο χρησιμοποιείται ΚΑΙ στο πρώτο άνοιγμα
    // (App.xaml.cs, πριν το MainWindow) ΚΑΙ όποτε ο χρήστης το ξανανοίγει από τη Βοήθεια (ρητό αίτημα
    // χρήστη: "από τις ρυθμίσεις αν θέλει στη βοήθεια να το ξαναδεί") - καμία διαφορά συμπεριφοράς
    // μεταξύ των δύο περιπτώσεων, το HasSeenOnboarding γίνεται πάντα true στο κλείσιμο (idempotent αν
    // ήταν ήδη true).
    public partial class OnboardingWindow : Window
    {
        private readonly List<(GlyphKind Icon, string Title, string Desc)> _slides;
        private int _index;
        private readonly List<Ellipse> _dots = new();

        public OnboardingWindow()
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);

            // ΔΙΟΡΘΩΣΗ (roadmap "Οπτικά": "custom εικονίδια στον οδηγό χρήσης" / "emoji εμφανίζονται ως
            // περιγράμματα") - 2 προσπάθειες μέσω γραμματοσειράς (FontFamily, variation selector
            // U+FE0F) επιβεβαιώθηκαν ζωντανά ΑΝΕΠΑΡΚΕΙΣ - αντικαταστάθηκαν με GlyphKind values του
            // ίδιου διανυσματικού HeaderGlyphIcon που χρησιμοποιείται σε όλη την εφαρμογή πλέον.
            _slides = new List<(GlyphKind, string, string)>
            {
                (GlyphKind.Gear, LanguageService.T("Onb_WelcomeTitle"), LanguageService.T("Onb_WelcomeDesc")),
                (GlyphKind.Home, LanguageService.T("TabHome"), LanguageService.T("Onb_HomeDesc")),
                (GlyphKind.Bolt, LanguageService.T("TabOptimization"), LanguageService.T("Onb_OptDesc")),
                (GlyphKind.Heart, LanguageService.T("TabHealth"), LanguageService.T("Onb_HealthDesc")),
                (GlyphKind.Globe, LanguageService.T("TabNetwork"), LanguageService.T("Onb_NetworkDesc")),
                (GlyphKind.Wrench, LanguageService.T("TabTweaks"), LanguageService.T("Onb_TweaksDesc")),
                (GlyphKind.Broom, LanguageService.T("TabBloatware"), LanguageService.T("Onb_BloatwareDesc")),
                (GlyphKind.Tools, LanguageService.T("TabAdvanced"), LanguageService.T("Onb_AdvancedDesc")),
                (GlyphKind.Monitor, LanguageService.T("TabSystem"), LanguageService.T("Onb_SystemDesc")),
                (GlyphKind.Check, LanguageService.T("Onb_FinishTitle"), LanguageService.T("Onb_FinishDesc")),
            };

            for (var i = 0; i < _slides.Count; i++)
            {
                var dot = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(4, 0, 4, 0) };
                _dots.Add(dot);
                DotsPanel.Children.Add(dot);
            }

            UpdateSlide();
        }

        private void UpdateSlide()
        {
            var (icon, title, desc) = _slides[_index];
            IconSlide.Kind = icon;
            TxtTitle.Text = title;
            TxtDesc.Text = desc;

            for (var i = 0; i < _dots.Count; i++)
                _dots[i].Fill = i == _index
                    ? (Brush)FindResource("AccentBrush")
                    : (Brush)FindResource("BtnDefaultBrush");

            BtnBack.Visibility = _index == 0 ? Visibility.Hidden : Visibility.Visible;
            var isLast = _index == _slides.Count - 1;
            BtnNext.Visibility = isLast ? Visibility.Collapsed : Visibility.Visible;
            BtnConfirm.Visibility = isLast ? Visibility.Visible : Visibility.Collapsed;

            // ΝΕΟ - roadmap "Βήμα ρύθμισης στον οδηγό" - ορατό μόνο στην τελευταία διαφάνεια.
            SetupStepPanel.Visibility = isLast ? Visibility.Visible : Visibility.Collapsed;
            if (isLast) UpdateThemeButtonText();
        }

        private void UpdateThemeButtonText() =>
            BtnOnbTheme.Content = ThemeManager.IsDarkMode ? LanguageService.T("Onb_SwitchToLight") : LanguageService.T("Onb_SwitchToDark");

        // ΔΙΟΡΘΩΣΗ - η καρτέλα Γλώσσα ενοποιήθηκε μέσα στην καρτέλα Μενού (index 1, βλ.
        // AppearanceSettingsWindow.xaml) - ο πρώην ξεχωριστός index 3 δεν υπάρχει πια.
        private void BtnOnbLanguage_Click(object sender, RoutedEventArgs e) =>
            new AppearanceSettingsWindow(initialTabIndex: 1) { Owner = this }.ShowDialog();

        private void BtnOnbTheme_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ToggleLightDark();
            UpdateThemeButtonText();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_index > 0) { _index--; UpdateSlide(); }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_index < _slides.Count - 1) { _index++; UpdateSlide(); }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Right) BtnNext_Click(sender, e);
            else if (e.Key == Key.Left) BtnBack_Click(sender, e);
            else if (e.Key == Key.Escape) Finish();
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e) => Finish();
        private void BtnConfirm_Click(object sender, RoutedEventArgs e) => Finish();

        private void Finish()
        {
            AppSettingsService.Current.HasSeenOnboarding = true;
            AppSettingsService.Save();
            Close();
        }
    }
}
