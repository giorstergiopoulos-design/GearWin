using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - ρητό αίτημα χρήστη: "όταν υπάρχουν updates σε εφαρμογές να εμφανίζεται παράθυρο πάνω από
    // την taskbar (όπως στα windows 10/11)... το ίδιο και για τους οδηγούς, όταν είναι minimized στην
    // taskbar" - περιοδική, background σάρωση (ο χρήστης δεν χρειάζεται να ανοίξει καν την καρτέλα
    // Βελτιστοποίηση) που ενημερώνει το ήδη υπάρχον UpdatesHubService (τροφοδοτεί την Αρχική) ΚΑΙ
    // δείχνει μια πραγματική ειδοποίηση Windows μέσω TrayIconService.ShowUpdateBalloon - στα Windows
    // 10/11 το NotifyIcon.ShowBalloonTip αποδίδεται ακριβώς ως το toast της περιοχής ειδοποιήσεων,
    // ορατό πάνω από τη γραμμή εργασιών ΑΝΕΞΑΡΤΗΤΑ αν το κύριο παράθυρο είναι ελαχιστοποιημένο/
    // κρυμμένο στο tray (ίδιο μηχανισμό ήδη χρησιμοποιεί το Quick Clean's balloon tip).
    //
    // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ σχεδιασμού: ΔΕΝ ειδοποιεί σε ΚΑΘΕ περιοδικό έλεγχο - μόνο όταν ο αριθμός
    // διαθέσιμων ενημερώσεων άλλαξε από την τελευταία φορά που πράγματι ειδοποιήθηκε ο χρήστης
    // (AppSettingsService.LastNotified*), ώστε να μην ξαναδείχνει το ΙΔΙΟ μήνυμα σε κάθε τικ του timer.
    public static class UpdateNotificationService
    {
        private static DispatcherTimer? _timer;
        private static bool _checkInFlight;

        // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη, επαναλαμβανόμενο: "δεν βλέπω αυτόματο έλεγχο ενημερώσεων/
        // balloon tip") - μέχρι τώρα ΟΛΗ αυτή η ροή ήταν 100% αόρατη εκτός αν άλλαζε ο αριθμός
        // ενημερώσεων (ΚΑΙ εμφανιζόταν πραγματικά το Windows toast - που μπορεί να καταπνιγεί σιωπηλά
        // από Focus Assist/ανά-εφαρμογή ρυθμίσεις ειδοποιήσεων, εκτός ελέγχου της εφαρμογής). Κάθε
        // βήμα γράφεται εδώ ΜΕ timestamp - επιτρέπει να επιβεβαιωθεί τι ΠΡΑΓΜΑΤΙΚΑ συνέβη (έτρεξε/
        // απέτυχε/βρήκε Χ) χωρίς να χρειάζεται ζωντανή παρατήρηση της οθόνης.
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf", "update-check.log");

        private static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { /* η ίδια η καταγραφή δεν πρέπει ποτέ να ρίξει τον έλεγχο ενημερώσεων */ }
        }

        public static void Start()
        {
            if (_timer != null) return;
            // Ο ίδιος ο timer τρέχει συχνά (κάθε 2 λεπτά) αλλά το RunCheckIfDueAsync παρακάτω κάνει
            // πραγματική σάρωση μόνο όταν έχει περάσει το πραγματικό διάστημα (UpdateCheckIntervalHours,
            // προεπιλογή 6 ώρες) - φθηνός έλεγχος ρολογιού συχνά, ακριβή σάρωση winget/οδηγών σπάνια.
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
            _timer.Tick += async (_, _) => await RunCheckIfDueAsync();
            _timer.Start();
        }

        // 6.1.0 - ΑΥΤΟΜΑΤΟΣ έλεγχος ενημερώσεων σε ΚΑΘΕ εκκίνηση της εφαρμογής (tray ή κανονική), με
        // ειδοποίηση. Στην αυτόματη εκκίνηση με τα Windows το δίκτυο δεν είναι πάντα έτοιμο και ο
        // προηγούμενος άμεσος έλεγχος απέτυχε σιωπηλά (και μετά το διάστημα των 6 ωρών δεν ξαναδοκίμαζε) -
        // τώρα περιμένουμε σύνδεση (έως ~3 λεπτά) και ο έλεγχος γίνεται μία φορά με announce.
        public static async Task StartupCheckAsync()
        {
            if (_checkInFlight) { Log("StartupCheckAsync: skipped, already in flight"); return; }
            if (!AppSettingsService.Current.UpdateNotificationsEnabled) { Log("StartupCheckAsync: skipped, UpdateNotificationsEnabled=false"); return; }
            Log("StartupCheckAsync: starting, waiting 8s then for network (up to 3 min)");
            _checkInFlight = true;
            // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "όταν γίνεται η διαδικασία θα φαίνεται στη γραμμή
            // κατάστασης, που δεν βλέπω κάτι") - μέχρι τώρα ΟΛΟ αυτό το 8s+έως 3' στάδιο αναμονής ΚΑΙ
            // η ίδια η σάρωση ήταν αόρατα στη γραμμή κατάστασης (μόνο το χειροκίνητο "Έλεγχος Τώρα" το
            // έκανε). SetBusy εδώ, SetIdle σε finally - ΠΑΝΤΑ καθαρίζει ακόμα κι αν πεταχτεί εξαίρεση.
            StatusService.SetBusy(LanguageService.T("UpdateNotify_AutoChecking"));
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(8));
                var waited = 0;
                while (waited < 18 && !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    waited++;
                }
                Log($"StartupCheckAsync: network available={System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()} after {waited * 10}s wait - running check");
                await RunCheckAsync(announce: true);
            }
            catch (Exception ex) { Log($"StartupCheckAsync: unexpected exception - {ex}"); }
            finally { StatusService.SetIdle(LanguageService.T("Ready")); _checkInFlight = false; }
        }

        public static void Stop()
        {
            _timer?.Stop();
            _timer = null;
        }

        private static async Task RunCheckIfDueAsync()
        {
            if (_checkInFlight || !AppSettingsService.Current.UpdateNotificationsEnabled) return;
            var last = AppSettingsService.Current.LastUpdateCheckAt;
            var intervalHours = Math.Max(1, AppSettingsService.Current.UpdateCheckIntervalHours);
            if (last.HasValue && DateTime.Now - last.Value < TimeSpan.FromHours(intervalHours)) return;

            _checkInFlight = true;
            Log("RunCheckIfDueAsync: interval elapsed, running periodic check");
            StatusService.SetBusy(LanguageService.T("UpdateNotify_AutoChecking"));
            try { await RunCheckAsync(); }
            finally { StatusService.SetIdle(LanguageService.T("Ready")); _checkInFlight = false; }
        }

        // Δημόσιο - χρησιμοποιείται ΚΑΙ από το κουμπί "Έλεγχος Τώρα" (Ρυθμίσεις Εμφάνισης), ώστε ο
        // χρήστης να μπορεί να ζητήσει άμεσο έλεγχο χωρίς να περιμένει το επόμενο περιοδικό τικ.
        public static async Task RunCheckAsync(bool announce = false)
        {
            Log($"RunCheckAsync: starting (announce={announce})");
            AppSettingsService.Current.LastUpdateCheckAt = DateTime.Now;
            AppSettingsService.Save();

            try
            {
                var appUpdates = await WingetService.ScanAsync();
                Log($"RunCheckAsync: winget scan found {appUpdates.Count} app update(s)");
                UpdatesHubService.ReportAppScan(appUpdates.Count);
                MaybeNotify(appUpdates.Count,
                    () => AppSettingsService.Current.LastNotifiedAppUpdateCount,
                    v => AppSettingsService.Current.LastNotifiedAppUpdateCount = v,
                    LanguageService.T("UpdateNotify_AppsTitle"),
                    string.Format(LanguageService.T("UpdateNotify_AppsBody"), appUpdates.Count), announce: announce);
            }
            // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη, επαναλαμβανόμενο) - πριν ήταν "catch { }" - μια πραγματική
            // αποτυχία εδώ (π.χ. αν το PATH fallback του WingetService δεν αρκεί σε κάποιο μηχάνημα)
            // ήταν ΑΔΥΝΑΤΟ να εντοπιστεί χωρίς αυτό το log.
            catch (Exception ex) { Log($"RunCheckAsync: winget scan threw - {ex}"); }

            try
            {
                var driverResult = await DriverService.ScanAsync();
                Log($"RunCheckAsync: driver scan found {driverResult.Updates.Count} update(s) (PolicyBlocked={driverResult.PolicyBlocked})");
                if (!driverResult.PolicyBlocked)
                {
                    UpdatesHubService.ReportDriverScan(driverResult.Updates.Count);
                    MaybeNotify(driverResult.Updates.Count,
                        () => AppSettingsService.Current.LastNotifiedDriverUpdateCount,
                        v => AppSettingsService.Current.LastNotifiedDriverUpdateCount = v,
                        LanguageService.T("UpdateNotify_DriversTitle"),
                        string.Format(LanguageService.T("UpdateNotify_DriversBody"), driverResult.Updates.Count), announce: announce);
                }
            }
            catch (Exception ex) { Log($"RunCheckAsync: driver scan threw - {ex}"); }

            // ΝΕΟ - roadmap ιδέα #3 (ρητό αίτημα χρήστη: "κάνε το 3 από τις ιδέες") - βλ.
            // Services/WindowsUpdateService.cs. Ίδιο μοτίβο best-effort/MaybeNotify με τα δύο παραπάνω.
            try
            {
                var (success, winUpdates, _) = await WindowsUpdateService.ScanAsync();
                Log($"RunCheckAsync: Windows Update scan success={success}, found {winUpdates.Count} update(s)");
                if (success)
                {
                    UpdatesHubService.ReportWindowsUpdateScan(winUpdates.Count);
                    MaybeNotify(winUpdates.Count,
                        () => AppSettingsService.Current.LastNotifiedWindowsUpdateCount,
                        v => AppSettingsService.Current.LastNotifiedWindowsUpdateCount = v,
                        LanguageService.T("UpdateNotify_WindowsTitle"),
                        string.Format(LanguageService.T("UpdateNotify_WindowsBody"), winUpdates.Count),
                        // Οι OS ενημερώσεις δεν έχουν δική τους διαχείριση μέσα στην εφαρμογή (σε
                        // αντίθεση με τις εφαρμογές/οδηγούς - καρτέλα Βελτιστοποίηση) - το κλικ πάει
                        // κατευθείαν στις πραγματικές Ρυθμίσεις Windows Update.
                        () => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:windowsupdate") { UseShellExecute = true }); } catch { } },
                        announce);
                }
            }
            catch (Exception ex) { Log($"RunCheckAsync: Windows Update scan threw - {ex}"); }

            AppSettingsService.Save();

            // ΝΕΟ - roadmap ιδέα #4 (ρητό αίτημα χρήστη: "κάνε τα 4-7") - "Ειδοποίηση χαμηλού χώρου
            // δίσκου" - ίδιο περιοδικό tick, ξεχωριστό (χρονικό, όχι αριθμητικό) dedup - βλ. σχόλιο στο
            // AppSettingsService.LastLowDiskNotifyAt.
            try { CheckLowDiskSpace(); } catch (Exception ex) { Log($"RunCheckAsync: low disk check threw - {ex}"); }
            Log("RunCheckAsync: finished");
        }

        private static void MaybeNotify(int count, Func<int?> getLastNotified, Action<int?> setLastNotified, string title, string body, Action? onClick = null, bool announce = false)
        {
            if (count <= 0) { setLastNotified(0); return; }
            // announce=true (έλεγχος κατά την εκκίνηση): ο χρήστης θέλει να δει ΠΑΝΤΑ το μήνυμα όταν ανοίγει η
            // εφαρμογή, ακόμα κι αν ο αριθμός είναι ίδιος με την προηγούμενη ειδοποίηση.
            if (!announce && getLastNotified() == count) { Log($"MaybeNotify: '{title}' skipped, count unchanged ({count}) and announce=false"); return; }
            setLastNotified(count);
            // ΣΗΜΕΙΩΣΗ: αν ΑΥΤΗ η γραμμή καταγράφεται αλλά ο χρήστης δεν βλέπει κανένα toast, η ίδια η
            // εφαρμογή ΚΑΛΕΙ σωστά το API - το Windows (Focus Assist/ρυθμίσεις ειδοποιήσεων ανά
            // εφαρμογή) το καταπνίγει σιωπηλά, εκτός ελέγχου της εφαρμογής.
            Log($"MaybeNotify: showing balloon '{title}' ({body})");
            TrayIconService.ShowNotificationBalloon(title, body, onClick);
        }

        // ΝΕΟ - roadmap ιδέα #4 - ελέγχει ΟΛΟΥΣ τους τοπικούς σταθερούς δίσκους (DriveType.Fixed, ΟΧΙ
        // αφαιρούμενα/δικτυακά - ίδιο φιλτράρισμα λογικής με το SystemView's Αποθηκευτικός Χώρος).
        // Μία μόνο ειδοποίηση ανά κύκλο ελέγχου (return στην πρώτη αντιστοίχιση) - αν ΠΟΛΛΟΙ δίσκοι
        // είναι χαμηλοί ταυτόχρονα, οι υπόλοιποι θα ειδοποιήσουν στον ΕΠΟΜΕΝΟ κύκλο (24ωρο cooldown
        // εφαρμόζεται καθολικά, όχι ανά δίσκο - αποδεκτός συμβιβασμός για ένα "μέτριο" χαρακτηριστικό,
        // αποφεύγει σκάγιασμα πολλαπλών toast στην ίδια στιγμή).
        private static void CheckLowDiskSpace()
        {
            if (!AppSettingsService.Current.LowDiskNotificationsEnabled) return;
            var last = AppSettingsService.Current.LastLowDiskNotifyAt;
            if (last.HasValue && DateTime.Now - last.Value < TimeSpan.FromHours(24)) return;

            var thresholdPercent = Math.Clamp(AppSettingsService.Current.LowDiskThresholdPercent, 1, 50);
            foreach (var drive in System.IO.DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType != System.IO.DriveType.Fixed || drive.TotalSize <= 0) continue;
                var freePercent = (double)drive.AvailableFreeSpace / drive.TotalSize * 100.0;
                if (freePercent >= thresholdPercent) continue;

                AppSettingsService.Current.LastLowDiskNotifyAt = DateTime.Now;
                AppSettingsService.Save();
                var freeGb = drive.AvailableFreeSpace / 1024.0 / 1024 / 1024;
                var driveName = drive.Name.TrimEnd('\\');
                TrayIconService.ShowNotificationBalloon(
                    LanguageService.T("LowDisk_Title"),
                    string.Format(LanguageService.T("LowDisk_Body"), driveName, freeGb.ToString("0.#")),
                    () => TrayIconService.OpenTab("System"));
                return;
            }
        }
    }
}
