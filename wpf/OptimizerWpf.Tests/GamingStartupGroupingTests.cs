using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using OptimizerWpf.Services;
using OptimizerWpf.Views;

namespace OptimizerWpf.Tests
{
    // 6.5.5 - ομαδοποίηση διακοπτών gaming και εκκίνησης (βλ. CHANGELOG).
    public class GamingStartupGroupingTests
    {
        [Fact]
        public void HagsHasASingleSwitchInGamesSettingsButStaysRestorable()
        {
            // μόνο ένας διακόπτης στο UI: τα Παιχνίδια › Ρυθμίσεις gaming (όχι πια και Επιπλέον Ρυθμίσεις › Απόδοση)
            Assert.DoesNotContain(TweakService.PerfTweaksSimple(), t => t.Id == TweakService.HagsBackupKey);
            Assert.Contains(GamingTweaksService.GlobalTweaks(), t => t.Id == "Hags");
            // αλλά η επαναφορά αλλαγών που είχαν γίνει από την παλιά θέση εξακολουθεί να τη βλέπει
            Assert.Contains(TweakService.PerfRegTweaks, t => t.BackupKey == TweakService.HagsBackupKey);
            // τα powercfg tweaks της κάρτας Απόδοση παραμένουν
            Assert.NotEmpty(TweakService.PerfTweaksSimple());
        }

        [Fact]
        public void HelpOfTheTweaksTabNoLongerListsHags()
        {
            foreach (var lang in LanguageService.AllTranslations.Keys)
                Assert.DoesNotContain("HAGS/", LanguageService.AllTranslations[lang]["Help_TweaksDesc"]);
            // και τα Παιχνίδια το περιγράφουν ακόμα
            Assert.Contains("HAGS", LanguageService.AllTranslations["en"]["Help_GamesDesc"]);
        }

        [Fact]
        public void StartupListOffersTheDelayedStartTool()
        {
            Exception? error = null;
            var t = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null) { var app = new OptimizerWpf.App(); app.InitializeComponent(); }
                    OptimizerWpf.ThemeManager.LoadPersisted();
                    var cards = new PowerToolsCards();
                    var window = new Window { Content = cards, Left = -5000, Top = 0, Width = 1000, Height = 800, WindowStartupLocation = WindowStartupLocation.Manual };
                    window.Show(); window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    var wanted = LanguageService.T("Center_TabStartup");
                    var texts = Descend<Button>(cards).Select(b => (b.Content as string) ?? (b.Content as TextBlock)?.Text).ToList();
                    Assert.Contains(wanted, texts);
                    window.Close();
                }
                catch (Exception ex) { error = ex; }
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            if (error != null) throw new Exception(error.Message, error);
        }

        private static System.Collections.Generic.IEnumerable<T> Descend<T>(DependencyObject root) where T : DependencyObject
        {
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var c = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (c is T x) yield return x;
                foreach (var y in Descend<T>(c)) yield return y;
            }
        }

        [Fact]
        public void MaintenanceCenterCanOpenOnTheStartupTab()
        {
            Exception? error = null;
            var t = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null) { var app = new OptimizerWpf.App(); app.InitializeComponent(); }
                    OptimizerWpf.ThemeManager.LoadPersisted();
                    var w = new MaintenanceCenterWindow { Left = -5000, Top = 0, WindowStartupLocation = WindowStartupLocation.Manual };
                    w.ShowStartupTab();
                    var tabs = (TabControl)w.FindName("Tabs");
                    Assert.Equal(3, tabs.SelectedIndex);
                    Assert.Equal(LanguageService.T("Center_TabStartup"), ((TabItem)tabs.Items[3]).Header?.ToString());
                    w.Close();
                }
                catch (Exception ex) { error = ex; }
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            if (error != null) throw new Exception(error.Message, error);
        }
    }
}
