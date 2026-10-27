using System;
using System.Collections.Generic;
using System.Globalization;

namespace OptimizerWpf.Services
{
    // Σημασιολογική σύγκριση εκδόσεων (SemVer 2.0): "v6.10.0" > "v6.9.9", "6.6.0-beta.1" < "6.6.0", 4ο τμήμα επιτρέπεται ("1.2.3.4"),
    // το build metadata ("+abc") αγνοείται. Κοινή για την ειδοποίηση νέας έκδοσης, τον κατάλογο της σουίτας και το "μία φορά ανά έκδοση"
    // (OncePerVersionStore) - χωρίς εξαρτήσεις από WPF ώστε να μοιράζεται εύκολα με άλλες εφαρμογές.
    public sealed class ReleaseVersion : IComparable<ReleaseVersion>, IEquatable<ReleaseVersion>
    {
        private readonly int[] _numbers;
        private readonly string[] _pre;

        private ReleaseVersion(int[] numbers, string[] pre)
        {
            _numbers = numbers;
            _pre = pre;
        }

        public int Major => _numbers[0];
        public int Minor => _numbers.Length > 1 ? _numbers[1] : 0;
        public int Patch => _numbers.Length > 2 ? _numbers[2] : 0;
        public bool IsPrerelease => _pre.Length > 0;

        public static bool TryParse(string? text, out ReleaseVersion version)
        {
            version = null!;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var s = text.Trim();
            if (s.Length > 64) return false;
            if (s[0] is 'v' or 'V') s = s[1..];
            var plus = s.IndexOf('+');
            if (plus >= 0) s = s[..plus];
            string[] pre = Array.Empty<string>();
            var dash = s.IndexOf('-');
            if (dash >= 0)
            {
                var tail = s[(dash + 1)..];
                s = s[..dash];
                if (tail.Length == 0) return false;
                pre = tail.Split('.');
                foreach (var p in pre) if (p.Length == 0) return false;
            }
            var parts = s.Split('.');
            if (parts.Length is < 1 or > 4) return false;
            var nums = new int[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0 || parts[i].Length > 9) return false;
                foreach (var ch in parts[i]) if (ch is < '0' or > '9') return false;
                nums[i] = int.Parse(parts[i], CultureInfo.InvariantCulture);
            }
            version = new ReleaseVersion(nums, pre);
            return true;
        }

        public static ReleaseVersion? ParseOrNull(string? text) => TryParse(text, out var v) ? v : null;

        public static ReleaseVersion FromVersion(Version v) => new(new[] { v.Major, v.Minor, Math.Max(0, v.Build) }, Array.Empty<string>());

        public int CompareTo(ReleaseVersion? other)
        {
            if (other is null) return 1;
            var n = Math.Max(_numbers.Length, other._numbers.Length);
            for (var i = 0; i < n; i++)
            {
                var a = i < _numbers.Length ? _numbers[i] : 0;
                var b = i < other._numbers.Length ? other._numbers[i] : 0;
                if (a != b) return a.CompareTo(b);
            }
            // μια έκδοση χωρίς επίθημα είναι μεγαλύτερη από την pre-release της
            if (_pre.Length == 0 && other._pre.Length == 0) return 0;
            if (_pre.Length == 0) return 1;
            if (other._pre.Length == 0) return -1;
            var m = Math.Min(_pre.Length, other._pre.Length);
            for (var i = 0; i < m; i++)
            {
                var c = ComparePreIdentifier(_pre[i], other._pre[i]);
                if (c != 0) return c;
            }
            return _pre.Length.CompareTo(other._pre.Length);
        }

        private static int ComparePreIdentifier(string a, string b)
        {
            var an = long.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out var x);
            var bn = long.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out var y);
            if (an && bn) return x.CompareTo(y);
            if (an) return -1;   // οι αριθμητικοί προηγούνται των αλφαριθμητικών
            if (bn) return 1;
            return string.CompareOrdinal(a, b);
        }

        // Μόνο major.minor (για "εμφάνιση οδηγού μία φορά ανά νέα minor έκδοση").
        public ReleaseVersion MajorMinor => new(new[] { Major, Minor }, Array.Empty<string>());

        public bool Equals(ReleaseVersion? other) => other is not null && CompareTo(other) == 0;
        public override bool Equals(object? obj) => obj is ReleaseVersion v && Equals(v);
        public override int GetHashCode()
        {
            var h = new HashCode();
            var last = _numbers.Length;
            while (last > 1 && _numbers[last - 1] == 0) last--;
            for (var i = 0; i < last; i++) h.Add(_numbers[i]);
            foreach (var p in _pre) h.Add(p);
            return h.ToHashCode();
        }

        public override string ToString() => string.Join('.', _numbers) + (_pre.Length > 0 ? "-" + string.Join('.', _pre) : "");

        public static bool operator >(ReleaseVersion a, ReleaseVersion b) => a.CompareTo(b) > 0;
        public static bool operator <(ReleaseVersion a, ReleaseVersion b) => a.CompareTo(b) < 0;
        public static bool operator >=(ReleaseVersion a, ReleaseVersion b) => a.CompareTo(b) >= 0;
        public static bool operator <=(ReleaseVersion a, ReleaseVersion b) => a.CompareTo(b) <= 0;

        // Βοηθητικό: μη έγκυρη έκδοση ποτέ δεν θεωρείται νεότερη.
        public static bool IsNewer(string? candidate, string? current) =>
            TryParse(candidate, out var c) && TryParse(current, out var r) && c > r;

        public static IEnumerable<ReleaseVersion> SortedDescending(IEnumerable<string> tags)
        {
            var list = new List<ReleaseVersion>();
            foreach (var t in tags) if (TryParse(t, out var v)) list.Add(v);
            list.Sort((a, b) => b.CompareTo(a));
            return list;
        }
    }
}
