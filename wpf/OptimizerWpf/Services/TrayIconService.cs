using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using OptimizerWpf.Views;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - roadmap "Mini widget / system tray live view": εικονίδιο στην περιοχή ειδοποιήσεων
    // ενεργό όσο τρέχει η εφαρμογή (ανεξάρτητα αν το κύριο παράθυρο είναι ανοιχτό/ελαχιστοποιημένο) -
    // αριστερό κλικ δείχνει ένα μικρό, ζωντανό popup (CPU/RAM/Δίκτυο) ΧΩΡΙΣ να χρειάζεται άνοιγμα
    // ολόκληρου του κύριου παραθύρου, διπλό κλικ επαναφέρει το κύριο παράθυρο. Το
    // System.Windows.Forms.NotifyIcon παραμένει το μόνο διαθέσιμο API για tray icon σε WPF - καμία
    // καθαρή WPF εναλλακτική υπάρχει, βλ. σχόλιο στο .csproj's UseWindowsForms.
    public static class TrayIconService
    {
        private static NotifyIcon? _icon;
        private static TrayPopupWindow? _popup;
        private static Window? _mainWindow;
        private static ToolStripMenuItem? _widgetMenuItem;
        private static ToolStripMenuItem? _quickCleanMenuItem;
        private static ToolStripMenuItem? _openMainItem;
        private static ToolStripMenuItem? _healthCheckItem;
        private static ToolStripMenuItem? _clipboardHistoryItem;
        private static ToolStripMenuItem? _exitItem;

        // ΝΕΟ - ρητό αίτημα χρήστη: "εμπλούτισε το tray με συντομεύσεις σημαντικών λειτουργιών" - το
        // μενού περιείχε μόνο "Άνοιγμα/Έξοδος". Προστέθηκαν 3 πραγματικές συντομεύσεις που δεν
        // χρειάζονται άνοιγμα ολόκληρου του κύριου παραθύρου: Γρήγορος Καθαρισμός (τρέχει απευθείας τα
        // ΑΣΦΑΛΗ/recommended στοιχεία του ήδη υπάρχοντος QuickCleanService, με ένδειξη σε εξέλιξη +
        // balloon tip με το αποτέλεσμα), Πλήρης Έλεγχος Υγείας (ανοίγει απευθείας το
        // HealthCheckWindow, ίδιο παράθυρο με αυτό της Αρχικής), και εναλλαγή του Widget Επιφάνειας
        // Εργασίας (βλ. DesktopWidgetService) - checkable item, η κατάσταση ξαναδιαβάζεται σε κάθε
        // Opening του μενού ώστε να παραμένει σωστή ακόμα κι αν το widget κλείσει από το δικό του
        // κουμπί ✕ ή από τις Ρυθμίσεις Εμφάνισης.
        public static void Start(Window mainWindow)
        {
            _mainWindow = mainWindow;
            var appIcon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule!.FileName!);

            var menu = new ContextMenuStrip();
            _openMainItem = new ToolStripMenuItem(LanguageService.T("Tray_OpenMain"), null, (_, _) => RestoreMainWindow());
            menu.Items.Add(_openMainItem);
            menu.Items.Add(new ToolStripSeparator());
            _quickCleanMenuItem = new ToolStripMenuItem(LanguageService.T("Tray_QuickClean"), null, async (_, _) => await RunQuickCleanAsync());
            menu.Items.Add(_quickCleanMenuItem);
            _healthCheckItem = new ToolStripMenuItem(LanguageService.T("HealthCheck_Title"), null, (_, _) => new HealthCheckWindow().Show());
            menu.Items.Add(_healthCheckItem);
            // ΝΕΟ - ρητό αίτημα χρήστη: "θα είναι χρηστικό εκεί ή να έχει δικό του ξεχωριστό μενού" -
            // και τα δύο: κύρια πρόσβαση μέσω global hotkey (Win+Shift+V, βλ. App.xaml.cs), ΚΑΙ εδώ ως
            // εναλλακτική για όποιον προτιμά το ποντίκι.
            _clipboardHistoryItem = new ToolStripMenuItem(LanguageService.T("Clipboard_Title"), null, (_, _) => mainWindow.Dispatcher.Invoke(() => new Views.ClipboardHistoryWindow().Show()));
            menu.Items.Add(_clipboardHistoryItem);
            _widgetMenuItem = new ToolStripMenuItem(LanguageService.T("Appr_DesktopWidget"), null, (_, _) => ToggleWidget());
            menu.Items.Add(_widgetMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            _exitItem = new ToolStripMenuItem(LanguageService.T("Tray_Exit"), null, (_, _) => System.Windows.Application.Current.Shutdown());
            menu.Items.Add(_exitItem);
            menu.Opening += (_, _) => _widgetMenuItem!.Checked = DesktopWidgetService.IsRunning;

            _icon = new NotifyIcon
            {
                Icon = appIcon,
                Text = "GearWin - Complete PC Care",
                Visible = true,
                ContextMenuStrip = menu,
            };
            _icon.MouseClick += (_, args) => { if (args.Button == MouseButtons.Left) TogglePopup(); };
            _icon.DoubleClick += (_, _) => RestoreMainWindow();

            // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "κάποια elements μένουν στην προηγούμενη γλώσσα μέχρι το
            // κλείσιμο και άνοιγμα ξανά") - το ContextMenuStrip είναι WinForms, εντελώς έξω από τον
            // μηχανισμό live-binding του WPF (TrExtension/LanguageService.Instance indexer) - το κείμενο
            // κάθε στοιχείου οριζόταν ΜΙΑ φορά εδώ και ποτέ δεν ξαναδιαβαζόταν. Τώρα εγγράφεται στο
            // LanguageService.Changed ώστε να ενημερώνεται ζωντανά, ίδιο πνεύμα με το SidebarNav.
            LanguageService.Changed += ApplyLanguage;
        }

        private static void ApplyLanguage()
        {
            if (_openMainItem != null) _openMainItem.Text = LanguageService.T("Tray_OpenMain");
            if (_quickCleanMenuItem != null) _quickCleanMenuItem.Text = LanguageService.T("Tray_QuickClean");
            if (_healthCheckItem != null) _healthCheckItem.Text = LanguageService.T("HealthCheck_Title");
            if (_clipboardHistoryItem != null) _clipboardHistoryItem.Text = LanguageService.T("Clipboard_Title");
            if (_widgetMenuItem != null) _widgetMenuItem.Text = LanguageService.T("Appr_DesktopWidget");
            if (_exitItem != null) _exitItem.Text = LanguageService.T("Tray_Exit");
        }

        private static void ToggleWidget()
        {
            if (DesktopWidgetService.IsRunning) { DesktopWidgetService.Stop(persistDisabled: true); return; }
            DesktopWidgetService.Start();
            AppSettingsService.Current.DesktopWidgetEnabled = true;
            AppSettingsService.Save();
        }

        private static async System.Threading.Tasks.Task RunQuickCleanAsync()
        {
            if (_quickCleanMenuItem == null || _icon == null) return;
            _quickCleanMenuItem.Enabled = false;
            _icon.Text = LanguageService.T("Tray_QuickCleanRunning");
            try
            {
                var items = await QuickCleanService.ScanAsync();
                var keys = items.Where(i => i.Recommended).Select(i => i.Key).ToList();
                var freed = await QuickCleanService.CleanAsync(keys);
                _icon.ShowBalloonTip(4000, "GearWin - Complete PC Care",
                    $"{LanguageService.T("Tray_QuickCleanDonePrefix")}{QuickCleanService.FormatSize(freed)}", ToolTipIcon.Info);
            }
            finally
            {
                _icon.Text = "GearWin - Complete PC Care";
                _quickCleanMenuItem.Enabled = true;
            }
        }

        private static void TogglePopup()
        {
            if (_popup is { IsVisible: true })
            {
                _popup.Close();
                _popup = null;
                return;
            }

            _popup = new TrayPopupWindow();
            _popup.Closed += (_, _) => _popup = null;
            _popup.Show();
            _popup.Activate();
        }

        private static void RestoreMainWindow()
        {
            if (_mainWindow == null) return;
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }

        public static void Stop()
        {
            LanguageService.Changed -= ApplyLanguage;
            _popup?.Close();
            if (_icon != null) { _icon.Visible = false; _icon.Dispose(); _icon = null; }
        }
    }
}
