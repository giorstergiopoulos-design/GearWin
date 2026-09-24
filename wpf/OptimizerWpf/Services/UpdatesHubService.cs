using System;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - roadmap "ενοποίηση Ενημερώσεων" (ρητό αίτημα χρήστη) - οι ενημερώσεις drivers/εφαρμογών/
    // Windows Update ζούσαν σε 3 ασύνδετα σημεία (Βελτιστοποίηση x2, Προχωρημένα Εργαλεία) χωρίς καμία
    // συνολική εικόνα. Αυτό ΔΕΝ αντικαθιστά καμία από τις 3 υπάρχουσες οθόνες σάρωσης (παραμένουν όπως
    // ήταν) - είναι απλώς μια κοινόχρηστη, session-lifetime cache των ΤΕΛΕΥΤΑΙΩΝ γνωστών αποτελεσμάτων,
    // ώστε η Αρχική να δείχνει ένα άμεσο στιγμιότυπο ("3 drivers, 7 εφαρμογές") χωρίς να χρειάζεται να
    // ξανατρέξει τις (αργές) σαρώσεις μόνη της. Null = "δεν έχει τρέξει ακόμα σάρωση σε αυτή τη
    // συνεδρία" (εμφανίζεται ρητά ως "—", ΟΧΙ ψευδές 0 - ίδιο πνεύμα ειλικρίνειας με το
    // NetworkTrafficService's "άγνωστο" αντί για ψεύτικη τιμή).
    public static class UpdatesHubService
    {
        public static int? DriverUpdatesAvailable { get; private set; }
        public static int? AppUpdatesAvailable { get; private set; }
        // ΝΕΟ - roadmap ιδέα #3 (ρητό αίτημα χρήστη) - βλ. Services/WindowsUpdateService.cs.
        public static int? WindowsUpdatesAvailable { get; private set; }

        public static event Action? Changed;

        public static void ReportDriverScan(int count)
        {
            DriverUpdatesAvailable = count;
            Changed?.Invoke();
        }

        public static void ReportAppScan(int count)
        {
            AppUpdatesAvailable = count;
            Changed?.Invoke();
        }

        public static void ReportWindowsUpdateScan(int count)
        {
            WindowsUpdatesAvailable = count;
            Changed?.Invoke();
        }
    }
}
