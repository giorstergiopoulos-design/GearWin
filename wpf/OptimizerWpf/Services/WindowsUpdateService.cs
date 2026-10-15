using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public record WindowsUpdateItem(
        string Title, string? KbArticleId, bool IsDownloaded,
        string UpdateId = "", int Revision = 0, bool IsOptional = false, string Category = "", string? Severity = null)
    {
        // Ίδιο αναγνωριστικό γραμμής με το WMT 6.7 ("UpdateID|Revision").
        public string RowId => $"{UpdateId}|{Revision}";
    }

    public record WindowsUpdateScanResult(bool Success, IReadOnlyList<WindowsUpdateItem> Updates, int PendingRebootCount, string? Error);

    // Windows Update Agent μέσω του επίσημου COM API (Microsoft.Update.Session) - ίδιος μηχανισμός με το WMT 6.7
    // (reference/WMT-GUI.ps1 ~47407-47600 σάρωση, ~41161-41290 εγκατάσταση):
    //  * κριτήριο "IsInstalled=0 and IsHidden=0" και searcher.Online=true (πριν: ΜΟΝΟ Type='Software', χωρίς ρητό Online)
    //  * ενημερώσεις που ΕΧΟΥΝ ήδη εγκατασταθεί από την τελευταία εκκίνηση και περιμένουν επανεκκίνηση ΔΕΝ μετρούν ως
    //    "διαθέσιμες" (ιστορικό WUA, Operation=1/ResultCode=2) - μετρούνται χωριστά ως PendingRebootCount
    //  * προαιρετικές (BrowseOnly) ενημερώσεις επισημαίνονται ως IsOptional
    //  * οι ενημερώσεις οδηγών (κατηγορία Drivers) αφήνονται στη σάρωση οδηγών (DriverService) για να μην μετριούνται δύο φορές
    // Η σάρωση ΔΕΝ κατεβάζει/εγκαθιστά τίποτα· το InstallAsync το κάνει μόνο όταν ο χρήστης το ζητήσει ρητά.
    public static class WindowsUpdateService
    {
        public static Task<(bool Success, IReadOnlyList<WindowsUpdateItem> Updates, string? Error)> ScanAsync() =>
            ScanDetailedAsync().ContinueWith(t => (t.Result.Success, t.Result.Updates, t.Result.Error));

        public static Task<WindowsUpdateScanResult> ScanDetailedAsync() => Task.Run(() =>
        {
            try
            {
                var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                if (sessionType == null) return Fail("Microsoft.Update.Session unavailable");

                dynamic session = Activator.CreateInstance(sessionType)!;
                dynamic searcher = session.CreateUpdateSearcher();
                try { searcher.Online = true; } catch { }
                dynamic result = searcher.Search("IsInstalled=0 and IsHidden=0");

                var completed = SuccessfulInstallIdsSinceBoot(searcher);
                var list = new List<WindowsUpdateItem>();
                var pendingReboot = 0;
                foreach (dynamic update in result.Updates)
                {
                    string title = ((string)update.Title ?? "").Trim();
                    if (title.Length == 0) title = "Windows Update";
                    string updateId = "";
                    int revision = 0;
                    try { updateId = ((string)update.Identity.UpdateID).Trim(); revision = (int)update.Identity.RevisionNumber; } catch { }
                    if (updateId.Length == 0) updateId = title;

                    if (completed.Contains($"{updateId}|{revision}")) { pendingReboot++; continue; }

                    var category = CategoryText(update);
                    if (category.Contains("driver", StringComparison.OrdinalIgnoreCase)) continue;

                    string? kb = null;
                    try { if (update.KBArticleIDs.Count > 0) kb = "KB" + update.KBArticleIDs[0]; } catch { }
                    string? severity = null;
                    try { var s = ((string)update.MsrcSeverity ?? "").Trim(); if (s.Length > 0) severity = s; } catch { }
                    var optional = false;
                    try { if ((bool)update.BrowseOnly) optional = true; } catch { }
                    var downloaded = false;
                    try { downloaded = (bool)update.IsDownloaded; } catch { }

                    list.Add(new WindowsUpdateItem(title, kb, downloaded, updateId, revision, optional, category, severity));
                }
                return new WindowsUpdateScanResult(true, list, pendingReboot, null);
            }
            catch (Exception ex)
            {
                // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: μπορεί να αποτύχει όταν η υπηρεσία wuauserv είναι απενεργοποιημένη ή διαχειρίζεται από
                // πολιτική (WSUS/Group Policy) - best-effort, ίδια ανοχή με τις υπόλοιπες σαρώσεις.
                return Fail(ex.Message);
            }

            static WindowsUpdateScanResult Fail(string error) =>
                new(false, Array.Empty<WindowsUpdateItem>(), 0, error);
        });

        private static string CategoryText(dynamic update)
        {
            var names = new List<string>();
            try
            {
                for (var i = 0; i < (int)update.Categories.Count; i++)
                {
                    var n = ((string)update.Categories.Item(i).Name ?? "").Trim();
                    if (n.Length > 0 && !names.Contains(n)) names.Add(n);
                }
            }
            catch { }
            return string.Join(", ", names);
        }

        // Ίδια λογική με το Get-WmtWuSuccessfulInstallIdsSinceBoot: ενημερώσεις που εγκαταστάθηκαν επιτυχώς μετά την τελευταία
        // εκκίνηση συνεχίζουν να εμφανίζονται ως IsInstalled=0 μέχρι την επανεκκίνηση - δεν είναι "νέες διαθέσιμες".
        private static HashSet<string> SuccessfulInstallIdsSinceBoot(dynamic searcher)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var lastBootUtc = LastBootUtc();
                int total = (int)searcher.GetTotalHistoryCount();
                var take = Math.Min(1000, total);
                if (take <= 0) return ids;
                dynamic history = searcher.QueryHistory(0, take);
                for (var i = 0; i < (int)history.Count; i++)
                {
                    dynamic entry = history.Item(i);
                    if (entry == null) continue;
                    DateTime date;
                    try { date = ((DateTime)entry.Date).ToUniversalTime(); } catch { continue; }
                    if (date < lastBootUtc) continue;
                    int operation = -1, resultCode = -1;
                    try { operation = (int)entry.Operation; resultCode = (int)entry.ResultCode; } catch { }
                    if (operation != 1 || resultCode != 2) continue;
                    try { ids.Add($"{((string)entry.UpdateIdentity.UpdateID).Trim()}|{(int)entry.UpdateIdentity.RevisionNumber}"); } catch { }
                }
            }
            catch { /* το ιστορικό είναι βοηθητικό - αποτυχία δεν πρέπει να χαλάσει την ίδια τη σάρωση */ }
            return ids;
        }

        private static DateTime LastBootUtc()
        {
            try
            {
                using var s = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
                foreach (ManagementObject o in s.Get())
                    return ManagementDateTimeConverter.ToDateTime((string)o["LastBootUpTime"]).ToUniversalTime();
            }
            catch { }
            return DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
        }

        // Port του Invoke-WmtWindowsUpdateInstall: εύρεση της ενημέρωσης με το ίδιο UpdateID|Revision, αποδοχή EULA, λήψη, εγκατάσταση.
        // ResultCode WUA: 2 = επιτυχία, 3 = επιτυχία με σφάλματα. Εκτελείται ΜΟΝΟ μετά από ρητή επιλογή του χρήστη.
        public static Task<(bool Ok, bool RebootRequired, string? Error)> InstallAsync(string rowId) => Task.Run<(bool Ok, bool RebootRequired, string? Error)>(() =>
        {
            try
            {
                var parts = rowId.Split('|');
                if (parts.Length != 2 || !int.TryParse(parts[1], out var revision)) return (false, false, "Invalid update id");

                dynamic session = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.Session")!)!;
                dynamic searcher = session.CreateUpdateSearcher();
                try { searcher.Online = true; } catch { }
                dynamic result = searcher.Search("IsInstalled=0 and IsHidden=0");

                dynamic? target = null;
                foreach (dynamic u in result.Updates)
                {
                    try
                    {
                        if (string.Equals((string)u.Identity.UpdateID, parts[0], StringComparison.OrdinalIgnoreCase) && (int)u.Identity.RevisionNumber == revision)
                        { target = u; break; }
                    }
                    catch { }
                }
                if (target == null) return (false, false, "Update no longer available");

                try { if (!(bool)target.EulaAccepted) target.AcceptEula(); } catch { }

                dynamic coll = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.UpdateColl")!)!;
                coll.Add(target);

                if (!(bool)target.IsDownloaded)
                {
                    dynamic downloader = session.CreateUpdateDownloader();
                    downloader.Updates = coll;
                    dynamic dres = downloader.Download();
                    int dcode = (int)dres.ResultCode;
                    if (dcode != 2 && dcode != 3) return (false, false, $"Download failed (code {dcode}, 0x{(int)dres.HResult:X8})");
                }

                dynamic installer = session.CreateUpdateInstaller();
                installer.Updates = coll;
                try { installer.AllowSourcePrompts = false; } catch { }
                try { installer.ForceQuiet = true; } catch { }
                dynamic ires = installer.Install();
                int code = (int)ires.ResultCode;
                var reboot = false;
                try { reboot = (bool)ires.RebootRequired; } catch { }
                if (code == 2 || code == 3) return (true, reboot, null);
                return (false, reboot, $"Install failed (code {code}, 0x{(int)ires.HResult:X8})");
            }
            catch (Exception ex)
            {
                return (false, false, ex.Message);
            }
        });
    }
}
