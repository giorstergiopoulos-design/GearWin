using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OptimizerWpf.Services
{
    // 6.1.0 - ΠΡΑΓΜΑΤΙΚΗ διαφάνεια παραθύρου με ΚΑΝΟΝΙΚΟ πλαίσιο των Windows, ίδια τεχνική με το MotionDesk
    // Studio (WinForms Form.Opacity). Το Form.Opacity δεν είναι τίποτα άλλο από WS_EX_LAYERED +
    // SetLayeredWindowAttributes(LWA_ALPHA) πάνω στο HWND - το κάνουμε ακριβώς το ίδιο εδώ, ώστε το
    // WPF παράθυρο να μην χρειάζεται πια AllowsTransparency="True" (που ανάγκαζε WindowStyle="None",
    // δηλ. παράθυρο χωρίς πλαίσιο/γραμμή τίτλου) ούτε το Window.Opacity (που χωρίς AllowsTransparency
    // απλά "σκούραινε" το περιεχόμενο αντί να δείχνει ό,τι υπάρχει πίσω - το bug που είχε αναφερθεί).
    public static class WindowOpacityService
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const uint LWA_ALPHA = 0x2;

        [DllImport("user32.dll", SetLastError = true)] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        public static int ClampPercent(int percent) => Math.Clamp(percent, 60, 100);

        public static byte ToAlpha(int percent) => (byte)Math.Round(ClampPercent(percent) * 255 / 100.0);

        // Ασφαλές να καλείται πριν υπάρξει HWND (απλά δεν κάνει τίποτα) - ο caller το ξανακαλεί στο
        // SourceInitialized. 100% αφαιρεί εντελώς το layered στυλ (καμία επιβάρυνση απόδοσης).
        public static void Apply(Window window, int percent)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;
                var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
                if (ClampPercent(percent) >= 100)
                {
                    if ((ex & WS_EX_LAYERED) != 0) SetWindowLong(hwnd, GWL_EXSTYLE, ex & ~WS_EX_LAYERED);
                    return;
                }
                if ((ex & WS_EX_LAYERED) == 0) SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_LAYERED);
                SetLayeredWindowAttributes(hwnd, 0, ToAlpha(percent), LWA_ALPHA);
            }
            catch { /* best-effort: χωρίς διαφάνεια το παράθυρο απλά μένει αδιαφανές */ }
        }
    }
}
