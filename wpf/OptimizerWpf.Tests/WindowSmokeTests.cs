using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using OptimizerWpf.Views;

namespace OptimizerWpf.Tests
{
    // REQ-580-01: an automated pass "clicking through every button" isn't achievable literally for a
    // WPF desktop app without a real UI Automation driver, which this environment doesn't have. The
    // honest, actually-verifiable equivalent is a headless smoke test: construct every secondary
    // window with a real (or default-valued) parameterless constructor on a genuine STA thread and
    // assert none of them throws. This catches crash-on-open bugs - null refs in constructors, broken
    // named-element bindings, exceptions from service calls made eagerly - though it does not simulate
    // clicking individual buttons; that's a documented scope limit, not a silent omission.
    public class WindowSmokeTests
    {
        [Fact]
        public void AllParameterlessWindowsConstructWithoutThrowing()
        {
            Exception? unexpected = null;

            var thread = new Thread(() =>
            {
                try
                {
                    // See UIAutomationClickThroughTests.cs for why this is App.InitializeComponent(),
                    // not a plain Application - that's what actually merges Themes/Styles.xaml.
                    if (Application.Current == null)
                    {
                        var app = new OptimizerWpf.App();
                        app.InitializeComponent();
                    }
                    OptimizerWpf.ThemeManager.LoadPersisted();

                    var factories = new List<(string Name, Func<Window> Create)>
                    {
                        ("ActionLogWindow", () => new ActionLogWindow()),
                        ("AddContextMenuDialog", () => new AddContextMenuDialog()),
                        ("AppearanceSettingsWindow", () => new AppearanceSettingsWindow()),
                        ("ClipboardHistoryWindow", () => new ClipboardHistoryWindow()),
                        ("DesktopWidgetWindow", () => new DesktopWidgetWindow()),
                        ("HealthCheckWindow", () => new HealthCheckWindow()),
                        // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "polish 6.3.5 με automated tests") - αυτά τα 2
                        // παράθυρα υπήρχαν ήδη στην εφαρμογή αλλά δεν ήταν ποτέ προστεθειμένα εδώ -
                        // πραγματικό κενό κάλυψης, όχι σκόπιμη εξαίρεση (κανένα από τα δύο κάνει
                        // οτιδήποτε destructive/αργό ΜΕΣΑ στον constructor - μόνο σε Loaded, που δεν
                        // πυροδοτείται ποτέ εδώ αφού δεν καλείται Show()/δεν αντλείται Dispatcher).
                        ("HealthTimelineWindow", () => new HealthTimelineWindow()),
                        ("MaintenanceCenterWindow", () => new MaintenanceCenterWindow()),
                        ("HelpWindow", () => new HelpWindow()),
                        ("OnboardingWindow", () => new OnboardingWindow()),
                        ("PasswordManagerWindow", () => new PasswordManagerWindow()),
                        ("TrayPopupWindow", () => new TrayPopupWindow()),
                        ("UwpAppManagerWindow", () => new UwpAppManagerWindow()),
                        ("ViveToolWindow", () => new ViveToolWindow()),
                        ("WifiPasswordWindow", () => new WifiPasswordWindow()),
                    };

                    var failures = new List<string>();
                    foreach (var (name, create) in factories)
                    {
                        try
                        {
                            var window = create();
                            window.Close();
                        }
                        catch (Exception ex)
                        {
                            failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
                        }
                    }

                    if (failures.Count > 0)
                        throw new Exception("Window(s) threw on construction:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
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
    }
}
