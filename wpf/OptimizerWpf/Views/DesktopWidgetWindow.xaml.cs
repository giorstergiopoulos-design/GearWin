using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class DesktopWidgetWindow : Window
    {
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter[]? _gpuCounters;
        private readonly DispatcherTimer _timer;
        private int _currentGamePid = -1;
        private bool _voltageQueryInFlight;

        public DesktopWidgetWindow()
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _timer.Tick += (_, _) => Refresh();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = AppSettingsService.Current;
            var area = SystemParameters.WorkArea;
            // ΝΕΟ - θέση null (πρώτη φορά) -> ίδια προεπιλογή κάτω-δεξιά με το tray popup. Αλλιώς
            // επαναφέρει την τελευταία θέση όπου το άφησε ο χρήστης (Window_Closing παρακάτω).
            Left = settings.DesktopWidgetX ?? (area.Right - Width - 12);
            Top = settings.DesktopWidgetY ?? (area.Bottom - Height - 12);

            try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); _cpuCounter.NextValue(); }
            catch { _cpuCounter = null; }
            _ = System.Threading.Tasks.Task.Run(() => { _gpuCounters = GpuInfoService.CreateUsageCounters(); });

            Refresh();
            _timer.Start();
        }

        private async void Refresh()
        {
            try { if (_cpuCounter != null) TxtCpu.Text = $"{_cpuCounter.NextValue():0}%"; }
            catch { }

            if (NativeMethods.GlobalMemoryStatusEx(out var mem) && mem.ullTotalPhys > 0)
                TxtRam.Text = $"{mem.dwMemoryLoad}%";

            var (connected, latency) = await NetworkService.CheckInternetAsync();
            TxtNetwork.Text = connected
                ? $"{LanguageService.T("Tray_Connected")}{(latency.HasValue ? $" ({latency} ms)" : "")}"
                : LanguageService.T("Tray_Disconnected");

            RefreshGameOverlay();
        }

        // ΝΕΟ - ρητό αίτημα χρήστη: "κάνε πάλι ως widget το FPS/in-game overlay που θα επεκτείνει το
        // Widget Επιφάνειας Εργασίας". Εντοπίζει fullscreen παιχνίδι (ίδιο heuristic με το
        // AutoGamingModeService, βλ. GameOverlayService.GetForegroundFullscreenProcess) - όταν
        // εντοπιστεί, εμφανίζει GPU%/FPS και ξεκινά/σταματά τη μέτρηση DXGI Present events για το
        // συγκεκριμένο PID (ξεκινά ξανά μόνο όταν αλλάξει το PID, όχι σε κάθε tick).
        private void RefreshGameOverlay()
        {
            var game = GameOverlayService.GetForegroundFullscreenProcess();
            if (game == null)
            {
                if (_currentGamePid != -1) { GameOverlayService.Stop(); _currentGamePid = -1; }
                RowGpu.Visibility = Visibility.Collapsed;
                RowGpuVoltage.Visibility = Visibility.Collapsed;
                RowFps.Visibility = Visibility.Collapsed;
                TxtGameName.Visibility = Visibility.Collapsed;
                return;
            }

            RowGpu.Visibility = Visibility.Visible;
            RowGpuVoltage.Visibility = Visibility.Visible;
            RowFps.Visibility = Visibility.Visible;
            TxtGameName.Visibility = Visibility.Visible;
            TxtGameName.Text = $"{LanguageService.T("Widget_MonitoringPrefix")}{game.Value.Name}";

            if (_gpuCounters != null) TxtGpu.Text = $"{GpuInfoService.SampleUsagePercent(_gpuCounters)}%";
            RefreshGpuVoltageAsync();

            if (_currentGamePid != game.Value.Pid)
            {
                _currentGamePid = game.Value.Pid;
                GameOverlayService.Start(game.Value.Pid);
            }
            var fps = GameOverlayService.SampleFps();
            TxtFps.Text = fps?.ToString() ?? "--";
        }

        // ΝΕΟ - ρητό αίτημα χρήστη: "πρόσθεσε και το voltage της GPU" - SensorService.GetGpuVoltageVolts
        // κάνει Update() σε LibreHardwareMonitorLib hardware objects, ίδιο σκεπτικό "όχι στο UI thread"
        // με το HomeView's RefreshGpuTemperatureAsync (βλ. HomeView.xaml.cs) - Task.Run εδώ ομοίως.
        // _voltageQueryInFlight αποτρέπει επικαλυπτόμενα queries αν ο tick (1.5s) είναι πιο γρήγορος από
        // την επιστροφή του προηγούμενου query.
        private async void RefreshGpuVoltageAsync()
        {
            if (_voltageQueryInFlight) return;
            _voltageQueryInFlight = true;
            try
            {
                var volts = await System.Threading.Tasks.Task.Run(SensorService.GetGpuVoltageVolts);
                TxtGpuVoltage.Text = volts.HasValue ? $"{volts.Value:0.000} V" : "--";
            }
            finally
            {
                _voltageQueryInFlight = false;
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => DesktopWidgetService.Stop(persistDisabled: true);

        // ΝΕΟ - θυμάται τη θέση όπου το άφησε ο χρήστης, ώστε να ξανανοίξει ΕΚΕΙ (όχι πάντα στην ίδια
        // προεπιλεγμένη γωνία) στην επόμενη εκκίνηση της εφαρμογής.
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            AppSettingsService.Current.DesktopWidgetX = Left;
            AppSettingsService.Current.DesktopWidgetY = Top;
            AppSettingsService.Save();
            _timer.Stop();
            _cpuCounter?.Dispose();
            if (_currentGamePid != -1) GameOverlayService.Stop();
        }
    }
}
