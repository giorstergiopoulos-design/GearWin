using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests
{
    // Το κουμπί Φωτεινό/Σκούρο Θέμα είχε σταθερό Width=120 και (μετά τη διόρθωση του Padding στο 6.4.0) κόβονταν η περιγραφή.
    // Ελέγχεται σε ΟΛΕΣ τις γλώσσες και στις δύο καταστάσεις ότι το κείμενο χωράει μέσα στο κουμπί.
    public class HeaderLayoutTests
    {
        [Fact]
        public void ThemeToggleTextFitsInEveryLanguageAndMode()
        {
            var problems = new List<string>();
            Exception? error = null;
            var t = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null) { var app = new OptimizerWpf.App(); app.InitializeComponent(); }
                    OptimizerWpf.ThemeManager.LoadPersisted();
                    SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
                    var original = LanguageService.Current;
                    var w = new MainWindow { Width = 1300, Height = 800, WindowStartupLocation = WindowStartupLocation.Manual, Left = -5000, Top = 0 };
                    w.Show();
                    var button = (Button)w.FindName("BtnThemeToggle");
                    var text = (TextBlock)w.FindName("TxtThemeToggle");
                    void Pump() => w.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    foreach (var code in new[] { "el", "en", "de", "fr", "es", "ko", "zh", "it", "ru", "ja", "pt", "tr", "ar", "hi" })
                    {
                        LanguageService.SetLanguage(code);
                        for (var mode = 0; mode < 2; mode++)
                        {
                            Pump(); w.UpdateLayout();
                            var natural = new System.Windows.Media.FormattedText(text.Text, System.Globalization.CultureInfo.CurrentUICulture, text.FlowDirection, new System.Windows.Media.Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize, System.Windows.Media.Brushes.Black, 1.0).WidthIncludingTrailingWhitespace; var needed = natural + button.Padding.Left + button.Padding.Right;
                            if (needed > button.ActualWidth + 0.5)
                                problems.Add($"{code} (dark={OptimizerWpf.ThemeManager.IsDarkMode}): '{text.Text}' needs {needed:0} px but the button is {button.ActualWidth:0} px");
                            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); // εναλλαγή φωτεινό/σκούρο
                        }
                    }
                    LanguageService.SetLanguage(original);
                    w.Close();
                }
                catch (Exception ex) { error = ex; }
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            if (error != null) throw new Exception(error.Message, error);
            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
        }
    }
}
