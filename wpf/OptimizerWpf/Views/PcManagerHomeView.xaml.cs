using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class PcManagerHomeView : UserControl
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        public PcManagerHomeView()
        {
            InitializeComponent();
            Loaded += async (_, _) => await RefreshScoreAsync();
        }

        private async System.Threading.Tasks.Task RefreshScoreAsync()
        {
            try
            {
                var result = await System.Threading.Tasks.Task.Run(HealthScoreService.Compute);
                ShowScore(result);
            }
            catch { TxtHeadline.Text = LanguageService.T("PcmHome_Unknown"); }
        }

        private void ShowScore(HealthResult result)
        {
            TxtScore.Text = result.Score.ToString();
            ScoreArc.Data = BuildArc(result.Score / 100.0, 100, 93);
            ScoreArc.Stroke = result.Score >= 80 ? (Brush)FindResource("AccentBrush")
                : new SolidColorBrush(result.Score >= 60 ? Color.FromRgb(0xF5, 0x9E, 0x0B) : Color.FromRgb(0xE5, 0x39, 0x35));
            TxtHeadline.Text = LanguageService.T(result.Issues.Count == 0 ? "PcmHome_Healthy" : "PcmHome_NeedsAttention");
            TxtDetail.Text = result.Issues.Count == 0
                ? LanguageService.T("PcmHome_HealthyDetail")
                : string.Join("  •  ", result.Issues.Take(3).Select(i => i.Title));
        }

        // Τόξο που ξεκινά από την κορυφή και προχωρά δεξιόστροφα· fraction 1.0 = σχεδόν πλήρης κύκλος.
        public static Geometry BuildArc(double fraction, double center, double radius)
        {
            fraction = Math.Clamp(fraction, 0.0, 0.9999);
            if (fraction <= 0) return Geometry.Empty;
            var angle = fraction * 2 * Math.PI;
            var start = new Point(center, center - radius);
            var end = new Point(center + radius * Math.Sin(angle), center - radius * Math.Cos(angle));
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(start, false, false);
                ctx.ArcTo(end, new Size(radius, radius), 0, angle > Math.PI, SweepDirection.Clockwise, true, false);
            }
            geo.Freeze();
            return geo;
        }

        // Ενίσχυση (Boost): ελευθερώνει μνήμη από διεργασίες παρασκηνίου + καθαρίζει τα "προτεινόμενα"
        // προσωρινά (QuickCleanService, ίδιο σύνολο με τον Έλεγχο Υγείας) - χωρίς άλλες αλλαγές συστήματος.
        private async void BtnBoost_Click(object sender, RoutedEventArgs e)
        {
            BtnBoost.IsEnabled = false;
            TxtBoostResult.Text = LanguageService.T("PcmHome_Boosting");
            StatusService.SetBusy(LanguageService.T("PcmHome_Boosting"));
            try
            {
                var (_, freedMb) = await SystemService.FreeBackgroundMemoryAsync();
                var items = await QuickCleanService.ScanAsync();
                var keys = items.Where(i => i.Recommended).Select(i => i.Key).ToList();
                var freedBytes = await QuickCleanService.CleanAsync(keys);
                ImpactTrackingService.RecordBytesFreed(freedBytes);
                TxtBoostResult.Text = string.Format(LanguageService.T("PcmHome_BoostDone"), Math.Round(freedMb), QuickCleanService.FormatSize(freedBytes));
                (Window.GetWindow(this) as MainWindow)?.ShowToast(TxtBoostResult.Text);
            }
            catch (Exception ex) { TxtBoostResult.Text = ex.Message; }
            finally
            {
                StatusService.SetIdle(LanguageService.T("Ready"));
                BtnBoost.IsEnabled = true;
            }
            await RefreshScoreAsync();
        }

        private void BtnHealthCheck_Click(object sender, RoutedEventArgs e) =>
            new HealthCheckWindow { Owner = Window.GetWindow(this) }.ShowDialog();

        private void QuickCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string tag }) (Window.GetWindow(this) as MainWindow)?.SelectTab(tag);
        }
    }
}
