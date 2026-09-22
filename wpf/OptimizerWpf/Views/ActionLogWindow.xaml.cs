using System;
using System.Windows;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class ActionLogWindow : Window
    {
        public ActionLogWindow()
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            Refresh();
            ActionLogService.Changed += Refresh;
        }

        private void Refresh() => Dispatcher.Invoke(() =>
        {
            TxtLog.Text = ActionLogService.Entries.Count == 0
                ? LanguageService.T("ActionLog_NoEntries")
                : string.Join(Environment.NewLine, ActionLogService.Entries);
            TxtLog.ScrollToEnd();
        });

        private void Window_Closed(object sender, EventArgs e) => ActionLogService.Changed -= Refresh;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
