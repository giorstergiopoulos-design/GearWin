using System.Windows;
using System.Windows.Controls;

namespace OptimizerWpf.Views
{
    public partial class AddContextMenuDialog : Window
    {
        public string MenuTextValue { get; private set; } = "";
        public string CommandValue { get; private set; } = "";
        public string ScopeValue { get; private set; } = "Directory";

        public AddContextMenuDialog() => InitializeComponent();

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtMenuText.Text) || string.IsNullOrWhiteSpace(TxtCommand.Text))
            {
                MessageBox.Show("Συμπληρώστε κείμενο μενού και εντολή.", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
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
