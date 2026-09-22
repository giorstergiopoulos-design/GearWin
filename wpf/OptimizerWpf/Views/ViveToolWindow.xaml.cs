using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class ViveToolWindow : Window
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        private readonly ObservableCollection<ViveFeatureRow> _rows = new();

        public ViveToolWindow()
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            ListFeatures.ItemsSource = _rows;
            foreach (var f in ViveToolService.CuratedFeatures)
                _rows.Add(new ViveFeatureRow { Feature = f, StateText = LanguageService.T("Vive_StateUnknownPress") });

            // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: δεν υπάρχει "επιβεβαιωμένο σε build X" ισχυρισμός εδώ (σε αντίθεση
            // με τη screenshot αναφορά της 2.8.2) - η λίστα IDs βασίζεται σε δημόσια τεκμηρίωση, όχι σε
            // επαλήθευση πάνω σε συγκεκριμένο build από τον ίδιο τον προγραμματιστή αυτού του port.
            var build = System.Environment.OSVersion.Version.Build;
            TxtBuildInfo.Text = $"Windows Build: {build}{LanguageService.T("Vive_BuildInfoSuffix")}";
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            TxtStatus.Text = LanguageService.T("Vive_DownloadCheckInProgress");
            StatusService.SetBusy(LanguageService.T("Vive_CheckingFeatureState"));
            foreach (var row in _rows)
            {
                row.StateText = LanguageService.T("Vive_Checking");
                var state = await ViveToolService.QueryStateAsync(row.Feature.Ids[0]);
                row.State = state;
                row.IsOn = state == ViveFeatureState.Enabled;
                row.StateText = state switch
                {
                    ViveFeatureState.Enabled => LanguageService.T("Health_WinREStatusEnabled"),
                    ViveFeatureState.Disabled => LanguageService.T("Health_WinREStatusDisabled"),
                    ViveFeatureState.NotFound => LanguageService.T("Vive_StateNotFoundInBuild"),
                    _ => LanguageService.T("Health_WinREStatusUnknown"),
                };
            }
            StatusService.SetIdle(LanguageService.T("Ready"));
            TxtStatus.Text = LanguageService.T("Vive_StatusUpdated");
        }

        // Port του Win11-style toggle (ρητό αίτημα χρήστη: "δεν έχουν το κουμπί των win11") - ένας
        // διακόπτης ανά χαρακτηριστικό αντί για ζεύγος κουμπιών Ενεργοποίηση/Απενεργοποίηση.
        private async void FeatureToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Win11ToggleSwitch toggle || toggle.Tag is not ViveFeatureRow row) return;
            var enable = toggle.IsChecked;

            toggle.IsEnabled = false;
            StatusService.SetBusy($"{(enable ? LanguageService.T("Adv_FeatureEnable") : LanguageService.T("Adv_FeatureDisable"))}: {row.Feature.Title}...");
            var output = enable ? await ViveToolService.EnableAsync(row.Feature.Ids) : await ViveToolService.DisableAsync(row.Feature.Ids);
            StatusService.SetIdle(LanguageService.T("Ready"));
            toggle.IsEnabled = true;

            if (output == null)
            {
                row.IsOn = !enable;
                ThemedMessageBox.Show(LanguageService.T("Vive_ExecFailedMsg"),
                    LanguageService.T("Adv_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            row.StateText = $"{LanguageService.T("Vive_StatusPrefix")}{(enable ? LanguageService.T("Vive_EnabledWord") : LanguageService.T("Vive_DisabledWord"))}{LanguageService.T("Vive_RestartNeededSuffix")}";
            OfferRestart(row.Feature.Title);
        }

        private async void BtnCustomEnable_Click(object sender, RoutedEventArgs e) => await CustomToggleAsync(enable: true);
        private async void BtnCustomDisable_Click(object sender, RoutedEventArgs e) => await CustomToggleAsync(enable: false);

        private async System.Threading.Tasks.Task CustomToggleAsync(bool enable)
        {
            // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): δεχόταν οποιοδήποτε μη-κενό κείμενο - port της
            // επικύρωσης του ps1 original (~17609/17618, regex ^\d{4,10}$) πριν σταλεί στο ViVeTool.
            var id = TxtCustomId.Text.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, @"^\d{4,10}$"))
            {
                ThemedMessageBox.Show(LanguageService.T("Vive_InvalidIdMsg"), LanguageService.T("Vive_InvalidIdTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusService.SetBusy($"{(enable ? LanguageService.T("Adv_FeatureEnable") : LanguageService.T("Adv_FeatureDisable"))}{LanguageService.T("Vive_CustomIdSuffix")}{id}...");
            var output = enable ? await ViveToolService.EnableAsync(new[] { id }) : await ViveToolService.DisableAsync(new[] { id });
            StatusService.SetIdle(LanguageService.T("Ready"));

            if (output == null)
            {
                ThemedMessageBox.Show(LanguageService.T("Vive_ExecFailedShortMsg"), LanguageService.T("Adv_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            OfferRestart($"Feature ID {id}");
        }

        private void OfferRestart(string label)
        {
            var restart = ThemedMessageBox.Show(
                $"{LanguageService.T("Vive_RestartOfferPrefix")}{label}{LanguageService.T("Vive_RestartOfferSuffix")}",
                LanguageService.T("Vive_RestartTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (restart == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("shutdown.exe", "/r /t 5") { UseShellExecute = false, CreateNoWindow = true });
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class ViveFeatureRow : INotifyPropertyChanged
    {
        public ViveFeature Feature { get; init; } = null!;
        public ViveFeatureState State { get; set; } = ViveFeatureState.Unknown;

        private string _stateText = "";
        public string StateText
        {
            get => _stateText;
            set { _stateText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StateText))); }
        }

        private bool _isOn;
        public bool IsOn
        {
            get => _isOn;
            set { _isOn = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOn))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
