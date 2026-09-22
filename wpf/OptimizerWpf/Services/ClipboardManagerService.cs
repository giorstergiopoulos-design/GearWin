using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - roadmap "Αναζητήσιμο ιστορικό clipboard" (ρητό αίτημα χρήστη, με global hotkey αντί για
    // πρόσβαση ΜΟΝΟ μέσω tray - βλ. συζήτηση). Χρησιμοποιεί το HWND του ΚΥΡΙΟΥ παραθύρου (πάντα ζωντανό
    // όσο τρέχει η εφαρμογή, ακόμα κι αν είναι ελαχιστοποιημένο/κρυμμένο) για δύο Win32 μηχανισμούς:
    // RegisterHotKey (Win+Shift+V - ΔΕΝ συγκρούεται με το εγγενές Win+V της ίδιας των Windows) και
    // AddClipboardFormatListener (ειδοποίηση WM_CLIPBOARDUPDATE σε κάθε αλλαγή clipboard, system-wide -
    // ΔΕΝ χρειάζεται polling).
    //
    // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ (ίδιο πνεύμα με το Advanced_AutologonWarning's plaintext password note): το
    // ιστορικό αποθηκεύεται ΤΟΠΙΚΑ σε απλό JSON αρχείο, ΧΩΡΙΣ κρυπτογράφηση - ό,τι αντιγράφετε
    // (συμπεριλαμβανομένων τυχόν κωδικών) μπορεί να παραμείνει εκεί μεταξύ επανεκκινήσεων μέχρι να το
    // καθαρίσετε. Ίδια συμπεριφορά με το εγγενές Win+V clipboard history των Windows - όχι ασυνήθιστο,
    // αλλά αναφέρεται ρητά στο UI. Μόνο ΚΕΙΜΕΝΟ παρακολουθείται (όχι εικόνες/αρχεία) για απλότητα.
    public static class ClipboardManagerService
    {
        private const int WM_HOTKEY = 0x0312;
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private const int HotkeyId = 0x3C1A;
        private const uint MOD_WIN = 0x0008, MOD_SHIFT = 0x0004;
        private const uint VK_V = 0x56;
        private const int MaxHistory = 50;

        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private static readonly string StorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf", "ClipboardHistory.json");

        private static HwndSource? _source;
        private static IntPtr _hwnd;
        private static readonly List<string> _history = new();
        private static readonly object Lock = new();
        private static string? _lastSeen;

        public static event Action? HotkeyPressed;
        public static event Action? HistoryChanged;
        public static bool IsRunning { get; private set; }

        public static IReadOnlyList<string> History { get { lock (Lock) return _history.ToList(); } }

        public static void Start(Window mainWindow)
        {
            if (IsRunning) return;
            LoadPersisted();

            var helper = new WindowInteropHelper(mainWindow);
            _hwnd = helper.Handle != IntPtr.Zero ? helper.Handle : helper.EnsureHandle();
            _source = HwndSource.FromHwnd(_hwnd);
            _source?.AddHook(WndProc);
            RegisterHotKey(_hwnd, HotkeyId, MOD_WIN | MOD_SHIFT, VK_V);
            AddClipboardFormatListener(_hwnd);
            IsRunning = true;
        }

        public static void Stop()
        {
            if (!IsRunning) return;
            try { UnregisterHotKey(_hwnd, HotkeyId); } catch { }
            try { RemoveClipboardFormatListener(_hwnd); } catch { }
            _source?.RemoveHook(WndProc);
            _source = null;
            IsRunning = false;
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }
            else if (msg == WM_CLIPBOARDUPDATE)
            {
                TryCaptureClipboard();
            }
            return IntPtr.Zero;
        }

        private static void TryCaptureClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText()) return;
                var text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text) || text == _lastSeen) return;
                _lastSeen = text;

                lock (Lock)
                {
                    _history.RemoveAll(x => x == text);
                    _history.Insert(0, text);
                    if (_history.Count > MaxHistory) _history.RemoveRange(MaxHistory, _history.Count - MaxHistory);
                }
                SavePersisted();
                HistoryChanged?.Invoke();
            }
            catch { }
        }

        public static void CopyToClipboard(string text)
        {
            try { _lastSeen = text; Clipboard.SetText(text); }
            catch { }
        }

        public static void RemoveItem(string text)
        {
            lock (Lock) _history.Remove(text);
            SavePersisted();
            HistoryChanged?.Invoke();
        }

        public static void ClearHistory()
        {
            lock (Lock) _history.Clear();
            SavePersisted();
            HistoryChanged?.Invoke();
        }

        private static void LoadPersisted()
        {
            try
            {
                if (!File.Exists(StorePath)) return;
                var items = System.Text.Json.JsonSerializer.Deserialize<List<string>>(File.ReadAllText(StorePath));
                if (items == null) return;
                lock (Lock) { _history.Clear(); _history.AddRange(items.Take(MaxHistory)); }
            }
            catch { }
        }

        private static void SavePersisted()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
                List<string> snapshot;
                lock (Lock) snapshot = _history.ToList();
                File.WriteAllText(StorePath, System.Text.Json.JsonSerializer.Serialize(snapshot));
            }
            catch { }
        }
    }
}
