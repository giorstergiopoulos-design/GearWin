using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OptimizerWpf.Services
{
    // Ρητό αίτημα χρήστη ("Windows Classic skin - τα παράθυρα τετράγωνα") - τα Windows 11 στρογγυλεύουν
    // ΑΥΤΟΜΑΤΑ τις γωνίες κάθε top-level παραθύρου σε επίπεδο DWM, ανεξάρτητα από το περιεχόμενο της
    // εφαρμογής (καμία καθαρά-XAML λύση) - το μόνο επίσημο, τεκμηριωμένο API να το απενεργοποιηθεί είναι
    // το DwmSetWindowAttribute με DWMWA_WINDOW_CORNER_PREFERENCE (Windows 11 build 22000+ only· σε
    // παλιότερα Windows η κλήση απλά αποτυγχάνει σιωπηλά - τα παράθυρα ήταν ήδη τετράγωνα εκεί).
    public static class WindowCornerService
    {
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_DEFAULT = 0;
        private const int DWMWCP_DONOTROUND = 1;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public static void SetSquareCorners(Window window, bool square)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;
                var pref = square ? DWMWCP_DONOTROUND : DWMWCP_DEFAULT;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            }
            catch { /* Παλιότερα Windows/απρόσμενο περιβάλλον - το παράθυρο απλά κρατάει τις προεπιλεγμένες γωνίες του. */ }
        }
    }
}
