using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    // Οδηγός καλωσορίσματος: 5 σελίδες, "Να μην ξαναεμφανιστεί" (ανά έκδοση major.minor), Παράλειψη/Esc, βέλη πληκτρολογίου.
    public partial class WelcomeGuideWindow : Window
    {
        public const string StoreKey = "welcome-guide";
        public const int PageCount = 5;

        private readonly MainWindow _main;
        private readonly OncePerVersionStore _store;
        private readonly List<Ellipse> _dots = new();
        private int _index;
        private bool _closing;

        // Ζητά από τον καλούντα να προτείνει τη σουίτα μετά το κλείσιμο (μία φορά)· ορίζεται από το StartupGuideCoordinator.
        public bool OpenFamilyAfterClose { get; private set; }

        public int PageIndex => _index;

        public WelcomeGuideWindow(MainWindow main, OncePerVersionStore? store = null)
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            _main = main;
            _store = store ?? new OncePerVersionStore(OncePerVersionStore.DefaultPath());
            TxtVersion.Text = App.DisplayVersion;
            for (var i = 0; i < PageCount; i++)
            {
                var dot = new Ellipse { Width = 9, Height = 9, Margin = new Thickness(5, 0, 5, 0) };
                _dots.Add(dot);
                DotsPanel.Children.Add(dot);
            }
            OverlayCardHost.Attach(this, main);
            ShowPage(0);
            Loaded += (_, _) =>
            {
                Focus();
                BtnNext.Focus();
                StartIconPulse();
            };
            // γλώσσα που αλλάζει όσο είναι ανοιχτός: ξαναγράφουμε τα κείμενα της τρέχουσας σελίδας
            LanguageService.Changed += OnLanguageChanged;
            Closed += (_, _) => LanguageService.Changed -= OnLanguageChanged;
        }

        private void OnLanguageChanged() => Dispatcher.BeginInvoke(new Action(() => ShowPage(_index)));

        private void StartIconPulse()
        {
            if (OverlayCardHost.SimpleAnimations) return;
            var pulse = new DoubleAnimation(1.0, 1.07, TimeSpan.FromMilliseconds(1600)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            IconScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            IconScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        }

        public void ShowPage(int index)
        {
            _index = Math.Clamp(index, 0, PageCount - 1);
            var n = _index + 1;
            TxtTitle.Text = LanguageService.T($"Guide_P{n}Title");
            TxtDesc.Text = LanguageService.T($"Guide_P{n}Desc");
            for (var i = 0; i < _dots.Count; i++)
                _dots[i].Fill = i == _index ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("BtnDefaultBrush");
            var last = _index == PageCount - 1;
            BtnBack.Visibility = _index == 0 ? Visibility.Hidden : Visibility.Visible;
            BtnNext.Visibility = last ? Visibility.Collapsed : Visibility.Visible;
            BtnFinish.Visibility = last ? Visibility.Visible : Visibility.Collapsed;
            BtnFamily.Visibility = last ? Visibility.Visible : Visibility.Collapsed;
            BtnSkip.Visibility = last ? Visibility.Collapsed : Visibility.Visible;
            if (last) BtnFinish.Focus();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) { if (_index > 0) ShowPage(_index - 1); }
        private void BtnNext_Click(object sender, RoutedEventArgs e) { if (_index < PageCount - 1) ShowPage(_index + 1); }
        private void BtnSkip_Click(object sender, RoutedEventArgs e) => Finish(false);
        private void BtnFinish_Click(object sender, RoutedEventArgs e) => Finish(false);
        private void BtnFamily_Click(object sender, RoutedEventArgs e) => Finish(true);
        private void Backdrop_Click(object sender, MouseButtonEventArgs e) { /* κλικ έξω από την κάρτα δεν κλείνει τον οδηγό (αποφυγή τυχαίου κλεισίματος) */ }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Right && FlowDirection == FlowDirection.LeftToRight || e.Key == Key.Left && FlowDirection == FlowDirection.RightToLeft) { BtnNext_Click(sender, e); e.Handled = true; }
            else if (e.Key == Key.Left && FlowDirection == FlowDirection.LeftToRight || e.Key == Key.Right && FlowDirection == FlowDirection.RightToLeft) { BtnBack_Click(sender, e); e.Handled = true; }
            else if (e.Key == Key.Escape) { Finish(false); e.Handled = true; }
        }

        // Καταγράφει «είδε αυτή την έκδοση» ανάλογα με το checkbox και κλείνει με το εφέ σμίκρυνσης στο εικονίδιο του κύριου παραθύρου.
        public void Finish(bool openFamily)
        {
            if (_closing) return;
            _closing = true;
            if (ChkDontShow.IsChecked == true) _store.MarkSeen(StoreKey, ReleaseVersion.ParseOrNull(App.DisplayVersion) ?? ReleaseVersion.FromVersion(SelfUpdateService.Current));
            OpenFamilyAfterClose = openFamily;
            OverlayCardHost.CloseIntoWindow(this, Card, Backdrop, OverlayCardHost.TitleIcon(_main));
        }
    }
}
