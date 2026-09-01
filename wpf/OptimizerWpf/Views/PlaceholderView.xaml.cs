using System.Windows.Controls;

namespace OptimizerWpf.Views
{
    public partial class PlaceholderView : UserControl
    {
        public PlaceholderView(string tabName)
        {
            InitializeComponent();
            TxtLabel.Text = $"{tabName} - δεν έχει μεταφερθεί ακόμα από το Optimizer.ps1";
        }
    }
}
