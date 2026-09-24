using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class AppearanceSettingsWindow : Window
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        public AppearanceSettingsWindow(int initialTabIndex = 0)
        {
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            ThemeManager.AttachBackground(Preview);
            ComboTheme.ItemsSource = ThemeCatalog.All;
            ComboTheme.SelectedItem = ThemeManager.CurrentPair;
            ChkAnimatedBg.IsChecked = ThemeManager.AnimatedBackgrounds;
            ChkDesktopWidget.IsChecked = AppSettingsService.Current.DesktopWidgetEnabled;

            ChkUpdateNotifications.IsChecked = AppSettingsService.Current.UpdateNotificationsEnabled;
            foreach (var hours in new[] { 1, 2, 4, 6, 12, 24 })
            {
                var item = new ComboBoxItem { Content = string.Format(LanguageService.T("Appr_UpdateIntervalHoursFormat"), hours), Tag = hours };
                ComboUpdateInterval.Items.Add(item);
                if (hours == AppSettingsService.Current.UpdateCheckIntervalHours) ComboUpdateInterval.SelectedItem = item;
            }
            if (ComboUpdateInterval.SelectedItem == null && ComboUpdateInterval.Items.Count > 3) ComboUpdateInterval.SelectedIndex = 3;
            RefreshUpdateCheckStatus();

            ChkLowDiskNotifications.IsChecked = AppSettingsService.Current.LowDiskNotificationsEnabled;
            foreach (var pct in new[] { 5, 10, 15, 20 })
            {
                var item = new ComboBoxItem { Content = string.Format(LanguageService.T("Appr_LowDiskThresholdFormat"), pct), Tag = pct };
                ComboLowDiskThreshold.Items.Add(item);
                if (pct == AppSettingsService.Current.LowDiskThresholdPercent) ComboLowDiskThreshold.SelectedItem = item;
            }
            if (ComboLowDiskThreshold.SelectedItem == null && ComboLowDiskThreshold.Items.Count > 1) ComboLowDiskThreshold.SelectedIndex = 1;

            var settings = AppSettingsService.Current;
            ChkSidebarEnabled.IsChecked = settings.SidebarEnabled;
            if (settings.SidebarPosition == "Left") RadioLeft.IsChecked = true;
            else RadioRight.IsChecked = true;
            if (settings.MenuMode == "Sidebar") RadioMenuSidebar.IsChecked = true;
            else RadioMenuHoriz.IsChecked = true;

            var currentThemeName = ThemeManager.CurrentPair.DisplayName;
            RadioSkinPcManager.IsChecked = currentThemeName == "Microsoft PC Manager";
            RadioSkinWindowsClassic.IsChecked = currentThemeName == "Windows Classic";
            RadioSkinClassic.IsChecked = currentThemeName != "Microsoft PC Manager" && currentThemeName != "Windows Classic";

            // ΔΙΟΡΘΩΣΗ (εξονυχιστικός έλεγχος εντόπισε): ήταν IsChecked="True" hardcoded στο XAML,
            // ΠΟΤΕ δεν αντανακλούσε το πραγματικό αποθηκευμένο DefaultTheme - τώρα αντικατοπτρίζει αν
            // το τρέχον θέμα ΕΙΝΑΙ πράγματι το persisted προεπιλεγμένο (ίδια λογική με το ps1 original,
            // $chkDefaultTheme.Checked = ($global:appSettings.DefaultTheme -eq $global:currentThemeName)).
            ChkSetDefaultTheme.IsChecked = settings.ThemeName == currentThemeName;

            (settings.AnimationStyleOverride switch
            {
                "Gears" => RadioAnimGears,
                "Orbit" => RadioAnimOrbit,
                "Particles" => RadioAnimParticles,
                "Waves" => RadioAnimWaves,
                "GridPulse" => RadioAnimGridPulse,
                _ => RadioAnimTheme,
            }).IsChecked = true;

            (settings.WindowOpacityMode switch
            {
                "Light" => RadioOpacityLight,
                "Medium" => RadioOpacityMedium,
                _ => RadioOpacityNone,
            }).IsChecked = true;

            MainTabs.SelectedIndex = initialTabIndex;

            ComboLanguage.SelectedIndex = LanguageService.Current switch { "en" => 1, "de" => 2, "fr" => 3, _ => 0 };
            ApplyTranslations();
            LanguageService.Changed += ApplyTranslations;
            Closed += (_, _) => LanguageService.Changed -= ApplyTranslations;
        }

        // Ρητό αίτημα χρήστη: "εισάγαγε τις μεταφράσεις" - μηχανισμός i18n (βλ. LanguageService),
        // εμβέλεια συμφωνημένη με τον χρήστη: προς το παρόν μόνο βασικά στοιχεία ΑΥΤΟΥ του παραθύρου,
        // ζωντανή απόδειξη ότι ο μηχανισμός δουλεύει - όχι πλήρης μετάφραση όλης της εφαρμογής ακόμα.
        private void ApplyTranslations()
        {
            TxtLanguageNote.Text = LanguageService.T("LanguageNote");
        }

        private void ComboLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboLanguage.SelectedItem is ComboBoxItem { Tag: string code }) LanguageService.SetLanguage(code);
        }

        // Ρητό αίτημα χρήστη (screenshot): "Ορισμός ως προεπιλεγμένο θέμα εκκίνησης" - χωρίς το
        // checkbox, η επιλογή εφαρμόζεται ζωντανά για την τρέχουσα συνεδρία ΜΟΝΟ (ΔΕΝ γράφεται στις
        // persisted ρυθμίσεις - στην επόμενη εκκίνηση επανέρχεται το πραγματικά αποθηκευμένο θέμα).
        private void ComboTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboTheme.SelectedItem is not ThemePair pair) return;
            if (ChkSetDefaultTheme.IsChecked == true) ThemeManager.SelectTheme(pair);
            else ThemeManager.PreviewTheme(pair);
            // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "τα skins PC Manager & Windows Classic... να απαλειφθούν
            // από τα θέματα") - το ComboTheme.ItemsSource (ThemeCatalog.All) πλέον ΔΕΝ περιέχει καθόλου
            // αυτά τα δύο skins, άρα κάθε επιλογή εδώ είναι εξ ορισμού ένα κανονικό, μη-skin θέμα.
            RadioSkinClassic.IsChecked = true;
            (Owner as MainWindow)?.ApplyMenuModeVisibility();
        }

        private void ChkAnimatedBg_Changed(object sender, RoutedEventArgs e) => ThemeManager.SetAnimatedBackgrounds(ChkAnimatedBg.IsChecked == true);

        // ΝΕΟ - βλ. σχόλιο στο XAML/UpdateNotificationService.cs.
        private void ChkUpdateNotifications_Changed(object sender, RoutedEventArgs e)
        {
            AppSettingsService.Current.UpdateNotificationsEnabled = ChkUpdateNotifications.IsChecked == true;
            AppSettingsService.Save();
        }

        private void ComboUpdateInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboUpdateInterval.SelectedItem is not ComboBoxItem { Tag: int hours }) return;
            AppSettingsService.Current.UpdateCheckIntervalHours = hours;
            AppSettingsService.Save();
        }

        private async void BtnUpdateCheckNow_Click(object sender, RoutedEventArgs e)
        {
            TxtUpdateCheckStatus.Text = LanguageService.T("Appr_UpdateChecking");
            StatusService.SetBusy(LanguageService.T("Appr_UpdateChecking"));
            await UpdateNotificationService.RunCheckAsync();
            StatusService.SetIdle(LanguageService.T("Ready"));
            RefreshUpdateCheckStatus();
        }

        private void RefreshUpdateCheckStatus()
        {
            var apps = UpdatesHubService.AppUpdatesAvailable;
            var drivers = UpdatesHubService.DriverUpdatesAvailable;
            TxtUpdateCheckStatus.Text = $"{LanguageService.T("Appr_UpdateStatusApps")}{(apps?.ToString() ?? "—")}{LanguageService.T("Appr_UpdateStatusDrivers")}{(drivers?.ToString() ?? "—")}";
        }

        // ΝΕΟ - roadmap ιδέα #4 - βλ. σχόλιο στο XAML.
        private void ChkLowDiskNotifications_Changed(object sender, RoutedEventArgs e)
        {
            AppSettingsService.Current.LowDiskNotificationsEnabled = ChkLowDiskNotifications.IsChecked == true;
            AppSettingsService.Save();
        }

        private void ComboLowDiskThreshold_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboLowDiskThreshold.SelectedItem is not ComboBoxItem { Tag: int pct }) return;
            AppSettingsService.Current.LowDiskThresholdPercent = pct;
            AppSettingsService.Save();
        }

        // ΝΕΟ - roadmap "Widget επιφάνειας εργασίας" - ζωντανή ενεργοποίηση/απενεργοποίηση, ίδιο μοτίβο
        // με το ChkAnimatedBg_Changed παραπάνω.
        private void ChkDesktopWidget_Changed(object sender, RoutedEventArgs e)
        {
            var enabled = ChkDesktopWidget.IsChecked == true;
            AppSettingsService.Current.DesktopWidgetEnabled = enabled;
            AppSettingsService.Save();
            if (enabled) DesktopWidgetService.Start();
            else DesktopWidgetService.Stop();
        }

        private void AnimationStyle_Changed(object sender, RoutedEventArgs e)
        {
            AppSettingsService.Current.AnimationStyleOverride = sender switch
            {
                var s when s == RadioAnimGears => "Gears",
                var s when s == RadioAnimOrbit => "Orbit",
                var s when s == RadioAnimParticles => "Particles",
                var s when s == RadioAnimWaves => "Waves",
                var s when s == RadioAnimGridPulse => "GridPulse",
                _ => "Theme",
            };
            AppSettingsService.Save();
            ThemeManager.RefreshBackgrounds();
        }

        private void Opacity_Changed(object sender, RoutedEventArgs e)
        {
            var mode = sender switch
            {
                var s when s == RadioOpacityLight => "Light",
                var s when s == RadioOpacityMedium => "Medium",
                _ => "None",
            };
            AppSettingsService.Current.WindowOpacityMode = mode;
            AppSettingsService.Save();
            if (Owner is MainWindow main) main.Opacity = mode switch { "Light" => 0.94, "Medium" => 0.85, _ => 1.0 };
        }

        private void ChkSidebarEnabled_Changed(object sender, RoutedEventArgs e)
        {
            AppSettingsService.Current.SidebarEnabled = ChkSidebarEnabled.IsChecked == true;
            AppSettingsService.Save();
            (Owner as MainWindow)?.ApplyMenuModeVisibility();
        }

        private void SidebarPosition_Changed(object sender, RoutedEventArgs e)
        {
            AppSettingsService.Current.SidebarPosition = RadioLeft.IsChecked == true ? "Left" : "Right";
            AppSettingsService.Save();
            (Owner as MainWindow)?.ApplyMenuModeVisibility();
        }

        private void MenuMode_Changed(object sender, RoutedEventArgs e)
        {
            AppSettingsService.Current.MenuMode = RadioMenuSidebar.IsChecked == true ? "Sidebar" : "HorizontalModern";
            AppSettingsService.Save();
            (Owner as MainWindow)?.ApplyMenuModeVisibility();
        }

        // Port του radioSkinClassic/radioSkinPcManager (Optimizer.ps1 ~8780-8812) - το skin "Microsoft
        // PC Manager" ΕΙΝΑΙ το ομώνυμο θέμα (ήδη υπάρχει στο ThemeCatalog) - η επιλογή εδώ απλώς
        // εναλλάσσει σε αυτό / επαναφέρει το προηγούμενο πραγματικό θέμα, ΔΕΝ είναι ξεχωριστό σύστημα.
        // Επεκτάθηκε σε 3-way (ρητό αίτημα χρήστη) - προστέθηκε το "Windows Classic" skin, ίδιο μοτίβο.
        private void Skin_Changed(object sender, RoutedEventArgs e)
        {
            var currentName = ThemeManager.CurrentPair.DisplayName;
            if (RadioSkinPcManager.IsChecked == true && currentName != "Microsoft PC Manager")
            {
                ThemeManager.SelectTheme(ThemeCatalog.MicrosoftPcManager);
            }
            else if (RadioSkinWindowsClassic.IsChecked == true && currentName != "Windows Classic")
            {
                ThemeManager.SelectTheme(ThemeCatalog.WindowsClassic);
            }
            else if (RadioSkinClassic.IsChecked == true && (currentName == "Microsoft PC Manager" || currentName == "Windows Classic"))
            {
                var name = AppSettingsService.Current.LastNonPcManagerTheme;
                var pair = ThemeCatalog.All.FirstOrDefault(p => p.DisplayName == name) ?? ThemeCatalog.Windows11Fluent;
                ThemeManager.SelectTheme(pair);
                ComboTheme.SelectedItem = pair;
            }
            (Owner as MainWindow)?.ApplyMenuModeVisibility();
        }

        private void BtnSaveTheme_Click(object sender, RoutedEventArgs e)
        {
            AppSettingsService.Save();
            ThemedMessageBox.Show(LanguageService.T("Appr_SettingsSaved"), LanguageService.T("AppearanceSettingsTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
