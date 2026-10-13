using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OptimizerWpf.Services
{
    // DPI-ασφαλής τοποθέτηση popup (tray popup, clipboard popup, προεπιλογή widget).
    // ΠΡΟΒΛΗΜΑ (αναφορά χρήστη: "σε χαμηλότερες αναλύσεις τα balloon εμφανίζονται σωστά"): η εφαρμογή είναι
    // PerMonitorV2 (app.manifest). Το SystemParameters.WorkArea ΔΕΝ ακολουθεί το DPI της οθόνης όπου έγινε
    // το κλικ, και το GetCursorPos επιστρέφει ΦΥΣΙΚΑ pixels που ανατίθονταν απευθείας σε Left/Top (DIPs) -
    // σε 100% scaling τα δύο συμπίπτουν (όλα σωστά), σε 125%/150%/200% το παράθυρο έβγαινε εκτός θέσης/οθόνης.
    // Εδώ ΟΛΑ υπολογίζονται σε φυσικά pixels από το Win32 work area της οθόνης που βρίσκεται ο δρομέας
    // (άρα σωστό και με taskbar σε οποιαδήποτε άκρη, και με πολλές οθόνες διαφορετικού DPI).
    public static class PopupPlacement
    {
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public int dwFlags; }

        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT pt);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT pt, int flags);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dx, out uint dy);

        private const int MONITOR_DEFAULTTONEAREST = 2;
        private const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;

        private static bool TryGetMonitor(POINT pt, out RECT work, out double scale)
        {
            work = default; scale = 1.0;
            try
            {
                var mon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
                var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (mon == IntPtr.Zero || !GetMonitorInfo(mon, ref info)) return false;
                work = info.rcWork;
                if (GetDpiForMonitor(mon, 0, out var dx, out _) == 0 && dx > 0) scale = dx / 96.0;
                return true;
            }
            catch { return false; }
        }

        private static void Apply(Window w, Func<RECT, double, (int Width, int Height), (int X, int Y)> pick, POINT pt)
        {
            if (!TryGetMonitor(pt, out var work, out var scale)) return;
            var hwnd = new WindowInteropHelper(w).EnsureHandle();

            // Βήμα 1: μεταφορά στην οθόνη-στόχο ώστε το WPF να εφαρμόσει το DPI της (WM_DPICHANGED) και να
            // ξέρουμε το πραγματικό τελικό μέγεθος σε φυσικά pixels.
            SetWindowPos(hwnd, IntPtr.Zero, work.Left, work.Top, 0, 0, SWP_NOZORDER | SWP_NOACTIVATE | 0x0001 /*SWP_NOSIZE*/);

            var dpi = VisualTreeHelperSafeScale(w, scale);
            var wPx = (int)Math.Round((w.ActualWidth > 0 ? w.ActualWidth : w.Width) * dpi);
            var hPx = (int)Math.Round((w.ActualHeight > 0 ? w.ActualHeight : w.Height) * dpi);
            var (x, y) = pick(work, dpi, (wPx, hPx));

            // Περιορισμός μέσα στο work area (ποτέ εκτός οθόνης, ό,τι κι αν υπολογίστηκε).
            x = Math.Max(work.Left, Math.Min(x, work.Right - wPx));
            y = Math.Max(work.Top, Math.Min(y, work.Bottom - hPx));
            SetWindowPos(hwnd, IntPtr.Zero, x, y, wPx, hPx, SWP_NOZORDER | SWP_NOACTIVATE);
        }

        private static double VisualTreeHelperSafeScale(Window w, double fallback)
        {
            try { return System.Windows.Media.VisualTreeHelper.GetDpi(w).DpiScaleX; }
            catch { return fallback; }
        }

        // Κάτω-δεξιά γωνία του work area της οθόνης όπου βρίσκεται ο δρομέας (π.χ. κλικ στο tray icon).
        public static void PlaceBottomRightOnCursorMonitor(Window w, int marginDip = 12)
        {
            GetCursorPos(out var pt);
            Apply(w, (work, dpi, size) =>
            {
                var m = (int)Math.Round(marginDip * dpi);
                return (work.Right - size.Width - m, work.Bottom - size.Height - m);
            }, pt);
        }

        // Κοντά στον δρομέα (clipboard hotkey popup), μέσα στο work area της οθόνης του.
        public static void PlaceNearCursor(Window w, int marginDip = 8)
        {
            GetCursorPos(out var pt);
            Apply(w, (work, dpi, size) => (pt.X, pt.Y), pt);
        }
    }
}
