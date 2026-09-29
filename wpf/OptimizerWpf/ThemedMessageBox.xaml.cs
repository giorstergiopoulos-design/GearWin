using System.Linq;
using System.Windows;
using System.Windows.Media;

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
            // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη - ROADMAP.md REQ-570-08: "το τετράγωνο εικονίδιο του pop-up
            // παραθύρου για την επιτυχή εγκατάσταση δεν φαίνεται σωστά σε dark mode") - το TxtIcon δεν
            // είχε ΚΑΘΟΛΟΥ ρητό Foreground (κληρονομούσε το προεπιλεγμένο, σκούρο χρώμα ενός απλού
            // TextBlock) - το γλυφ "ℹ" (Information) συγκεκριμένα αποδίδεται σε πολλές γραμματοσειρές
            // ως ένα κοντό "i" μέσα σε τετράγωνο περίγραμμα, ΟΧΙ κύκλο - σκούρο-πάνω-σε-σκούρο σε dark
            // mode, σχεδόν αόρατο/κακόσχηματο. Ρητό, θεματισμένο χρώμα ανά τύπο εικονιδίου (ίδιο πνεύμα
            // με τα ήδη υπάρχοντα χρωματιστά status badges αλλού στην εφαρμογή) - ορατό σε ΚΑΘΕ θέμα.
            box.TxtIcon.Foreground = icon switch
            {
                MessageBoxImage.Error => new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)),
                MessageBoxImage.Warning => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
                MessageBoxImage.Question => (Brush)box.FindResource("AccentBrush"),
                MessageBoxImage.Information => (Brush)box.FindResource("AccentBrush"),
                _ => (Brush)box.FindResource("TextBrush"),
            };

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
