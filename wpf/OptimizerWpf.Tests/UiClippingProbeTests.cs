using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests
{
    // 6.5.3 - έλεγχος "κομμένων κειμένων" στα κουμπιά (χρήστης: "ελέγξτε για κομμένα κείμενα στα κουμπιά"). Ανοίγει το κανονικό MainWindow, περνά από ΚΑΘΕ καρτέλα και υπο-σελίδα
    // σε ΟΛΕΣ τις γλώσσες και βρίσκει κάθε κείμενο μέσα σε κουμπί (ή toggle/radio) που (α) είναι περιορισμένο από τη διάταξη (το φυσικό πλάτος του είναι μεγαλύτερο από
    // το πλάτος που πήρε) ή (β) ξεπερνά τα όρια του κουμπιού που το περιέχει. Ο έλεγχος αποτυγχάνει με τη λίστα των προβλημάτων.
    // GEARWIN_UIPROBE_OUT=<φάκελος>: γράφει επιπλέον screenshots (PNG) κάθε σελίδας στα el/en και την αναφορά ως .txt.
    public class UiClippingProbeTests
    {
        private static readonly string[] Languages = { "el", "en", "de", "fr", "es", "ko", "zh", "it", "ru", "ja", "pt", "tr", "ar", "hi" };

        private static double NaturalWidth(TextBlock tb)
        {
            var text = tb.Text;
            if (string.IsNullOrEmpty(text)) return 0;
            var ft = new FormattedText(text, CultureInfo.CurrentUICulture, tb.FlowDirection, new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch), tb.FontSize, Brushes.Black, 1.0);
            return ft.WidthIncludingTrailingWhitespace;
        }

        private static bool IsReallyVisible(FrameworkElement e)
        {
            for (DependencyObject d = e; d != null; d = VisualTreeHelper.GetParent(d))
            {
                if (d is UIElement u && u.Visibility != Visibility.Visible) return false;   // Opacity αγνοείται: το fade-in της καρτέλας δεν έχει τελειώσει στο headless τρέξιμο
            }
            return e.ActualWidth > 1 && e.ActualHeight > 1;
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

        private static string Describe(FrameworkElement b, string text)
        {
            var name = string.IsNullOrEmpty(b.Name) ? "" : "#" + b.Name;
            return $"{b.GetType().Name}{name} '{text}'";
        }

        internal static int Examined;

        internal static List<string> FindClippedButtonText(DependencyObject root, string context)
        {
            var problems = new List<string>();
            var buttons = Descendants<FrameworkElement>(root).Where(e => e is ButtonBase || (e is UserControl && e.GetType().Name.EndsWith("Button", StringComparison.Ordinal)));
            foreach (var fe in buttons)
            {
                if (!IsReallyVisible(fe)) continue;
                var button = fe;
                foreach (var tb in Descendants<TextBlock>(button))
                {
                    if (!IsReallyVisible(tb) || string.IsNullOrWhiteSpace(tb.Text)) continue;
                    Examined++;
                    if (tb.TextWrapping != TextWrapping.NoWrap)
                    {
                        // πολυγραμμικό κείμενο: αν το ύψος που πήρε είναι μικρότερο από όσο χρειάζεται στο πλάτος που έχει, κόβεται κάθετα
                        var wrapped = new FormattedText(tb.Text, CultureInfo.CurrentUICulture, tb.FlowDirection, new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch), tb.FontSize, Brushes.Black, 1.0) { MaxTextWidth = Math.Max(1, tb.ActualWidth) };
                        if (wrapped.Height > tb.ActualHeight + 2 && tb.MaxHeight == double.PositiveInfinity)
                            problems.Add($"{context}: {Describe(button, tb.Text)} wraps to {wrapped.Height:0} px but only {tb.ActualHeight:0} px are available");
                        continue;
                    }
                    var natural = NaturalWidth(tb);

                    // (α) η διάταξη έδωσε στο κείμενο λιγότερο πλάτος από το φυσικό του
                    if (natural > tb.ActualWidth + 1.5 && tb.ActualWidth > 0)
                        problems.Add($"{context}: {Describe(button, tb.Text)} text needs {natural:0} px but got {tb.ActualWidth:0} px");

                    // (β) το κείμενο ξεπερνά τα όρια του κουμπιού
                    try
                    {
                        var origin = tb.TransformToAncestor(button).Transform(new Point(0, 0));
                        var right = origin.X + Math.Min(tb.ActualWidth, natural);
                        if (origin.X < -1.5 || right > button.ActualWidth + 1.5)
                            problems.Add($"{context}: {Describe(button, tb.Text)} sticks out of its button (text {origin.X:0}..{right:0}, button {button.ActualWidth:0} px)");
                    }
                    catch (InvalidOperationException) { }
                }
            }
            return problems.Distinct().ToList();
        }

        // Απογραφή λειτουργιών (για την ανασκόπηση): επικεφαλίδες καρτών και κείμενα κουμπιών ανά καρτέλα/υπο-σελίδα.
        private static IEnumerable<string> Inventory(DependencyObject root, string context)
        {
            var seen = new HashSet<string>();
            foreach (var tb in Descendants<TextBlock>(root))
            {
                if (!IsReallyVisible(tb) || string.IsNullOrWhiteSpace(tb.Text) || tb.Text.Length > 70) continue;
                bool inButton = false;
                for (DependencyObject d = tb; d != null; d = VisualTreeHelper.GetParent(d)) if (d is ButtonBase || (d is UserControl && d.GetType().Name.EndsWith("Button", StringComparison.Ordinal))) { inButton = true; break; }
                var heading = !inButton && tb.FontWeight >= FontWeights.SemiBold && tb.FontSize >= 13;
                if (!inButton && !heading) continue;
                var line = $"{context}	{(inButton ? "BTN" : "HEAD")}	{tb.Text.Trim()}";
                if (seen.Add(line)) yield return line;
            }
        }

        // Η λωρίδα καρτελών (κανονικό θέμα): καμία καρτέλα δεν πρέπει να είναι κομμένη στο άκρο του ScrollViewer της.
        internal static IEnumerable<string> TabStripCutProblems(Window w, string context)
        {
            if (w.FindName("TabStripScroll") is not ScrollViewer sv || sv.Visibility != Visibility.Visible || sv.ActualWidth <= 0) yield break;
            if (w.FindName("TabStrip") is not Panel strip) yield break;
            foreach (var rb in strip.Children.OfType<RadioButton>())
            {
                if (rb.Visibility != Visibility.Visible || rb.ActualWidth <= 0) continue;
                var left = rb.TransformToAncestor(sv).Transform(new Point(0, 0)).X;
                var right = left + rb.ActualWidth;
                if (left < -1.5 || right > sv.ViewportWidth + 1.5)
                    yield return $"{context}: tab '{rb.Tag}' is cut by the tab strip ({left:0}..{right:0} of {sv.ViewportWidth:0} px)";
            }
        }

        private static void Pump(Window w)
        {
            w.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            w.UpdateLayout();
        }

        private static void Snapshot(FrameworkElement el, string path)
        {
            try
            {
                var dpi = 1.0;
                var rtb = new RenderTargetBitmap((int)Math.Max(1, el.ActualWidth * dpi), (int)Math.Max(1, el.ActualHeight * dpi), 96, 96, PixelFormats.Pbgra32);
                rtb.Render(el);
                var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
                using var fs = File.Create(path); enc.Save(fs);
            }
            catch (Exception) { }
        }

        [Fact]
        public void NoButtonTextIsClippedInAnyTabOrLanguage()
        {
            var problems = new List<string>();
            var inventory = new List<string>();
            Exception? error = null;
            var outDir = Environment.GetEnvironmentVariable("GEARWIN_UIPROBE_OUT");
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
            var t = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null) { var app = new OptimizerWpf.App(); app.InitializeComponent(); }
                    OptimizerWpf.ThemeManager.LoadPersisted();
                    SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
                    var themeName = Environment.GetEnvironmentVariable("GEARWIN_UIPROBE_THEME");
                    OptimizerWpf.ThemeManager.SelectTheme(string.IsNullOrEmpty(themeName)
                        ? System.Linq.Enumerable.First(OptimizerWpf.ThemeCatalog.All)
                        : System.Linq.Enumerable.First(OptimizerWpf.ThemeCatalog.AllIncludingSkins, p => p.DisplayName == themeName));
                    var original = LanguageService.Current;
                    var probeWidth = double.TryParse(Environment.GetEnvironmentVariable("GEARWIN_UIPROBE_WIDTH"), out var pw) ? pw : 1280;
                    var w = new MainWindow { Width = probeWidth, Height = 860, WindowStartupLocation = WindowStartupLocation.Manual, Left = -5000, Top = 0 };
                    w.Show(); Pump(w);
                    var pending = typeof(MainWindow).GetField("_pendingSubPage", BindingFlags.Instance | BindingFlags.NonPublic);
                    var langEnv = Environment.GetEnvironmentVariable("GEARWIN_UIPROBE_LANGS") ?? "el,en,de,ru";   // τα μακρύτερα κείμενα· GEARWIN_UIPROBE_LANGS=all για και τις 14
                    foreach (var lang in langEnv == "all" ? Languages : langEnv.Split(','))
                    {
                        LanguageService.SetLanguage(lang);
                        foreach (var item in NavItems.All)
                        {
                            var pages = TabLayout.IsComposite(item.Tag) ? TabLayout.SubPages(item.Tag)!.Count : 1;
                            for (var page = 0; page < pages; page++)
                            {
                                pending?.SetValue(w, page);
                                w.SelectTab(item.Tag);
                                Pump(w); Pump(w);
                                // η υπο-σελίδα επιλέγεται όπως από τον χρήστη: κλικ στο chip της (το _pendingSubPage μόνο δεν την άλλαζε)
                                if (page > 0 && ((ContentControl)w.FindName("ContentHost")).Content is OptimizerWpf.Views.CompositeTabView composite && composite.FindName("SubStrip") is Panel strip && strip.Children.Count > page)
                                {
                                    ((RadioButton)strip.Children[page]).IsChecked = true;
                                    Pump(w); Pump(w);
                                    Assert.Equal(page, composite.CurrentPage);
                                }
                                var ctx = $"{lang}/{item.Tag}/{page}";
                                problems.AddRange(FindClippedButtonText(w, ctx));
                                problems.AddRange(TabStripCutProblems(w, ctx));
                                if (!string.IsNullOrEmpty(outDir) && lang == "en") inventory.AddRange(Inventory(w, ctx));
                                if (!string.IsNullOrEmpty(outDir) && (lang == "el" || lang == "en" || Environment.GetEnvironmentVariable("GEARWIN_UIPROBE_ALLSHOTS") == "1")) Snapshot(w, Path.Combine(outDir, $"{lang}_{item.Tag}_{page}.png"));
                            }
                        }
                    }
                    LanguageService.SetLanguage(original);
                    w.Close();
                }
                catch (Exception ex) { error = ex; }
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            if (!string.IsNullOrEmpty(outDir)) File.WriteAllLines(Path.Combine(outDir, "inventory.txt"), inventory);
            if (!string.IsNullOrEmpty(outDir)) File.WriteAllLines(Path.Combine(outDir, "clipping-report.txt"), new[] { "# button texts examined: " + Examined }.Concat(problems));
            Assert.True(Examined > 200, "the probe examined only " + Examined + " button texts - the layout was not realised");
            if (error != null) throw new Exception(error.Message, error);
            Assert.True(problems.Count == 0, problems.Count + " clipped button texts:" + Environment.NewLine + string.Join(Environment.NewLine, problems.Take(60)));
        }
    }
}
