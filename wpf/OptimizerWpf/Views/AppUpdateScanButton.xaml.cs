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

        // captionKey: κλειδί μετάφρασης για το κείμενο κάτω από το εικονίδιο κατά τη λειτουργία (π.χ.
        // "Opt_InstallingCaps" στην εγκατάσταση αντί του προεπιλεγμένου "Opt_ScanCaps" στη σάρωση) -
        // μικρότερο FontSize για λέξεις μεγαλύτερες από "ΣΑΡΩΣΗ"/"SCAN" ώστε να μη ξεχειλίζουν τον κύκλο.
        public void SetScanning(bool scanning, string? captionKey = null)
        {
            ProgressArc.Visibility = scanning ? Visibility.Visible : Visibility.Collapsed;
            IconGroup.Opacity = scanning ? 1.0 : 0.55;
            if (captionKey != null)
            {
                TxtCaption.Text = Services.LanguageService.T(captionKey);
                TxtCaption.FontSize = 13;
            }
            else
            {
                TxtCaption.Text = Services.LanguageService.T("Opt_ScanCaps");
                TxtCaption.FontSize = 17;
            }
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
