using System.Configuration;
using System.Data;
using System.Windows;

namespace OptimizerWpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Applies the default theme's brushes BEFORE MainWindow is constructed, so every
        // DynamicResource binding in its XAML already has a real value on the very first frame
        // instead of resolving to nothing until the first theme change.
        ThemeManager.ApplyTheme(ThemeCatalog.Windows11FluentDark, forceDarkMode: true);
    }
}

