using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OptimizerWpf.Services;
using OptimizerWpf.Views;

namespace OptimizerWpf.Tests
{
    // 6.5.3 - παράπονα χρήστη για τους ενημερωτές: (1) "αν αλλάξω καρτέλα πρέπει να ξεκινήσω από την αρχή", (2) "αν κάνω δύο φορές έλεγχο βλέπω τα
    // αποτελέσματα δύο φορές", (3) "η ροδέλα πάνω στη λίστα κυλά το παράθυρο και όχι τη λίστα".
    public class ScanStateTests
    {
        [Fact]
        public void OnlyOneScanRunsAtATimeEvenFromAnotherViewInstance()
        {
            var state = new ScanState();
            Assert.True(state.TryBegin());
            Assert.False(state.TryBegin());                 // ο χρήστης άλλαξε καρτέλα και ξαναπάτησε: δεύτερη σάρωση ΔΕΝ ξεκινά
            Assert.False(state.TryBegin(installing: true));
            Assert.True(state.Busy);
            state.End();
            Assert.False(state.Busy);
            Assert.True(state.TryBegin(installing: true));
            Assert.True(state.Installing);
        }

        [Fact]
        public void StatusAndBusySurviveForANewViewAndRaiseChanged()
        {
            var state = new ScanState();
            int changes = 0;
            state.Changed += () => changes++;
            state.TryBegin();
            state.SetStatus("scanning");
            // ένα "νέο View" απλώς διαβάζει την κοινή κατάσταση
            Assert.True(state.Busy);
            Assert.Equal("scanning", state.Status);
            state.End();
            Assert.True(changes >= 3);
            Assert.Equal("scanning", state.Status);
        }

        [Fact]
        public void ReplacingTheUpdateListNeverDuplicatesResults()
        {
            var first = new[]
            {
                new WingetUpdate("App A", "Vendor.A", "1.0", "2.0", "winget"),
                new WingetUpdate("App B", "Vendor.B", "1.0", "2.0", "winget"),
            };
            OptimizationView.ReplaceUpdates(first);
            Assert.Equal(2, OptimizationView.UpdateCountForTests);
            OptimizationView.ReplaceUpdates(first);          // δεύτερος έλεγχος με τα ίδια αποτελέσματα
            Assert.Equal(2, OptimizationView.UpdateCountForTests);
            OptimizationView.ReplaceUpdates(first.Concat(new[] { new WingetUpdate("App A", "vendor.a", "1.0", "2.0", "winget"), new WingetUpdate("App A", "Vendor.A", "1.0", "2.0", "msstore") }));
            Assert.Equal(3, OptimizationView.UpdateCountForTests);   // ίδιο id/πηγή (πεζά-κεφαλαία) = ένα· άλλη πηγή = ξεχωριστό
            OptimizationView.ReplaceUpdates(Array.Empty<WingetUpdate>());
            Assert.Equal(0, OptimizationView.UpdateCountForTests);
        }
    }

    public class NestedScrollTests
    {
        private static void Wheel(UIElement target, int delta)
        {
            var preview = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = UIElement.PreviewMouseWheelEvent, Source = target };
            target.RaiseEvent(preview);
            if (preview.Handled) return;
            var bubble = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = target };
            target.RaiseEvent(bubble);
        }

        private static void Pump(Window w) { w.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); w.UpdateLayout(); }

        private static void OnSta(Action body)
        {
            Exception? error = null;
            var t = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null) { var app = new OptimizerWpf.App(); app.InitializeComponent(); }
                    body();
                }
                catch (Exception ex) { error = ex; }
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            if (error != null) throw new Exception(error.Message, error);
        }

        // Ίδια δομή με την παραγωγή (CompositeTabView): σελίδα (Auto) > φιλοξενούμενη προβολή με ScrollViewer ΧΩΡΙΣ κάθετη κύλιση + Forward > λίστα (ScrollViewer + Forward).
        private static (Window w, ScrollViewer page, ScrollViewer list, TextBlock item) Build()
        {
            var items = new StackPanel();
            for (var i = 0; i < 40; i++) items.Children.Add(new TextBlock { Text = "update " + i, Height = 24 });
            var list = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = items };
            list.AddHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(NestedScrollHelper.Forward));
            var hostedPanel = new StackPanel();
            hostedPanel.Children.Add(new Border { MaxHeight = 150, Child = list });
            var hosted = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = hostedPanel };
            hosted.AddHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(NestedScrollHelper.Forward));
            var pagePanel = new StackPanel();
            pagePanel.Children.Add(new Border { Height = 200 });
            pagePanel.Children.Add(hosted);
            pagePanel.Children.Add(new Border { Height = 900 });
            var page = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = pagePanel };
            var w = new Window { Width = 500, Height = 400, Left = -5000, Top = 0, WindowStartupLocation = WindowStartupLocation.Manual, Content = page };
            w.Show(); Pump(w);
            return (w, page, list, (TextBlock)items.Children[3]);
        }

        [Fact]
        public void WheelOverTheUpdateListScrollsTheListNotThePage()
        {
            OnSta(() =>
            {
                var (w, page, list, item) = Build();
                Assert.True(list.ScrollableHeight > 0, "the test list must be scrollable");
                for (var i = 0; i < 3; i++) { Wheel(item, -120); Pump(w); }
                Assert.True(list.VerticalOffset > 0, $"the list did not scroll (list {list.VerticalOffset}/{list.ScrollableHeight}, page {page.VerticalOffset}/{page.ScrollableHeight})");
                Assert.Equal(0, page.VerticalOffset);
                w.Close();
            });
        }

        [Fact]
        public void WheelAtTheEndOfTheListContinuesWithThePage()
        {
            OnSta(() =>
            {
                var (w, page, list, item) = Build();
                list.ScrollToEnd(); Pump(w);
                var before = page.VerticalOffset;
                Wheel(item, -120); Pump(w);
                Assert.True(page.VerticalOffset > before, $"the page should scroll once the list is at its end (list {list.VerticalOffset}/{list.ScrollableHeight}, page {page.VerticalOffset}/{page.ScrollableHeight})");
                // και προς τα πάνω: η λίστα στο τέλος της κυλά πρώτα η ίδια
                var listOffset = list.VerticalOffset;
                Wheel(item, 120); Pump(w);
                Assert.True(list.VerticalOffset < listOffset, "scrolling up must move the list first");
                w.Close();
            });
        }

        [Fact]
        public void WheelOverContentWithoutAnInnerScrollerStillScrollsThePage()
        {
            OnSta(() =>
            {
                var (w, page, _, _) = Build();
                var spacer = ((StackPanel)page.Content).Children[0];
                Wheel(spacer, -120); Pump(w);
                Assert.True(page.VerticalOffset > 0);
                w.Close();
            });
        }
    }
}
