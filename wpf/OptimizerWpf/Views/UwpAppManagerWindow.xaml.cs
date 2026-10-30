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
            _view = System.Windows.Data.CollectionViewSource.GetDefaultView(_apps);
            _view.Filter = o => o is UwpAppInfo a && (TxtSearch.Text.Trim().Length == 0 || a.Name.Contains(TxtSearch.Text.Trim(), System.StringComparison.OrdinalIgnoreCase)
                                                    || a.PackageFullName.Contains(TxtSearch.Text.Trim(), System.StringComparison.OrdinalIgnoreCase));
            ListApps.ItemsSource = _view;
            Loaded += (_, _) => BtnScan_Click(this, new RoutedEventArgs());   // 6.9.1: η λίστα φαίνεται αμέσως (αυτόματη σάρωση στο άνοιγμα)
        }

        private readonly System.ComponentModel.ICollectionView _view;
        private bool _scanning;

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => _view.Refresh();

        // ΔΙΟΡΘΩΣΗ (γενικός έλεγχος γραμμής κατάστασης, ρητό αίτημα χρήστη) - και οι δύο χειριστές
        // εδώ έκαναν πραγματική, όχι-στιγμιαία δουλειά (σάρωση Appx, απεγκατάσταση) χωρίς να αγγίζουν
        // καθόλου το StatusService - το τοπικό TxtStatus δεν αρκεί, ασυνεπές με όλες τις άλλες
        // σαρώσεις/ενέργειες της εφαρμογής που ήδη δείχνουν το κυκλάκι στην κύρια γραμμή κατάστασης.
        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            if (_scanning) return;
            _scanning = true;
            TxtStatus.Text = LanguageService.T("Adv_Scanning");
            StatusService.SetBusy(LanguageService.T("Adv_Scanning"));
            _apps.Clear();
            var results = await BloatwareService.ScanUwpAppsAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            foreach (var a in results) _apps.Add(a);
            TxtStatus.Text = $"{LanguageService.T("Uwp_FoundPrefix")}{results.Count}{LanguageService.T("Uwp_FoundSuffix")}";
            _scanning = false;
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
