using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public record WindowsUpdateItem(string Title, string? KbArticleId, bool IsDownloaded);

    // ΝΕΟ - roadmap ιδέα #3 (ρητό αίτημα χρήστη: "κάνε το 3 από τις ιδέες" - "Sync ενημερώσεων
    // Windows Update στο ίδιο hub") - επίσημο COM API (Microsoft.Update.Session/CreateUpdateSearcher,
    // το ΙΔΙΟ που χρησιμοποιεί εσωτερικά το ίδιο το Windows Update) αντί για τρίτο εργαλείο - καμία
    // πρόσθετη εξάρτηση, καμία εγκατάσταση PowerShell module. Το Search() εδώ είναι ΜΟΝΟ αναζήτηση
    // (καμία λήψη/εγκατάσταση) - καθαρά ενημερωτικό, ίδιο πνεύμα με τις υπόλοιπες "μόνο σάρωση"
    // λειτουργίες της εφαρμογής (WingetService.ScanAsync/DriverService.ScanAsync).
    public static class WindowsUpdateService
    {
        public static Task<(bool Success, IReadOnlyList<WindowsUpdateItem> Updates, string? Error)> ScanAsync() =>
            Task.Run(() =>
            {
                try
                {
                    var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                    if (sessionType == null) return (false, (IReadOnlyList<WindowsUpdateItem>)Array.Empty<WindowsUpdateItem>(), "Microsoft.Update.Session unavailable");

                    dynamic session = Activator.CreateInstance(sessionType)!;
                    dynamic searcher = session.CreateUpdateSearcher();
                    dynamic result = searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Software'");

                    var list = new List<WindowsUpdateItem>();
                    foreach (dynamic update in result.Updates)
                    {
                        string? kb = null;
                        try { if (update.KBArticleIDs.Count > 0) kb = "KB" + update.KBArticleIDs[0]; } catch { }
                        list.Add(new WindowsUpdateItem((string)update.Title, kb, (bool)update.IsDownloaded));
                    }
                    return (true, (IReadOnlyList<WindowsUpdateItem>)list, (string?)null);
                }
                catch (Exception ex)
                {
                    // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: η αναζήτηση Windows Update μπορεί να αποτύχει σε
                    // περιβάλλοντα όπου η υπηρεσία wuauserv είναι απενεργοποιημένη ή διαχειρίζεται από
                    // πολιτική (WSUS/Group Policy) - best-effort, ίδια ανοχή με τις υπόλοιπες σαρώσεις.
                    return (false, (IReadOnlyList<WindowsUpdateItem>)Array.Empty<WindowsUpdateItem>(), ex.Message);
                }
            });
    }
}
