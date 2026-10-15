using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class TrayPopupWindow : Window
    {
        private PerformanceCounter? _cpuCounter;
        private readonly DispatcherTimer _timer;
        private bool _networkCheckInFlight;
        private DateTime _lastNetworkCheck = DateTime.MinValue;

        public TrayPopupWindow()
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _timer.Tick += (_, _) => Refresh();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // DPI-ασφαλής τοποθέτηση στην οθόνη του δρομέα (βλ. PopupPlacement) - το SystemParameters.WorkArea
            // έβγαζε λάθος θέση σε οθόνες με scaling > 100%.
            PopupPlacement.PlaceBottomRightOnCursorMonitor(this);

            try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); _cpuCounter.NextValue(); }
            catch { _cpuCounter = null; }

            Refresh();
            _timer.Start();
        }

        private async void Refresh()
        {
            try { if (_cpuCounter != null) TxtCpu.Text = $"{_cpuCounter.NextValue():0}%"; }
            catch { }

            if (NativeMethods.GlobalMemoryStatusEx(out var mem) && mem.ullTotalPhys > 0)
                TxtRam.Text = $"{mem.dwMemoryLoad}%";

            // Το ping τρέχει το πολύ ανά 10s και ΠΟΤΕ επικαλυπτόμενο — πριν γινόταν σε κάθε tick (1.5s) χωρίς φρένο, και
            // με αργό δίκτυο συσσωρεύονταν ταυτόχρονα pings που έγραφαν αποτελέσματα εκτός σειράς.
            if (_networkCheckInFlight || (DateTime.UtcNow - _lastNetworkCheck).TotalSeconds < 10) return;
            _networkCheckInFlight = true;
            bool connected; int? latency;
            try { (connected, latency) = await NetworkService.CheckInternetAsync(); }
            catch { return; }
            finally { _networkCheckInFlight = false; }
            _lastNetworkCheck = DateTime.UtcNow;
            if (!IsLoaded) return; // το παράθυρο έκλεισε όσο περιμέναμε
            TxtNetwork.Text = connected
                ? $"{LanguageService.T("Tray_Connected")}{(latency.HasValue ? $" ({latency} ms)" : "")}"
                : LanguageService.T("Tray_Disconnected");
        }

        // ΔΙΟΡΘΩΣΗ (βρέθηκε από το live click-through): το Deactivated μπορεί να έρθει ΕΝΩ το παράθυρο ήδη κλείνει (π.χ. Close από
        // κουμπί/Esc → απώλεια focus → δεύτερο Close). Το WPF τότε ρίχνει InvalidOperationException ("...while a Window is
        // closing") από μέσα στο WmActivate - μη χειρίσιμο από τον καθολικό handler και σε ορισμένα σενάρια τερμάτιζε τη διεργασία.
        private bool _closeRequested;
        private void CloseOnce()
        {
            if (_closeRequested) return;
            _closeRequested = true;
            try { Close(); } catch (System.InvalidOperationException) { /* ήδη κλείνει */ }
        }

        private void Window_Deactivated(object sender, EventArgs e) => CloseOnce();

        private void BtnOpenMain_Click(object sender, RoutedEventArgs e)
        {
            if (Owner != null) Owner.Show();
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
                Application.Current.MainWindow.WindowState = WindowState.Normal;
                Application.Current.MainWindow.Activate();
            }
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            _cpuCounter?.Dispose();
            base.OnClosed(e);
        }
    }
}
