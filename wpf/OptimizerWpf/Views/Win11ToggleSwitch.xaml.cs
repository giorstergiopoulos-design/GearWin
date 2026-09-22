using System.Windows;
using System.Windows.Controls;

namespace OptimizerWpf.Views
{
    public partial class Win11ToggleSwitch : UserControl
    {
        // DependencyProperty (ΟΧΙ απλή CLR property) ώστε το IsChecked να μπορεί να γίνει data-bind
        // στο ItemsControl.ItemTemplate (π.χ. IsChecked="{Binding IsOn}") - χρειάζεται στο
        // ViveToolWindow ώστε ο διακόπτης να δείχνει τη ΠΡΑΓΜΑΤΙΚΗ κατάσταση μετά από Ανανέωση.
        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
            nameof(IsChecked), typeof(bool), typeof(Win11ToggleSwitch),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsCheckedChanged));

        public event RoutedEventHandler? Click;

        public Win11ToggleSwitch() => InitializeComponent();

        public bool IsChecked
        {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (Win11ToggleSwitch)d;
            var value = (bool)e.NewValue;
            if (ctrl.Toggle.IsChecked != value) ctrl.Toggle.IsChecked = value;
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            IsChecked = Toggle.IsChecked == true;
            Click?.Invoke(this, e);
        }
    }
}
