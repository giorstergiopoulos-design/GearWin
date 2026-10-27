using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OptimizerWpf.Services
{
    // Μικρό, επαναχρησιμοποιήσιμο αρχείο "τι έχει ήδη δει/ειδοποιηθεί ο χρήστης και για ποια έκδοση" (key -> έκδοση).
    // Χρησιμοποιείται για: τον οδηγό καλωσορίσματος (μία φορά ανά major.minor), την πρόταση της σουίτας (μία φορά) και την
    // ειδοποίηση νέας έκδοσης (μία φορά ανά έκδοση). Ανθεκτικό σε κατεστραμμένο αρχείο (ξεκινά κενό), ατομική εγγραφή, thread-safe.
    public sealed class OncePerVersionStore
    {
        private readonly string _path;
        private readonly object _gate = new();
        private Dictionary<string, string> _data;

        public OncePerVersionStore(string path)
        {
            _path = path;
            _data = Load(path);
        }

        public static string DefaultPath() => Path.Combine(AppDataDir(), "seen-versions.json");

        // Ίδιος κανόνας φακέλου δεδομένων με τις υπόλοιπες υπηρεσίες (τα tests ορίζουν GEARWIN_DATA_DIR).
        public static string AppDataDir() =>
            Environment.GetEnvironmentVariable("GEARWIN_DATA_DIR") is { Length: > 0 } c
                ? c
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf");

        private static Dictionary<string, string> Load(string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 64 * 1024) return new();
                var d = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
                return d ?? new();
            }
            catch { return new(); }
        }

        public string? Get(string key)
        {
            lock (_gate) return _data.TryGetValue(key, out var v) ? v : null;
        }

        // true όταν δεν έχει καταγραφεί τίποτα για το key ή η καταγεγραμμένη έκδοση είναι μικρότερη από την τρέχουσα.
        // majorMinorOnly: οι αλλαγές patch (6.6.0 -> 6.6.1) δεν θεωρούνται "νέα έκδοση".
        public bool ShouldShow(string key, ReleaseVersion current, bool majorMinorOnly = true)
        {
            var seen = ReleaseVersion.ParseOrNull(Get(key));
            if (seen == null) return true;
            return majorMinorOnly ? current.MajorMinor > seen.MajorMinor : current > seen;
        }

        public void MarkSeen(string key, ReleaseVersion version)
        {
            lock (_gate)
            {
                // δεν πάμε ποτέ προς τα πίσω (π.χ. downgrade ή παλαιότερο tag)
                var old = ReleaseVersion.ParseOrNull(_data.TryGetValue(key, out var o) ? o : null);
                if (old != null && old >= version) return;
                _data[key] = version.ToString();
                Save();
            }
        }

        // Ελεύθερο κείμενο (π.χ. ETag) στο ίδιο αρχείο - χωρίς έλεγχο έκδοσης.
        public void SetRaw(string key, string? value)
        {
            lock (_gate)
            {
                if (value == null) { if (_data.Remove(key)) Save(); return; }
                if (_data.TryGetValue(key, out var o) && o == value) return;
                _data[key] = value;
                Save();
            }
        }

        public void Forget(string key)
        {
            lock (_gate) { if (_data.Remove(key)) Save(); }
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var tmp = _path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(_data));
                File.Move(tmp, _path, overwrite: true);
            }
            catch { /* η αποτυχία αποθήκευσης δεν πρέπει ποτέ να ρίξει την εφαρμογή· χειρότερη περίπτωση: ο οδηγός ξαναεμφανίζεται */ }
        }
    }
}
