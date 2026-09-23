using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Diagnostics.Tracing.Session;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - roadmap "FPS/in-game overlay ως επέκταση του Widget Επιφάνειας Εργασίας" (ρητό αίτημα
    // χρήστη). Ίδιο ETW μοτίβο με το NetworkTrafficService (TraceEventSession, MIT, ήδη dependency) -
    // εδώ ενεργοποιείται ο provider "Microsoft-Windows-DXGI" (system-wide, manifest-based, ΟΧΙ kernel
    // provider) και μετράται ο ρυθμός events "Present" ανά PID.
    //
    // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ (ίδιο πνεύμα με το NetworkTrafficService's kernel-logger note και το
    // FileShredderService's SSD wear-leveling note): αυτό ΔΕΝ είναι ένα πλήρες PresentMon-equivalent
    // (κανένα frame-pacing/latency ανάλυση, καμία γνώση για το ακριβές schema του κάθε DXGI event
    // version) - μετράει ΜΟΝΟ το πλήθος events που περιέχουν "Present" στο όνομά τους για το
    // συγκεκριμένο PID μέσα σε κάθε παράθυρο ενός δευτερολέπτου, μέσω των ΓΕΝΙΚΩΝ πεδίων TraceEvent
    // (ProcessID/EventName - διαθέσιμα για ΚΑΘΕ manifest-based provider, χωρίς να χρειάζεται
    // strongly-typed parser γι' αυτόν συγκεκριμένα). Καλή προσέγγιση για παιχνίδια DXGI (D3D11/D3D12,
    // η συντριπτική πλειοψηφία σήμερα)· παιχνίδια OpenGL/Vulkan ΔΕΝ περνούν από DXGI Present και δεν θα
    // μετρηθούν - εμφανίζεται ρητά "--" αντί για ψευδή τιμή όταν δεν έρχονται events.
    public static class GameOverlayService
    {
        private const string DxgiProviderName = "Microsoft-Windows-DXGI";
        private const string SessionName = "OptimizerWpf-DxgiFps";

        private static TraceEventSession? _session;
        private static Thread? _thread;
        private static int _targetPid;
        private static long _frameCount;
        private static DateTime _windowStart = DateTime.UtcNow;
        private static readonly object Lock = new();

        public static bool IsRunning { get; private set; }
        public static string? LastError { get; private set; }
        public static int? LastFps { get; private set; }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
        private struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        // ΔΙΟΡΘΩΣΗ (bug εντοπίστηκε - χρήστης ανέφερε: "εξακολουθεί να μην εμφανίζεται ο μετρητής των
        // FPS") - GetSystemMetrics(SM_CXSCREEN/SM_CYSCREEN) επιστρέφει ΠΑΝΤΑ την ανάλυση της ΚΥΡΙΑΣ
        // οθόνης, ΟΧΙ της οθόνης όπου βρίσκεται πραγματικά το παράθυρο. Σε setup πολλαπλών οθονών όπου
        // το παιχνίδι τρέχει fullscreen σε ΔΕΥΤΕΡΕΥΟΥΣΑ οθόνη με διαφορετική ανάλυση από την κύρια, η
        // σύγκριση απέτυχε ΠΑΝΤΑ (ψευδώς αρνητικό) - ο εντοπισμός fullscreen δεν πυροδοτούνταν ΠΟΤΕ σε
        // τέτοιο setup, όσο "σωστό" fullscreen κι αν ήταν το παιχνίδι. MonitorFromWindow+GetMonitorInfo
        // παίρνει τα όρια της ΣΥΓΚΕΚΡΙΜΕΝΗΣ οθόνης που περιέχει το παράθυρο - λειτουργεί σωστά σε
        // οποιαδήποτε οθόνη/ανάλυση.
        //
        // Ίδιο heuristic (exclusive/borderless fullscreen = παράθυρο == όρια οθόνης) με το
        // AutoGamingModeService - ΣΚΟΠΙΜΑ διπλότυπο μικρό P/Invoke block αντί για κοινόχρηστο service,
        // ώστε η αλλαγή εδώ να μη ρισκάρει το ήδη δουλεμένο/σταθερό AutoGamingModeService.
        public static (int Pid, string Name)? GetForegroundFullscreenProcess()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var r)) return null;
                var w = r.Right - r.Left; var h = r.Bottom - r.Top;

                var hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (!GetMonitorInfo(hMonitor, ref mi)) return null;
                var monW = mi.rcMonitor.Right - mi.rcMonitor.Left;
                var monH = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
                if (w != monW || h != monH) return null;

                GetWindowThreadProcessId(hwnd, out var pid);
                var proc = System.Diagnostics.Process.GetProcessById(pid);
                if (proc.ProcessName is "explorer" or "dwm" or "GearWin" or "dotnet") return null;
                return (pid, proc.ProcessName);
            }
            catch { return null; }
        }

        public static bool Start(int targetPid)
        {
            lock (Lock)
            {
                if (IsRunning && _targetPid == targetPid) return true;
                if (IsRunning) StopInternal();

                _targetPid = targetPid;
                _frameCount = 0;
                _windowStart = DateTime.UtcNow;
                LastFps = null;

                try
                {
                    // ΔΙΟΡΘΩΣΗ (bug εντοπίστηκε - χρήστης ανέφερε: "δεν υπάρχει η πληροφορία για τα
                    // FPS") - αν η εφαρμογή είχε τερματιστεί προηγουμένως ενώ αυτό το session ήταν
                    // ενεργό (crash/Task kill, χωρίς να περάσει από Stop()/Dispose()), το named
                    // real-time ETW session παραμένει "ορφανό" σε επίπεδο ΛΣ ΑΚΟΜΑ και μετά το κλείσιμο
                    // της εφαρμογής - τα named ETW sessions ΔΕΝ συνδέονται με τη ζωή της διεργασίας που
                    // τα δημιούργησε. Το TraceEventSessionOptions.Create απαιτεί ΝΕΟ, ανύπαρκτο όνομα -
                    // πετάει exception αν βρει ήδη ενεργό ορφανό session με το ίδιο όνομα, πράγμα που
                    // έκανε το Start() να αποτυγχάνει ΜΟΝΙΜΑ και σιωπηλά (το LastError δεν εμφανιζόταν
                    // πουθενά στο UI) σε ΚΑΘΕ επόμενη εκκίνηση της εφαρμογής, μέχρι επανεκκίνηση των
                    // Windows. Σταματά ρητά οποιοδήποτε προϋπάρχον session με το ίδιο όνομα πρώτα, ώστε
                    // το Create να πετυχαίνει πάντα.
                    if (TraceEventSession.GetActiveSessionNames().Contains(SessionName))
                    {
                        try { using var stale = new TraceEventSession(SessionName, TraceEventSessionOptions.Attach); stale.Stop(); } catch { }
                    }

                    var session = new TraceEventSession(SessionName, TraceEventSessionOptions.Create) { StopOnDispose = true };
                    session.EnableProvider(DxgiProviderName);
                    session.Source.Dynamic.All += OnEvent;

                    _session = session;
                    _thread = new Thread(() => { try { session.Source.Process(); } catch { } })
                    {
                        IsBackground = true,
                        Name = "GameOverlayDxgiEtw",
                    };
                    _thread.Start();

                    IsRunning = true;
                    LastError = null;
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    IsRunning = false;
                    return false;
                }
            }
        }

        private static void OnEvent(Microsoft.Diagnostics.Tracing.TraceEvent data)
        {
            // ΔΙΟΡΘΩΣΗ - ένα μεμονωμένο event με απρόσμενο/ασύμβατο schema (π.χ. σε νεότερη/παλαιότερη
            // έκδοση του DXGI manifest από αυτή που αναμένεται) θα πετούσε exception ΕΔΩ, το οποίο θα
            // "σκότωνε" ΟΛΟΚΛΗΡΟ το session.Source.Process() loop στο thread του (βλ. Start() - το μόνο
            // "δίχτυ ασφαλείας" εκεί είναι ένα σιωπηλό try/catch γύρω από ΟΛΟΚΛΗΡΟ το Process(), δεν
            // ξαναρχίζει) - μόνιμη, σιωπηλή παύση της μέτρησης FPS από το πρώτο τέτοιο event και μετά.
            try
            {
                if (data.ProcessID != _targetPid) return;
                if (data.EventName.IndexOf("Present", StringComparison.OrdinalIgnoreCase) < 0) return;
                Interlocked.Increment(ref _frameCount);
            }
            catch { }
        }

        // Καλείται από τον DispatcherTimer του widget κάθε ~1s - μετατρέπει το τρέχον πλήθος frames σε
        // FPS και μηδενίζει το παράθυρο μέτρησης, ίδιο μοτίβο "sample and reset" με το
        // GpuInfoService.SampleUsagePercent.
        public static int? SampleFps()
        {
            if (!IsRunning) return null;
            var now = DateTime.UtcNow;
            var elapsed = (now - _windowStart).TotalSeconds;
            if (elapsed < 0.5) return LastFps;
            var count = Interlocked.Exchange(ref _frameCount, 0);
            _windowStart = now;
            LastFps = elapsed > 0 ? (int)Math.Round(count / elapsed) : null;
            return LastFps;
        }

        public static void Stop()
        {
            lock (Lock) { StopInternal(); }
        }

        private static void StopInternal()
        {
            try { _session?.Dispose(); } catch { }
            _session = null;
            _thread = null;
            IsRunning = false;
            LastFps = null;
        }
    }
}
