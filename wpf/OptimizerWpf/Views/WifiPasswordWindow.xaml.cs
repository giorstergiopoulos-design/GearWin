using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class WifiPasswordWindow : Window
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        private readonly ObservableCollection<WifiPasswordRow> _rows = new();

        public WifiPasswordWindow()
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            ListWifiProfiles.ItemsSource = _rows;
            _ = ScanAsync();
        }

        private async Task ScanAsync()
        {
            TxtStatus.Text = LanguageService.T("WifiPwd_Scanning");
            StatusService.SetBusy(LanguageService.T("WifiPwd_Scanning"));

            var profiles = await WifiPasswordService.GetProfilesAsync();

            _rows.Clear();
            foreach (var p in profiles.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
                _rows.Add(new WifiPasswordRow(p.Name, p.Password));

            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtStatus.Text = _rows.Count == 0
                ? LanguageService.T("WifiPwd_NoneFound")
                : string.Format(LanguageService.T("WifiPwd_FoundFormat"), _rows.Count);
        }

        private void BtnToggleReveal_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button { Tag: WifiPasswordRow row }) row.IsRevealed = !row.IsRevealed;
        }

        private void BtnCopyPassword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button { Tag: WifiPasswordRow row } || row.Password == null) return;
            SecureClipboardService.SetSensitiveText(row.Password);
            StatusService.SetIdle(LanguageService.T("PwdMgr_Copied"));
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class WifiPasswordRow : INotifyPropertyChanged
    {
        public string Name { get; }
        public string? Password { get; }

        public WifiPasswordRow(string name, string? password)
        {
            Name = name;
            Password = password;
        }

        private bool _isRevealed;
        public bool IsRevealed
        {
            get => _isRevealed;
            set
            {
                _isRevealed = value;
                OnChanged(nameof(IsRevealed));
                OnChanged(nameof(DisplayPassword));
                OnChanged(nameof(ToggleLabel));
            }
        }

        public string DisplayPassword => Password == null
            ? LanguageService.T("WifiPwd_OpenNetwork")
            : (IsRevealed ? Password : new string('•', Math.Min(Math.Max(Password.Length, 6), 14)));

        public string ToggleLabel => IsRevealed ? LanguageService.T("PwdMgr_Hide") : LanguageService.T("PwdMgr_Show");

        public Visibility HasPasswordVisibility => Password == null ? Visibility.Collapsed : Visibility.Visible;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
