using System;
using System.Windows;
using System.Windows.Controls;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class AppearanceSettingsWindow : Window
    {
        private void NestedScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => NestedScrollHelper.Forward(sender, e);

        // Τα event handlers είναι δεσμευμένα στο XAML, άρα ενεργοποιούνται ήδη κατά την αρχικοποίηση των
        // controls στον constructor - το _loading τα αδρανοποιεί ώστε το άνοιγμα του παραθύρου να μην
        // γράφει ρυθμίσεις/εκκινεί υπηρεσίες/αλλάζει γλώσσα.
        private bool _loading = true;
        private System.Windows.Threading.DispatcherTimer? _opacitySaveTimer;

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

            // ΝΕΟ - ROADMAP.md REQ-570-02/12 - βλ. SystemService.SetLaunchWithWindowsToTray.
            ChkLaunchToTray.IsChecked = AppSettingsService.Current.LaunchWithWindowsToTray;

            var settings = AppSettingsService.Current;
            ChkSidebarEnabled.IsChecked = settings.SidebarEnabled;
            if (settings.SidebarPosition == "Left") RadioLeft.IsChecked = true;
            else RadioRight.IsChecked = true;
            if (settings.MenuMode == "Sidebar") RadioMenuSidebar.IsChecked = true;
            else RadioMenuHoriz.IsChecked = true;

            var currentThemeName = ThemeManager.CurrentPair.DisplayName;
            foreach (var (radio, theme) in SkinRadios()) radio.IsChecked = currentThemeName == theme.DisplayName;
            RadioSkinClassic.IsChecked = !SkinCatalog.IsSkin(currentThemeName);

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

            SliderOpacity.Value = Math.Clamp(settings.WindowOpacityPercent, 60, 100);
            TxtOpacityValue.Text = $"{(int)SliderOpacity.Value}%";
            SliderOpacity.ValueChanged += SliderOpacity_ValueChanged;

            MainTabs.SelectedIndex = initialTabIndex;

            // ΔΙΟΡΘΩΣΗ: η επιλογή γινόταν με σκληρό index μόνο για el/en/de/fr - για οποιαδήποτε από τις
            // άλλες 10 γλώσσες (es, it, ru, zh, ja, pt, ko, tr, ar, hi) επιλεγόταν η Ελληνική και το
            // SelectionChanged ΕΠΑΝΕΦΕΡΕ σιωπηλά τη γλώσσα της εφαρμογής στα Ελληνικά στο άνοιγμα.
            foreach (var item in ComboLanguage.Items)
                if (item is ComboBoxItem { Tag: string code } && code == LanguageService.Current) { ComboLanguage.SelectedItem = item; break; }
            ApplyTranslations();
            LanguageService.Changed += ApplyTranslations;
            Closed += (_, _) =>
            {
                LanguageService.Changed -= ApplyTranslations;
                if (_opacitySaveTimer != null) { _opacitySaveTimer.Stop(); _opacitySaveTimer = null; AppSettingsService.Save(); }
            };
            _loading = false;
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
            if (_loading) return;
            if (ComboLanguage.SelectedItem is ComboBoxItem { Tag: string code }) LanguageService.SetLanguage(code);
        }

        // Ρητό αίτημα χρήστη (screenshot): "Ορισμός ως προεπιλεγμένο θέμα εκκίνησης" - χωρίς το
        // checkbox, η επιλογή εφαρμόζεται ζωντανά για την τρέχουσα συνεδρία ΜΟΝΟ (ΔΕΝ γράφεται στις
        // persisted ρυθμίσεις - στην επόμενη εκκίνηση επανέρχεται το πραγματικά αποθηκευμένο θέμα).
        private void ComboTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            if (ComboTheme.SelectedItem is not ThemePair pair) return;
            if (ChkSetDefaultTheme.IsChecked == true) ThemeManager.SelectTheme(pair);
            else ThemeManager.PreviewTheme(pair);
            // ΔΙΟΡΘΩΣΗ (ρητό αίτημα χρήστη: "τα skins PC Manager & Windows Classic... να απαλειφθούν
            // από τα θέματα") - το ComboTheme.ItemsSource (ThemeCatalog.All) πλέον ΔΕΝ περιέχει καθόλου
            // αυτά τα δύο skins, άρα κάθε επιλογή εδώ είναι εξ ορισμού ένα κανονικό, μη-skin θέμα.
            RadioSkinClassic.IsChecked = true;
            (Owner as MainWindow)?.ApplyMenuModeVisibility();
        }

        private void ChkAnimatedBg_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            ThemeManager.SetAnimatedBackgrounds(ChkAnimatedBg.IsChecked == true);
        }

        // ΝΕΟ - βλ. σχόλιο στο XAML/UpdateNotificationService.cs.
        private void ChkUpdateNotifications_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            AppSettingsService.Current.UpdateNotificationsEnabled = ChkUpdateNotifications.IsChecked == true;
            AppSettingsService.Save();
        }

        private void ComboUpdateInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            if (ComboUpdateInterval.SelectedItem is not ComboBoxItem { Tag: int hours }) return;
            AppSettingsService.Current.UpdateCheckIntervalHours = hours;
            AppSettingsService.Save();
        }

        private async void BtnUpdateCheckNow_Click(object sender, RoutedEventArgs e)
        {
            TxtUpdateCheckStatus.Text = LanguageService.T("Appr_UpdateChecking");
            StatusService.SetBusy(LanguageService.T("Appr_UpdateChecking"));
            try { await UpdateNotificationService.RunCheckAsync(); }
            catch { /* δίκτυο/API - η κατάσταση ανανεώνεται παρακάτω με ό,τι είναι γνωστό */ }
            finally { StatusService.SetIdle(LanguageService.T("Ready")); }
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
            if (_loading) return;
            AppSettingsService.Current.LowDiskNotificationsEnabled = ChkLowDiskNotifications.IsChecked == true;
            AppSettingsService.Save();
        }

        private void ComboLowDiskThreshold_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            if (ComboLowDiskThreshold.SelectedItem is not ComboBoxItem { Tag: int pct }) return;
            AppSettingsService.Current.LowDiskThresholdPercent = pct;
            AppSettingsService.Save();
        }

        // ΝΕΟ - ROADMAP.md REQ-570-02/12 (ρητό αίτημα χρήστη) - εγγράφει/αφαιρεί ΜΙΑ τιμή στο ίδιο
        // registry Run key που ήδη χρησιμοποιεί το SystemService για τα startup items ΤΡΙΤΩΝ
        // εφαρμογών (ξεχωριστό όνομα τιμής, καμία σύγκρουση) - βλ. App.xaml.cs's "--tray" χειρισμό.
        private async void ChkLaunchToTray_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            var enabled = ChkLaunchToTray.IsChecked == true;
            ChkLaunchToTray.IsEnabled = false;
            bool ok;
            try { ok = await SystemService.SetLaunchWithWindowsToTrayAsync(enabled); }
            catch (Exception ex) { ok = false; ThemedMessageBox.Show(ex.Message, LanguageService.T("AppearanceSettingsTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); }
            ChkLaunchToTray.IsEnabled = true;
            if (!ok)
            {
                // Η εργασία δεν δημιουργήθηκε - ειλικρινής επαναφορά του checkbox αντί για ψεύτικο "ενεργό".
                _loading = true; ChkLaunchToTray.IsChecked = !enabled; _loading = false;
                ThemedMessageBox.Show(LanguageService.T("Autostart_Failed"), LanguageService.T("AppearanceSettingsTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            AppSettingsService.Current.LaunchWithWindowsToTray = enabled;
            AppSettingsService.Save();
        }

        // ΝΕΟ - roadmap "Widget επιφάνειας εργασίας" - ζωντανή ενεργοποίηση/απενεργοποίηση, ίδιο μοτίβο
        // με το ChkAnimatedBg_Changed παραπάνω.
        private void ChkDesktopWidget_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            var enabled = ChkDesktopWidget.IsChecked == true;
            AppSettingsService.Current.DesktopWidgetEnabled = enabled;
            AppSettingsService.Save();
            try { if (enabled) DesktopWidgetService.Start(); else DesktopWidgetService.Stop(); }
            catch (Exception ex) { ThemedMessageBox.Show(ex.Message, LanguageService.T("AppearanceSettingsTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void AnimationStyle_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
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

        // REQ-580-05: port του MotionDeskStudio's opacity slider - ζωντανό preview καθώς σέρνεις
        // (εφαρμόζεται απευθείας στο MainWindow σε κάθε ValueChanged, όχι μόνο στο "Αποθήκευση").
        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var percent = (int)Math.Round(SliderOpacity.Value);
            TxtOpacityValue.Text = $"{percent}%";
            AppSettingsService.Current.WindowOpacityPercent = percent;
            // Αποθήκευση με debounce (όχι εγγραφή στο δίσκο σε κάθε tick του slider)
            if (_opacitySaveTimer == null)
            {
                _opacitySaveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _opacitySaveTimer.Tick += (_, _) => { _opacitySaveTimer?.Stop(); _opacitySaveTimer = null; AppSettingsService.Save(); };
            }
            _opacitySaveTimer.Stop(); _opacitySaveTimer.Start();
            if (Owner is MainWindow main) main.ApplyWindowOpacity();
        }

        private void ChkSidebarEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            AppSettingsService.Current.SidebarEnabled = ChkSidebarEnabled.IsChecked == true;
            AppSettingsService.Save();
            (Owner as MainWindow)?.ApplyMenuModeVisibility();
        }

        private void SidebarPosition_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            AppSettingsService.Current.SidebarPosition = RadioLeft.IsChecked == true ? "Left" : "Right";
            AppSettingsService.Save();
            (Owner as MainWindow)?.ApplyMenuModeVisibility();
        }

        private void MenuMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            AppSettingsService.Current.MenuMode = RadioMenuSidebar.IsChecked == true ? "Sidebar" : "HorizontalModern";
            AppSettingsService.Save();
            (Owner as MainWindow)?.ApplyMenuModeVisibility();
        }

        // 6.1.0 - ΟΛΑ τα skins (βλ. SkinCatalog) εναλλάσσονται από εδώ· τα radio buttons παραμένουν (ρητό
        // αίτημα χρήστη). "Κανονικό" = επαναφορά του τελευταίου πραγματικού θέματος.
        private (RadioButton Radio, ThemePair Theme)[] SkinRadios() => new[]
        {
            (RadioSkinPcManager, ThemeCatalog.MicrosoftPcManager),
            (RadioSkinWindowsClassic, ThemeCatalog.WindowsClassic),
            (RadioSkinSettings11, ThemeCatalog.Windows11Settings),
            (RadioSkinGamingHub, ThemeCatalog.GamingHub),
            (RadioSkinOfficeRibbon, ThemeCatalog.OfficeRibbon),
        };

        private void Skin_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            var currentName = ThemeManager.CurrentPair.DisplayName;
            var chosen = SkinRadios().FirstOrDefault(r => r.Radio.IsChecked == true);
            if (chosen.Radio != null)
            {
                if (currentName != chosen.Theme.DisplayName) ThemeManager.SelectTheme(chosen.Theme);
            }
            else if (RadioSkinClassic.IsChecked == true && SkinCatalog.IsSkin(currentName))
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

        private void BtnExportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"GearWin-Settings-{DateTime.Now:yyyy-MM-dd}.json",
                Filter = "JSON (*.json)|*.json"
            };
            if (dialog.ShowDialog(this) != true) return;

            var ok = AppSettingsService.Export(dialog.FileName);
            ThemedMessageBox.Show(
                LanguageService.T(ok ? "Appr_BackupExportOk" : "Appr_BackupExportFail"),
                LanguageService.T("AppearanceSettingsTitle"), MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Error);
        }

        private void BtnImportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "JSON (*.json)|*.json" };
            if (dialog.ShowDialog(this) != true) return;

            var confirm = ThemedMessageBox.Show(LanguageService.T("Appr_BackupImportConfirm"), LanguageService.T("AppearanceSettingsTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            var ok = AppSettingsService.Import(dialog.FileName);
            ThemedMessageBox.Show(
                LanguageService.T(ok ? "Appr_BackupImportOk" : "Appr_BackupImportFail"),
                LanguageService.T("AppearanceSettingsTitle"), MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Error);
            // ΔΙΟΡΘΩΣΗ: οι νέες ρυθμίσεις (θέμα, γλώσσα, opacity κ.λπ.) απαιτούν επανεκκίνηση για να
            // εφαρμοστούν πλήρως σε ΟΛΑ τα ήδη ανοιχτά παράθυρα - τίμια ενημέρωση αντί να προσποιούμαστε
            // ζωντανή εφαρμογή που δεν συμβαίνει στην πραγματικότητα.
            if (ok) ThemedMessageBox.Show(LanguageService.T("Appr_BackupRestartNeeded"), LanguageService.T("AppearanceSettingsTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
