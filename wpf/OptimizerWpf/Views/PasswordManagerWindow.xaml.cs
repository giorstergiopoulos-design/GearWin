using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class PasswordManagerWindow : Window
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        private readonly ObservableCollection<PasswordRow> _all = new();
        private readonly ObservableCollection<PasswordRow> _filtered = new();

        public PasswordManagerWindow()
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            ListCredentials.ItemsSource = _filtered;
            BtnRemoveLock.Visibility = PasswordVaultLock.IsSet ? Visibility.Visible : Visibility.Collapsed;
            // 6.9.0: με ενεργό κλείδωμα ο κωδικός ζητείται ΠΡΙΝ γίνει οποιαδήποτε σάρωση των κωδικών του browser
            Loaded += (_, _) =>
            {
                if (PasswordVaultLock.IsSet && !AskUnlock()) { Close(); return; }
                _ = ScanAsync();
            };
        }

        private int _attempts;

        // true αν ο χρήστης έδωσε τον σωστό κωδικό (καθυστέρηση μετά από λάθος προσπάθειες για να μη δοκιμάζονται γρήγορα κωδικοί)
        private bool AskUnlock()
        {
            var dlg = new PasswordPromptDialog(LanguageService.T("PwdMgr_LockBtn"), LanguageService.T("PwdMgr_EnterPassword"), pw =>
            {
                if (PasswordVaultLock.Verify(pw)) return null;
                System.Threading.Thread.Sleep(Math.Min(++_attempts, 6) * 400);
                return LanguageService.T("PwdMgr_WrongPassword");
            }) { Owner = this };
            return dlg.ShowDialog() == true;
        }

        private void BtnLock_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordVaultLock.IsSet && !AskUnlock()) return;
            var dlg = new PasswordPromptDialog(LanguageService.T("PwdMgr_LockBtn"), LanguageService.T("PwdMgr_NewPassword"),
                pw => pw.Length < PasswordVaultLock.MinLength ? LanguageService.T("PwdMgr_PasswordTooShort") : null, LanguageService.T("PwdMgr_RepeatPassword")) { Owner = this };
            if (dlg.ShowDialog() != true || !PasswordVaultLock.TrySet(dlg.Value)) return;
            BtnRemoveLock.Visibility = Visibility.Visible;
            TxtStatus.Text = LanguageService.T("PwdMgr_LockSet");
            StatusService.SetIdle(LanguageService.T("PwdMgr_LockSet"));
        }

        private void BtnRemoveLock_Click(object sender, RoutedEventArgs e)
        {
            if (!PasswordVaultLock.IsSet || !AskUnlock()) return;
            PasswordVaultLock.Clear();
            BtnRemoveLock.Visibility = Visibility.Collapsed;
            TxtStatus.Text = LanguageService.T("PwdMgr_LockRemoved");
            StatusService.SetIdle(LanguageService.T("PwdMgr_LockRemoved"));
        }

        // 6.9.0: εξαγωγή όλων των κωδικών σε αρχείο .txt (απλό κείμενο, με προειδοποίηση· αν υπάρχει κλείδωμα ζητείται ξανά ο κωδικός)
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_all.Count == 0) return;
            if (ThemedMessageBox.Show(LanguageService.T("PwdMgr_ExportWarn"), LanguageService.T("PwdMgr_ExportBtn"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (PasswordVaultLock.IsSet && !AskUnlock()) return;
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Text (*.txt)|*.txt", FileName = "browser-passwords.txt" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var text = PasswordVaultLock.FormatExport(_all.Select(r => new PasswordVaultService.SavedCredential(r.Browser, r.Origin, r.Username, r.Password)), DateTime.Now);
                System.IO.File.WriteAllText(dlg.FileName, text, new System.Text.UTF8Encoding(true));
                TxtStatus.Text = string.Format(LanguageService.T("PwdMgr_ExportDone"), dlg.FileName);
                StatusService.SetIdle(LanguageService.T("Ready"));
            }
            catch (Exception ex) { ThemedMessageBox.Show(ex.Message, LanguageService.T("PwdMgr_ExportBtn"), MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private async Task ScanAsync()
        {
            TxtStatus.Text = LanguageService.T("PwdMgr_Scanning");
            StatusService.SetBusy(LanguageService.T("PwdMgr_Scanning"));

            var (credentials, browsers) = await PasswordVaultService.ScanAllAsync();

            _all.Clear();
            foreach (var c in credentials.OrderBy(c => c.Origin, StringComparer.OrdinalIgnoreCase))
                _all.Add(new PasswordRow(c.Browser, c.Origin, c.Username, c.Password));
            ApplyFilter();

            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtStatus.Text = _all.Count == 0
                ? LanguageService.T("PwdMgr_NoneFound")
                : string.Format(LanguageService.T("PwdMgr_FoundFormat"), _all.Count, string.Join(", ", browsers.Select(b => b.Split(' ')[0]).Distinct()));
        }

        private void ApplyFilter()
        {
            var query = TxtSearch.Text?.Trim() ?? "";
            _filtered.Clear();
            foreach (var row in _all)
            {
                if (query.Length == 0
                    || row.Origin.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || row.Username.Contains(query, StringComparison.OrdinalIgnoreCase))
                    _filtered.Add(row);
            }
        }

        private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyFilter();

        private void BtnToggleReveal_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button { Tag: PasswordRow row }) row.IsRevealed = !row.IsRevealed;
        }

        private void BtnCopyPassword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button { Tag: PasswordRow row }) return;
            SecureClipboardService.SetSensitiveText(row.Password);
            StatusService.SetIdle(LanguageService.T("PwdMgr_Copied"));
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class PasswordRow : INotifyPropertyChanged
    {
        public string Browser { get; }
        public string Origin { get; }
        public string Username { get; }
        public string Password { get; }

        public PasswordRow(string browser, string origin, string username, string password)
        {
            Browser = browser;
            Origin = origin;
            Username = username;
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

        public string DisplayPassword => IsRevealed ? Password : new string('•', Math.Min(Math.Max(Password.Length, 6), 14));
        public string ToggleLabel => IsRevealed ? LanguageService.T("PwdMgr_Hide") : LanguageService.T("PwdMgr_Show");

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
