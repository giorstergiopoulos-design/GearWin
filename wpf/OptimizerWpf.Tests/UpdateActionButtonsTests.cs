using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using OptimizerWpf.Services;
using OptimizerWpf.Views;

namespace OptimizerWpf.Tests
{
    // 6.9.6 - Διαχειριστής ενημερώσεων: ΠΟΤΕ τα δύο κουμπιά μαζί. Πριν τη σάρωση / χωρίς αποτελέσματα μόνο «Σάρωση» (γρανάζι)· με λίστα αποτελεσμάτων μόνο «Ενημέρωση»·
    // κατά τη σάρωση μόνο το γρανάζι (σε πρόοδο)· κατά την ενημέρωση μόνο η «Ενημέρωση» (σε πρόοδο).
    public class UpdateActionButtonsTests
    {
        private static readonly string[] Languages = { "el", "en", "de", "fr", "es", "ko", "zh", "it", "ru", "ja", "pt", "tr", "ar", "hi" };

        [Theory]
        //   results scanning installing -> scanVisible scanEnabled updateVisible updateEnabled
        [InlineData(0, false, false, true, true, false, false)]    // αρχική / κενά αποτελέσματα / σφάλμα / ακύρωση: μόνο σάρωση
        [InlineData(0, true, false, true, false, false, false)]    // σάρωση χωρίς προηγούμενα αποτελέσματα
        [InlineData(5, true, false, true, false, false, false)]    // επανασάρωση με παλιά αποτελέσματα: μόνο το γρανάζι σε πρόοδο
        [InlineData(5, false, false, false, false, true, true)]    // αποτελέσματα: μόνο «Ενημέρωση»
        [InlineData(5, false, true, false, false, true, false)]    // ενημέρωση σε εξέλιξη: μόνο «Ενημέρωση», σε πρόοδο
        [InlineData(0, false, true, false, false, true, false)]    // ενημέρωση όπου οι γραμμές αδειάζουν πρώτα
        public void ExactlyOneActionButtonIsShownInEveryState(int results, bool scanning, bool installing, bool scanVisible, bool scanEnabled, bool updateVisible, bool updateEnabled)
        {
            var s = OptimizationView.DecideActionButtons(results, scanning, installing);
            Assert.Equal((scanVisible, scanEnabled, updateVisible, updateEnabled), (s.ScanVisible, s.ScanEnabled, s.UpdateVisible, s.UpdateEnabled));
            Assert.NotEqual(s.ScanVisible, s.UpdateVisible);
        }

        // DriverScanButton / AppUpdateScanButton verbergen FrameworkElement.IsEnabled με τη δική τους ιδιότητα (new bool IsEnabled)
        private static bool Enabled(FrameworkElement e) => (bool)e.GetType().GetProperty("IsEnabled", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!.GetValue(e)!;

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
        public void TheRealViewShowsOnlyOneActionButtonInEveryStateAndLanguage()
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
                    var f = BindingFlags.NonPublic | BindingFlags.Static;
                    var windows = (ObservableCollection<WingetUpdateRow>)typeof(OptimizationView).GetField("_windowsUpdates", f)!.GetValue(null)!;
                    var apps = (ObservableCollection<WingetUpdateRow>)typeof(OptimizationView).GetField("_updates", f)!.GetValue(null)!;
                    windows.Clear(); apps.Clear();
                    var w = new MainWindow { Width = 946, Height = 860, WindowStartupLocation = WindowStartupLocation.Manual, Left = -5000, Top = 0 };
                    w.Show();
                    void Pump() { w.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); w.UpdateLayout(); }
                    var ctor = typeof(WingetUpdateRow).GetConstructors()[0];
                    WingetUpdateRow Row(string src) => (WingetUpdateRow)ctor.Invoke(new object[] { new WingetUpdate("App", "Vendor.App", "1.0", "2.0", src) });
                    foreach (var lang in Languages)
                    {
                        LanguageService.SetLanguage(lang);
                        w.SelectTab("Bloatware"); Pump();
                        var view = Descendants<OptimizationView>(w).FirstOrDefault();
                        var gear = view == null ? null : (FrameworkElement?)view.FindName("BtnScanDrivers");
                        var update = view == null ? null : (FrameworkElement?)view.FindName("BtnScanWinget");
                        if (gear == null || update == null) { problems.Add($"{lang}: buttons not found"); continue; }
                        void Check(string state, bool scanVisible, bool updateVisible, bool? scanEnabled = null, bool? updateEnabled = null)
                        {
                            Pump();
                            var sv = gear.Visibility == Visibility.Visible; var uv = update.Visibility == Visibility.Visible;
                            if (sv != scanVisible || uv != updateVisible) problems.Add($"{lang}/{state}: scan visible={sv} (want {scanVisible}), update visible={uv} (want {updateVisible})");
                            if (scanEnabled.HasValue && Enabled(gear) != scanEnabled.Value) problems.Add($"{lang}/{state}: scan enabled={Enabled(gear)} (want {scanEnabled})");
                            if (updateEnabled.HasValue && Enabled(update) != updateEnabled.Value) problems.Add($"{lang}/{state}: update enabled={Enabled(update)} (want {updateEnabled})");
                            var expected = lang == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                            if (w.FlowDirection != expected) problems.Add($"{lang}/{state}: flow direction {w.FlowDirection}");
                        }

                        Check("initial", true, false, true);
                        OptimizationView.AppScan.TryBegin(); Check("scanning", true, false, false);
                        windows.Add(Row("windowsupdate")); OptimizationView.AppScan.End(); Check("results", false, true, null, true);
                        OptimizationView.AppScan.TryBegin(installing: true); Check("updating", false, true, null, false);
                        OptimizationView.AppScan.End(); Check("after update, results remain", false, true, null, true);
                        OptimizationView.DriverScan.TryBegin(); Check("rescanning with old results", true, false, false);
                        OptimizationView.DriverScan.End();
                        windows.Clear(); Check("empty results / error / cancel", true, false, true);
                    }
                    windows.Clear(); apps.Clear();
                    OptimizationView.AppScan.Reset(); OptimizationView.DriverScan.Reset();
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
