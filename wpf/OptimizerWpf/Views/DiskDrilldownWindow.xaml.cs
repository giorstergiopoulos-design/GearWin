using System.Collections.Generic;
using System.Linq;
using System.Windows;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class DiskDrilldownWindow : Window
    {
        private readonly IReadOnlyList<string> _roots;

        public DiskDrilldownWindow(string categoryLabel, IReadOnlyList<string> roots)
        {
            InitializeComponent();
            Title = $"Περαιτέρω Ανάλυση: {categoryLabel}";
            _roots = roots;
            Loaded += async (_, _) => await RunScanAsync();
        }

        private async System.Threading.Tasks.Task RunScanAsync()
        {
            var results = await DiskAnalysisService.DrilldownAsync(_roots);
            if (results.Count == 0)
            {
                TxtStatus.Text = "Δεν βρέθηκαν υποφάκελοι.";
                return;
            }
            TxtStatus.Text = $"Ταξινομημένα κατά μέγεθος ({results.Count}):";
            ListResults.ItemsSource = results.ToList();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
