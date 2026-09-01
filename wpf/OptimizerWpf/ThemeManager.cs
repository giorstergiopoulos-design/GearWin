using System.Windows;
using System.Windows.Media;

namespace OptimizerWpf
{
    // Applies a ThemeColors palette directly onto the app's resource brushes (MainBgBrush,
    // CardBgBrush, etc. - the same keys every XAML file already binds to via DynamicResource).
    // Replaced the earlier Dark.xaml/Light.xaml ResourceDictionary-swap approach: with 21 themes
    // now in ThemeCatalog, one dictionary file per theme would mean 21 near-duplicate XAML files -
    // setting brushes straight from a C# record is far less to keep in sync, and DynamicResource
    // still repaints every bound control automatically either way.
    public static class ThemeManager
    {
        public static bool IsDarkMode { get; private set; } = true;
        public static ThemeColors CurrentTheme { get; private set; } = ThemeCatalog.Windows11FluentDark;

        public static void ApplyTheme(ThemeColors theme, bool? forceDarkMode = null)
        {
            CurrentTheme = theme;
            if (forceDarkMode.HasValue) IsDarkMode = forceDarkMode.Value;

            var res = Application.Current.Resources;
            res["MainBgBrush"] = new SolidColorBrush(theme.MainBg);
            res["TabBgBrush"] = new SolidColorBrush(theme.TabBg);
            res["TabSelectBrush"] = new SolidColorBrush(theme.TabSelect);
            res["TextBrush"] = new SolidColorBrush(theme.Text);
            res["SubTextBrush"] = new SolidColorBrush(theme.SubText);
            res["StatusTextBrush"] = new SolidColorBrush(theme.StatusText);
            res["CardBgBrush"] = new SolidColorBrush(theme.CardBg);
            res["CardBorderBrush"] = new SolidColorBrush(theme.CardBorder);
            res["BtnDefaultBrush"] = new SolidColorBrush(theme.BtnDefault);
            res["BtnHoverBrush"] = new SolidColorBrush(theme.BtnHover);
            res["AccentBrush"] = new SolidColorBrush(theme.Accent);
        }

        // The Light Mode toggle only has real data for the default Windows 11 Fluent theme (the
        // only one Optimizer.ps1 fully built out a light variant for) - toggling it while any other
        // theme is selected just re-applies that theme's one (dark) palette. Documented gap, not a
        // bug: matches what's actually available to port right now.
        public static void ToggleLightDark()
        {
            var goingDark = !IsDarkMode;
            var isFluent = CurrentTheme == ThemeCatalog.Windows11FluentDark || CurrentTheme == ThemeCatalog.Windows11FluentLight;
            var next = isFluent
                ? (goingDark ? ThemeCatalog.Windows11FluentDark : ThemeCatalog.Windows11FluentLight)
                : CurrentTheme;
            ApplyTheme(next, goingDark);
        }
    }
}
