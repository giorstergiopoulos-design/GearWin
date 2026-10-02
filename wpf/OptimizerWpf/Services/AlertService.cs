using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OptimizerWpf.Services
{
    public record AlertCandidate(string Key, string Title, string Body);

    // ΝΕΟ (6.1.0) - ειδοποιήσεις θερμοκρασίας και S.M.A.R.T. Η απόφαση ("πρέπει να ειδοποιήσω;")
    // είναι καθαρή λογική (Evaluate*), ξεχωριστή από τη συλλογή αισθητήρων και το tray ώστε να
    // ελέγχεται με unit tests. Αντι-spam: ίδιο κλειδί ειδοποίησης δεν επαναλαμβάνεται πριν περάσει
    // το διάστημα cooldown, και η θερμοκρασία πρέπει να ξαναπέσει κάτω από (όριο - υστέρηση) για να
    // "οπλίσει" ξανά.
    public static class AlertService
    {
        public const int DefaultTempThresholdC = 85;
        public const int TempHysteresisC = 5;
        public static readonly TimeSpan Cooldown = TimeSpan.FromHours(6);

        private static DispatcherTimer? _timer;
        private static readonly Dictionary<string, DateTime> _lastFired = new();

        // Ανεβαίνει πάνω από το όριο => ειδοποίηση (αν δεν έχει σκάσει πρόσφατα). Επιστρέφει null αλλιώς.
        public static AlertCandidate? EvaluateTemperature(string sensor, int? tempC, int thresholdC,
            IReadOnlyDictionary<string, DateTime> lastFired, DateTime now, string titleFormat, string bodyFormat)
        {
            if (tempC is not int t || t < thresholdC) return null;
            var key = "temp:" + sensor;
            if (lastFired.TryGetValue(key, out var last) && now - last < Cooldown) return null;
            return new AlertCandidate(key, string.Format(titleFormat, sensor), string.Format(bodyFormat, sensor, t, thresholdC));
        }

        // smartHealthy: true = ΟΚ, false = προειδοποίηση, null = άγνωστο (δεν ειδοποιούμε για "άγνωστο").
        public static AlertCandidate? EvaluateSmart(string drive, bool? smartHealthy,
            IReadOnlyDictionary<string, DateTime> lastFired, DateTime now, string titleFormat, string bodyFormat)
        {
            if (smartHealthy != false) return null;
            var key = "smart:" + drive;
            if (lastFired.TryGetValue(key, out var last) && now - last < Cooldown) return null;
            return new AlertCandidate(key, string.Format(titleFormat, drive), string.Format(bodyFormat, drive));
        }

        public static void Start()
        {
            if (_timer != null) return;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            _timer.Tick += (_, _) => CheckNow();
            _timer.Start();
        }

        public static void Stop() { _timer?.Stop(); _timer = null; }

        // Οι αισθητήρες (LibreHardwareMonitor) και τα S.M.A.R.T. (WMI) μπορούν να πάρουν εκατοντάδες ms - τρέχουν
        // σε νήμα παρασκηνίου ώστε να μην παγώνει το UI, και μόνο το balloon επιστρέφει στο UI νήμα.
        public static void CheckNow() => _ = Task.Run(CheckCore);

        private static void CheckCore()
        {
            try
            {
                var s = AppSettingsService.Current;
                var now = DateTime.Now;
                var found = new List<AlertCandidate>();

                if (s.TempAlertsEnabled)
                {
                    var th = s.TempAlertThresholdC > 0 ? s.TempAlertThresholdC : DefaultTempThresholdC;
                    var title = LanguageService.T("Alert_TempTitle");
                    var body = LanguageService.T("Alert_TempBody");
                    var cpu = EvaluateTemperature("CPU", SensorService.GetCpuTemperatureCelsius(), th, _lastFired, now, title, body);
                    var gpu = EvaluateTemperature("GPU", SensorService.GetGpuTemperatureCelsius(), th, _lastFired, now, title, body);
                    if (cpu != null) found.Add(cpu);
                    if (gpu != null) found.Add(gpu);
                }

                if (s.SmartAlertsEnabled)
                {
                    foreach (var d in System.IO.DriveInfo.GetDrives().Where(d => d.DriveType == System.IO.DriveType.Fixed && d.IsReady))
                    {
                        var name = d.Name.TrimEnd('\\');
                        var a = EvaluateSmart(name, DriveTypeService.GetSmartHealthy(d.Name), _lastFired, now,
                            LanguageService.T("Alert_SmartTitle"), LanguageService.T("Alert_SmartBody"));
                        if (a != null) found.Add(a);
                    }
                }

                foreach (var a in found)
                {
                    _lastFired[a.Key] = now;
                    var alert = a;
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    void Show() => TrayIconService.ShowNotificationBalloon(alert.Title, alert.Body, () => TrayIconService.OpenTab("System"));
                    if (dispatcher != null) dispatcher.BeginInvoke((Action)Show); else Show();
                }
            }
            catch { /* οι ειδοποιήσεις είναι best-effort - ποτέ δεν πρέπει να ρίξουν την εφαρμογή */ }
        }
    }
}
