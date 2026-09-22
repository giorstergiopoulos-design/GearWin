using System;
using System.Collections.Generic;

namespace OptimizerWpf.Services
{
    // Port του Show-ActionLogWindow / $global:actionLogHistory (Optimizer.ps1 ~9010) - αντί να
    // προστεθεί μια ξεχωριστή κλήση καταγραφής σε ΚΑΘΕ μέθοδο κάθε service (μεγάλη, επαναλαμβανόμενη
    // αλλαγή σε όλο τον κώδικα), γίνεται hook στο ήδη υπάρχον StatusService.Changed - κάθε SetBusy
    // κλήση σε ΟΛΗ την εφαρμογή καταγράφεται αυτόματα με timestamp. Οι SetIdle/"Έτοιμο για χρήση"
    // μεταβάσεις ΔΕΝ καταγράφονται (θόρυβος, όχι ουσιαστικές ενέργειες).
    public static class ActionLogService
    {
        private static readonly List<string> _entries = new();
        public static IReadOnlyList<string> Entries => _entries;
        public static event Action? Changed;

        static ActionLogService()
        {
            StatusService.Changed += (message, isBusy) =>
            {
                if (!isBusy) return;
                _entries.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                Changed?.Invoke();
            };
        }

        // Καλείται μία φορά από το App.xaml.cs OnStartup ώστε ο static constructor να τρέξει από την
        // αρχή της εκκίνησης (χωρίς αυτό, η καταγραφή θα ξεκινούσε μόνο μόλις κάποιος άνοιγε πρώτη
        // φορά το παράθυρο Ιστορικού Ενεργειών - θα έχαναν όλες τις ενέργειες πριν από εκείνη τη στιγμή).
        public static void EnsureStarted() { }
    }
}
