using System;
using System.Collections.Generic;
using System.Linq;

namespace OptimizerWpf.Services
{
    // Global, view-agnostic "what's happening right now" signal for the status bar (ρητό αίτημα
    // χρήστη: το κυκλικό εικονίδιο φόρτωσης της WinForms έκδοσης / $global:activitySpinner δεν είχε
    // περάσει στο WPF shell). Any view/service can call SetBusy/SetIdle - MainWindow is the only
    // subscriber and owns the actual spinner animation + status text (see MainWindow.xaml.cs).
    // Not tied to any specific async operation - purely a shared broadcast so unrelated tabs
    // (HomeView, OptimizationView, ...) can all drive the same status bar without knowing about
    // each other or about MainWindow.
    //
    // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "όταν δύο ή περισσότερες εργασίες εκτελούνται ταυτόχρονα να
    // φαίνονται στη γραμμή κατάστασης") - πριν ήταν ΕΝΤΕΛΩΣ stateless (SetBusy απλά προωθούσε το νέο
    // μήνυμα, ΧΩΡΙΣ καμία μνήμη ότι υπήρχε ήδη μια προηγούμενη ενεργή εργασία) - πραγματικό bug:
    // Α ξεκινά (SetBusy "Σάρωση Α..."), Β ξεκινά (SetBusy "Σάρωση Β..." - ΣΒΗΝΕΙ το μήνυμα του Α από
    // την οθόνη), Β τελειώνει (SetIdle "Έτοιμο" - η γραμμή δείχνει "Έτοιμο" ΕΝΩ το Α ΑΚΟΜΑ τρέχει
    // στο παρασκήνιο). Τώρα: μια απλή λίστα ενεργών μηνυμάτων - SetBusy προσθέτει, SetIdle αφαιρεί ΕΝΑ
    // (FIFO - δεν χρειάζεται να ταιριάζει ακριβώς με ΠΟΙΟ SetBusy αντιστοιχεί, αφού η ήδη καθιερωμένη
    // σύμβαση σε ΟΛΗ την εφαρμογή είναι ΠΑΝΤΑ ζευγάρι SetBusy->await->SetIdle σε κάθε μέθοδο - το
    // ΠΛΗΘΟΣ παραμένει πάντα σωστό, και το ΠΕΡΙΕΧΟΜΕΝΟ της λίστας ανά πάσα στιγμή αντιπροσωπεύει
    // ακριβώς τις εργασίες που όντως δεν έχουν ακόμα τελειώσει). ΚΑΜΙΑ αλλαγή χρειάστηκε σε κανένα από
    // τα 100+ ήδη υπάρχοντα σημεία κλήσης SetBusy/SetIdle σε όλη την εφαρμογή - ίδια υπογραφή μεθόδων.
    public static class StatusService
    {
        public static event Action<string, bool>? Changed;

        public sealed class Entry
        {
            public string Message = "";
            public readonly DateTime Started = DateTime.UtcNow;
        }

        // 6.9.0: μια εργασία που ξεχάστηκε να κλείσει (SetBusy χωρίς SetIdle) δεν πρέπει να «παγώνει» τη γραμμή κατάστασης για πάντα - οι εγγραφές
        // παλαιότερες από StaleAfter αγνοούνται. Οι νέες εργασίες με πολλά βήματα χρησιμοποιούν BeginScope (ένα μήνυμα που ενημερώνεται και κλείνει μία φορά).
        public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(3);

        private static readonly List<Entry> _active = new();
        private static readonly object _lock = new();

        private static void Prune() => _active.RemoveAll(e => DateTime.UtcNow - e.Started > StaleAfter);

        public static void SetBusy(string message)
        {
            string combined;
            lock (_lock)
            {
                Prune();
                _active.Add(new Entry { Message = message });
                combined = Combine();
            }
            Changed?.Invoke(combined, true);
        }

        public static void SetIdle(string message)
        {
            string display; bool stillBusy;
            lock (_lock)
            {
                Prune();
                if (_active.Count > 0) _active.RemoveAt(0);
                stillBusy = _active.Count > 0;
                display = stillBusy ? Combine() : message;
            }
            Changed?.Invoke(display, stillBusy);
        }

        public static int ActiveCount { get { lock (_lock) { Prune(); return _active.Count; } } }

        // Εργασία πολλών βημάτων: Update() αλλάζει το μήνυμά της, End(τελικό μήνυμα) την κλείνει ΑΚΡΙΒΩΣ μία φορά (ασφαλές να κληθεί πολλές).
        public sealed class Scope : IDisposable
        {
            private readonly Entry _entry;
            private bool _ended;
            internal Scope(Entry e) { _entry = e; }

            public void Update(string message)
            {
                string combined;
                lock (_lock) { if (_ended) return; _entry.Message = message; combined = Combine(); }
                Changed?.Invoke(combined, true);
            }

            public void End(string finalMessage)
            {
                string display; bool stillBusy;
                lock (_lock)
                {
                    if (_ended) return;
                    _ended = true;
                    _active.Remove(_entry);
                    Prune();
                    stillBusy = _active.Count > 0;
                    display = stillBusy ? Combine() : finalMessage;
                }
                Changed?.Invoke(display, stillBusy);
            }

            public void Dispose() => End("");
        }

        public static Scope BeginScope(string message)
        {
            Entry e;
            string combined;
            lock (_lock) { Prune(); e = new Entry { Message = message }; _active.Add(e); combined = Combine(); }
            Changed?.Invoke(combined, true);
            return new Scope(e);
        }

        // 2+ ταυτόχρονες εργασίες εμφανίζονται μαζί, χωρισμένες με "  •  ".
        private static string Combine() => string.Join("  •  ", _active.Select(e => e.Message).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct());
    }
}
