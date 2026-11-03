using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests
{
    // 6.9.3 - Διαχειριστής ενημερώσεων: φίλτρα και κουμπιά ενέργειας στην ίδια γραμμή, οι περιγραφές των καρτελών δεν χάνονται ποτέ,
    // ροή LTR εκτός από τα αραβικά (RTL) - σε όλες τις 14 γλώσσες στο ελάχιστο πλάτος παραθύρου.
    public class UpdateManagerLayoutTests
    {
        private static readonly string[] Languages = { "el", "en", "de", "fr", "es", "ko", "zh", "it", "ru", "ja", "pt", "tr", "ar", "hi" };

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var c = VisualTreeHelper.GetChild(root, i);
                if (c is T t) yield return t;
                foreach (var x in Descendants<T>(c)) yield return x;
            }
        }

        [Fact]
        public void TabLabelsStayVisibleAndFlowFollowsTheLanguageInEveryLanguage()
        {
            var problems = new List<string>();
            Exception? error = null;
            var th = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null) { var app = new OptimizerWpf.App(); app.InitializeComponent(); }
                    OptimizerWpf.ThemeManager.LoadPersisted();
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                    var original = LanguageService.Current;
                    var w = new MainWindow { Width = 946, Height = 800, WindowStartupLocation = WindowStartupLocation.Manual, Left = -5000, Top = 0 };
                    w.Show();
                    foreach (var lang in Languages)
                    {
                        LanguageService.SetLanguage(lang);
                        w.SelectTab("Bloatware");
                        w.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                        w.UpdateLayout();
                        var strip = (Panel)w.FindName("TabStrip");
                        foreach (var rb in strip.Children.OfType<RadioButton>())
                            foreach (var tb in Descendants<TextBlock>(rb).Where(t => !string.IsNullOrWhiteSpace(t.Text)))
                                if (tb.Visibility != Visibility.Visible) problems.Add($"{lang}: label '{tb.Text}' of tab '{rb.Tag}' is hidden");
                        var ys = strip.Children.OfType<RadioButton>().Select(r => Math.Round(r.TransformToAncestor(w).Transform(new Point(0, 0)).Y)).Distinct().Count();
                        if (ys != 1) problems.Add($"{lang}: the tabs are on {ys} rows (must be one)");
                        var expected = lang == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                        if (w.FlowDirection != expected) problems.Add($"{lang}: window flow direction is {w.FlowDirection}");
                        // φίλτρα και κουμπιά «Αναβάθμιση επιλεγμένων» / MS Store στο ίδιο ύψος (ίδια γραμμή)
                        var chips = Descendants<RadioButton>(w).FirstOrDefault(r => r.Name == "ChipAll");
                        var upg = Descendants<Button>(w).FirstOrDefault(b => b.Name == "BtnUpgradeSelected");
                        var ms = Descendants<Button>(w).FirstOrDefault(b => b.Name == "BtnCheckMsStore");
                        if (chips == null || upg == null || ms == null) { problems.Add($"{lang}: filter chips or action buttons not found"); continue; }
                        double Mid(FrameworkElement e) => e.TransformToAncestor(w).Transform(new Point(0, e.ActualHeight / 2)).Y;
                        if (Math.Abs(Mid(chips) - Mid(upg)) > 24 || Math.Abs(Mid(upg) - Mid(ms)) > 1) problems.Add($"{lang}: filters and action buttons are not on one row ({Mid(chips):0}/{Mid(upg):0}/{Mid(ms):0})");
                    }
                    LanguageService.SetLanguage(original);
                    w.Close();
                }
                catch (Exception ex) { error = ex; }
            });
            th.SetApartmentState(ApartmentState.STA); th.Start(); th.Join();
            if (error != null) throw new Exception(error.Message, error);
            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Take(30)));
        }
    }
}
