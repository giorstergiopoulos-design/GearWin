using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OptimizerWpf.Views
{
    // Κοινή βοήθεια για τα παράθυρα-κουρτίνα (οδηγός καλωσορίσματος, παράθυρο σουίτας): καλύπτουν ΑΚΡΙΒΩΣ την περιοχή περιεχομένου του κύριου
    // παραθύρου (όπως το SplashWindow) και όταν κλείνουν η "κάρτα" τους σμικρύνεται, μετακινείται και σβήνει (~400 ms) μέσα στο εικονίδιο του
    // κύριου παραθύρου (TitleGearBorder) - ίδιο εφέ με την κουρτίνα εκκίνησης. Σε αδύναμους υπολογιστές (Render tier 0) ή όταν οι animations
    // των Windows είναι κλειστές, γίνεται απλό fade.
    public static class OverlayCardHost
    {
        public const int CloseMilliseconds = 400;

        public static bool SimpleAnimations =>
            !SystemParameters.ClientAreaAnimation || (RenderCapability.Tier >> 16) == 0
            || Environment.GetEnvironmentVariable("GEARWIN_SIMPLE_ANIM") == "1";

        // Τοποθετεί το overlay ακριβώς πάνω στο περιεχόμενο του κύριου παραθύρου και το κρατά εκεί όσο το main μετακινείται/αλλάζει μέγεθος.
        public static void Attach(Window overlay, Window main)
        {
            var root = main.Content as FrameworkElement ?? main;
            void Fit()
            {
                try
                {
                    var origin = root.PointToScreen(new Point(0, 0));
                    var toDip = PresentationSource.FromVisual(main)?.CompositionTarget?.TransformFromDevice;
                    if (toDip != null) origin = toDip.Value.Transform(origin);
                    overlay.Left = origin.X;
                    overlay.Top = origin.Y;
                    overlay.Width = root.ActualWidth > 0 ? root.ActualWidth : main.ActualWidth;
                    overlay.Height = root.ActualHeight > 0 ? root.ActualHeight : main.ActualHeight;
                }
                catch (InvalidOperationException) { /* το main δεν είναι (πια) ορατό */ }
            }
            overlay.Owner = main;
            overlay.WindowStartupLocation = WindowStartupLocation.Manual;
            Fit();
            EventHandler h = (_, _) => Fit();
            SizeChangedEventHandler sh = (_, _) => Fit();
            main.LocationChanged += h;
            main.SizeChanged += sh;
            overlay.Closed += (_, _) => { main.LocationChanged -= h; main.SizeChanged -= sh; };
        }

        // Κλείνει το overlay με εφέ "σμίκρυνση μέσα στο εικονίδιο του παραθύρου". `card` = το στοιχείο που μικραίνει, `backdrop` = το φόντο που σβήνει.
        public static void CloseIntoWindow(Window overlay, FrameworkElement card, UIElement backdrop, FrameworkElement? target, Action? completed = null)
        {
            var done = false;
            void Finish()
            {
                if (done) return;
                done = true;
                completed?.Invoke();
                overlay.Close();
            }

            if (target == null || !target.IsVisible || target.ActualWidth <= 0 || SimpleAnimations)
            {
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(SimpleAnimations ? 160 : CloseMilliseconds)) { FillBehavior = FillBehavior.HoldEnd };
                fade.Completed += (_, _) => Finish();
                card.BeginAnimation(UIElement.OpacityProperty, fade);
                backdrop.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(backdrop.Opacity, 0, TimeSpan.FromMilliseconds(SimpleAnimations ? 160 : CloseMilliseconds)));
                return;
            }

            try
            {
                // διαφορετικά παράθυρα -> υπολογισμός μέσω συντεταγμένων οθόνης (px) και μετατροπή σε DIP του overlay
                var tScreen = target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
                var cScreen = card.PointToScreen(new Point(card.ActualWidth / 2, card.ActualHeight / 2));
                var fromDevice = PresentationSource.FromVisual(overlay)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
                var delta = fromDevice.Transform(new Vector(tScreen.X - cScreen.X, tScreen.Y - cScreen.Y));
                var scale = new ScaleTransform(1, 1);
                var move = new TranslateTransform(0, 0);
                var group = new TransformGroup();
                group.Children.Add(scale);
                group.Children.Add(move);
                card.RenderTransformOrigin = new Point(0.5, 0.5);
                card.RenderTransform = group;

                var dur = new Duration(TimeSpan.FromMilliseconds(CloseMilliseconds));
                var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
                var targetScale = Math.Max(0.03, Math.Min(0.3, target.ActualWidth / Math.Max(1, card.ActualWidth)));
                var sx = new DoubleAnimation(1, targetScale, dur) { EasingFunction = ease };
                sx.Completed += (_, _) => Finish();
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, targetScale, dur) { EasingFunction = ease });
                move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, delta.X, dur) { EasingFunction = ease });
                move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, delta.Y, dur) { EasingFunction = ease });
                card.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, dur) { BeginTime = TimeSpan.FromMilliseconds(CloseMilliseconds * 0.35) });
                backdrop.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(backdrop.Opacity, 0, dur));
            }
            catch (InvalidOperationException) { Finish(); }
        }

        // Θέση/μέγεθος του εικονιδίου-στόχου του κύριου παραθύρου (για snapshots: ενδιάμεση φάση της κίνησης).
        public static FrameworkElement? TitleIcon(Window main) => (main as MainWindow)?.TitleGearBorder;
    }
}
