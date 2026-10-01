using System;
using System.Windows;
using System.Windows.Threading;

namespace OptimizerWpf.Services
{
    // Αντιγραφή ευαίσθητου κειμένου (κωδικοί browser / Wi-Fi) στο πρόχειρο ΜΕ αυτόματο καθαρισμό: πριν, ο κωδικός
    // έμενε στο πρόχειρο επ' άπειρον (και στο ιστορικό Win+V) μέχρι να αντικατασταθεί από κάτι άλλο. Καθαρίζουμε
    // μετά από λίγα δευτερόλεπτα ΜΟΝΟ αν το πρόχειρο περιέχει ακόμα ΑΚΡΙΒΩΣ το δικό μας κείμενο (δεν πειράζουμε
    // κάτι που αντέγραψε ο χρήστης στο μεταξύ). Η εξαίρεση από το ιστορικό/cloud πρόχειρο γίνεται με τα επίσημα
    // formats "ExcludeClipboardContentFromMonitorProcessing" και "CanIncludeInClipboardHistory"=0.
    public static class SecureClipboardService
    {
        private const int ClearAfterSeconds = 30;
        private static DispatcherTimer? _timer;

        public static void SetSensitiveText(string text)
        {
            var data = new DataObject();
            data.SetText(text);
            // 0 = μην συμπεριληφθεί στο ιστορικό (Win+V) / cloud clipboard.
            data.SetData("CanIncludeInClipboardHistory", new System.IO.MemoryStream(BitConverter.GetBytes(0)));
            data.SetData("CanUploadToCloudClipboard", new System.IO.MemoryStream(BitConverter.GetBytes(0)));
            data.SetData("ExcludeClipboardContentFromMonitorProcessing", new System.IO.MemoryStream(BitConverter.GetBytes(1)));
            Clipboard.SetDataObject(data, copy: true);

            _timer?.Stop();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(ClearAfterSeconds) };
            _timer.Tick += (_, _) =>
            {
                _timer?.Stop();
                try { if (Clipboard.ContainsText() && Clipboard.GetText() == text) Clipboard.Clear(); } catch { /* το πρόχειρο κλειδωμένο από άλλη εφαρμογή */ }
            };
            _timer.Start();
        }
    }
}
