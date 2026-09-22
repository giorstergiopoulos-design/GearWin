using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OptimizerWpf.Views
{
    public partial class AppUpdateScanButton : UserControl
    {
        public event RoutedEventHandler? Click;

        public AppUpdateScanButton()
        {
            InitializeComponent();
        }

        private bool _isEnabled = true;
        public new bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; Root.Cursor = value ? Cursors.Hand : Cursors.Arrow; Root.Opacity = value ? 1.0 : 0.6; }
        }

        private void Root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isEnabled) Click?.Invoke(this, new RoutedEventArgs());
        }

        public void SetScanning(bool scanning)
        {
            ProgressArc.Visibility = scanning ? Visibility.Visible : Visibility.Collapsed;
            IconGroup.Opacity = scanning ? 1.0 : 0.55;
            if (scanning)
            {
                var arcAnim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1.4))) { RepeatBehavior = RepeatBehavior.Forever };
                ArcRotate.BeginAnimation(RotateTransform.AngleProperty, arcAnim);
                var bobAnim = new DoubleAnimation { From = 0, To = -8, Duration = TimeSpan.FromSeconds(0.55), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };
                IconBob.BeginAnimation(TranslateTransform.YProperty, bobAnim);
            }
            else
            {
                ArcRotate.BeginAnimation(RotateTransform.AngleProperty, null);
                IconBob.BeginAnimation(TranslateTransform.YProperty, null);
            }
        }
    }
}
