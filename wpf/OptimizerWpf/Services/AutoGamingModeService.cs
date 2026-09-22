using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - roadmap "Αυτόματο Gaming Mode" - ανίχνευση παιχνιδιού σε εξέλιξη (heuristic: το ενεργό
    // παράθυρο καλύπτει ΑΚΡΙΒΩΣ όλη την οθόνη - exclusive fullscreen, ο πιο αξιόπιστος δείκτης χωρίς να
    // χρειάζεται λίστα γνωστών παιχνιδιών/launchers), προσωρινή εφαρμογή Gaming Mode, αυτόματη
    // επαναφορά μόλις το fullscreen παράθυρο δεν είναι πια το ενεργό. Ζει ως static service (ΟΧΙ μέσα
    // στο OptimizationView) ώστε να συνεχίζει να δουλεύει ανεξάρτητα από το ποια καρτέλα είναι ανοιχτή -
    // ίδιο σκεπτικό με το γιατί το ThemedBackgroundControl ζει στο MainWindow, όχι σε κάθε tab view.
    // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: ο χειροκίνητος διακόπτης Gaming Mode στη Βελτιστοποίηση ΔΕΝ συγχρονίζεται
    // οπτικά με αυτή την αυτόματη ενεργοποίηση (θα απαιτούσε το static service να κρατά αναφορά στο
    // εκάστοτε ζωντανό OptimizationView instance) - αν χρειαστεί αργότερα, προστίθεται εύκολα.
    public static class AutoGamingModeService
    {
        private static DispatcherTimer? _timer;
        private static bool _isActive;
        private static IntPtr _lastFullscreenHwnd;
        private static int _consecutiveFullscreenTicks;
        private static int _consecutiveNonFullscreenTicks;

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        private struct RECT { public int Left, Top, Right, Bottom; }
        private const int SM_CXSCREEN = 0, SM_CYSCREEN = 1;

        public static void Start()
        {
            if (_timer != null) return;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timer.Tick += (_, _) => Tick();
            _timer.Start();
        }

        public static void Stop()
        {
            _timer?.Stop();
            _timer = null;
            if (_isActive) { try { PowerModeService.DisableGamingMode(); } catch { } _isActive = false; }
        }

        private static void Tick()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero || !IsFullscreen(hwnd) || IsSelfOrShell(hwnd))
                {
                    _consecutiveFullscreenTicks = 0;
                    _consecutiveNonFullscreenTicks++;
                    // 2 συνεχόμενοι έλεγχοι (10s) χωρίς fullscreen πριν την επαναφορά - αποφεύγει
                    // τρεμόπαιγμα σε σύντομη εναλλαγή παραθύρου (π.χ. Alt+Tab στιγμιαία).
                    if (_isActive && _consecutiveNonFullscreenTicks >= 2)
                    {
                        PowerModeService.DisableGamingMode();
                        _isActive = false;
                    }
                    return;
                }

                _consecutiveNonFullscreenTicks = 0;
                _consecutiveFullscreenTicks = hwnd == _lastFullscreenHwnd ? _consecutiveFullscreenTicks + 1 : 1;
                _lastFullscreenHwnd = hwnd;

                // 2 συνεχόμενοι έλεγχοι (10s) στο ΙΔΙΟ fullscreen παράθυρο πριν την ενεργοποίηση -
                // αποφεύγει ψευδές θετικό από σύντομη fullscreen προβολή (π.χ. video player splash).
                if (!_isActive && _consecutiveFullscreenTicks >= 2)
                {
                    PowerModeService.SetGamingMode();
                    _isActive = true;
                }
            }
            catch { }
        }

        private static bool IsFullscreen(IntPtr hwnd)
        {
            if (!GetWindowRect(hwnd, out var r)) return false;
            var w = r.Right - r.Left; var h = r.Bottom - r.Top;
            return w == GetSystemMetrics(SM_CXSCREEN) && h == GetSystemMetrics(SM_CYSCREEN);
        }

        private static bool IsSelfOrShell(IntPtr hwnd)
        {
            try
            {
                GetWindowThreadProcessId(hwnd, out var pid);
                var name = Process.GetProcessById(pid).ProcessName;
                // ΔΙΟΡΘΩΣΗ - ρητό αίτημα χρήστη: "το όνομα του exe να προσαρμοστεί στο όνομα της
                // εφαρμογής" - το AssemblyName άλλαξε (βλ. OptimizerWpf.csproj), οπότε το πραγματικό
                // Process.ProcessName της εγκατεστημένης εφαρμογής είναι πλέον διαφορετικό -
                // "OptimizerWpf" εδώ θα ΠΑΨΕΙ να ταιριάζει με το πραγματικό όνομα διεργασίας.
                return name is "explorer" or "dwm" or "GearWin" or "dotnet";
            }
            catch { return true; }
        }
    }
}
