using System.Windows;
using System.Windows.Input;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class TextInputDialog : Window
    {
        public string Value { get; private set; } = "";

        public TextInputDialog(string title, string prompt, string initialValue = "")
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            Title = title;
            TxtPrompt.Text = prompt;
            TxtValue.Text = initialValue;
            Loaded += (_, _) => { TxtValue.Focus(); TxtValue.SelectAll(); };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Value = TxtValue.Text.Trim();
            DialogResult = !string.IsNullOrEmpty(Value);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void TxtValue_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BtnOk_Click(sender, e);
        }
    }
}
