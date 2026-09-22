using System.Collections.Generic;
using System.Linq;
using System.Windows;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class DiskDrilldownWindow : Window
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        private readonly IReadOnlyList<string> _roots;

        public DiskDrilldownWindow(string categoryLabel, IReadOnlyList<string> roots)
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            Title = $"{LanguageService.T("Drilldown_Title")}: {categoryLabel}";
            _roots = roots;
            Loaded += async (_, _) => await RunScanAsync();
        }

        // ΔΙΟΡΘΩΣΗ (γενικός έλεγχος γραμμής κατάστασης, ρητό αίτημα χρήστη) - η σάρωση είχε ήδη το
        // δικό της τοπικό TxtStatus, αλλά ΔΕΝ άγγιζε καθόλου το κυκλάκι της κύριας γραμμής κατάστασης
        // (StatusService) - ασυνεπές με κάθε άλλη σάρωση της εφαρμογής.
        private async System.Threading.Tasks.Task RunScanAsync()
        {
            StatusService.SetBusy(LanguageService.T("Drilldown_InProgress"));
            var results = await DiskAnalysisService.DrilldownAsync(_roots);
            StatusService.SetIdle(LanguageService.T("Ready"));
            if (results.Count == 0)
            {
                TxtStatus.Text = LanguageService.T("Drilldown_NoSubfolders");
                return;
            }
            TxtStatus.Text = $"{LanguageService.T("Drilldown_SortedBySizePrefix")}{results.Count}):";
            ListResults.ItemsSource = results.ToList();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
