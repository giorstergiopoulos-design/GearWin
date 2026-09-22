using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public record QuickCleanItem(string Key, string Label, long SizeBytes, bool Recommended);

    // ΝΕΟ - ρητό αίτημα χρήστη: "θέλω αυτόματο καθαρισμό συστήματος στο Home όπως στο PC Manager,
    // έλεγξε ποιες λειτουργίες ενσωματώνει αυτό το εργαλείο γρήγορου καθαρισμού εκτός από temp αρχεία".
    // Έρευνα (WebSearch, βλ. roadmap #research) έδειξε ότι το Microsoft PC Manager's Quick/Deep Clean
    // καλύπτει: temp αρχεία, Κάδο Ανακύκλωσης, cache μικρογραφιών, cache Windows Update, αρχεία
    // Delivery Optimization, cache shader κάρτας γραφικών, και cache περιηγητών - όλα τα παρακάτω.
    public static class QuickCleanService
    {
        private static string SystemRoot => Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
        private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        private static string WindowsTempDir => Path.Combine(SystemRoot, "Temp");
        private static string ExplorerCacheDir => Path.Combine(LocalAppData, "Microsoft", "Windows", "Explorer");
        private static string WindowsUpdateCacheDir => Path.Combine(SystemRoot, "SoftwareDistribution", "Download");
        private static string DeliveryOptimizationDir => Path.Combine(SystemRoot, "SoftwareDistribution", "DeliveryOptimization");

        private static IEnumerable<string> ShaderCacheDirs() => new[]
        {
            Path.Combine(LocalAppData, "D3DSCache"),
            Path.Combine(LocalAppData, "NVIDIA", "DXCache"),
            Path.Combine(LocalAppData, "NVIDIA", "GLCache"),
            Path.Combine(LocalAppData, "AMD", "DxCache"),
            Path.Combine(LocalAppData, "AMD", "GLCache"),
        };

        public static Task<IReadOnlyList<QuickCleanItem>> ScanAsync() => Task.Run(() =>
        {
            var items = new List<QuickCleanItem>
            {
                new("UserTemp", LanguageService.T("QuickClean_UserTemp"), DirSize(Path.GetTempPath()), true),
                new("WindowsTemp", LanguageService.T("QuickClean_WindowsTemp"), DirSize(WindowsTempDir), true),
                new("RecycleBin", LanguageService.T("QuickClean_RecycleBin"), RecycleBinSize(), true),
                new("ThumbnailCache", LanguageService.T("QuickClean_ThumbnailCache"), ThumbCacheSize(), true),
                new("WindowsUpdateCache", LanguageService.T("QuickClean_WindowsUpdateCache"), DirSize(WindowsUpdateCacheDir), false),
                new("DeliveryOptimization", LanguageService.T("QuickClean_DeliveryOptimization"), DirSize(DeliveryOptimizationDir), false),
                new("ShaderCache", LanguageService.T("QuickClean_ShaderCache"), ShaderCacheDirs().Sum(DirSize), false),
                new("BrowserCache", LanguageService.T("QuickClean_BrowserCache"), HealthCleanupService.DetectBrowsers().Sum(BrowserCacheSize), false),
            };
            return (IReadOnlyList<QuickCleanItem>)items;
        });

        public static async Task<long> CleanAsync(IReadOnlyList<string> keys)
        {
            long freed = 0;
            foreach (var key in keys)
            {
                switch (key)
                {
                    case "UserTemp":
                        freed += DirSize(Path.GetTempPath());
                        await Task.Run(() => DeleteContents(Path.GetTempPath()));
                        break;
                    case "WindowsTemp":
                        freed += DirSize(WindowsTempDir);
                        await Task.Run(() => DeleteContents(WindowsTempDir));
                        break;
                    case "RecycleBin":
                        freed += RecycleBinSize();
                        await Task.Run(EmptyRecycleBin);
                        break;
                    case "ThumbnailCache":
                        freed += ThumbCacheSize();
                        await Task.Run(DeleteThumbCache);
                        break;
                    case "WindowsUpdateCache":
                        freed += DirSize(WindowsUpdateCacheDir);
                        await Task.Run(() => DeleteContents(WindowsUpdateCacheDir));
                        break;
                    case "DeliveryOptimization":
                        freed += DirSize(DeliveryOptimizationDir);
                        await Task.Run(() => DeleteContents(DeliveryOptimizationDir));
                        break;
                    case "ShaderCache":
                        foreach (var dir in ShaderCacheDirs())
                        {
                            freed += DirSize(dir);
                            await Task.Run(() => DeleteContents(dir));
                        }
                        break;
                    case "BrowserCache":
                        foreach (var browser in HealthCleanupService.DetectBrowsers())
                        {
                            freed += BrowserCacheSize(browser);
                            await HealthCleanupService.ClearBrowserCacheAsync(browser);
                        }
                        break;
                }
            }
            return freed;
        }

        private static long BrowserCacheSize(BrowserCacheEntry browser)
        {
            long total = 0;
            foreach (var basePath in browser.Paths)
            {
                if (browser.Name is "Chrome" or "Edge" or "Brave" or "Vivaldi")
                {
                    foreach (var profileDir in SafeDirs(basePath))
                    {
                        foreach (var cacheSub in new[] { "Cache", "Code Cache", "GPUCache" })
                            total += DirSize(Path.Combine(profileDir, cacheSub));
                    }
                }
                else
                {
                    total += DirSize(basePath);
                }
            }
            return total;
        }

        // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): Directory.EnumerateFiles(dir, "*",
        // SearchOption.AllDirectories) είναι ΤΕΜΠΕΛΗΣ (lazy) - το UnauthorizedAccessException από έναν
        // απαγορευμένο υποφάκελο (π.χ. AppContainer temp folders μέσα στο C:\Windows\Temp, ακόμα και με
        // Administrator) πετάγεται ΜΕΣΑ στο foreach, ΟΧΙ στην αρχική κλήση - το try/catch γύρω από την
        // κλήση δεν το έπιανε καθόλου, με αποτέλεσμα η σάρωση να "κολλάει" σιωπηλά για πάντα (καμία
        // εξαίρεση, καμία ενημέρωση UI, βλ. LoadQuickCleanAsync). Αναδρομικός, ΑΝΑ-ΦΑΚΕΛΟ προστατευμένος
        // περίπατος αντί για αυτό - ένας απρόσβατος υποφάκελος παραλείπεται (συνεισφέρει 0), η σάρωση
        // συνεχίζει κανονικά στους υπόλοιπους.
        internal static long DirSize(string dir)
        {
            if (!Directory.Exists(dir)) return 0;
            long total = 0;

            string[] files;
            try { files = Directory.GetFiles(dir); } catch { files = Array.Empty<string>(); }
            foreach (var f in files)
            {
                try { total += new FileInfo(f).Length; } catch { }
            }

            string[] subdirs;
            try { subdirs = Directory.GetDirectories(dir); } catch { subdirs = Array.Empty<string>(); }
            foreach (var d in subdirs) total += DirSize(d);

            return total;
        }

        private static void DeleteContents(string dir)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in SafeTopFiles(dir))
            {
                try { File.Delete(f); } catch { }
            }
            foreach (var d in SafeDirs(dir))
            {
                try { Directory.Delete(d, true); } catch { }
            }
        }

        private static long ThumbCacheSize()
        {
            if (!Directory.Exists(ExplorerCacheDir)) return 0;
            long total = 0;
            foreach (var pattern in new[] { "thumbcache_*.db", "iconcache_*.db" })
                foreach (var f in SafeFilesPattern(ExplorerCacheDir, pattern))
                {
                    try { total += new FileInfo(f).Length; } catch { }
                }
            return total;
        }

        private static void DeleteThumbCache()
        {
            if (!Directory.Exists(ExplorerCacheDir)) return;
            foreach (var pattern in new[] { "thumbcache_*.db", "iconcache_*.db" })
                foreach (var f in SafeFilesPattern(ExplorerCacheDir, pattern))
                {
                    try { File.Delete(f); } catch { } // κλειδωμένα από την ενεργή Εξερεύνηση - αγνοούνται σιωπηλά
                }
        }

        private static IEnumerable<string> SafeDirs(string root)
        {
            try { return Directory.EnumerateDirectories(root); } catch { return Array.Empty<string>(); }
        }

        private static IEnumerable<string> SafeTopFiles(string root)
        {
            try { return Directory.EnumerateFiles(root); } catch { return Array.Empty<string>(); }
        }

        private static IEnumerable<string> SafeFilesPattern(string root, string pattern)
        {
            try { return Directory.EnumerateFiles(root, pattern); } catch { return Array.Empty<string>(); }
        }

        // ===== Κάδος Ανακύκλωσης μέσω shell32.dll - καμία διαθέσιμη διαχειριζόμενη API για
        // μέγεθος/άδειασμα, ίδιο μοτίβο P/Invoke με τα ήδη υπάρχοντα Win32 calls της εφαρμογής. =====
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        private static long RecycleBinSize()
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO)) };
            return SHQueryRecycleBin(null, ref info) == 0 ? info.i64Size : 0;
        }

        private static void EmptyRecycleBin() =>
            SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);

        public static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            var unit = 0;
            while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
            return $"{size:0.#} {units[unit]}";
        }
    }
}
