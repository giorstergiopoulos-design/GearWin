using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests
{
    // 6.9.1 - προσβασιμότητα: κάθε ορατό κουμπί/toggle/καρτέλα πρέπει να έχει όνομα για UI Automation (AutomationProperties.Name, κείμενο
    // περιεχομένου, ToolTip ή ετικέτα). Κουμπιά μόνο-εικονίδιο χωρίς όνομα εμφανίζονται ως «άσημα» στους screen readers.
    public class AccessibleNamesTests
    {
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var c = VisualTreeHelper.GetChild(root, i);
                if (c is T t) yield return t;
                foreach (var x in Descendants<T>(c)) yield return x;
            }
        }

        private static bool IsInsideComboBox(DependencyObject d) { for (; d != null; d = VisualTreeHelper.GetParent(d)) if (d is ComboBox) return true; return false; }

        private static bool Retry(ButtonBase b)
        {
            typeof(OptimizerWpf.App).GetMethod("NameToggleFromRowLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, new object?[] { b, null });
            var still = string.IsNullOrWhiteSpace(AccessibleName(b));
            if (!still) Console.Error.WriteLine("DEBUG named only after manual call: " + b.Name);
            return still;
        }

        private static string Chain(DependencyObject d) { var l = new List<string>(); for (var i = 0; i < 6 && d != null; i++) { d = VisualTreeHelper.GetParent(d); if (d != null) l.Add(d.GetType().Name); } return string.Join(">", l); }

        internal static string? AccessibleName(FrameworkElement e)
        {
            var n = AutomationProperties.GetName(e);
            if (!string.IsNullOrWhiteSpace(n)) return n;
            if (e is ContentControl { Content: string s } && !string.IsNullOrWhiteSpace(s)) return s;
            if (e.ToolTip is string tip && !string.IsNullOrWhiteSpace(tip)) return tip;
            foreach (var tb in Descendants<TextBlock>(e)) if (!string.IsNullOrWhiteSpace(tb.Text)) return tb.Text;
            var labelledBy = AutomationProperties.GetLabeledBy(e);
            return labelledBy != null ? "labelled" : null;
        }

        [Fact]
        public void EveryTabButtonAndMainWindowControlHasAnAccessibleName()
        {
            var problems = new List<string>();
            Exception? error = null;
            var t = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null) { var app = new OptimizerWpf.App(); app.InitializeComponent(); }
                    OptimizerWpf.ThemeManager.LoadPersisted();
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                    LanguageService.SetLanguage("en");
                    var w = new MainWindow { Width = 1000, Height = 760, WindowStartupLocation = WindowStartupLocation.Manual, Left = -5000, Top = 0 };
                    w.Show();
                    w.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    foreach (var (item, sub) in NavItems.All.SelectMany(n => Enumerable.Range(0, TabLayout.IsComposite(n.Tag) ? TabLayout.SubPages(n.Tag)!.Count : 1).Select(i => (n, i))))
                    {
                        w.SelectTab(sub == 0 ? item.Tag : item.Tag + "#" + sub);
                        w.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                        w.UpdateLayout();
                        foreach (var rb in Descendants<RadioButton>(w).Where(r => r.IsVisible))
                            if (string.IsNullOrWhiteSpace(AccessibleName(rb))) problems.Add($"{item.Tag}: RadioButton '{rb.Tag}' has no accessible name");
                        foreach (var b in Descendants<ButtonBase>(w).Where(b => b.IsVisible && b is not RadioButton && b is not RepeatButton && !IsInsideComboBox(b)))
                            if (string.IsNullOrWhiteSpace(AccessibleName(b))) problems.Add($"{item.Tag}: {b.GetType().Name}#{b.Name} (tag {b.Tag}) has no accessible name; loaded={b.IsLoaded} content={b.Content?.GetType().Name} chain={Chain(b)}");
                    }
                    w.Close();
                }
                catch (Exception ex) { error = ex; }
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            if (error != null) throw new Exception(error.Message, error);
            problems = problems.Distinct().ToList();
            Assert.True(problems.Count == 0, problems.Count + " controls without a name:" + Environment.NewLine + string.Join(Environment.NewLine, problems.Take(40)));
        }
    }
}
