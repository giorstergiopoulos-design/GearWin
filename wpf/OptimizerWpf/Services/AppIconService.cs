using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    // Ρητό αίτημα χρήστη: "στα κουμπιά βάλε τα logos των εφαρμογών" (καθαρισμός cache περιηγητών +
    // προτεινόμενες εφαρμογές winget). Εξάγει το ΠΡΑΓΜΑΤΙΚΟ εικονίδιο από το ίδιο το εγκατεστημένο exe
    // (SHGetFileInfo - ήδη διαθέσιμο μέσω P/Invoke, καμία ανάγκη για νέο πακέτο System.Drawing.Common)
    // αντί να φτιάχνει/κατεβάζει δικά μας αντίγραφα εμπορικών λογότυπων. ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: μόνο
    // ήδη εγκατεστημένες εφαρμογές έχουν πραγματικό, τοπικό εικονίδιο διαθέσιμο - για τις προτεινόμενες
    // εφαρμογές winget που ΔΕΝ είναι ακόμα εγκατεστημένες δεν υπάρχει τοπική πηγή λογότυπου χωρίς λήψη
    // από το διαδίκτυο (εκτός εμβέλειας εδώ) - εμφανίζεται γενικό εικονίδιο πακέτου σε αυτή την περίπτωση.
    public static class AppIconService
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;

        public static ImageSource? GetIconForFile(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
                var info = new SHFILEINFO();
                var result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info), SHGFI_ICON | SHGFI_LARGEICON);
                if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
                try
                {
                    var src = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();
                    return src;
                }
                finally { DestroyIcon(info.hIcon); }
            }
            catch { return null; }
        }

        // Γενική αναζήτηση εγκατεστημένου exe/icon βάσει τίτλου - ψάχνει τα Uninstall κλειδιά μητρώου
        // (ΟΧΙ σκληροκωδικοποιημένες διαδρομές ανά εφαρμογή, που θα έσπαγαν εύκολα σε διαφορετικές
        // εκδόσεις/τοποθεσίες εγκατάστασης) για DisplayName που περιέχει τον τίτλο, διαβάζει DisplayIcon.
        public static string? FindInstalledIconPath(string appTitle)
        {
            foreach (var (hive, subPath) in new[]
                     {
                         (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                         (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                         (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                     })
            {
                using var key = hive.OpenSubKey(subPath);
                if (key == null) continue;
                foreach (var name in key.GetSubKeyNames())
                {
                    using var sub = key.OpenSubKey(name);
                    var displayName = sub?.GetValue("DisplayName") as string;
                    if (displayName == null || !displayName.Contains(appTitle, StringComparison.OrdinalIgnoreCase)) continue;
                    var icon = sub?.GetValue("DisplayIcon") as string;
                    if (string.IsNullOrWhiteSpace(icon)) continue;
                    var path = icon.Split(',')[0].Trim('"');
                    if (File.Exists(path)) return path;
                }
            }
            return null;
        }
    }
}
