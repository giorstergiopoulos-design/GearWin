using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public enum TimelineKind { Bsod, Unexpected, AppCrash, Install, Driver }

    public sealed record TimelineEntry(DateTime Time, TimelineKind Kind, string Text)
    {
        public bool IsProblem => Kind is TimelineKind.Bsod or TimelineKind.Unexpected or TimelineKind.AppCrash;
        // Για προβλήματα: η πιο πρόσφατη εγκατάσταση/driver που προηγήθηκε μέσα στο παράθυρο συσχέτισης.
        public TimelineEntry? Suspect { get; init; }
    }

    // REQ-590-04 - χρονολόγιο υγείας: crashes/BSOD/απρόσμενοι τερματισμοί (Event Log) συσχετισμένα με πρόσφατες
    // εγκαταστάσεις εφαρμογών/drivers. Μόνο ανάγνωση τοπικών logs - τίποτα δεν στέλνεται και τίποτα δεν αλλάζει.
    public static class HealthTimelineService
    {
        public static readonly TimeSpan CorrelationWindow = TimeSpan.FromHours(48);

        // Καθαρή λογική (testable): συνδέει κάθε πρόβλημα με την πιο πρόσφατη εγκατάσταση που προηγήθηκε εντός του παραθύρου.
        public static List<TimelineEntry> Correlate(IEnumerable<TimelineEntry> events, TimeSpan? window = null)
        {
            var win = window ?? CorrelationWindow;
            var all = events.OrderByDescending(e => e.Time).ToList();
            var installs = all.Where(e => !e.IsProblem).ToList();
            var result = new List<TimelineEntry>(all.Count);
            foreach (var e in all)
            {
                if (e.IsProblem && e.Kind != TimelineKind.AppCrash)
                {
                    var s = installs.Where(i => i.Time <= e.Time && e.Time - i.Time <= win).OrderByDescending(i => i.Time).FirstOrDefault();
                    result.Add(s == null ? e : e with { Suspect = s });
                }
                else result.Add(e);
            }
            return result;
        }

        public static Task<List<TimelineEntry>> ScanAsync(int days = 30, int max = 400) => Task.Run(() =>
        {
            var found = new List<TimelineEntry>();
            Query("System", "(EventID=41 or EventID=6008 or EventID=1001 or EventID=20001)", days, found, max);
            Query("Application", "(EventID=1000 or EventID=11707)", days, found, max);
            return Correlate(found).Take(max).ToList();
        });

        private static void Query(string log, string idFilter, int days, List<TimelineEntry> into, int max)
        {
            try
            {
                var ms = (long)TimeSpan.FromDays(days).TotalMilliseconds;
                var xpath = $"*[System[{idFilter} and TimeCreated[timediff(@SystemTime) <= {ms}]]]";
                var q = new System.Diagnostics.Eventing.Reader.EventLogQuery(log, System.Diagnostics.Eventing.Reader.PathType.LogName, xpath) { ReverseDirection = true };
                using var reader = new System.Diagnostics.Eventing.Reader.EventLogReader(q);
                for (var ev = reader.ReadEvent(); ev != null && into.Count < max * 2; ev = reader.ReadEvent())
                {
                    using (ev)
                    {
                        var entry = Map(ev);
                        if (entry != null) into.Add(entry);
                    }
                }
            }
            catch { /* log μη προσβάσιμο (δικαιώματα/απενεργοποιημένο) - απλά δεν προστίθενται γεγονότα */ }
        }

        private static TimelineEntry? Map(System.Diagnostics.Eventing.Reader.EventRecord ev)
        {
            var time = ev.TimeCreated ?? DateTime.MinValue;
            var provider = ev.ProviderName ?? "";
            string Desc()
            {
                try { var d = ev.FormatDescription() ?? ""; var nl = d.IndexOf('\n'); d = (nl > 0 ? d[..nl] : d).Trim(); return d.Length > 140 ? d[..140] + "…" : d; }
                catch { return ""; }
            }
            switch (ev.Id)
            {
                case 1001 when provider.Contains("WER-SystemErrorReporting", StringComparison.OrdinalIgnoreCase): return new(time, TimelineKind.Bsod, Desc());
                case 41 when provider.Contains("Kernel-Power", StringComparison.OrdinalIgnoreCase): return new(time, TimelineKind.Unexpected, "");
                case 6008 when provider.Equals("EventLog", StringComparison.OrdinalIgnoreCase): return new(time, TimelineKind.Unexpected, "");
                case 1000 when provider.Equals("Application Error", StringComparison.OrdinalIgnoreCase):
                    return new(time, TimelineKind.AppCrash, ev.Properties.Count > 0 ? ev.Properties[0].Value?.ToString() ?? "" : "");
                case 11707 when provider.Equals("MsiInstaller", StringComparison.OrdinalIgnoreCase): return new(time, TimelineKind.Install, Desc());
                case 20001 when provider.Contains("UserPnp", StringComparison.OrdinalIgnoreCase): return new(time, TimelineKind.Driver, Desc());
                default: return null;
            }
        }
    }
}
