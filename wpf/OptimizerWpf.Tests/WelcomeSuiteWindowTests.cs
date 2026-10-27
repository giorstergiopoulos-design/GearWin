using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OptimizerWpf.Services;
using OptimizerWpf.Views;

namespace OptimizerWpf.Tests
{
    // 6.6.0 - οδηγός καλωσορίσματος + «οικογένεια Opsis»: κείμενα χωρίς κόψιμο σε ΟΛΕΣ τις γλώσσες, η κάρτα χωράει στο παράθυρο,
    // και (με GEARWIN_UIPROBE_OUT=<φάκελος>) εικόνες PNG των σελίδων, της σουίτας και της ενδιάμεσης φάσης του εφέ σμίκρυνσης.
    public class WelcomeSuiteWindowTests
    {
        private static readonly string[] Languages = { "el", "en", "de", "fr", "es", "ko", "zh", "it", "ru", "ja", "pt", "tr", "ar", "hi" };

        private static void Pump(Window w)
        {
            w.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            w.UpdateLayout();
        }

        private static void PumpFor(Dispatcher d, int ms)
        {
            var frame = new DispatcherFrame();
            var t = new DispatcherTimer(DispatcherPriority.Normal, d) { Interval = TimeSpan.FromMilliseconds(ms) };
            t.Tick += (_, _) => { t.Stop(); frame.Continue = false; };
            t.Start();
            Dispatcher.PushFrame(frame);
        }

        private static void Snapshot(FrameworkElement el, string path)
        {
            var rtb = new RenderTargetBitmap((int)Math.Max(1, el.ActualWidth), (int)Math.Max(1, el.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            // το παράθυρο έχει διαφάνεια: ζωγραφίζουμε πάνω σε φόντο θέματος ώστε το PNG να διαβάζεται
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen()) dc.DrawRectangle((Brush)Application.Current.FindResource("MainBgBrush"), null, new Rect(0, 0, el.ActualWidth, el.ActualHeight));
            rtb.Render(dv);
            rtb.Render(el);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(path); enc.Save(fs);
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var c = VisualTreeHelper.GetChild(root, i);
                if (c is T t) yield return t;
                foreach (var x in Descendants<T>(c)) yield return x;
            }
        }

        // Κόψιμο κειμένου εκτός κουμπιών: μονογραμμικό κείμενο που δεν χωράει στο πλάτος του, ή πολυγραμμικό που χρειάζεται περισσότερο ύψος.
        private static IEnumerable<string> TextProblems(DependencyObject root, string ctx)
        {
            foreach (var tb in Descendants<TextBlock>(root))
            {
                if (string.IsNullOrWhiteSpace(tb.Text) || !tb.IsVisible || tb.ActualWidth < 2) continue;
                var ft = new FormattedText(tb.Text, CultureInfo.CurrentUICulture, tb.FlowDirection, new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch), tb.FontSize, Brushes.Black, 1.0);
                if (tb.TextWrapping == TextWrapping.NoWrap)
                {
                    if (ft.WidthIncludingTrailingWhitespace > tb.ActualWidth + 1.5) yield return $"{ctx}: '{tb.Text}' needs {ft.WidthIncludingTrailingWhitespace:0} px, has {tb.ActualWidth:0}";
                }
                else
                {
                    ft.MaxTextWidth = tb.ActualWidth;
                    if (tb.LineHeight > 0 && !double.IsNaN(tb.LineHeight)) ft.LineHeight = tb.LineHeight;
                    if (ft.Height > tb.ActualHeight + 3) yield return $"{ctx}: '{Trim(tb.Text)}' wraps to {ft.Height:0} px, has {tb.ActualHeight:0}";
                }
            }
        }

        private static string Trim(string s) => s.Length > 40 ? s[..40] + "…" : s;

        private static void RunSta(Action<MainWindow> body)
        {
            Exception? error = null;
            var t = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null) { var app = new OptimizerWpf.App(); app.InitializeComponent(); }
                    OptimizerWpf.ThemeManager.LoadPersisted();
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                    var original = LanguageService.Current;
                    var main = new MainWindow { Width = 1000, Height = 760, WindowStartupLocation = WindowStartupLocation.Manual, Left = -5000, Top = 0 };
                    main.Show(); Pump(main);
                    try { body(main); }
                    finally { LanguageService.SetLanguage(original); main.Close(); }
                }
                catch (Exception ex) { error = ex; }
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            if (error != null) throw new Exception(error.Message, error);
        }

        private static OncePerVersionStore TempStore() => new(Path.Combine(Path.GetTempPath(), "gwwin_" + Guid.NewGuid().ToString("N"), "s.json"));

        [Fact]
        public void WelcomeGuideTextFitsInEveryLanguageAndPage()
        {
            var outDir = Environment.GetEnvironmentVariable("GEARWIN_UIPROBE_OUT");
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
            var problems = new List<string>();
            RunSta(main =>
            {
                foreach (var lang in Languages)
                {
                    LanguageService.SetLanguage(lang);
                    var g = new WelcomeGuideWindow(main, TempStore());
                    g.Show(); Pump(g); Pump(g);
                    for (var page = 0; page < WelcomeGuideWindow.PageCount; page++)
                    {
                        g.ShowPage(page); Pump(g); Pump(g);
                        var ctx = $"guide/{lang}/{page}";
                        problems.AddRange(UiClippingProbeTests.FindClippedButtonText(g, ctx));
                        problems.AddRange(TextProblems(g, ctx));
                        var card = (FrameworkElement)g.FindName("Card");
                        if (card.ActualHeight + 32 > g.ActualHeight + 1 || card.ActualWidth + 32 > g.ActualWidth + 1) problems.Add($"{ctx}: card {card.ActualWidth:0}x{card.ActualHeight:0} does not fit the window {g.ActualWidth:0}x{g.ActualHeight:0}");
                        if (!string.IsNullOrEmpty(outDir) && (lang is "el" or "en" || Environment.GetEnvironmentVariable("GEARWIN_UIPROBE_ALLSHOTS") == "1") && page is 0 or 2 or 4)
                            Snapshot((FrameworkElement)g.Content, Path.Combine(outDir, $"guide_{lang}_{page + 1}.png"));
                    }
                    g.Close();
                }
            });
            Assert.True(problems.Count == 0, problems.Count + " problems:" + Environment.NewLine + string.Join(Environment.NewLine, problems.Take(40)));
        }

        [Fact]
        public void SuiteShowcaseShowsEveryAppAndFitsInEveryLanguage()
        {
            var outDir = Environment.GetEnvironmentVariable("GEARWIN_UIPROBE_OUT");
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
            var problems = new List<string>();
            RunSta(main =>
            {
                var probe = new FakeProbe();
                probe.Files.Add(@"C:\Program Files\Opsis - Desktop Customization\Opsis.exe");
                probe.Versions[@"C:\Program Files\Opsis - Desktop Customization\Opsis.exe"] = "2.0.0";
                foreach (var lang in Languages)
                {
                    LanguageService.SetLanguage(lang);
                    var s = new SuiteShowcaseWindow(main, probe, allowNetwork: false);
                    s.Show(); Pump(s); Pump(s);
                    var ctx = $"suite/{lang}";
                    if (s.CardCount != 7) problems.Add($"{ctx}: {s.CardCount} cards");
                    problems.AddRange(UiClippingProbeTests.FindClippedButtonText(s, ctx));
                    problems.AddRange(TextProblems(s, ctx));
                    // η κάρτα της ίδιας της εφαρμογής: «Εδώ είστε», χωρίς κουμπί εγκατάστασης
                    var status = Descendants<TextBlock>(s).First(t => t.Name == "Status_gearwin");
                    Assert.Equal(LanguageService.T("Suite_YouAreHere"), status.Text);
                    Assert.Contains(LanguageService.T("Suite_Installed").Replace("{0}", "2.0.0"), Descendants<TextBlock>(s).First(t => t.Name == "Status_opsis").Text);
                    Assert.Equal(LanguageService.T("Suite_NotInstalled"), Descendants<TextBlock>(s).First(t => t.Name == "Status_kombos").Text);
                    if (!string.IsNullOrEmpty(outDir) && (lang is "el" or "en" || Environment.GetEnvironmentVariable("GEARWIN_UIPROBE_ALLSHOTS") == "1"))
                        Snapshot((FrameworkElement)s.Content, Path.Combine(outDir, $"suite_{lang}.png"));
                    s.Close();
                }
            });
            Assert.True(problems.Count == 0, problems.Count + " problems:" + Environment.NewLine + string.Join(Environment.NewLine, problems.Take(40)));
        }

        [Fact]
        public void GuideRemembersVersionOnlyWhenDontShowIsChecked_AndClosesWithEffect()
        {
            var outDir = Environment.GetEnvironmentVariable("GEARWIN_UIPROBE_OUT");
            RunSta(main =>
            {
                LanguageService.SetLanguage("en");
                var store = TempStore();
                var cur = ReleaseVersion.ParseOrNull(App.DisplayVersion)!;

                var g1 = new WelcomeGuideWindow(main, store);
                g1.Show(); Pump(g1);
                ((CheckBox)g1.FindName("ChkDontShow")).IsChecked = false;
                bool closed1 = false; g1.Closed += (_, _) => closed1 = true;
                g1.Finish(false);
                PumpFor(g1.Dispatcher, 700);
                Assert.True(closed1);
                Assert.True(store.ShouldShow(WelcomeGuideWindow.StoreKey, cur));   // not ticked -> shows again next start

                var g2 = new WelcomeGuideWindow(main, store);
                g2.Show(); Pump(g2);
                if (!string.IsNullOrEmpty(outDir))
                {
                    // ενδιάμεση φάση του εφέ (Windows animations πρέπει να είναι ενεργές)
                    Environment.SetEnvironmentVariable("GEARWIN_SIMPLE_ANIM", null);
                    g2.Finish(false);
                    PumpFor(g2.Dispatcher, 180);
                    if (g2.IsLoaded) Snapshot((FrameworkElement)g2.Content, Path.Combine(outDir, "guide_closing_midway.png"));
                    PumpFor(g2.Dispatcher, 600);
                }
                else
                {
                    g2.Finish(false);
                    PumpFor(g2.Dispatcher, 700);
                }
                Assert.False(store.ShouldShow(WelcomeGuideWindow.StoreKey, cur));   // ticked (default) -> remembered for this version
                Assert.True(store.ShouldShow(WelcomeGuideWindow.StoreKey, ReleaseVersion.ParseOrNull("99.0.0")!));   // a future minor shows it again
            });
        }
    }
}
