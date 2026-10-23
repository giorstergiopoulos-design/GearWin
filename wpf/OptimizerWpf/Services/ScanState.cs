using System;

namespace OptimizerWpf.Services
{
    // 6.5.3 - ΡΙΖΙΚΗ ΑΙΤΙΑ δύο παραπόνων του χρήστη στους ενημερωτές ("αν αλλάξω καρτέλα πρέπει να ξεκινήσω από την αρχή" και
    // "αν κάνω δύο φορές έλεγχο βλέπω τα ίδια αποτελέσματα δύο φορές"): κάθε αλλαγή καρτέλας/υπο-σελίδας δημιουργεί ΝΕΟ View, και η
    // κατάσταση "σαρώνει τώρα" ζούσε μόνο στο αντικείμενο του παλιού View. Το νέο View έδειχνε το κουμπί ελεύθερο, ο χρήστης
    // ξανάπατούσε σάρωση ενώ η πρώτη έτρεχε ακόμα, και οι δύο σαρώσεις πρόσθεταν τα αποτελέσματά τους στην ίδια (static) λίστα.
    // Η κατάσταση ζει πλέον εδώ (μία static παρουσία ανά λειτουργία): ένα νέο View τη διαβάζει κατά τη δημιουργία του και
    // ακούει το Changed όσο είναι φορτωμένο· το TryBegin επιτρέπει ΜΙΑ σάρωση τη φορά, από όποιο View κι αν πατηθεί το κουμπί.
    public sealed class ScanState
    {
        private readonly object _gate = new();

        public bool Busy { get; private set; }
        public bool Installing { get; private set; }
        /// <summary>Τελευταίο μήνυμα κατάστασης (επιβιώνει της αλλαγής καρτέλας). null = κανένα ακόμα.</summary>
        public string? Status { get; private set; }

        /// <summary>Ενεργοποιείται (στο νήμα που κάλεσε την αλλαγή) όταν αλλάξει η κατάσταση ή το μήνυμα.</summary>
        public event Action? Changed;

        /// <summary>Ξεκινά εργασία. False αν τρέχει ήδη μία (ο καλών πρέπει να επιστρέψει χωρίς να κάνει τίποτα).</summary>
        public bool TryBegin(bool installing = false)
        {
            lock (_gate)
            {
                if (Busy) return false;
                Busy = true;
                Installing = installing;
            }
            Changed?.Invoke();
            return true;
        }

        public void End()
        {
            lock (_gate) { Busy = false; Installing = false; }
            Changed?.Invoke();
        }

        public void SetStatus(string? text)
        {
            lock (_gate) Status = text;
            Changed?.Invoke();
        }

        /// <summary>Μόνο για δοκιμές: επαναφορά σε αρχική κατάσταση.</summary>
        internal void Reset()
        {
            lock (_gate) { Busy = false; Installing = false; Status = null; }
            Changed = null;
        }
    }
}
