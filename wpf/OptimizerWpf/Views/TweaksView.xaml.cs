using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class TweaksView : UserControl
    {
        private readonly ObservableCollection<CustomContextMenuItem> _customMenuItems = new();

        public TweaksView()
        {
            InitializeComponent();
            ListMainTweaks.ItemsSource = TweakService.AllMainTweaks().Select(t => new TweakRowVm(t)).ToList();
            ListAiTweaks.ItemsSource = TweakService.AiCopilotTweaksSimple().Select(t => new TweakRowVm(t)).ToList();
            ListPerfTweaks.ItemsSource = TweakService.PerfTweaksSimple().Select(t => new TweakRowVm(t)).ToList();
            ListCustomMenu.ItemsSource = _customMenuItems;
            foreach (var item in CustomContextMenuService.Load()) _customMenuItems.Add(item);
        }

        private void ToggleTweak_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton { Tag: TweakRowVm row }) return;
            if (row.IsOn) row.Tweak.OnAction(); else row.Tweak.OffAction();
        }

        private void BtnDeviceEncryption_Click(object sender, RoutedEventArgs e) => TweakService.OpenDeviceEncryptionSettings();

        private void ToggleTakeOwnership_Click(object sender, RoutedEventArgs e)
        {
            if (ToggleTakeOwnership.IsChecked == true) TweakService.InstallTakeOwnership();
            else TweakService.RemoveTakeOwnership();
        }

        private void TogglePSHere_Click(object sender, RoutedEventArgs e)
        {
            if (TogglePSHere.IsChecked == true) TweakService.InstallOpenPowerShellHere();
            else TweakService.RemoveOpenPowerShellHere();
        }

        private void BtnVisualAppearance_Click(object sender, RoutedEventArgs e) => ConfirmAndApplyVisual(1, 1);
        private void BtnVisualPerformance_Click(object sender, RoutedEventArgs e) => ConfirmAndApplyVisual(2, 0);
        private void BtnVisualBalanced_Click(object sender, RoutedEventArgs e) => ConfirmAndApplyVisual(0, 1);

        private void ConfirmAndApplyVisual(int visualFx, int onOff)
        {
            if (MessageBox.Show("Θα εφαρμοστεί το προφίλ οπτικών εφέ και θα γίνει επανεκκίνηση της Εξερεύνησης (τα ανοιχτά παράθυρα θα κλείσουν). Συνέχεια;",
                    "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            TweakService.SetVisualEffectsPreset(visualFx, onOff);
        }

        private void BtnAddCustomMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddContextMenuDialog { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;
            var item = CustomContextMenuService.Add(dialog.MenuTextValue, dialog.CommandValue, dialog.ScopeValue);
            _customMenuItems.Add(item);
        }

        private void BtnRemoveCustomMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: CustomContextMenuItem item }) return;
            CustomContextMenuService.Remove(item);
            _customMenuItems.Remove(item);
        }
    }

    public class TweakRowVm : INotifyPropertyChanged
    {
        public SimpleTweak Tweak { get; }
        private bool _isOn;
        public bool IsOn { get => _isOn; set { _isOn = value; PropertyChanged?.Invoke(this, new(nameof(IsOn))); } }
        public TweakRowVm(SimpleTweak tweak) => Tweak = tweak;
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
