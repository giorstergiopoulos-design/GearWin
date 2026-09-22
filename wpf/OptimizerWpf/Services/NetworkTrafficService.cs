using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace OptimizerWpf.Services
{
    public record ProcessNetworkUsage(int Pid, string ProcessName, double DownloadKBps, double UploadKBps);

    // ΝΕΟ - roadmap "Κίνηση δικτύου ανά εφαρμογή" (v4.3.5). Δεν υπάρχει καμία WMI/perf-counter κλάση
    // στα Windows που να εκθέτει bytes-ανά-διεργασία (το ΙΔΙΟ API που χρησιμοποιεί εσωτερικά η στήλη
    // "Δίκτυο" της Διαχείρισης Εργασιών) - το μόνο επίσημα τεκμηριωμένο μονοπάτι είναι ETW (Event
    // Tracing for Windows), μέσω του "NT Kernel Logger"'s Network TCP/IP provider. Το TraceEvent
    // (Microsoft.Diagnostics.Tracing.TraceEvent, MIT, dotnet org) είναι η καθιερωμένη βιβλιοθήκη γι'
    // αυτό - καταναλώνει ΑΠΕΥΘΕΙΑΣ τα ίδια kernel events, καμία custom driver/hook.
    //
    // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ (ίδιο πνεύμα με το FileShredderService's SSD wear-leveling note και το
    // AdvancedToolsService's autologon plaintext note): το "NT Kernel Logger" είναι ΕΝΑ ΜΟΝΑΔΙΚΟ,
    // system-wide reserved ETW session name - αν κάποιο ΑΛΛΟ εργαλείο (π.χ. Windows Performance
    // Recorder, ένας profiler) το χρησιμοποιεί ήδη, το Start() εδώ θα το ΚΛΕΙΣΕΙ και θα το ανοίξει
    // ξανά δικό του (καθιερωμένη συμπεριφορά του TraceEventSession όταν βρει ήδη ενεργό session με
    // το ίδιο όνομα) - ΔΕΝ υπάρχει τρόπος να «μοιραστεί» το session με άλλον καταναλωτή. Απαιτεί
    // Administrator (η εφαρμογή το έχει ήδη, app.manifest). Αν η δημιουργία του session αποτύχει για
    // οποιονδήποτε λόγο, το IsRunning παραμένει false και το UI δείχνει ρητά "μη διαθέσιμο" αντί να
    // προσποιείται ότι λειτουργεί.
    public static class NetworkTrafficService
    {
        private sealed class Counter { public long Sent; public long Recv; }

        private static readonly object Lock = new();
        private static readonly ConcurrentDictionary<int, Counter> Accum = new();
        private static TraceEventSession? _session;
        private static Thread? _thread;

        public static bool IsRunning { get; private set; }
        public static string? LastError { get; private set; }

        public static bool Start()
        {
            lock (Lock)
            {
                if (IsRunning) return true;
                Accum.Clear();
                try
                {
                    var session = new TraceEventSession(KernelTraceEventParser.KernelSessionName, TraceEventSessionOptions.Create)
                    {
                        StopOnDispose = true,
                    };
                    session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP, KernelTraceEventParser.Keywords.None);

                    session.Source.Kernel.TcpIpSend += d => AddBytes(d.ProcessID, d.size, sent: true);
                    session.Source.Kernel.TcpIpRecv += d => AddBytes(d.ProcessID, d.size, sent: false);
                    session.Source.Kernel.TcpIpSendIPV6 += d => AddBytes(d.ProcessID, d.size, sent: true);
                    session.Source.Kernel.TcpIpRecvIPV6 += d => AddBytes(d.ProcessID, d.size, sent: false);
                    session.Source.Kernel.UdpIpSend += d => AddBytes(d.ProcessID, d.size, sent: true);
                    session.Source.Kernel.UdpIpRecv += d => AddBytes(d.ProcessID, d.size, sent: false);
                    session.Source.Kernel.UdpIpSendIPV6 += d => AddBytes(d.ProcessID, d.size, sent: true);
                    session.Source.Kernel.UdpIpRecvIPV6 += d => AddBytes(d.ProcessID, d.size, sent: false);

                    _session = session;
                    // Το Source.Process() μπλοκάρει μέχρι να σταματήσει το session - ΔΙΚΟ του, μη-
                    // threadpool νήμα (IsBackground ώστε να μην εμποδίζει το κλείσιμο της εφαρμογής
                    // αν το Stop() δεν προλάβει να κληθεί για οποιονδήποτε λόγο).
                    _thread = new Thread(() => { try { session.Source.Process(); } catch { } })
                    {
                        IsBackground = true,
                        Name = "NetworkTrafficEtw",
                    };
                    _thread.Start();

                    IsRunning = true;
                    LastError = null;
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    try { _session?.Dispose(); } catch { /* best effort */ }
                    _session = null;
                    IsRunning = false;
                    return false;
                }
            }
        }

        public static void Stop()
        {
            lock (Lock)
            {
                if (!IsRunning) return;
                try { _session?.Stop(true); } catch { /* best effort */ }
                try { _session?.Dispose(); } catch { /* best effort */ }
                _session = null;
                _thread = null;
                IsRunning = false;
                Accum.Clear();
            }
        }

        private static void AddBytes(int pid, int size, bool sent)
        {
            if (size <= 0) return;
            var counter = Accum.GetOrAdd(pid, _ => new Counter());
            if (sent) Interlocked.Add(ref counter.Sent, size);
            else Interlocked.Add(ref counter.Recv, size);
        }

        // Καλείται περιοδικά από το UI (π.χ. κάθε 1s) - διαβάζει ΚΑΙ μηδενίζει τους μετρητές κάθε
        // διεργασίας, ώστε η επόμενη κλήση να αντιπροσωπεύει μόνο το επόμενο χρονικό παράθυρο
        // (ρυθμός, όχι αθροιστικό σύνολο από την αρχή της σάρωσης).
        public static System.Collections.Generic.IReadOnlyList<ProcessNetworkUsage> SampleAndReset(double windowSeconds)
        {
            var result = new System.Collections.Generic.List<ProcessNetworkUsage>();
            if (windowSeconds <= 0) return result;

            foreach (var pid in Accum.Keys.ToArray())
            {
                if (!Accum.TryGetValue(pid, out var counter)) continue;
                var sent = Interlocked.Exchange(ref counter.Sent, 0);
                var recv = Interlocked.Exchange(ref counter.Recv, 0);
                if (sent == 0 && recv == 0) continue;

                result.Add(new ProcessNetworkUsage(pid, ResolveProcessName(pid), BytesToKBps(recv, windowSeconds), BytesToKBps(sent, windowSeconds)));
            }

            return result.OrderByDescending(r => r.DownloadKBps + r.UploadKBps).ToList();
        }

        // Καθαρή, testable μαθηματική λογική - ξεχωρισμένη από το I/O (process lookup) παραπάνω.
        internal static double BytesToKBps(long bytes, double windowSeconds) =>
            windowSeconds <= 0 ? 0 : bytes / 1024.0 / windowSeconds;

        private static string ResolveProcessName(int pid)
        {
            if (pid == 0) return "System Idle Process";
            try { return Process.GetProcessById(pid).ProcessName; }
            catch { return $"PID {pid}"; } // η διεργασία μπορεί να έχει ήδη τερματίσει
        }
    }
}
