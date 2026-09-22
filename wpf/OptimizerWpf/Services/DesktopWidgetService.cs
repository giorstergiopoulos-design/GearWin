namespace OptimizerWpf.Services
{
    // ΝΕΟ - roadmap "Widget επιφάνειας εργασίας" - διαχειρίζεται το ΜΟΝΑΔΙΚΟ instance του
    // Views.DesktopWidgetWindow (ίδιο μοτίβο singleton-window με το TrayIconService's TrayPopupWindow,
    // απλά εδώ το παράθυρο παραμένει ανοιχτό αντί να κλείνει αυτόματα). Καλείται από το App.xaml.cs
    // στην εκκίνηση (αν το AppSettings.DesktopWidgetEnabled είναι true) και από το checkbox στο
    // AppearanceSettingsWindow όποτε ο χρήστης το ενεργοποιεί/απενεργοποιεί ζωντανά.
    public static class DesktopWidgetService
    {
        private static Views.DesktopWidgetWindow? _window;

        public static bool IsRunning => _window != null;

        public static void Start()
        {
            if (_window != null) return;
            _window = new Views.DesktopWidgetWindow();
            _window.Closed += (_, _) => _window = null;
            _window.Show();
        }

        // persistDisabled=true όταν ο χρήστης κλείνει το widget ΑΠΕΥΘΕΙΑΣ από το δικό του κουμπί ✕
        // (πρέπει να θυμάται "μη το ξανανοίξεις στην επόμενη εκκίνηση" - βλ. AppSettings.
        // DesktopWidgetEnabled). false όταν σταματά απλώς επειδή κλείνει ολόκληρη η εφαρμογή
        // (Exit handler στο App.xaml.cs) - καμία ανάγκη να αγγίξει τη ρύθμιση εκεί.
        public static void Stop(bool persistDisabled = false)
        {
            _window?.Close();
            _window = null;
            if (persistDisabled)
            {
                AppSettingsService.Current.DesktopWidgetEnabled = false;
                AppSettingsService.Save();
            }
        }
    }
}
