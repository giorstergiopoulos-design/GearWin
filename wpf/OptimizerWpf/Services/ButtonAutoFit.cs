using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OptimizerWpf.Services
{
    // Universal λύση στο "το κείμενο εφάπτεται στις άκρες του κουμπιού" (ο χρήστης το ανέφερε
    // επανειλημμένα σε βάθος μηνών - βλ. PROJECT_STATE.md). Το Padding ΗΔΗ δουλεύει σωστά παντού· το
    // πραγματικό πρόβλημα είναι ότι το WPF Grid, ως έσχατο μέτρο όταν δεν επαρκεί ο διαθέσιμος χώρος
    // μιας σειράς, μπορεί να συμπιέσει μια Auto στήλη ΚΑΤΩ από το φυσικό της μέγεθος - χωρίς σφάλμα,
    // "καταπίνοντας" οπτικά το ήδη σωστό Padding. Αντί να μαντεύουμε MinWidth ανά κουμπί με το χέρι
    // (ό,τι είχε γίνει μέχρι τώρα), αυτό το attached behavior μετρά αυτόματα το πραγματικό απαιτούμενο
    // πλάτος κάθε κουμπιού (κείμενο + Padding + BorderThickness, στη ΔΙΚΗ του γραμματοσειρά/μέγεθος)
    // και του βάζει MinWidth μόνο του - σε κάθε γλώσσα, σε κάθε κουμπί, και σε κάθε νέο κουμπί που θα
    // γραφτεί στο μέλλον, αρκεί το style του να έχει το Enabled="True" (βλ. FlatButtonStyle στο
    // Styles.xaml - εφαρμόζεται μία φορά εκεί, κληρονομείται από το AccentButtonStyle).
    public static class ButtonAutoFit
    {
        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached(
                "Enabled",
                typeof(bool),
                typeof(ButtonAutoFit),
                new PropertyMetadata(false, OnEnabledChanged));

        public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
        public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

        private static readonly DependencyPropertyDescriptor ContentDescriptor =
            DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(Button))!;

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Button button) return;

            if ((bool)e.NewValue)
            {
                button.Loaded += Button_LoadedOrContentChanged;
                ContentDescriptor.AddValueChanged(button, Button_LoadedOrContentChanged);
            }
            else
            {
                button.Loaded -= Button_LoadedOrContentChanged;
                ContentDescriptor.RemoveValueChanged(button, Button_LoadedOrContentChanged);
            }
        }

        private static void Button_LoadedOrContentChanged(object? sender, EventArgs e)
        {
            // IsLoaded εδώ (όχι μέσα στο ApplyMinWidth) ώστε το ApplyMinWidth να είναι μονάδα-ελέγξιμο
            // ανεξάρτητα από το αν το κουμπί είναι προσαρτημένο σε ζωντανό visual tree (βλ.
            // ButtonAutoFitTests.cs) - FontFamily/Padding/BorderThickness που θέτει κανείς απευθείας
            // (χωρίς Style) είναι ήδη επίσημα τη στιγμή της κλήσης.
            if (sender is Button button && button.IsLoaded)
                ApplyMinWidth(button);
        }

        internal static void ApplyMinWidth(Button button)
        {
            // Κουμπιά χωρίς απλό κειμενικό Content (π.χ. εικονίδιο μέσα σε StackPanel/Border) δεν
            // καλύπτονται από αυτή τη μέτρηση - παραμένουν όπως ήταν, καμία οπισθοδρόμηση.
            if (button.Content is not string text || string.IsNullOrEmpty(text)) return;

            var typeface = new Typeface(button.FontFamily, button.FontStyle, button.FontWeight, button.FontStretch);
            double dpi = VisualTreeHelper.GetDpi(button).PixelsPerDip;
            var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                typeface, button.FontSize, System.Windows.Media.Brushes.Black, dpi);

            double needed = formatted.Width
                + button.Padding.Left + button.Padding.Right
                + button.BorderThickness.Left + button.BorderThickness.Right
                + 2; // μικρό περιθώριο μέτρησης (FormattedText έναντι πραγματικού glyph rendering)

            // Ποτέ δεν μικραίνει ένα ήδη μεγαλύτερο MinWidth (π.χ. από παλαιότερη χειροκίνητη διόρθωση) -
            // μόνο μεγαλώνει όταν η πραγματική μέτρηση το απαιτεί.
            if (needed > button.MinWidth)
                button.MinWidth = needed;
        }
    }
}
