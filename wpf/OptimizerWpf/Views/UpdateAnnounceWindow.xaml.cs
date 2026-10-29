using System;
using System.Windows;
using System.Windows.Input;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    // Μικρό popup κάτω δεξιά για νέα έκδοση του GearWin (ίδια κουμπιά με του Crawler). Δεν κλέβει το focus. «Εγκατάσταση»: Ρυθμίσεις › Ενημέρωση
    // GearWin (λήψη με υποχρεωτικό SHA-256 και ρητή επιβεβαίωση)· αν το release δεν έχει digest, ανοίγει η σελίδα του.
    public partial class UpdateAnnounceWindow : Window
    {
        private static UpdateAnnounceWindow? _open;
        private readonly ReleaseCheckOutcome _outcome;
        private readonly OncePerVersionStore _store;

        public const string SkippedKey = "gearwin.skipped";

        public UpdateAnnounceWindow(ReleaseCheckOutcome outcome, OncePerVersionStore store)
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            _outcome = outcome;
            _store = store;
            TxtTitle.Text = LanguageService.T("SelfUpdate_NotifyTitle") + " " + outcome.Latest;
            TxtBody.Text = string.Format(LanguageService.T("SelfUpdate_NotifyBodyPage"), outcome.Latest);
            Loaded += (_, _) =>
            {
                var wa = SystemParameters.WorkArea;
                Left = wa.Right - ActualWidth - 8;
                Top = wa.Bottom - ActualHeight - 8;
            };
        }

        public static void ShowFor(ReleaseCheckOutcome outcome, OncePerVersionStore store)
        {
            if (_open != null) return;
            _open = new UpdateAnnounceWindow(outcome, store);
            _open.Closed += (_, _) => _open = null;
            _open.Show();
        }

        private void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            Close();
            if (_outcome.CanInstallInApp) TrayIconService.OpenSettings();
            else ReleaseCheckService.OpenReleasePage(_outcome.PageUrl);
        }

        private void BtnLater_Click(object sender, RoutedEventArgs e) => Close();

        // «Παράλειψη έκδοσης»: δεν ξαναειδοποιεί για αυτή την έκδοση (ούτε για παλαιότερη).
        private void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            if (_outcome.Latest != null) _store.MarkSeen(SkippedKey, _outcome.Latest);
            Close();
        }

        private void BtnWhatsNew_Click(object sender, RoutedEventArgs e) => ReleaseCheckService.OpenReleasePage(_outcome.PageUrl);

        private void Window_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
    }
}
