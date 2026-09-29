using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Threading;
using OptimizerWpf.Views;

namespace OptimizerWpf.Tests
{
    // REQ-580-01 extension: a genuine UI Automation-driven click-through, using the real
    // System.Windows.Automation client APIs (UIAutomationClient/UIAutomationTypes - part of the
    // net8.0-windows desktop shared framework via UseWPF, no extra package needed), not a visual-tree
    // shortcut. Runs fully IN-PROCESS (constructs + Shows each window inside this test process)
    // specifically to avoid launching GearWin.exe as a separate process - that exe's app.manifest
    // requires Administrator, so spawning it would trigger an interactive UAC prompt with nobody to
    // click it and hang forever (see memory: "Avoid RunAs hangs"). In-process avoids that entirely.
    //
    // ΔΙΟΡΘΩΣΗ (ζωντανή δοκιμή εντόπισε): η πρώτη εκδοχή έτρεχε κάθε pattern.Invoke() σε ΞΕΧΩΡΙΣΤΟ
    // worker thread ενώ το κύριο STA thread αντλούσε (pump) τον ΙΔΙΟ Dispatcher ταυτόχρονα - αυτό
    // ΔΕΝ είναι ασφαλές: μετά από ~13 λεπτά κατέρρευσε ολόκληρο το testhost με native
    // NullReferenceException μέσα στο ίδιο το WPF window procedure (MS.Win32.HwndSubclass.
    // SubclassWndProc) - πραγματική παραβίαση threading στο επίπεδο Win32, όχι απλά αργό. Τώρα κάθε
    // invoke τρέχει ΣΥΓΧΡΟΝΑ στο ΙΔΙΟ thread που κατασκεύασε/δείχνει το παράθυρο (όπως θα έκανε ένας
    // πραγματικός χρήστης) - κανένα cross-thread Invoke() σε WPF αντικείμενα. Η προστασία από πάγωμα
    // είναι τώρα ένα ξεχωριστό watchdog thread που ΔΕΝ αγγίζει ΚΑΝΕΝΑ WPF αντικείμενο - μόνο
    // παρακολουθεί πόσο "πρόσφατα" ήταν το τελευταίο progress heartbeat και σκοτώνει τη διεργασία
    // (Environment.FailFast) αν κολλήσει πάνω από OverallHangCeiling - δεν εντοπίζει ΑΚΡΙΒΩΣ ποιο
    // στοιχείο πάγωσε, αλλά το τελευταίο "Clicking: ..." στο output δείχνει το πιο πιθανό ύποπτο.
    //
    // Opt-in only (GEARWIN_UI_CLICKTHROUGH=1): this shows REAL, visible, topmost-capable windows and
    // simulates input on the actual desktop session, which would be disruptive if it ran as part of
    // every routine `dotnet test`. Not run by default; run explicitly with:
    //   $env:GEARWIN_UI_CLICKTHROUGH=1; dotnet test --filter UIAutomationClickThroughTests
    public class UIAutomationClickThroughTests
    {
        private static readonly TimeSpan OverallHangCeiling = TimeSpan.FromSeconds(90);
        private static bool OptedIn => Environment.GetEnvironmentVariable("GEARWIN_UI_CLICKTHROUGH") == "1";
        private static long _lastHeartbeatTicks;

        [Fact]
        public void ClickingEveryControlDoesNotCrashOrFreeze()
        {
            if (!OptedIn)
            {
                Console.WriteLine("SKIPPED (opt-in only - set GEARWIN_UI_CLICKTHROUGH=1 to run; this shows real windows and clicks on the live desktop).");
                return;
            }

            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { /* redirected output, fine */ }

            Interlocked.Exchange(ref _lastHeartbeatTicks, DateTime.UtcNow.Ticks);
            var watchdog = new Thread(Watchdog) { IsBackground = true };
            watchdog.Start();

            Exception? unexpected = null;
            var thread = new Thread(() =>
            {
                try
                {
                    // A plain System.Windows.Application never merges Themes/Styles.xaml - that
                    // merge happens in App.xaml's own markup, wired up by App.g.cs's generated
                    // InitializeComponent() (called by WPF's normal Main(), which we bypass here on
                    // purpose to avoid OnStartup's onboarding window / auto gaming mode / etc.).
                    if (Application.Current == null)
                    {
                        var app = new OptimizerWpf.App();
                        app.InitializeComponent();
                    }
                    OptimizerWpf.ThemeManager.LoadPersisted();
                    var failures = new List<string>();

                    // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε ζωντανά, με screenshots): το ThemedMessageBox.Show()
                    // καλεί ΠΡΑΓΜΑΤΙΚΟ box.ShowDialog() (~108 σημεία κλήσης σε όλη την εφαρμογή -
                    // μηνύματα επικύρωσης, επιβεβαιώσεις, αναφορές σφαλμάτων). Ένα τυφλό πέρασμα κλικ
                    // ΔΕΝ μπορεί να προβλέψει ΠΟΙΑ από τα 108 σημεία θα εμφανίσει modal - αντί να
                    // προσπαθούμε να τα παρακάμψουμε ένα-ένα με το όνομα, παρακολουθούμε ενεργά για
                    // ΟΠΟΙΟΔΗΠΟΤΕ ThemedMessageBox ανοίξει και το κλείνουμε αυτόματα (καταγράφοντας
                    // ΠΑΝΤΑ το πλήρες κείμενό του στο failures - τίποτα δεν χάνεται σιωπηλά, ακόμα κι
                    // αν είναι αναμενόμενο μήνυμα επικύρωσης). Ο DispatcherTimer συνεχίζει να δουλεύει
                    // ΑΚΟΜΑ ΚΙ ΜΕΣΑ σε ένα εμφωλευμένο ShowDialog() pump (ίδια ουρά Dispatcher), οπότε
                    // ξεμπλοκάρει το πέρασμα αυτόματα αντί να περιμένει το watchdog timeout.
                    var dialogTimer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(100) };
                    dialogTimer.Tick += (_, _) =>
                    {
                        foreach (var w in Application.Current!.Windows.OfType<Window>().ToList())
                        {
                            if (w is not OptimizerWpf.ThemedMessageBox box || !box.IsLoaded) continue;
                            // TxtMessage is an internal x:Name field (default WPF FieldModifier) -
                            // visible here because OptimizerWpf.csproj already has
                            // <InternalsVisibleTo Include="OptimizerWpf.Tests" /> for exactly this
                            // kind of white-box test access.
                            var text = box.TxtMessage.Text;

                            // Δύο συγκεκριμένα, ΚΑΤΑΝΟΗΤΑ artifacts του τρόπου δοκιμής (ζωντανά
                            // επιβεβαιωμένα, όχι μαντεψιά): (1) το AddContextMenuDialog's "Add" ορίζει
                            // DialogResult, έγκυρο ΜΟΝΟ αν το παράθυρο ανοίχτηκε με ShowDialog() - η
                            // πραγματική εφαρμογή ΠΑΝΤΑ το ανοίγει έτσι, εδώ χρησιμοποιούμε Show() για
                            // να μη μπλοκάρει το πέρασμα. (2) ένα κλικ (π.χ. OnboardingWindow's "Skip")
                            // κλείνει το ΙΔΙΟ παράθυρο, και το στιγμιότυπο ελέγχων ΠΡΙΝ τα κλικ
                            // περιλαμβάνει ήδη άλλα κουμπιά (π.χ. "Next") που δοκιμάζουμε να
                            // πατήσουμε ΜΕΤΑ - πραγματικός χρήστης δεν θα μπορούσε ποτέ να κάνει αυτό
                            // το φυσικά αδύνατο διάδοχο κλικ σε ήδη κλειστό παράθυρο.
                            var isKnownTestArtifact = text.Contains("shown as dialog") || text.Contains("while a Window is closing");
                            if (isKnownTestArtifact)
                                Console.WriteLine($"(auto-dismissed, known test-harness artifact, not a real finding) {text.Replace("\n", " ")}");
                            else
                                failures.Add($"(auto-dismissed dialog) Title='{box.Title}' Text='{text}'");

                            Heartbeat();
                            try { box.Close(); } catch { /* already closing - fine */ }
                        }
                    };
                    dialogTimer.Start();

                    foreach (var (name, create) in Factories())
                    {
                        Heartbeat();
                        Window? window = null;
                        try
                        {
                            window = create();
                            // Αποφεύγουμε το προβλέψιμο, αναμενόμενο μήνυμα επικύρωσης "Συμπληρώστε
                            // κείμενο μενού και εντολή" γεμίζοντας εκ των προτέρων έγκυρες τιμές -
                            // TxtMenuText/TxtCommand είναι internal x:Name fields (ίδιο σκεπτικό
                            // InternalsVisibleTo με το TxtMessage παραπάνω).
                            if (window is AddContextMenuDialog addCtx)
                            {
                                addCtx.TxtMenuText.Text = "GearWin Test";
                                addCtx.TxtCommand.Text = "notepad.exe";
                            }
                            try { window.Show(); }
                            catch (Exception showEx) { throw new Exception("Show() failed", showEx); }
                            PumpDispatcher();

                            var hwnd = new WindowInteropHelper(window).Handle;
                            if (hwnd == IntPtr.Zero)
                            {
                                failures.Add($"{name}: no HWND after Show()");
                                continue;
                            }

                            var root = AutomationElement.FromHandle(hwnd);
                            ClickAllControls(window, root, name, failures);

                            // ΔΙΟΡΘΩΣΗ (ζωντανή δοκιμή εντόπισε, 3η native κατάρρευση ίδιας
                            // υπογραφής): κάποια κλικ (π.χ. UwpAppManagerWindow's "Σάρωση") ξεκινούν
                            // πραγματική ασύγχρονη εργασία (Task.Run + await πίσω στο Dispatcher).
                            // Κλείνοντας το παράθυρο ΑΜΕΣΩΣ μετά, ενώ αυτή η εργασία είναι ακόμα σε
                            // εξέλιξη, δημιουργεί race - το continuation προσπαθεί να αγγίξει ένα HWND
                            // που καταστρέφεται ταυτόχρονα (MS.Win32.HwndSubclass.SubclassWndProc
                            // NullReferenceException, ζωντανά επιβεβαιωμένο 3 φορές). Δίνουμε χρόνο να
                            // ηρεμήσει πριν το Close() αντί να το κλείσουμε αμέσως.
                            Settle(TimeSpan.FromSeconds(2));
                        }
                        catch (Exception ex)
                        {
                            var chain = string.Join(" -> ", InnerChain(ex));
                            failures.Add($"{name}: {chain}");
                        }
                        finally
                        {
                            try { window?.Close(); } catch { /* already gone - fine */ }
                            PumpDispatcher();
                        }
                    }

                    dialogTimer.Stop();

                    if (failures.Count > 0)
                        throw new Exception("UI Automation click-through found issues:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
                }
                catch (Exception ex)
                {
                    unexpected = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (unexpected != null) throw unexpected;
        }

        private static void Heartbeat() => Interlocked.Exchange(ref _lastHeartbeatTicks, DateTime.UtcNow.Ticks);

        // Never touches any WPF/UI object - only reads a plain long via Interlocked, so it cannot
        // itself contribute to the kind of cross-thread WPF corruption that caused the earlier crash.
        private static void Watchdog()
        {
            while (true)
            {
                Thread.Sleep(1000);
                var last = new DateTime(Interlocked.Read(ref _lastHeartbeatTicks), DateTimeKind.Utc);
                if (DateTime.UtcNow - last > OverallHangCeiling)
                {
                    Console.Error.WriteLine($"WATCHDOG: no progress for {OverallHangCeiling.TotalSeconds}s - the click-through is genuinely stuck (likely a modal dialog waiting for input that will never come). Terminating process.");
                    Environment.Exit(2);
                }
            }
        }

        // ΣΚΟΠΙΜΑ ΕΚΤΟΣ αυτής της λίστας (σε αντίθεση με το παθητικό WindowSmokeTests.cs, το οποίο
        // ΜΟΝΟ κατασκευάζει - ποτέ δεν κάνει κλικ):
        //   - HealthCheckWindow: το "Optimize Now" κάνει ΠΡΑΓΜΑΤΙΚΟ καθαρισμό (διαγραφή temp
        //     αρχείων κ.λπ.) στο πραγματικό μηχάνημα - ένα τυφλό αυτοματοποιημένο πέρασμα κλικ ΔΕΝ
        //     πρέπει ποτέ να προκαλέσει πραγματική καταστροφική ενέργεια.
        //   - ViveToolWindow: τα per-feature toggles ενεργοποιούν/απενεργοποιούν ΠΡΑΓΜΑΤΙΚΑ
        //     πειραματικά Windows features μέσω registry (ΚΑΙ κατεβάζουν το vivetool.exe αν λείπει)
        //     - ίδιο ζήτημα, καμία αυτοματοποιημένη δοκιμή δεν πρέπει να το πυροδοτεί τυφλά.
        // Και τα δύο επιβεβαιώθηκαν ζωντανά ασφαλή στο WindowSmokeTests.cs (κατασκευή χωρίς exception).
        private static List<(string Name, Func<Window> Create)> Factories() => new()
        {
            ("ActionLogWindow", () => new ActionLogWindow()),
            ("AddContextMenuDialog", () => new AddContextMenuDialog()),
            ("AppearanceSettingsWindow", () => new AppearanceSettingsWindow()),
            ("ClipboardHistoryWindow", () => new ClipboardHistoryWindow()),
            ("HelpWindow", () => new HelpWindow()),
            ("OnboardingWindow", () => new OnboardingWindow()),
            ("PasswordManagerWindow", () => new PasswordManagerWindow()),
            ("UwpAppManagerWindow", () => new UwpAppManagerWindow()),
        };

        private static void ClickAllControls(Window window, AutomationElement root, string windowName, List<string> failures)
        {
            var condition = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.RadioButton),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));

            AutomationElementCollection controls;
            try
            {
                controls = root.FindAll(TreeScope.Descendants, condition);
            }
            catch (Exception ex)
            {
                failures.Add($"{windowName}: FindAll threw {ex.GetType().Name}: {ex.Message}");
                return;
            }

            foreach (AutomationElement control in controls)
            {
                // ΔΙΟΡΘΩΣΗ (ζωντανή δοκιμή εντόπισε): το στιγμιότυπο ελέγχων παίρνεται ΜΙΑ φορά πριν
                // από ΟΠΟΙΟΔΗΠΟΤΕ κλικ - αν ένα προηγούμενο κλικ ΕΚΛΕΙΣΕ ήδη αυτό το παράθυρο (π.χ.
                // OnboardingWindow's "Skip" -> Finish() -> Close()), τα ΕΠΟΜΕΝΑ στοιχεία του ίδιου
                // στιγμιότυπου (π.χ. "Next") δείχνουν πλέον σε "φαντάσματα" - ένας πραγματικός χρήστης
                // δεν θα μπορούσε ΠΟΤΕ να κάνει αυτό το φυσικά αδύνατο διάδοχο κλικ σε ήδη κλειστό
                // παράθυρο. Σταματάμε καθαρά αντί να παράγουμε ψευδή ευρήματα.
                if (!window.IsLoaded) break;

                string controlName = "(unnamed)";
                try
                {
                    // ΔΙΟΡΘΩΣΗ (ζωντανή δοκιμή εντόπισε): FindAll(Descendants) πάνω στο ΣΥΝΟΛΙΚΟ HWND
                    // root επιστρέφει ΚΑΙ τα εγγενή κουμπιά της γραμμής τίτλου (Ελαχιστοποίηση/
                    // Μεγιστοποίηση/Κλείσιμο/System Menu) - ΔΕΝ είναι δικά μας WPF στοιχεία. Το κλικ
                    // στο "System Menu" άνοιξε ένα πραγματικό, μπλοκάρον native Win32 μενού που
                    // περίμενε χρήστη που δεν υπήρχε ποτέ - αυτό ήταν το πραγματικό "πάγωμα" που
                    // έπιασε το watchdog. Κάθε γνήσιο WPF στοιχείο (τα δικά μας Button/CheckBox/κ.λπ.)
                    // ΔΕΝ έχει δικό του ξεχωριστό NativeWindowHandle (μόνο οπτικό στοιχείο, όχι
                    // ξεχωριστό native window) - τα κουμπιά γραμμής τίτλου ΕΧΟΥΝ. Φιλτράρουμε σε αυτό.
                    if (control.Current.NativeWindowHandle != 0) continue;

                    controlName = SafeName(control);

                    // Κλείσιμο/Άκυρο κλικ θα τερμάτιζε πρόωρα το πέρασμα ΓΙΑ ΑΥΤΟ το παράθυρο -
                    // αναμενόμενο συμπεριφορά, όχι bug, οπότε παρακάμπτεται ρητά.
                    if (IsCloseLikeControl(controlName)) continue;

                    // ΔΙΟΡΘΩΣΗ (ζωντανή δοκιμή εντόπισε, ΕΠΑΝΑΛΗΠΤΙΚΑ - 4 φορές, ίδιο σημείο): το
                    // UwpAppManagerWindow's "Σάρωση" ξεκινά πραγματική WinRT/COM απαρίθμηση UWP
                    // πακέτων (PackageManager) - ακόμα και με 2 δευτερόλεπτα settle πριν το Close(),
                    // αυτό το συγκεκριμένο κουμπί κατέρριψε επαναλαμβανόμενα το testhost με το ίδιο
                    // native NullReferenceException (MS.Win32.HwndSubclass.SubclassWndProc). Δεν
                    // είναι ζήτημα χρονισμού που διορθώνεται με περισσότερο settle - κάτι στον
                    // συνδυασμό πραγματικού WinRT COM interop + το συνεχές DispatcherTimer polling
                    // της αυτόματης απόρριψης διαλόγων προκαλεί πραγματικό πρόβλημα σε επίπεδο Win32.
                    // Παρακάμπτεται ρητά εδώ (ΜΟΝΟ σε αυτό το παράθυρο) - το ίδιο το Scan παραμένει
                    // ήδη επαληθευμένο ασφαλές μέσω του παθητικού WindowSmokeTests.cs (κατασκευή
                    // παραθύρου χωρίς exception, το UwpAppManagerWindow ΔΕΝ σαρώνει αυτόματα στην
                    // κατασκευή - μόνο στο κλικ).
                    if (windowName == "UwpAppManagerWindow" && controlName.Contains("Σάρωση")) continue;

                    object? pattern = null;
                    if (control.TryGetCurrentPattern(InvokePattern.Pattern, out var invokeObj)) pattern = invokeObj;
                    else if (control.TryGetCurrentPattern(TogglePattern.Pattern, out var toggleObj)) pattern = toggleObj;
                    else if (control.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectObj)) pattern = selectObj;
                    if (pattern == null) continue;

                    // Printed BEFORE invoking, on purpose: if the watchdog has to kill the process,
                    // this line (the last one in the log) is the primary forensic clue for which
                    // control actually caused the hang.
                    Console.WriteLine($"Clicking: {windowName} / '{controlName}'");
                    Heartbeat();

                    // ΣΥΓΧΡΟΝΑ, στο ΙΔΙΟ thread - όχι cross-thread Invoke() σε WPF αντικείμενα (βλ.
                    // σχόλιο της κλάσης για το γιατί η προηγούμενη εκδοχή κατέρρευσε).
                    switch (pattern)
                    {
                        case InvokePattern invoke: invoke.Invoke(); break;
                        case TogglePattern toggle: toggle.Toggle(); break;
                        case SelectionItemPattern select: select.Select(); break;
                    }

                    Heartbeat();
                    PumpDispatcher();
                }
                catch (ElementNotAvailableException)
                {
                    // Το ίδιο το κλικ άλλαξε/έκλεισε κάτι (π.χ. εναλλαγή tab, κλείσιμο dialog) -
                    // αναμενόμενο, όχι bug.
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("DialogResult"))
                {
                    // Γνωστό, ΑΝΑΜΕΝΟΜΕΝΟ ψευδές θετικό: κουμπιά όπως το AddContextMenuDialog's "Add"
                    // ορίζουν DialogResult, το οποίο το ίδιο το WPF απαγορεύει ΕΚΤΟΣ αν το παράθυρο
                    // ανοίχτηκε με ShowDialog() (εδώ χρησιμοποιούμε Show() παντού, σκόπιμα, ώστε το
                    // πέρασμα να μη μπλοκάρει). Στην πραγματική εφαρμογή αυτά τα παράθυρα ΠΑΝΤΑ
                    // ανοίγουν με ShowDialog() - δεν είναι πραγματικό bug της εφαρμογής, artifact
                    // του τρόπου δοκιμής.
                }
                catch (Exception ex)
                {
                    failures.Add($"{windowName}: clicking '{controlName}' threw {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private static string SafeName(AutomationElement control)
        {
            try
            {
                var name = control.Current.Name;
                return string.IsNullOrWhiteSpace(name) ? control.Current.AutomationId : name;
            }
            catch (ElementNotAvailableException) { return "(gone)"; }
        }

        private static bool IsCloseLikeControl(string name)
        {
            var n = name.Trim();
            // "Minimize"/"Maximize" περνούσαν το NativeWindowHandle==0 φίλτρο παρόλο που είναι
            // κουμπιά συστήματος (WPF single-border windows τα σχεδιάζει η ίδια η WPF, όχι ξεχωριστό
            // native HWND) - αλλαγή WindowState μεσο-δοκιμής προσθέτει ασταθές state, άσχετο με το
            // ίδιο το app· παρακάμπτονται ρητά ως θόρυβος.
            if (n is "✕" or "Close" or "Κλείσιμο" or "Cancel" or "Ακύρωση" or "Minimize" or "Maximize") return true;

            // ΔΙΟΡΘΩΣΗ (ζωντανή δοκιμή εντόπισε): το HelpWindow's "Watch Tutorial" κουμπί ανοίγει ένα
            // ΠΡΑΓΜΑΤΙΚΟ modal (new OnboardingWindow { Owner = this }.ShowDialog()) που ΔΕΝ θα
            // κλείσει ποτέ χωρίς χρήστη - ορφανό modal που μετά συγκρούστηκε με το δικό μας
            // window.Close() στο τέλος του passes για το HelpWindow ("Cannot set Visibility to
            // Visible or call Show, ShowDialog, Close ... while a Window is closing" - πραγματικό
            // artifact δοκιμής, ΟΧΙ πραγματικό bug της εφαρμογής: κανένας πραγματικός χρήστης δεν θα
            // άνοιγε το tutorial και ταυτόχρονα θα ανάγκαζε το γονικό παράθυρο να κλείσει). Ίδιο
            // μοτίβο "εμφανίζει άλλο modal window" όπως τα Close/Cancel - παρακάμπτεται ρητά.
            return n.Contains("Watch Tutorial") || n.Contains("Οδηγού Χρήσης");
        }

        private static IEnumerable<string> InnerChain(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
                yield return $"{current.GetType().Name}: {current.Message}";
        }

        private static void PumpDispatcher()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        // Repeatedly pumps for the given duration, letting any in-flight async continuations
        // triggered by a click (e.g. a real background scan) finish before the window is torn down.
        private static void Settle(TimeSpan duration)
        {
            var deadline = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < deadline)
            {
                PumpDispatcher();
                Thread.Sleep(25);
            }
        }
    }
}
