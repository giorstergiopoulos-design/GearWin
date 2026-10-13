using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace OptimizerWpf.Services
{
    public enum StoreUpdateOutcome { Updated, AlreadyCurrent, Failed, TimedOut }

    // Port του Invoke-WmtStoreGuiUpdate / Close-WmtStoreGui του WMT 6.7 (reference/WMT-GUI.ps1 ~39237-39584).
    // Το "store.exe install/updates" και το "winget upgrade --source msstore" δεν ενημερώνουν αξιόπιστα Store
    // apps (για αυτό το GearWin πριν απλώς άνοιγε το Store και δεν επιβεβαίωνε τίποτα). Το WMT κάνει αυτό που
    // θα έκανε ο χρήστης: ανοίγει Downloads & updates, βρίσκει τη γραμμή της εφαρμογής (AutomationId
    // "ProductTitle"), πατά το "ActionButton" της, παρακολουθεί τη γραμμή μέχρι να ολοκληρωθεί.
    // ΠΡΟΣΑΡΜΟΓΗ: το WMT ταιριάζει αγγλικά κείμενα κατάστασης (", Update available") - εδώ προστέθηκαν και τα
    // ελληνικά (η γλώσσα του χρήστη), και αν το κείμενο κατάστασης δεν αναγνωρίζεται (άλλη γλώσσα) η γραμμή
    // κρίνεται από αλλαγή ονόματος/εξαφάνιση, όχι από το κείμενο. Αποτέλεσμα που δεν επιβεβαιώνεται
    // επιστρέφεται ως TimedOut (ο caller το δείχνει ως "ανοίχτηκε στο Store"), ΠΟΤΕ ως ψεύτικη επιτυχία.
    public static class StoreGuiUpdateService
    {
        private const string StoreUri = "ms-windows-store://downloadsandupdates";
        private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private static readonly Regex UpdateAvailable = new(@",\s*(Update available|Διαθέσιμη ενημέρωση)", Opt);
        private static readonly Regex Active = new(@",\s*(Downloading|Installing|Pending|Queued|Starting|Acquiring|Processing|Updating|Downloaded|Λήψη|Γίνεται λήψη|Εγκατάσταση|Γίνεται εγκατάσταση|Σε αναμονή|Σε ουρά|Εκκίνηση|Επεξεργασία|Ενημέρωση σε εξέλιξη|Γίνεται ενημέρωση)", Opt);
        private static readonly Regex Finished = new(@",\s*(Modified moments ago|Updated|Installed|Ενημερώθηκε|Εγκαταστάθηκε|Τροποποιήθηκε)", Opt);

        public static Task<StoreUpdateOutcome> UpdateAsync(string packageName, Action<string>? log, CancellationToken ct, int timeoutSeconds = 300) =>
            Task.Run(() => UpdateBlocking(packageName, log, ct, Math.Max(30, timeoutSeconds)), ct);

        private static AutomationElement? FindStoreWindow() =>
            AutomationElement.RootElement
                .FindAll(TreeScope.Children, Condition.TrueCondition)
                .Cast<AutomationElement>()
                .FirstOrDefault(w => w.Current.Name == "Microsoft Store");

        private static bool NameMatches(string title, string wanted) =>
            title.Equals(wanted, StringComparison.OrdinalIgnoreCase)
            || (wanted.Length >= 4 && (title.Contains(wanted, StringComparison.OrdinalIgnoreCase) || wanted.Contains(title, StringComparison.OrdinalIgnoreCase)));

        private static AutomationElement? FindRow(AutomationElement storeWindow, string wanted)
        {
            var titleCond = new PropertyCondition(AutomationElement.AutomationIdProperty, "ProductTitle");
            var all = storeWindow.FindAll(TreeScope.Descendants, titleCond).Cast<AutomationElement>().ToList();
            var title = all.FirstOrDefault(t => t.Current.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                        ?? all.FirstOrDefault(t => NameMatches(t.Current.Name, wanted));
            if (title == null) return null;
            try
            {
                var row = TreeWalker.ControlViewWalker.GetParent(title);
                return row != null && row.Current.ControlType == ControlType.Custom ? row : null;
            }
            catch { return null; }
        }

        private static StoreUpdateOutcome UpdateBlocking(string wanted, Action<string>? log, CancellationToken ct, int timeoutSeconds)
        {
            wanted = (wanted ?? "").Trim();
            if (wanted.Length == 0) return StoreUpdateOutcome.Failed;

            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(StoreUri) { UseShellExecute = true }); }
            catch (Exception ex) { log?.Invoke($"Store open failed: {ex.Message}"); return StoreUpdateOutcome.Failed; }

            var started = DateTime.UtcNow;
            var deadline = started.AddSeconds(timeoutSeconds);
            DateTime? windowMissingSince = null;
            var invoked = false;
            var checkInvoked = false;
            string preInvokeName = "";
            DateTime invokedAt = DateTime.MinValue;
            var lastStatus = "";
            var seenRowAfterInvoke = false;

            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var win = FindStoreWindow();
                    if (win == null)
                    {
                        windowMissingSince ??= DateTime.UtcNow;
                        if ((DateTime.UtcNow - windowMissingSince.Value).TotalSeconds >= 20) { log?.Invoke("Store window unavailable"); return StoreUpdateOutcome.Failed; }
                        Thread.Sleep(500);
                        continue;
                    }
                    windowMissingSince = null;

                    var row = FindRow(win, wanted);
                    var rowName = row?.Current.Name ?? "";
                    var actionBtn = row?.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "ActionButton"));
                    var actionEnabled = actionBtn != null && actionBtn.Current.IsEnabled;

                    var updateAvail = row != null && UpdateAvailable.IsMatch(rowName);
                    var active = row != null && !updateAvail && Active.IsMatch(rowName);
                    var finished = row != null && Finished.IsMatch(rowName);

                    if (!invoked)
                    {
                        if (active) { invoked = true; invokedAt = DateTime.UtcNow; preInvokeName = rowName; log?.Invoke($"{wanted}: already updating"); }
                        else if (row != null && !finished && (updateAvail || actionEnabled) && actionBtn != null && actionEnabled)
                        {
                            // Σε μη αναγνωρισμένη γλώσσα το κείμενο κατάστασης δεν ταιριάζει - η παρουσία ενεργού ActionButton
                            // σε γραμμή που δεν είναι ενεργή/τελειωμένη θεωρείται "διαθέσιμη ενημέρωση" (όπως θα πατούσε ο χρήστης).
                            preInvokeName = rowName;
                            ((InvokePattern)actionBtn.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                            invoked = true; invokedAt = DateTime.UtcNow;
                            log?.Invoke($"{wanted}: update button invoked");
                            Thread.Sleep(1000);
                            continue;
                        }
                        else if (!checkInvoked && (DateTime.UtcNow - started).TotalSeconds >= 2)
                        {
                            var check = win.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "CheckForUpdatesButton"));
                            if (check != null && check.Current.IsEnabled)
                                ((InvokePattern)check.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                            checkInvoked = true;
                        }
                        else if (checkInvoked && (DateTime.UtcNow - started).TotalSeconds >= 15 && (row == null || finished))
                        {
                            log?.Invoke($"{wanted}: no update button found - already current");
                            return StoreUpdateOutcome.AlreadyCurrent;
                        }
                    }
                    else
                    {
                        if (row != null) seenRowAfterInvoke = true;
                        if (active && rowName != lastStatus) { lastStatus = rowName; log?.Invoke(rowName); }

                        if (finished) return StoreUpdateOutcome.Updated;
                        // Η γραμμή εξαφανίστηκε από τη λίστα ενημερώσεων μετά το πάτημα = ενημερώθηκε.
                        if (row == null && seenRowAfterInvoke && (DateTime.UtcNow - invokedAt).TotalSeconds >= 2) return StoreUpdateOutcome.Updated;
                        if (!active && !updateAvail && (DateTime.UtcNow - invokedAt).TotalSeconds >= 2 && rowName != preInvokeName && !actionEnabled)
                            return StoreUpdateOutcome.Updated;
                        // Το κουμπί πατήθηκε αλλά τίποτα δεν άλλαξε για 20s: το Store απέρριψε/αγνόησε το πάτημα.
                        if (row != null && rowName == preInvokeName && actionEnabled && (DateTime.UtcNow - invokedAt).TotalSeconds >= 20)
                            return StoreUpdateOutcome.Failed;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { log?.Invoke($"UIA: {ex.Message}"); }

                Thread.Sleep(750);
            }
            return StoreUpdateOutcome.TimedOut;
        }

        // Close-WmtStoreGui: κλείνει το παράθυρο του Store (AutomationId "Close") αφού τελειώσουν όλες οι ενημερώσεις.
        public static void CloseStoreWindow()
        {
            try
            {
                var win = FindStoreWindow();
                var close = win?.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "Close"));
                if (close != null && close.Current.IsEnabled)
                    ((InvokePattern)close.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            }
            catch { /* best-effort όπως στο WMT */ }
        }
    }
}
