using System.Windows;
using System.Windows.Controls;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class AddContextMenuDialog : Window
    {
        public string MenuTextValue { get; private set; } = "";
        public string CommandValue { get; private set; } = "";
        public string ScopeValue { get; private set; } = "Directory";

        public AddContextMenuDialog()
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtMenuText.Text) || string.IsNullOrWhiteSpace(TxtCommand.Text))
            {
                ThemedMessageBox.Show(LanguageService.T("Ctx_FillFieldsMsg"), LanguageService.T("Adv_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            MenuTextValue = TxtMenuText.Text.Trim();
            CommandValue = TxtCommand.Text.Trim();
            ScopeValue = (CmbScope.SelectedItem as ComboBoxItem)?.Tag as string ?? "Directory";
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
