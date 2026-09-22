using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OptimizerWpf.Views
{
    public partial class SplashWindow : Window
    {
        private readonly MainWindow _main;

        public SplashWindow(MainWindow main)
        {
            InitializeComponent();
            TxtSplashVersion.Text = App.DisplayVersion;
            _main = main;
            Left = main.Left;
            Top = main.Top;
            Width = main.ActualWidth;
            Height = main.ActualHeight;
            Loaded += SplashWindow_Loaded;
        }

        private void SplashWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeManager.AttachBackground(SplashBackground);

            Canvas.SetLeft(SplashGearBorder, (Width - SplashGearBorder.Width) / 2);
            Canvas.SetTop(SplashGearBorder, (Height - SplashGearBorder.Height) / 2 - 70);

            var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
            delay.Tick += (_, _) => { delay.Stop(); AnimateIntoMainWindow(); };
            delay.Start();
        }

        // Το γρανάζι "προσγειώνεται" στη ΘΕΣΗ και το ΜΕΓΕΘΟΣ του πραγματικού γραναζιού του κύριου
        // παραθύρου (TitleGearBorder) - η μετάφραση (Canvas.Left/Top) χρησιμοποιεί ΠΑΝΤΑ το αρχικό,
        // αναλλοίωτο ActualWidth/Height του SplashGearBorder ώστε το ΚΕΝΤΡΟ του να παραμένει συνεπές
        // ενώ το ScaleTransform (γύρω από το ίδιο κέντρο, RenderTransformOrigin 0.5,0.5) το σμικρύνει
        // οπτικά - οι δύο κινήσεις δεν παρεμβαίνουν η μία στην άλλη.
        private void AnimateIntoMainWindow()
        {
            var targetScreen = _main.TitleGearBorder.PointToScreen(
                new Point(_main.TitleGearBorder.ActualWidth / 2, _main.TitleGearBorder.ActualHeight / 2));
            var targetLocal = new Point(targetScreen.X - Left, targetScreen.Y - Top);
            var targetScale = _main.TitleGearBorder.ActualWidth / SplashGearBorder.Width;

            var duration = new Duration(TimeSpan.FromMilliseconds(650));
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseInOut };

            TitleBlock.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(350))));

            var moveX = new DoubleAnimation(Canvas.GetLeft(SplashGearBorder), targetLocal.X - SplashGearBorder.Width / 2, duration) { EasingFunction = ease };
            var moveY = new DoubleAnimation(Canvas.GetTop(SplashGearBorder), targetLocal.Y - SplashGearBorder.Height / 2, duration) { EasingFunction = ease };
            var scale = new DoubleAnimation(1, targetScale, duration) { EasingFunction = ease };
            scale.Completed += (_, _) => Close();

            SplashGearBorder.BeginAnimation(Canvas.LeftProperty, moveX);
            SplashGearBorder.BeginAnimation(Canvas.TopProperty, moveY);
            GearScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            GearScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        }
    }
}
