using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class UwpAppManagerWindow : Window
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        private readonly ObservableCollection<UwpAppInfo> _apps = new();

        public UwpAppManagerWindow()
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            ListApps.ItemsSource = _apps;
        }

        // ΔΙΟΡΘΩΣΗ (γενικός έλεγχος γραμμής κατάστασης, ρητό αίτημα χρήστη) - και οι δύο χειριστές
        // εδώ έκαναν πραγματική, όχι-στιγμιαία δουλειά (σάρωση Appx, απεγκατάσταση) χωρίς να αγγίζουν
        // καθόλου το StatusService - το τοπικό TxtStatus δεν αρκεί, ασυνεπές με όλες τις άλλες
        // σαρώσεις/ενέργειες της εφαρμογής που ήδη δείχνουν το κυκλάκι στην κύρια γραμμή κατάστασης.
        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            TxtStatus.Text = LanguageService.T("Adv_Scanning");
            StatusService.SetBusy(LanguageService.T("Adv_Scanning"));
            _apps.Clear();
            var results = await BloatwareService.ScanUwpAppsAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            foreach (var a in results) _apps.Add(a);
            TxtStatus.Text = $"{LanguageService.T("Uwp_FoundPrefix")}{results.Count}{LanguageService.T("Uwp_FoundSuffix")}";
        }

        private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: UwpAppInfo app }) return;
            if (ThemedMessageBox.Show($"{LanguageService.T("Uwp_UninstallConfirmPrefix")}{app.PackageFullName};", LanguageService.T("Adv_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            StatusService.SetBusy($"{LanguageService.T("Uwp_UninstallConfirmPrefix")}{app.PackageFullName}...");
            var ok = await BloatwareService.RemoveUwpAppAsync(app.PackageFullName);
            StatusService.SetIdle(LanguageService.T("Ready"));
            if (ok) _apps.Remove(app);
            else ThemedMessageBox.Show(LanguageService.T("Uwp_UninstallFailed"), LanguageService.T("Adv_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
