using System;
using System.Linq;
using System.Windows;

namespace OptimizerWpf
{
    // Swaps the merged theme ResourceDictionary at runtime - mirrors Optimizer.ps1's
    // $global:isDarkMode toggle + Update-UITheme, but WPF's DynamicResource bindings mean every
    // control that reads a theme brush repaints itself automatically, with no manual per-control
    // refresh loop needed (unlike the WinForms allCards/allTextLabels tracking arrays).
    public static class ThemeManager
    {
        public static bool IsDarkMode { get; private set; } = true;

        public static void SetTheme(bool dark)
        {
            IsDarkMode = dark;
            var dictName = dark ? "Themes/Dark.xaml" : "Themes/Light.xaml";
            var merged = Application.Current.Resources.MergedDictionaries;

            var themeDict = merged.FirstOrDefault(d =>
                d.Source != null &&
                (d.Source.OriginalString.Contains("Dark.xaml") || d.Source.OriginalString.Contains("Light.xaml")));

            var newDict = new ResourceDictionary { Source = new Uri(dictName, UriKind.Relative) };

            if (themeDict != null)
            {
                var index = merged.IndexOf(themeDict);
                merged.RemoveAt(index);
                merged.Insert(index, newDict);
            }
            else
            {
                merged.Insert(0, newDict);
            }
        }

        public static void ToggleTheme() => SetTheme(!IsDarkMode);
    }
}
