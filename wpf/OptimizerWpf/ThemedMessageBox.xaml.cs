using System.Linq;
using System.Windows;

namespace OptimizerWpf
{
    public partial class ThemedMessageBox : Window
    {
        private MessageBoxResult _result = MessageBoxResult.None;

        private ThemedMessageBox()
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
        }

        public static MessageBoxResult Show(string messageBoxText) =>
            Show(messageBoxText, string.Empty, MessageBoxButton.OK, MessageBoxImage.None);

        public static MessageBoxResult Show(string messageBoxText, string caption) =>
            Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None);

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button) =>
            Show(messageBoxText, caption, button, MessageBoxImage.None);

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            var box = new ThemedMessageBox { Title = caption };
            box.TxtMessage.Text = messageBoxText;
            box.TxtIcon.Text = icon switch
            {
                MessageBoxImage.Error => "⛔",
                MessageBoxImage.Warning => "⚠",
                MessageBoxImage.Question => "❓",
                MessageBoxImage.Information => "ℹ",
                _ => string.Empty
            };
            box.TxtIcon.Visibility = icon == MessageBoxImage.None ? Visibility.Collapsed : Visibility.Visible;

            if (button == MessageBoxButton.YesNo)
            {
                box.BtnYes.Content = Services.LanguageService.T("Common_Yes");
                box.BtnNo.Content = Services.LanguageService.T("Common_No");
                box.BtnYes.Visibility = Visibility.Visible;
                box.BtnNo.Visibility = Visibility.Visible;
                box._result = MessageBoxResult.No;
            }
            else
            {
                box.BtnOk.Content = Services.LanguageService.T("Common_Ok");
                box.BtnOk.Visibility = Visibility.Visible;
                box._result = MessageBoxResult.OK;
            }

            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                        ?? Application.Current?.MainWindow;
            if (owner != null && owner != box && owner.IsLoaded)
                box.Owner = owner;

            box.ShowDialog();
            return box._result;
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.Yes;
            Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.No;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.OK;
            Close();
        }
    }
}
