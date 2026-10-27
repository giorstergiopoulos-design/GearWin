using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    // Κινούμενο εικονίδιο εφαρμογής της σουίτας: πραγματικό εικονίδιο (.ico ως resource) που αιωρείται ήπια, "ανασαίνει" και αναπηδά στο hover.
    // Αν λείπει το εικονίδιο, σχεδιάζεται μονόγραμμα. Σε αδύναμα μηχανήματα δεν τρέχει συνεχής κίνηση (μόνο στατικό).
    public sealed class SuiteIcon : Border
    {
        private readonly TranslateTransform _float = new();
        private readonly ScaleTransform _scale = new(1, 1);

        public SuiteIcon(string appId, string name, double size = 64)
        {
            Width = Height = size;
            CornerRadius = new CornerRadius(size * 0.22);
            Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));
            Padding = new Thickness(size * 0.1);
            RenderTransformOrigin = new Point(0.5, 0.5);
            var group = new TransformGroup();
            group.Children.Add(_scale);
            group.Children.Add(_float);
            RenderTransform = group;
            var img = LoadIcon(appId);
            if (img != null) Child = new Image { Source = img, Stretch = Stretch.Uniform, SnapsToDevicePixels = true };
            else Child = new TextBlock { Text = name.Length > 0 ? name[..1].ToUpperInvariant() : "?", FontSize = size * 0.5, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            MouseEnter += (_, _) => Bounce();
            Loaded += (_, _) => StartIdle();
        }

        public static ImageSource? LoadIcon(string appId)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/{typeof(SuiteIcon).Assembly.GetName().Name};component/Assets/suite/{appId}.ico", UriKind.Absolute);
                var dec = BitmapDecoder.Create(uri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var best = dec.Frames.OrderByDescending(f => f.PixelWidth).FirstOrDefault(f => f.PixelWidth <= 256) ?? dec.Frames.FirstOrDefault();
                best?.Freeze();
                return best;
            }
            catch { return null; }
        }

        private void StartIdle()
        {
            if (OverlayCardHost.SimpleAnimations) return;
            var rnd = new Random(Name?.GetHashCode() ?? Environment.TickCount);
            var bob = new DoubleAnimation(-3, 3, TimeSpan.FromMilliseconds(1700 + rnd.Next(0, 700))) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase(), BeginTime = TimeSpan.FromMilliseconds(rnd.Next(0, 800)) };
            _float.BeginAnimation(TranslateTransform.YProperty, bob);
        }

        private void Bounce()
        {
            if (OverlayCardHost.SimpleAnimations) return;
            var a = new DoubleAnimation(1, 1.18, TimeSpan.FromMilliseconds(180)) { AutoReverse = true, EasingFunction = new BackEase { Amplitude = 0.4 } };
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }
    }

    public partial class SuiteShowcaseWindow : Window
    {
        public const string OfferedKey = "suite-offered";
        public const string SelfId = "gearwin";

        private readonly MainWindow _main;
        private readonly IInstallProbe _probe;
        private readonly bool _network;
        private SuiteCatalog _catalog;
        private readonly Dictionary<string, ReleaseData?> _latest = new();
        private bool _closing;

        public SuiteShowcaseWindow(MainWindow main, IInstallProbe? probe = null, SuiteCatalog? catalog = null, bool allowNetwork = true)
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            _main = main;
            _probe = probe ?? new SystemInstallProbe();
            _network = allowNetwork;
            _catalog = catalog ?? SuiteCatalogService.LoadEmbedded();
            OverlayCardHost.Attach(this, main);
            Rebuild();
            LanguageService.Changed += OnLanguageChanged;
            Closed += (_, _) => LanguageService.Changed -= OnLanguageChanged;
            Loaded += async (_, _) => { Focus(); BtnClose.Focus(); if (_network) await RefreshAsync(); };
        }

        private void OnLanguageChanged() => Dispatcher.BeginInvoke(new Action(Rebuild));

        private string Lang => LanguageService.Current;

        // Ανιχνεύει εγκατεστημένες (μόνο ανάγνωση) και, αν επιτρέπεται δίκτυο, φρεσκάρει τον κατάλογο + τις τελευταίες εκδόσεις από το GitHub.
        private async Task RefreshAsync()
        {
            try
            {
                var (cat, live, source) = await SuiteCatalogService.LoadAsync();
                if (!ReferenceEquals(cat, _catalog) && cat.Updated != _catalog.Updated) { _catalog = cat; Rebuild(); }
                var installed = await Task.Run(() => _catalog.Apps.Where(a => a.Id != SelfId && InstalledAppDetector.Detect(a, _probe).Installed).ToList());
                var tasks = installed.Select(async a =>
                {
                    var r = await GitHubReleaseClient.GetLatestAsync(SuiteCatalogService.Owner, a.Repo);
                    return (a.Id, r.Release);
                }).ToList();
                var any = false;
                foreach (var t in await Task.WhenAll(tasks)) { _latest[t.Id] = t.Release; any |= t.Release != null; }
                if (any) Rebuild();
                else if (installed.Count > 0) TxtNote.Text = LanguageService.T("Suite_Offline");
            }
            catch { TxtNote.Text = LanguageService.T("Suite_Offline"); }
        }

        public int CardCount => CardsPanel.Children.Count;

        private void Rebuild()
        {
            CardsPanel.Children.Clear();
            foreach (var app in _catalog.Apps) CardsPanel.Children.Add(BuildCard(app));
        }

        private FrameworkElement BuildCard(SuiteApp app)
        {
            var self = app.Id == SelfId;
            var info = self ? new InstalledInfo(true, ReleaseVersion.FromVersion(SelfUpdateService.Current), Environment.ProcessPath) : InstalledAppDetector.Detect(app, _probe);
            _latest.TryGetValue(app.Id, out var latest);
            var state = InstalledAppDetector.StateFor(self, info, latest?.Version);

            Brush Res(string key) => (Brush)FindResource(key);
            var root = new Border
            {
                Width = 330, Margin = new Thickness(8), Padding = new Thickness(16, 14, 16, 14), CornerRadius = new CornerRadius(12),
                Background = Res("MainBgBrush"), BorderBrush = Res(self ? "AccentBrush" : "BtnDefaultBrush"), BorderThickness = new Thickness(self ? 2 : 1),
            };
            AutomationProperties.SetName(root, app.Name);
            var stack = new StackPanel();
            root.Child = stack;

            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var icon = new SuiteIcon(app.Id, app.Name, 60) { Name = "Icon_" + app.Id, Margin = new Thickness(0, 0, 12, 0) };
            head.Children.Add(icon);
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(new TextBlock { Text = app.Name, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Res("TextBrush"), TextWrapping = TextWrapping.Wrap });
            titles.Children.Add(new TextBlock { Text = app.TaglineFor(Lang), FontSize = 12, Foreground = Res("SubTextBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(titles, 1);
            head.Children.Add(titles);
            stack.Children.Add(head);

            var statusText = state switch
            {
                SuiteInstallState.ThisApp => LanguageService.T("Suite_YouAreHere"),
                SuiteInstallState.UpdateAvailable => string.Format(LanguageService.T("Suite_UpdateAvail"), info.Version, latest!.Version),
                SuiteInstallState.Installed => info.Version != null ? string.Format(LanguageService.T("Suite_Installed"), info.Version) : LanguageService.T("Suite_InstalledNoVer"),
                _ => LanguageService.T("Suite_NotInstalled"),
            };
            var chip = new TextBlock
            {
                Text = statusText, FontSize = 12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 6),
                Foreground = state is SuiteInstallState.ThisApp or SuiteInstallState.UpdateAvailable ? Res("AccentBrush") : Res("SubTextBrush"),
            };
            chip.Name = "Status_" + app.Id;
            stack.Children.Add(chip);

            foreach (var cap in app.CapabilitiesFor(Lang).Take(5))
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.Children.Add(new TextBlock { Text = "•", Foreground = Res("AccentBrush"), FontSize = 12 });
                var t = new TextBlock { Text = cap, FontSize = 12, Foreground = Res("TextBrush"), TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(t, 1);
                row.Children.Add(t);
                stack.Children.Add(row);
            }

            var buttons = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            Button Make(string text, bool accent, RoutedEventHandler click)
            {
                var b = new Button { Content = text, Style = (Style)FindResource(accent ? "AccentButtonStyle" : "FlatButtonStyle"), Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 6) };
                b.Click += click;
                return b;
            }
            if (state is SuiteInstallState.Installed or SuiteInstallState.UpdateAvailable && info.ExePath != null)
                buttons.Children.Add(Make(LanguageService.T("Suite_Open"), state == SuiteInstallState.Installed, (_, _) => OpenApp(info.ExePath!)));
            if (state == SuiteInstallState.NotInstalled)
                buttons.Children.Add(Make(LanguageService.T("Suite_Install"), true, async (s, _) => await InstallAsync(app, (Button)s)));
            else if (state == SuiteInstallState.UpdateAvailable)
                buttons.Children.Add(Make(LanguageService.T("Suite_Update"), true, async (s, _) => await InstallAsync(app, (Button)s)));
            buttons.Children.Add(Make(LanguageService.T("Suite_Details"), false, (_, _) => ReleaseCheckService.OpenReleasePage(app.RepoUrl)));
            stack.Children.Add(buttons);
            return root;
        }

        private static void OpenApp(string exe)
        {
            try { if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) }); } catch { }
        }

        // Install/Update: ποτέ σιωπηλά. Επιβεβαίωση με όνομα/μέγεθος/πηγή → λήψη στις Λήψεις → έλεγχος SHA-256 (απόρριψη σε αναντιστοιχία).
        // Χωρίς δυνατότητα επαλήθευσης ανοίγει μόνο η σελίδα του release.
        private async Task InstallAsync(SuiteApp app, Button button)
        {
            var original = button.Content;
            button.IsEnabled = false;
            try
            {
                var fetch = await GitHubReleaseClient.GetLatestAsync(SuiteCatalogService.Owner, app.Repo);
                if (fetch.Release == null)
                {
                    ThemedMessageBox.Show(LanguageService.T("Suite_NoRelease"), app.Name, MessageBoxButton.OK, MessageBoxImage.Information);
                    ReleaseCheckService.OpenReleasePage(app.ReleasesUrl);
                    return;
                }
                var rel = fetch.Release;
                var item = await SuiteInstallerService.ResolveAsync(app, rel);
                if (item == null)
                {
                    ThemedMessageBox.Show(LanguageService.T("Suite_NoVerify"), app.Name, MessageBoxButton.OK, MessageBoxImage.Information);
                    ReleaseCheckService.OpenReleasePage(rel.PageUrl);
                    return;
                }
                var size = item.Asset.Size > 0 ? $"{item.Asset.Size / 1048576.0:0.#} MB" : "?";
                var ask = string.Format(LanguageService.T("Suite_ConfirmBody"), app.Name, rel.Tag, item.Asset.Name, size, $"github.com/{SuiteCatalogService.Owner}/{app.Repo}");
                if (ThemedMessageBox.Show(ask, LanguageService.T("Suite_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                var progress = new Progress<double>(p => button.Content = $"{p:P0}");
                string path;
                try { path = await SuiteInstallerService.DownloadAsync(item, SuiteInstallerService.DownloadsFolder(), progress: progress); }
                catch (InvalidDataException) { ThemedMessageBox.Show(LanguageService.T("Suite_HashMismatch"), app.Name, MessageBoxButton.OK, MessageBoxImage.Error); return; }
                catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException)
                { ThemedMessageBox.Show(LanguageService.T("Suite_DownloadFailed") + "\n" + ex.Message, app.Name, MessageBoxButton.OK, MessageBoxImage.Error); return; }
                ThemedMessageBox.Show(string.Format(LanguageService.T("Suite_DownloadDone"), path), app.Name, MessageBoxButton.OK, MessageBoxImage.Information);
                try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }); } catch { }
            }
            finally { button.Content = original; button.IsEnabled = true; }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => CloseWithEffect();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { CloseWithEffect(); e.Handled = true; }
        }

        public void CloseWithEffect()
        {
            if (_closing) return;
            _closing = true;
            OverlayCardHost.CloseIntoWindow(this, Card, Backdrop, OverlayCardHost.TitleIcon(_main));
        }
    }
}
