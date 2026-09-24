using System;
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
            try { await RunCheckAsync(); }
            finally { _checkInFlight = false; }
        }

        // Δημόσιο - χρησιμοποιείται ΚΑΙ από το κουμπί "Έλεγχος Τώρα" (Ρυθμίσεις Εμφάνισης), ώστε ο
        // χρήστης να μπορεί να ζητήσει άμεσο έλεγχο χωρίς να περιμένει το επόμενο περιοδικό τικ.
        public static async Task RunCheckAsync()
        {
            AppSettingsService.Current.LastUpdateCheckAt = DateTime.Now;
            AppSettingsService.Save();

            try
            {
                var appUpdates = await WingetService.ScanAsync();
                UpdatesHubService.ReportAppScan(appUpdates.Count);
                MaybeNotify(appUpdates.Count,
                    () => AppSettingsService.Current.LastNotifiedAppUpdateCount,
                    v => AppSettingsService.Current.LastNotifiedAppUpdateCount = v,
                    LanguageService.T("UpdateNotify_AppsTitle"),
                    string.Format(LanguageService.T("UpdateNotify_AppsBody"), appUpdates.Count));
            }
            catch { /* best-effort - ίδια ανοχή με τις υπόλοιπες background σαρώσεις της εφαρμογής */ }

            try
            {
                var driverResult = await DriverService.ScanAsync();
                if (!driverResult.PolicyBlocked)
                {
                    UpdatesHubService.ReportDriverScan(driverResult.Updates.Count);
                    MaybeNotify(driverResult.Updates.Count,
                        () => AppSettingsService.Current.LastNotifiedDriverUpdateCount,
                        v => AppSettingsService.Current.LastNotifiedDriverUpdateCount = v,
                        LanguageService.T("UpdateNotify_DriversTitle"),
                        string.Format(LanguageService.T("UpdateNotify_DriversBody"), driverResult.Updates.Count));
                }
            }
            catch { }

            // ΝΕΟ - roadmap ιδέα #3 (ρητό αίτημα χρήστη: "κάνε το 3 από τις ιδέες") - βλ.
            // Services/WindowsUpdateService.cs. Ίδιο μοτίβο best-effort/MaybeNotify με τα δύο παραπάνω.
            try
            {
                var (success, winUpdates, _) = await WindowsUpdateService.ScanAsync();
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
                        () => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:windowsupdate") { UseShellExecute = true }); } catch { } });
                }
            }
            catch { }

            AppSettingsService.Save();

            // ΝΕΟ - roadmap ιδέα #4 (ρητό αίτημα χρήστη: "κάνε τα 4-7") - "Ειδοποίηση χαμηλού χώρου
            // δίσκου" - ίδιο περιοδικό tick, ξεχωριστό (χρονικό, όχι αριθμητικό) dedup - βλ. σχόλιο στο
            // AppSettingsService.LastLowDiskNotifyAt.
            try { CheckLowDiskSpace(); } catch { }
        }

        private static void MaybeNotify(int count, Func<int?> getLastNotified, Action<int?> setLastNotified, string title, string body, Action? onClick = null)
        {
            if (count <= 0) { setLastNotified(0); return; }
            if (getLastNotified() == count) return; // ίδιος αριθμός με την τελευταία ειδοποίηση - καμία επανάληψη
            setLastNotified(count);
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
