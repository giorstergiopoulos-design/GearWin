using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // ΝΕΟ (6.1.0) - ενιαία λίστα αλλαγών με αναίρεση: κάθε αλλαγή ρύθμισης που κάνει η εφαρμογή
    // (π.χ. διακόπτης Tweaks, εισαγωγή προφίλ) καταγράφεται εδώ μαζί με την αντίθετη ενέργεια, ώστε ο
    // χρήστης να τη γυρίσει με ένα κλικ χωρίς να ψάχνει ποιο tweak ήταν. Μόνο στη μνήμη (οι ενέργειες
    // αναίρεσης είναι closures) - ισχύει για την τρέχουσα συνεδρία. Το ActionLogService μένει το
    // "τι έτρεξε", αυτό είναι το "τι άλλαξε και μπορεί να αναιρεθεί".
    public class ChangeEntry
    {
        public ChangeEntry(string description, Func<Task>? undo)
        {
            Description = description;
            UndoAction = undo;
            Time = DateTime.Now;
        }

        public DateTime Time { get; }
        public string Description { get; }
        public bool IsUndone { get; internal set; }
        public bool CanUndo => UndoAction != null && !IsUndone;
        internal Func<Task>? UndoAction { get; }
        public override string ToString() => $"[{Time:HH:mm:ss}] {Description}{(IsUndone ? " ↩" : "")}";
    }

    public static class ChangeJournalService
    {
        private const int MaxEntries = 200;
        private static readonly List<ChangeEntry> _entries = new();
        private static readonly object _gate = new();

        public static event Action? Changed;

        // Νεότερο πρώτο.
        public static IReadOnlyList<ChangeEntry> Entries
        {
            get { lock (_gate) return _entries.AsEnumerable().Reverse().ToList(); }
        }

        public static ChangeEntry Record(string description, Action? undo) =>
            Record(description, undo == null ? null : () => { undo(); return Task.CompletedTask; });

        public static ChangeEntry Record(string description, Func<Task>? undo)
        {
            var entry = new ChangeEntry(description, undo);
            lock (_gate)
            {
                _entries.Add(entry);
                if (_entries.Count > MaxEntries) _entries.RemoveAt(0);
            }
            Changed?.Invoke();
            return entry;
        }

        // true αν η αναίρεση ολοκληρώθηκε χωρίς σφάλμα. Η ίδια η αναίρεση ΔΕΝ καταγράφεται ως νέα αλλαγή.
        public static async Task<bool> UndoAsync(ChangeEntry entry)
        {
            if (!entry.CanUndo) return false;
            try { await entry.UndoAction!(); }
            catch { return false; }
            entry.IsUndone = true;
            Changed?.Invoke();
            return true;
        }

        public static void Clear()
        {
            lock (_gate) _entries.Clear();
            Changed?.Invoke();
        }
    }
}
