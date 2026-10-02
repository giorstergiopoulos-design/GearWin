using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class HealthTimelineWindow : Window
    {
        public HealthTimelineWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            TxtStatus.Text = LanguageService.T("Timeline_Scanning");
            var entries = await HealthTimelineService.ScanAsync();
            TxtStatus.Text = entries.Count == 0 ? LanguageService.T("Timeline_None") : "";
            ListTimeline.ItemsSource = entries.Select(Row).ToList();
        }

        private static string Label(TimelineEntry e) => e.Kind switch
        {
            TimelineKind.Bsod => LanguageService.T("Timeline_Bsod") + (e.Text.Length > 0 ? " - " + e.Text : ""),
            TimelineKind.Unexpected => LanguageService.T("Timeline_Unexpected"),
            TimelineKind.AppCrash => string.Format(LanguageService.T("Timeline_AppCrash"), e.Text),
            TimelineKind.Driver => string.Format(LanguageService.T("Timeline_Driver"), e.Text),
            _ => string.Format(LanguageService.T("Timeline_Install"), e.Text),
        };

        private static object Row(TimelineEntry e) => new
        {
            When = e.Time.ToString("g"),
            Label = Label(e),
            Brush = e.IsProblem ? new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)) : (Brush)Application.Current.FindResource("TextBrush"),
            Suspect = e.Suspect == null ? "" : string.Format(LanguageService.T("Timeline_Suspect"), Label(e.Suspect), e.Suspect.Time.ToString("g")),
            SuspectVisibility = e.Suspect == null ? Visibility.Collapsed : Visibility.Visible,
        };
    }
}
