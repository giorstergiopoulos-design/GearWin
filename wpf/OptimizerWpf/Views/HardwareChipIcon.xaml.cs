using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OptimizerWpf.Views
{
    public partial class HardwareChipIcon : UserControl
    {
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(HardwareChipIcon), new PropertyMetadata(""));
        public static readonly DependencyProperty AccentProperty =
            DependencyProperty.Register(nameof(Accent), typeof(SolidColorBrush), typeof(HardwareChipIcon), new PropertyMetadata(new SolidColorBrush(Colors.DeepSkyBlue)));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public SolidColorBrush Accent
        {
            get => (SolidColorBrush)GetValue(AccentProperty);
            set => SetValue(AccentProperty, value);
        }

        public HardwareChipIcon() => InitializeComponent();
    }
}
