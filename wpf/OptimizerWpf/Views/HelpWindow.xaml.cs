using System.Windows;

namespace OptimizerWpf.Views
{
    public partial class HelpWindow : Window
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        // initialTabIndex - ρητό αίτημα χρήστη: "στην Βοήθεια/Οδηγίες να ανοίγει το παράθυρο με δύο
        // tabs όπου το δεύτερο θα είναι η άδεια χρήσης" - το κλασικό μενού's "Άδεια Χρήσης" άνοιγμα
        // περνάει 1 εδώ ώστε να πηγαίνει κατευθείαν στο 2ο tab αντί να ανοίγει πάντα στα Οδηγίες
        // (ίδιο μοτίβο με το AppearanceSettingsWindow(initialTabIndex:)).
        public HelpWindow(int initialTabIndex = 0)
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            MainTabs.SelectedIndex = initialTabIndex;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnReplayOnboarding_Click(object sender, RoutedEventArgs e) =>
            new OnboardingWindow { Owner = this }.ShowDialog();
    }
}
