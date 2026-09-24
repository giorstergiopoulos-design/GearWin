using System;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - roadmap ιδέα #5 (ρητό αίτημα χρήστη: "κάνε τα 4-7" - "Σύγκριση πριν/μετά σε όλες τις
    // σαρώσεις... πόσος χώρος... κέρδισε ο χρήστης συνολικά, ιστορικό στο Home"). ΣΗΜΕΙΩΣΗ
    // ΕΙΛΙΚΡΙΝΕΙΑΣ σχεδιασμού: "σε ΟΛΕΣ τις σαρώσεις" θα σήμαινε αγγίζοντας δεκάδες σημεία σε όλη την
    // εφαρμογή - αντ' αυτού, ΕΝΑΣ κεντρικός αθροιστικός μετρητής (persisted, βλ.
    // AppSettingsService.TotalBytesFreedAllTime) που τροφοδοτείται από τα σημεία που ΗΔΗ υπολογίζουν
    // πραγματικά bytes ελευθερωμένα (Γρήγορος Καθαρισμός από το tray/HealthCheckWindow, καθαρισμός
    // cache browser) - πραγματικά, ΠΟΤΕ εικασμένα νούμερα, απλά όχι ΚΑΘΕ πιθανή πηγή καθαρισμού.
    public static class ImpactTrackingService
    {
        public static long TotalBytesFreedAllTime => AppSettingsService.Current.TotalBytesFreedAllTime;

        public static event Action? Changed;

        public static void RecordBytesFreed(long bytes)
        {
            if (bytes <= 0) return;
            AppSettingsService.Current.TotalBytesFreedAllTime += bytes;
            AppSettingsService.Save();
            Changed?.Invoke();
        }
    }
}
