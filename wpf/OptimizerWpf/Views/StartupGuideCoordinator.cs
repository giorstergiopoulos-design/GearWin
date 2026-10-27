using System;
using System.Windows;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    // Ροή οδηγού καλωσορίσματος → πρόταση «οικογένειας» (μία φορά). Κοινή για την εκκίνηση, τη Βοήθεια, το tray και το κλασικό μενού.
    public static class StartupGuideCoordinator
    {
        private static WelcomeGuideWindow? _guide;
        private static SuiteShowcaseWindow? _suite;

        private static ReleaseVersion Current => ReleaseVersion.ParseOrNull(App.DisplayVersion) ?? ReleaseVersion.FromVersion(SelfUpdateService.Current);

        // Μετά την κουρτίνα εκκίνησης: οδηγός μία φορά ανά νέα major.minor έκδοση. `onboardingShown`: στην πρώτη εγκατάσταση ο παλιός
        // αναλυτικός οδηγός μόλις εμφανίστηκε - δεν στοιβάζουμε δεύτερον, απλώς καταγράφεται η έκδοση.
        public static void RunAtStartup(MainWindow main, bool onboardingShown)
        {
            var store = new OncePerVersionStore(OncePerVersionStore.DefaultPath());
            if (onboardingShown) { store.MarkSeen(WelcomeGuideWindow.StoreKey, Current); return; }
            if (!store.ShouldShow(WelcomeGuideWindow.StoreKey, Current)) return;
            ShowWelcome(main, store, offerFamilyAfter: true);
        }

        public static void ShowWelcome(MainWindow main, OncePerVersionStore? store = null, bool offerFamilyAfter = false)
        {
            if (_guide != null || _suite != null || !main.IsVisible) return;
            store ??= new OncePerVersionStore(OncePerVersionStore.DefaultPath());
            var g = new WelcomeGuideWindow(main, store);
            _guide = g;
            g.Closed += (_, _) =>
            {
                _guide = null;
                var offered = store.Get(SuiteShowcaseWindow.OfferedKey) != null;
                if (g.OpenFamilyAfterClose || (offerFamilyAfter && !offered))
                {
                    store.MarkSeen(SuiteShowcaseWindow.OfferedKey, Current);
                    main.Dispatcher.BeginInvoke(new Action(() => ShowSuite(main)));
                }
            };
            g.Show();
        }

        public static void ShowSuite(MainWindow main)
        {
            if (_suite != null || _guide != null || !main.IsVisible) return;
            var s = new SuiteShowcaseWindow(main);
            _suite = s;
            s.Closed += (_, _) => _suite = null;
            s.Show();
        }

        // Από παράθυρα-διαλόγους (Βοήθεια): κλείνει πρώτα τον διάλογο και μετά ανοίγει την κουρτίνα.
        public static void ShowWelcomeFrom(Window dialog)
        {
            var main = Application.Current?.MainWindow as MainWindow;
            dialog.Close();
            if (main != null) main.Dispatcher.BeginInvoke(new Action(() => ShowWelcome(main)));
        }

        public static void ShowSuiteFrom(Window dialog)
        {
            var main = Application.Current?.MainWindow as MainWindow;
            dialog.Close();
            if (main != null) main.Dispatcher.BeginInvoke(new Action(() => ShowSuite(main)));
        }
    }
}
