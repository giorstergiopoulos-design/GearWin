using System;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using OptimizerWpf.Services;
using Xunit;

namespace OptimizerWpf.Tests
{
    // ButtonAutoFit (Services/ButtonAutoFit.cs) is the universal fix for "button text touches its
    // edges" - instead of hand-picking a MinWidth per button (what every previous fix did), it
    // measures the button's own text+Padding+BorderThickness and sets MinWidth automatically. These
    // tests run the measurement directly (no visual tree / Loaded event needed - see the IsLoaded
    // check living in the Loaded handler, not in ApplyMinWidth, specifically so this is testable) to
    // lock in the behavior so it can't silently regress.
    public class ButtonAutoFitTests
    {
        private static T RunOnSta<T>(Func<T> action)
        {
            T result = default!;
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try { result = action(); }
                catch (Exception ex) { error = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error != null) throw error;
            return result;
        }

        private static Button MakeButton(string text, string padding = "14,8", double minWidth = 0)
        {
            var parts = padding.Split(',');
            var button = new Button
            {
                Content = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                Padding = new System.Windows.Thickness(double.Parse(parts[0]), double.Parse(parts[1]), double.Parse(parts[0]), double.Parse(parts[1])),
                BorderThickness = new System.Windows.Thickness(0),
                MinWidth = minWidth,
            };
            return button;
        }

        [Fact]
        public void ApplyMinWidth_GrowsMinWidthToFitTextAndPadding()
        {
            RunOnSta(() =>
            {
                var button = MakeButton("Έλεγχος Ενημερώσεων μέσω Microsoft Store");
                ButtonAutoFit.ApplyMinWidth(button);

                Assert.True(button.MinWidth > button.Padding.Left + button.Padding.Right,
                    "MinWidth should cover at least the text width plus padding, not just padding.");
                return true;
            });
        }

        [Fact]
        public void ApplyMinWidth_LongerTextNeedsMoreWidthThanShorterText()
        {
            RunOnSta(() =>
            {
                var shortButton = MakeButton("OK");
                var longButton = MakeButton("Αναβάθμιση Επιλεγμένων Εφαρμογών Τώρα");

                ButtonAutoFit.ApplyMinWidth(shortButton);
                ButtonAutoFit.ApplyMinWidth(longButton);

                Assert.True(longButton.MinWidth > shortButton.MinWidth);
                return true;
            });
        }

        [Fact]
        public void ApplyMinWidth_NeverShrinksAnExistingLargerMinWidth()
        {
            RunOnSta(() =>
            {
                var button = MakeButton("OK", minWidth: 500);
                ButtonAutoFit.ApplyMinWidth(button);

                Assert.Equal(500, button.MinWidth);
                return true;
            });
        }

        [Fact]
        public void ApplyMinWidth_RaisesAnExistingMinWidthThatIsTooSmall()
        {
            RunOnSta(() =>
            {
                var button = MakeButton("Έλεγχος Ενημερώσεων μέσω Microsoft Store", minWidth: 10);
                ButtonAutoFit.ApplyMinWidth(button);

                Assert.True(button.MinWidth > 10);
                return true;
            });
        }

        [Fact]
        public void ApplyMinWidth_IgnoresNonStringContentWithoutThrowing()
        {
            RunOnSta(() =>
            {
                var button = new Button { Content = new StackPanel() };
                ButtonAutoFit.ApplyMinWidth(button);

                Assert.Equal(0, button.MinWidth);
                return true;
            });
        }

        [Fact]
        public void ApplyMinWidth_WiderPaddingNeedsMoreWidthThanNarrowerPadding()
        {
            RunOnSta(() =>
            {
                var tight = MakeButton("Αποθήκευση", padding: "4,2");
                var wide = MakeButton("Αποθήκευση", padding: "40,20");

                ButtonAutoFit.ApplyMinWidth(tight);
                ButtonAutoFit.ApplyMinWidth(wide);

                Assert.True(wide.MinWidth > tight.MinWidth);
                return true;
            });
        }
    }
}
