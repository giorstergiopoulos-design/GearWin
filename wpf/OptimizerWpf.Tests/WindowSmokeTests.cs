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
                    if (Application.Current == null)
                    {
                        var app = new Application();
                        // Assembly name is "GearWin" (see OptimizerWpf.csproj's <AssemblyName>), not
                        // the project/namespace name "OptimizerWpf" - pack URIs need the real assembly.
                        app.Resources.MergedDictionaries.Add(new ResourceDictionary
                        {
                            Source = new Uri("pack://application:,,,/GearWin;component/Themes/Styles.xaml")
                        });
                    }

                    var factories = new List<(string Name, Func<Window> Create)>
                    {
                        ("ActionLogWindow", () => new ActionLogWindow()),
                        ("AddContextMenuDialog", () => new AddContextMenuDialog()),
                        ("AppearanceSettingsWindow", () => new AppearanceSettingsWindow()),
                        ("ClipboardHistoryWindow", () => new ClipboardHistoryWindow()),
                        ("DesktopWidgetWindow", () => new DesktopWidgetWindow()),
                        ("HealthCheckWindow", () => new HealthCheckWindow()),
                        ("HelpWindow", () => new HelpWindow()),
                        ("OnboardingWindow", () => new OnboardingWindow()),
                        ("PasswordManagerWindow", () => new PasswordManagerWindow()),
                        ("TrayPopupWindow", () => new TrayPopupWindow()),
                        ("UwpAppManagerWindow", () => new UwpAppManagerWindow()),
                        ("ViveToolWindow", () => new ViveToolWindow()),
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
