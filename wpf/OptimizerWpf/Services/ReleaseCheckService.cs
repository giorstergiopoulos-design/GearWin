using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OptimizerWpf.Services
{
    public sealed record ReleaseCheckOutcome(ReleaseFetchStatus Status, string? Tag, ReleaseVersion? Latest, string PageUrl, bool CanInstallInApp, bool ShouldNotify);

    // Ειδοποίηση νέας έκδοσης του GearWin (balloon). Ανεξάρτητη από τις βαριές σαρώσεις (winget/οδηγοί/Windows Update) ώστε να μη
    // καθυστερεί λεπτά: πρώτος έλεγχος ~30 s μετά την εκκίνηση και μετά κάθε 24 ώρες. Ένα GET στο GitHub API με ETag (το 304 δεν μετράει στο όριο),
    // timeout 8 s, σιωπηλή αποτυχία. ΜΙΑ ειδοποίηση ανά έκδοση (OncePerVersionStore). Δεν ειδοποιεί όταν ο χρήστης παίζει/παρουσιάζει σε πλήρη οθόνη
    // (θα ξαναδοκιμάσει σε 30 λεπτά). Η εγκατάσταση παραμένει στο SelfUpdateService (υποχρεωτικό SHA-256, ρητή επιβεβαίωση)·
    // αν το release δεν έχει εγκαταστάσιμο asset με digest, το κλικ ανοίγει μόνο τη σελίδα του release.
    public static class ReleaseCheckService
    {
        private const string KeyEtag = "gearwin.etag", KeyTag = "gearwin.latest", KeyPage = "gearwin.page", KeyCan = "gearwin.cani", KeyBalloon = "gearwin.balloon";
        public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan Interval = TimeSpan.FromHours(24);
        public static readonly TimeSpan BusyRetry = TimeSpan.FromMinutes(30);

        private static DispatcherTimer? _timer;
        private static bool _inFlight;

        // Καθαρός κανόνας: ειδοποίηση μόνο αν η νέα έκδοση είναι μεγαλύτερη από την τρέχουσα ΚΑΙ μεγαλύτερη από αυτήν για την οποία ειδοποιήθηκε ο χρήστης.
        public static bool ShouldNotify(ReleaseVersion current, ReleaseVersion latest, string? notifiedVersion, string? skippedVersion = null)
        {
            if (!(latest > current)) return false;
            var skipped = ReleaseVersion.ParseOrNull(skippedVersion);
            if (skipped != null && latest <= skipped) return false;   // «Παράλειψη έκδοσης»
            var seen = ReleaseVersion.ParseOrNull(notifiedVersion);
            return seen == null || latest > seen;
        }

        public static async Task<ReleaseCheckOutcome> CheckAsync(OncePerVersionStore store, ReleaseVersion current, HttpMessageHandler? handler = null, CancellationToken ct = default)
        {
            var res = await GitHubReleaseClient.GetLatestAsync(SelfUpdateService.Owner, SelfUpdateService.Repo, store.Get(KeyEtag), handler, ct: ct);
            string? tag; string page; bool can;
            switch (res.Status)
            {
                case ReleaseFetchStatus.Ok when res.Release != null:
                    tag = res.Release.Tag;
                    page = res.Release.PageUrl;
                    var info = SelfUpdateService.ParseRelease(res.Release.Json);
                    can = info?.Sha256 != null;   // χωρίς digest το SelfUpdateService δεν θα εγκαταστήσει τίποτα
                    store.SetRaw(KeyEtag, res.ETag);
                    store.SetRaw(KeyTag, tag);
                    store.SetRaw(KeyPage, page);
                    store.SetRaw(KeyCan, can ? "1" : "0");
                    break;
                case ReleaseFetchStatus.NotModified:
                    tag = store.Get(KeyTag);
                    page = store.Get(KeyPage) ?? $"https://github.com/{SelfUpdateService.Owner}/{SelfUpdateService.Repo}/releases";
                    can = store.Get(KeyCan) == "1";
                    break;
                default:
                    return new ReleaseCheckOutcome(res.Status, null, null, "", false, false);
            }
            var latest = ReleaseVersion.ParseOrNull(tag);
            if (latest == null) return new ReleaseCheckOutcome(res.Status, tag, null, page, can, false);
            return new ReleaseCheckOutcome(res.Status, tag, latest, page, can, ShouldNotify(current, latest, store.Get(KeyBalloon), store.Get(Views.UpdateAnnounceWindow.SkippedKey)));
        }

        public static void Start()
        {
            if (_timer != null) return;
            _timer = new DispatcherTimer { Interval = FirstDelay };
            _timer.Tick += async (_, _) => await TickAsync();
            _timer.Start();
        }

        public static void Stop() { _timer?.Stop(); _timer = null; }

        private static async Task TickAsync()
        {
            if (_timer == null || _inFlight) return;
            _timer.Stop();
            _inFlight = true;
            var next = Interval;
            try
            {
                if (!AppSettingsService.Current.SelfUpdateCheckEnabled) return;
                var store = new OncePerVersionStore(OncePerVersionStore.DefaultPath());
                var outcome = await CheckAsync(store, ReleaseVersion.FromVersion(SelfUpdateService.Current));
                if (!outcome.ShouldNotify || outcome.Latest == null) return;
                if (UserIsBusy()) { next = BusyRetry; return; }
                store.MarkSeen(KeyBalloon, outcome.Latest);
                // 6.7.1: popup με Εγκατάσταση / Αργότερα / Παράλειψη έκδοσης / Τι νέο υπάρχει (όπως στο Crawler) + μπαλόνι του tray
                Views.UpdateAnnounceWindow.ShowFor(outcome, store);
                TrayIconService.ShowNotificationBalloon(LanguageService.T("SelfUpdate_NotifyTitle"),
                    string.Format(LanguageService.T("SelfUpdate_NotifyBodyPage"), outcome.Latest),
                    outcome.CanInstallInApp ? TrayIconService.OpenSettings : () => OpenReleasePage(outcome.PageUrl));
            }
            catch { /* ο έλεγχος νέας έκδοσης δεν πρέπει ποτέ να ρίξει την εφαρμογή */ }
            finally
            {
                _inFlight = false;
                if (_timer != null) { _timer.Interval = next; _timer.Start(); }
            }
        }

        public static void OpenReleasePage(string url)
        {
            if (!SuiteCatalogService.IsAllowedUrl(url)) return;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        // Πλήρης οθόνη / παιχνίδι D3D / παρουσίαση / κλειδωμένη οθόνη -> όχι ειδοποίηση τώρα.
        public static bool UserIsBusy()
        {
            try { return SHQueryUserNotificationState(out var s) == 0 && s is 1 or 2 or 3 or 4; }
            catch { return false; }
        }

        [DllImport("shell32.dll")]
        private static extern int SHQueryUserNotificationState(out int state);
    }
}
