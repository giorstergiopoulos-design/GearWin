using System.Windows;
using System.Windows.Media;

namespace OptimizerWpf
{
    // Applies a ThemeColors palette directly onto the app's resource brushes (MainBgBrush,
    // CardBgBrush, etc. - the same keys every XAML file already binds to via DynamicResource).
    public static class ThemeManager
    {
        public static bool IsDarkMode { get; private set; } = true;
        public static ThemePair CurrentPair { get; private set; } = ThemeCatalog.Windows11Fluent;
        public static ThemeColors CurrentTheme => CurrentPair.Get(IsDarkMode);

        // Selects a theme (keeps the current Light/Dark state, applying that variant of the newly
        // picked theme) - what the theme picker calls.
        public static void SelectTheme(ThemePair pair)
        {
            CurrentPair = pair;
            Apply();
        }

        // Every theme has both variants now (see ThemeCatalog) - toggling always re-applies the
        // SAME theme's other palette, matching Optimizer.ps1's Get-ThemeColors (branches on
        // isDarkMode first, theme name second - the theme selection itself never changes on toggle).
        public static void ToggleLightDark()
        {
            IsDarkMode = !IsDarkMode;
            Apply();
        }

        private static void Apply()
        {
            var theme = CurrentTheme;
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
    }
}
