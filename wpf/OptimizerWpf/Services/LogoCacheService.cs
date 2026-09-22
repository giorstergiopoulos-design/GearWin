using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OptimizerWpf.Services
{
    // Ρητό αίτημα χρήστη: "κατέβασε όλα τα logos... και έχε τα πάντα διαθέσιμα χωρίς να κάνεις
    // συνεχεία σάρωση στο net ή στο κάθε σύστημα" - κατεβάζει το favicon του επίσημου site κάθε
    // εφαρμογής ΜΙΑ φορά (μέσω δημόσιου favicon-proxy, icons.duckduckgo.com - καμία αναπαραγωγή
    // εμπορικού λογότυπου από εμάς, μόνο ό,τι ήδη δημοσιεύει η ίδια η εταιρεία στο site της) και το
    // αποθηκεύει τοπικά (%LOCALAPPDATA%\OptimizerWpf\LogoCache) - κάθε επόμενη εκκίνηση διαβάζει
    // απευθείας το τοπικό cache, ΚΑΜΙΑ επανάληψη δικτυακού αιτήματος ή σάρωσης μητρώου/συστήματος.
    public static class LogoCacheService
    {
        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf", "LogoCache");

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

        // Επίσημο domain ανά κλειδί (WingetId για προτεινόμενες εφαρμογές, όνομα browser για το
        // Health tab) - χρησιμοποιείται ΜΟΝΟ για να ζητηθεί το δημόσιο favicon του, καμία άλλη χρήση.
        private static readonly Dictionary<string, string> DomainByKey = new()
        {
            ["Google.Chrome"] = "google.com",
            // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "το firefox εικονίδιο ήταν σωστό, τώρα όχι") - το mozilla.org
            // δεν έχει πάντα το αναγνωρίσιμο λογότυπο της αλεπούς ως favicon (γενικό "M" ή fallback) -
            // το firefox.com (ίδια εταιρεία, επίσημο marketing site του browser) το έχει αξιόπιστα.
            ["Mozilla.Firefox"] = "firefox.com",
            ["Opera.Opera"] = "opera.com",
            ["Microsoft.Edge"] = "microsoft.com",
            ["7zip.7zip"] = "7-zip.org",
            ["RARLab.WinRAR"] = "win-rar.com",
            ["VideoLAN.VLC"] = "videolan.org",
            ["Spotify.Spotify"] = "spotify.com",
            ["GOMLab.GOMPlayer"] = "gomlab.com",
            ["Winamp.Winamp"] = "winamp.com",
            ["CodecGuide.K-LiteCodecPack.Mega"] = "codecguide.com",
            ["Zoom.Zoom"] = "zoom.us",
            ["Discord.Discord"] = "discord.com",
            ["Microsoft.Teams"] = "microsoft.com",
            // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "του viber θέλει διόρθωση") - το "Viber.Viber" ΔΕΝ υπάρχει
            // καν στο winget repository (επιβεβαιώθηκε με "winget search Viber") - το σωστό winget ID
            // είναι "Rakuten.Viber" (η Rakuten είναι η μητρική εταιρεία του Viber) - βλ. και το ίδιο
            // κλειδί στο BloatwareService.RecommendedApps, που έπρεπε επίσης να διορθωθεί.
            ["Rakuten.Viber"] = "viber.com",
            ["AnyDeskSoftwareGmbH.AnyDesk"] = "anydesk.com",
            ["TeamViewer.TeamViewer"] = "teamviewer.com",
            ["Microsoft.PowerToys"] = "microsoft.com",
            ["Apache.OpenOffice"] = "openoffice.org",
            ["Adobe.Acrobat.Reader.64-bit"] = "adobe.com",
            ["Microsoft.DotNet.DesktopRuntime.8"] = "microsoft.com",
            ["Microsoft.DirectX"] = "microsoft.com",
            ["Microsoft.VCRedist.2015+.x64"] = "microsoft.com",
            // Browsers (Health tab - Καθαρισμός Cache Περιηγητών)
            ["Chrome"] = "google.com",
            ["Edge"] = "microsoft.com",
            ["Brave"] = "brave.com",
            ["Opera"] = "opera.com",
            ["Firefox"] = "firefox.com",
        };

        // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "άλλα εικονίδια δεν έχουν φορτωθεί") - το favicon-proxy απαντά
        // ΚΑΝΟΝΙΚΑ (HTTP 200) ακόμα και όταν δεν βρίσκει πραγματικό favicon, με ένα μικροσκοπικό
        // "placeholder" εικονίδιο - χωρίς αυτόν τον έλεγχο, ένα τέτοιο placeholder αποθηκευόταν ΜΟΝΙΜΑ
        // στο cache σαν να ήταν επιτυχία, και ΠΟΤΕ δεν ξαναδοκιμαζόταν.
        private const int MinValidIconBytes = 300;

        public static async Task<ImageSource?> GetLogoAsync(string key)
        {
            if (!DomainByKey.TryGetValue(key, out var domain)) return null;
            // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "το firefox εικονίδιο είναι ακόμα λάθος" - παρότι το domain
            // είχε ήδη διορθωθεί mozilla.org->firefox.com) - το cache filename βασιζόταν ΜΟΝΟ στο key,
            // άρα ένα ήδη κατεβασμένο (λάθος) αρχείο από το ΠΑΛΙΟ domain δεν ξαναδοκιμαζόταν ΠΟΤΕ, αφού
            // υπήρχε ήδη ένα αρχείο αρκετά μεγάλο να περάσει τον έλεγχο μεγέθους. Το domain είναι πλέον
            // ΜΕΡΟΣ του filename - μια αλλαγή domain στο DomainByKey γίνεται αυτόματα cache-miss.
            var cachePath = Path.Combine(CacheDir, SafeFileName($"{key}_{domain}") + ".ico");

            // Αυτο-επούλωση: ένα ήδη cached αρχείο από πριν από αυτή τη διόρθωση μπορεί να είναι ένα
            // τέτοιο "placeholder" - αν είναι ύποπτα μικρό, διαγράφεται ώστε να ξαναδοκιμαστεί παρακάτω.
            if (File.Exists(cachePath) && new FileInfo(cachePath).Length < MinValidIconBytes)
            {
                try { File.Delete(cachePath); } catch { }
            }

            if (!File.Exists(cachePath))
            {
                try
                {
                    Directory.CreateDirectory(CacheDir);
                    // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "vlc winamp και adobe acrobat reader λείπουν") -
                    // επιβεβαιώθηκε ότι το icons.duckduckgo.com δεν έχει καλυμμένα αυτά τα 3 domains
                    // (404/σχεδόν-κενή απάντηση). Δεύτερη, ανεξάρτητη πηγή ως fallback - το ίδιο δημόσιο
                    // favicon endpoint της Google (χρησιμοποιείται ευρέως ακριβώς γι' αυτόν τον σκοπό,
                    // πολύ ευρύτερη κάλυψη domains) - δοκιμάζεται ΜΟΝΟ αν η πρώτη πηγή αποτύχει.
                    // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "vlc εικονίδιο δεν υπάρχει") - επιβεβαιώθηκε ότι ΚΑΙ οι
                    // δύο proxies (duckduckgo, google) επιστρέφουν 404 συγκεκριμένα για το videolan.org
                    // (όχι placeholder - πραγματική αποτυχία). Τρίτο fallback: το ίδιο το favicon.ico
                    // του επίσημου site, απευθείας - αυτό δουλεύει πάντα όταν το site το φιλοξενεί.
                    var bytes = await TryDownloadAsync($"https://icons.duckduckgo.com/ip3/{domain}.ico")
                                ?? await TryDownloadAsync($"https://www.google.com/s2/favicons?domain={domain}&sz=64")
                                ?? await TryDownloadAsync($"https://{domain}/favicon.ico");
                    if (bytes == null || bytes.Length < MinValidIconBytes) return null;
                    await File.WriteAllBytesAsync(cachePath, bytes);
                }
                catch
                {
                    return null; // Χωρίς σύνδεση/αποτυχία λήψης - απλά καμία εικόνα αυτή τη φορά, ξαναδοκιμάζεται στην επόμενη εκκίνηση.
                }
            }

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(cachePath);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                try { File.Delete(cachePath); } catch { } // πιθανό κατεστραμμένο cache αρχείο - καθαρισμός για επόμενη προσπάθεια
                return null;
            }
        }

        private static async Task<byte[]?> TryDownloadAsync(string url)
        {
            try
            {
                var bytes = await Http.GetByteArrayAsync(url);
                return bytes.Length >= MinValidIconBytes ? bytes : null;
            }
            catch
            {
                return null;
            }
        }

        private static string SafeFileName(string name) =>
            string.Concat(Array.ConvertAll(name.ToCharArray(), c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c));
    }
}
