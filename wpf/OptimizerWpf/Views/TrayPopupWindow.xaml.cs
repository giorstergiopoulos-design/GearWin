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

        public TrayPopupWindow()
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _timer.Tick += (_, _) => Refresh();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 12;
            Top = area.Bottom - Height - 12;

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

            var (connected, latency) = await NetworkService.CheckInternetAsync();
            TxtNetwork.Text = connected
                ? $"{LanguageService.T("Tray_Connected")}{(latency.HasValue ? $" ({latency} ms)" : "")}"
                : LanguageService.T("Tray_Disconnected");
        }

        private void Window_Deactivated(object sender, EventArgs e) => Close();

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
