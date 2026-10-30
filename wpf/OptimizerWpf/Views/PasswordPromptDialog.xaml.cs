using System;
using System.Windows;
using System.Windows.Input;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class PasswordPromptDialog : Window
    {
        private readonly Func<string, string?> _validate;
        private readonly bool _confirm;

        public string Value { get; private set; } = "";

        // validate: επιστρέφει null αν ο κωδικός γίνεται δεκτός, αλλιώς το μήνυμα σφάλματος που θα εμφανιστεί (ο διάλογος μένει ανοιχτός).
        // confirmPrompt != null: δεύτερο πεδίο "επανάληψη κωδικού" (ορισμός νέου κωδικού).
        public PasswordPromptDialog(string title, string prompt, Func<string, string?> validate, string? confirmPrompt = null)
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            Title = title;
            TxtPrompt.Text = prompt;
            _validate = validate;
            _confirm = confirmPrompt != null;
            if (_confirm)
            {
                TxtPrompt2.Text = confirmPrompt;
                TxtPrompt2.Visibility = Visibility.Visible;
                Pwd2.Visibility = Visibility.Visible;
            }
            Loaded += (_, _) => Pwd1.Focus();
        }

        private void Pwd_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) BtnOk_Click(sender, e); }
        private void BtnCancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (_confirm && Pwd1.Password != Pwd2.Password) { Show(LanguageService.T("PwdMgr_PasswordMismatch")); return; }
            var error = _validate(Pwd1.Password);
            if (error != null) { Show(error); Pwd1.Clear(); Pwd2.Clear(); Pwd1.Focus(); return; }
            Value = Pwd1.Password;
            DialogResult = true;
        }

        private void Show(string message) { TxtError.Text = message; TxtError.Visibility = Visibility.Visible; }
    }
}
