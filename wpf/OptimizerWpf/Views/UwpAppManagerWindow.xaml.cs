using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class UwpAppManagerWindow : Window
    {
        private readonly ObservableCollection<UwpAppInfo> _apps = new();

        public UwpAppManagerWindow()
        {
            InitializeComponent();
            ListApps.ItemsSource = _apps;
        }

        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            TxtStatus.Text = "Σάρωση...";
            _apps.Clear();
            var results = await BloatwareService.ScanUwpAppsAsync();
            foreach (var a in results) _apps.Add(a);
            TxtStatus.Text = $"Βρέθηκαν {results.Count} εφαρμογές.";
        }

        private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: UwpAppInfo app }) return;
            if (MessageBox.Show($"Απεγκατάσταση του πακέτου: {app.PackageFullName};", "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var ok = await BloatwareService.RemoveUwpAppAsync(app.PackageFullName);
            if (ok) _apps.Remove(app);
            else MessageBox.Show("Η απεγκατάσταση απέτυχε.", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
