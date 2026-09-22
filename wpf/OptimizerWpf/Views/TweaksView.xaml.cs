using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Microsoft.Win32;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class TweaksView : UserControl
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        private readonly ObservableCollection<CustomContextMenuItem> _customMenuItems = new();

        public TweaksView()
        {
            InitializeComponent();
            ListMainTweaks.ItemsSource = TweakService.AllMainTweaks().Select(t => new TweakRowVm(t, "Main")).ToList();
            ListAiTweaks.ItemsSource = TweakService.AiCopilotTweaksSimple().Select(t => new TweakRowVm(t, "Ai")).ToList();
            ListPerfTweaks.ItemsSource = TweakService.PerfTweaksSimple().Select(t => new TweakRowVm(t, "Perf")).ToList();
            ListLighterWindows.ItemsSource = TweakService.LighterWindowsTweaksSimple().Select(t => new TweakRowVm(t, "Lighter")).ToList();
            ListCustomMenu.ItemsSource = _customMenuItems;
            foreach (var item in CustomContextMenuService.Load()) _customMenuItems.Add(item);

            // ΔΙΟΡΘΩΣΗ (γνωστό κενό #01 του roadmap) - τα 2 hardcoded ToggleButton δεν περνούν από
            // TweakRowVm, οπότε διαβάζουν την πραγματική κατάσταση απευθείας εδώ.
            ToggleTakeOwnership.IsChecked = TweakService.IsTakeOwnershipInstalled();
            TogglePSHere.IsChecked = TweakService.IsOpenPowerShellHereInstalled();

            LoadNamedProfilesList();
        }

        // ΝΕΟ - roadmap "Αναζήτηση μέσα στη λίστα" - φιλτράρει και τις 4 λίστες tweaks ταυτόχρονα μέσω
        // CollectionView.Filter πάνω στο ήδη υπάρχον ItemsSource (χωρίς αλλαγή του underlying δεδομένου).
        private void TxtTweaksSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var term = TxtTweaksSearch.Text.Trim();
            foreach (var ic in new[] { ListMainTweaks, ListAiTweaks, ListPerfTweaks, ListLighterWindows })
            {
                var view = CollectionViewSource.GetDefaultView(ic.ItemsSource);
                if (view == null) continue;
                view.Filter = string.IsNullOrEmpty(term) ? null : o =>
                    o is TweakRowVm row &&
                    (row.Tweak.Label.Contains(term, System.StringComparison.CurrentCultureIgnoreCase) ||
                     row.Tweak.Description.Contains(term, System.StringComparison.CurrentCultureIgnoreCase));
            }
        }

        private void ToggleTweak_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton { Tag: TweakRowVm row }) return;
            if (row.IsOn) row.Tweak.OnAction(); else row.Tweak.OffAction();
        }

        // ΝΕΟ - roadmap "Καρφιτσωμένες συντομεύσεις" - καρφίτσωμα/ξεκαρφίτσωμα ενός tweak για γρήγορη
        // πρόσβαση από την Αρχική (βλ. HomeView.xaml.cs's LoadPinnedTweaks).
        private void BtnPinTweak_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: TweakRowVm row }) return;
            row.IsPinned = !row.IsPinned;
            var pins = AppSettingsService.Current.PinnedTweakKeys;
            if (row.IsPinned) { if (!pins.Contains(row.PinKey)) pins.Add(row.PinKey); }
            else pins.Remove(row.PinKey);
            AppSettingsService.Save();
        }

        private void BtnDeviceEncryption_Click(object sender, RoutedEventArgs e) => TweakService.OpenDeviceEncryptionSettings();

        // ΝΕΟ - roadmap "διαχείριση κλειδιών ανάκτησης BitLocker" - καθαρά ενημερωτικό (βλ.
        // BitLockerKeyService) - καμία αλλαγή στην ίδια την κρυπτογράφηση εδώ.
        private async void BtnBitLockerKeys_Click(object sender, RoutedEventArgs e)
        {
            BtnBitLockerKeys.IsEnabled = false;
            TxtBitLockerStatus.Visibility = Visibility.Visible;
            TxtBitLockerStatus.Text = LanguageService.T("BitLocker_Scanning");
            StatusService.SetBusy(LanguageService.T("BitLocker_Scanning"));
            try
            {
                var drives = await BitLockerKeyService.GetDrivesAsync();
                ListBitLockerDrives.ItemsSource = drives;
                TxtBitLockerStatus.Text = drives.Count == 0
                    ? LanguageService.T("BitLocker_NoneFound")
                    : "";
                TxtBitLockerStatus.Visibility = drives.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            finally
            {
                StatusService.SetIdle(LanguageService.T("Ready"));
                BtnBitLockerKeys.IsEnabled = true;
            }
        }

        private void BtnOpenIndexing_Click(object sender, RoutedEventArgs e) => TweakService.OpenIndexingOptions();

        private void BtnLowResourceProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ThemedMessageBox.Show(LanguageService.T("Tweaks_LowResourceConfirm"),
                    LanguageService.T("Tweaks_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            TweakService.ApplyLowResourceProfile();
            ListLighterWindows.ItemsSource = TweakService.LighterWindowsTweaksSimple().Select(t => new TweakRowVm(t, "Lighter")).ToList();
        }

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
            if (ThemedMessageBox.Show(LanguageService.T("Tweaks_VisualEffectsConfirm"),
                    LanguageService.T("Tweaks_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
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

        // ΝΕΟ - roadmap "Προφίλ tweaks" - αποθήκευση/φόρτωση ενός συνόλου επιλογών ως αρχείο, χρήσιμο
        // για επανάληψη σε νέο υπολογιστή. Κλειδί ανά tweak είναι το ΙΔΙΟ PinKey που χρησιμοποιεί το
        // καρφίτσωμα (βλ. TweakRowVm) - ίδιος γνωστός περιορισμός: εξαρτάται από το τρέχον μεταφρασμένο
        // Label, ένα προφίλ εξαγόμενο σε μία γλώσσα δεν θα ταιριάξει tweaks αν εισαχθεί υπό άλλη γλώσσα.
        private IEnumerable<TweakRowVm> AllTweakRows() =>
            ((IEnumerable<TweakRowVm>)ListMainTweaks.ItemsSource)
            .Concat((IEnumerable<TweakRowVm>)ListAiTweaks.ItemsSource)
            .Concat((IEnumerable<TweakRowVm>)ListPerfTweaks.ItemsSource)
            .Concat((IEnumerable<TweakRowVm>)ListLighterWindows.ItemsSource);

        // ΝΕΟ - roadmap "Εξαγωγή/εισαγωγή ΠΛΗΡΟΥΣ προφίλ ρυθμίσεων": καλύπτει ΚΑΙ θέμα, γλώσσα,
        // πλευρικό μενού, και καρφιτσωμένες συντομεύσεις - εύκολη μεταφορά ΟΛΟΚΛΗΡΗΣ της εμπειρίας
        // χρήστη σε νέο PC, όχι μόνο τα tweaks. ΠΑΝΤΑ γράφει το ΝΕΟ format (με "Tweaks" ως
        // ξεχωριστή ιδιότητα)· η εισαγωγή αναγνωρίζει ΚΑΙ το ΠΑΛΙΟ επίπεδο format (απλό
        // Dictionary&lt;string,bool&gt;, όπως έγραφαν παλιότερες εκδόσεις) ελέγχοντας αν υπάρχει η
        // ιδιότητα "Tweaks" στη ρίζα του JSON - καμία απώλεια συμβατότητας με ήδη εξαγμένα αρχεία.
        // Το FullProfileData μετακινήθηκε στο Services/NamedProfileService.cs ώστε να το
        // ξαναχρησιμοποιεί ΚΑΙ το νέο, ονομασμένο-προφίλ μονοπάτι παρακάτω.
        private FullProfileData BuildProfile()
        {
            var settings = AppSettingsService.Current;
            return new FullProfileData
            {
                Tweaks = AllTweakRows().ToDictionary(r => r.PinKey, r => r.IsOn),
                ThemeName = settings.ThemeName,
                IsDarkMode = settings.IsDarkMode,
                Language = LanguageService.Current,
                SidebarEnabled = settings.SidebarEnabled,
                SidebarPosition = settings.SidebarPosition,
                MenuMode = settings.MenuMode,
                PinnedTweakKeys = settings.PinnedTweakKeys,
            };
        }

        // Επιστρέφει πόσα tweaks πραγματικά άλλαξαν κατάσταση - καλείται ΚΑΙ από το αρχείο-βασισμένο
        // import ΚΑΙ από τη φόρτωση ονομασμένου προφίλ παρακάτω.
        private int ApplyProfile(FullProfileData full)
        {
            var applied = 0;
            foreach (var row in AllTweakRows())
            {
                if (!full.Tweaks.TryGetValue(row.PinKey, out var desired) || row.IsOn == desired) continue;
                try { if (desired) row.Tweak.OnAction(); else row.Tweak.OffAction(); applied++; }
                catch { /* ένα μεμονωμένο tweak μπορεί να αποτύχει (π.χ. δεν υποστηρίζεται σε αυτό το build) - τα υπόλοιπα συνεχίζουν */ }
            }

            ListMainTweaks.ItemsSource = TweakService.AllMainTweaks().Select(t => new TweakRowVm(t, "Main")).ToList();
            ListAiTweaks.ItemsSource = TweakService.AiCopilotTweaksSimple().Select(t => new TweakRowVm(t, "Ai")).ToList();
            ListPerfTweaks.ItemsSource = TweakService.PerfTweaksSimple().Select(t => new TweakRowVm(t, "Perf")).ToList();
            ListLighterWindows.ItemsSource = TweakService.LighterWindowsTweaksSimple().Select(t => new TweakRowVm(t, "Lighter")).ToList();

            var settings = AppSettingsService.Current;
            if (full.ThemeName != null)
            {
                // ThemeCatalog.All δεν περιλαμβάνει πια τα skins (Microsoft PC Manager/Windows Classic) -
                // AllIncludingSkins εδώ ώστε ένα προφίλ αποθηκευμένο με ενεργό skin να αποκαθίσταται σωστά.
                var pair = ThemeCatalog.AllIncludingSkins.FirstOrDefault(p => p.DisplayName == full.ThemeName) ?? ThemeCatalog.Windows11Fluent;
                ThemeManager.SelectTheme(pair);
            }
            if (full.IsDarkMode.HasValue && full.IsDarkMode.Value != ThemeManager.IsDarkMode) ThemeManager.ToggleLightDark();
            if (full.Language != null) LanguageService.SetLanguage(full.Language);
            if (full.SidebarEnabled.HasValue) settings.SidebarEnabled = full.SidebarEnabled.Value;
            if (full.SidebarPosition != null) settings.SidebarPosition = full.SidebarPosition;
            if (full.MenuMode != null) settings.MenuMode = full.MenuMode;
            if (full.PinnedTweakKeys != null) settings.PinnedTweakKeys = full.PinnedTweakKeys;
            AppSettingsService.Save();

            return applied;
        }

        private void BtnExportTweakProfile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "App Profile (*.json)|*.json", FileName = $"AppProfile_{DateTime.Now:yyyy-MM-dd}.json" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(BuildProfile(), new JsonSerializerOptions { WriteIndented = true }));
                TxtTweakProfileStatus.Text = LanguageService.T("Tweaks_ProfileExportDone");
            }
            catch { TxtTweakProfileStatus.Text = LanguageService.T("Tweaks_ProfileExportFailed"); }
        }

        private void BtnImportTweakProfile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "App Profile (*.json)|*.json" };
            if (dlg.ShowDialog() != true) return;
            if (ThemedMessageBox.Show(LanguageService.T("Tweaks_ProfileImportConfirm"), LanguageService.T("Tweaks_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            FullProfileData? full;
            try
            {
                var json = File.ReadAllText(dlg.FileName);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("Tweaks", out _))
                {
                    full = JsonSerializer.Deserialize<FullProfileData>(json);
                }
                else
                {
                    var tweaks = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                    full = tweaks == null ? null : new FullProfileData { Tweaks = tweaks };
                }
            }
            catch { ThemedMessageBox.Show(LanguageService.T("Tweaks_ProfileImportFailed"), LanguageService.T("Adv_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (full == null) return;

            var applied = ApplyProfile(full);
            TxtTweakProfileStatus.Text = string.Format(LanguageService.T("Tweaks_ProfileImportDone"), applied);
        }

        // ΝΕΟ - roadmap "Ονομασμένα προφίλ πολλαπλών χρηστών/μηχανημάτων" - βλ. NamedProfileService.
        // Γρηγορότερος δρόμος από το αρχείο-βασισμένο export/import παραπάνω: αποθηκεύει/φορτώνει
        // από ΕΝΑ σταθερό φάκελο με απλή λίστα ονομάτων, χωρίς SaveFileDialog/OpenFileDialog κάθε
        // φορά. Ίδιο FullProfileData schema, πλήρως συμβατό και με τα δύο μονοπάτια.
        private void LoadNamedProfilesList()
        {
            var current = ListNamedProfiles.SelectedItem as string;
            ListNamedProfiles.ItemsSource = NamedProfileService.ListProfiles();
            if (current != null) ListNamedProfiles.SelectedItem = current;
        }

        private void BtnSaveNamedProfile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new TextInputDialog(LanguageService.T("Tweaks_NamedProfileSaveTitle"), LanguageService.T("Tweaks_NamedProfileSavePrompt")) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true) return;

            if (NamedProfileService.Save(dlg.Value, BuildProfile()))
            {
                LoadNamedProfilesList();
                ListNamedProfiles.SelectedItem = dlg.Value;
                TxtTweakProfileStatus.Text = string.Format(LanguageService.T("Tweaks_NamedProfileSaved"), dlg.Value);
            }
            else
            {
                TxtTweakProfileStatus.Text = LanguageService.T("Tweaks_ProfileExportFailed");
            }
        }

        private void BtnLoadNamedProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ListNamedProfiles.SelectedItem is not string name) return;
            if (ThemedMessageBox.Show(LanguageService.T("Tweaks_ProfileImportConfirm"), LanguageService.T("Tweaks_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            var full = NamedProfileService.Load(name);
            if (full == null) { TxtTweakProfileStatus.Text = LanguageService.T("Tweaks_ProfileImportFailed"); return; }

            var applied = ApplyProfile(full);
            TxtTweakProfileStatus.Text = string.Format(LanguageService.T("Tweaks_ProfileImportDone"), applied);
        }

        private void BtnDeleteNamedProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ListNamedProfiles.SelectedItem is not string name) return;
            if (ThemedMessageBox.Show(string.Format(LanguageService.T("Tweaks_NamedProfileDeleteConfirm"), name), LanguageService.T("Tweaks_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            NamedProfileService.Delete(name);
            LoadNamedProfilesList();
        }
    }

    public class TweakRowVm : INotifyPropertyChanged
    {
        public SimpleTweak Tweak { get; }
        public string PinKey { get; }
        private bool _isOn;
        public bool IsOn { get => _isOn; set { _isOn = value; PropertyChanged?.Invoke(this, new(nameof(IsOn))); } }

        // ΝΕΟ - roadmap "Καρφιτσωμένες συντομεύσεις" - βλ. AppSettingsService.PinnedTweakKeys.
        private bool _isPinned;
        public bool IsPinned
        {
            get => _isPinned;
            set { _isPinned = value; PropertyChanged?.Invoke(this, new(nameof(IsPinned))); PropertyChanged?.Invoke(this, new(nameof(PinGlyph))); }
        }
        public string PinGlyph => IsPinned ? "★" : "☆";

        // ΔΙΟΡΘΩΣΗ (γνωστό κενό #01 του roadmap: "οι διακόπτες δεν διαβάζουν την πραγματική τρέχουσα
        // τιμή - ξεκινούν πάντα ανενεργοί") - το DetectState (αν υπάρχει για αυτό το tweak) διαβάζει
        // την πραγματική κατάσταση του μητρώου/powercfg στην κατασκευή της γραμμής, αντί ο διακόπτης
        // να ξεκινά πάντα false. Σφάλμα κατά την ανίχνευση (π.χ. μη αναμενόμενη τιμή μητρώου) υποβαθμίζει
        // σε false αντί να ρίξει την κάρτα - καλύτερα ένας λάθος-ανενεργός διακόπτης παρά κρασάρισμα.
        public TweakRowVm(SimpleTweak tweak, string listKey)
        {
            Tweak = tweak;
            PinKey = $"{listKey}:{tweak.Label}";
            try { _isOn = tweak.DetectState?.Invoke() ?? false; } catch { _isOn = false; }
            _isPinned = AppSettingsService.Current.PinnedTweakKeys.Contains(PinKey);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
