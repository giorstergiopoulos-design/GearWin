using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class ReportsCard : UserControl
    {
        public ReportsCard() => InitializeComponent();

        private void SetBusy(bool busy, string? message = null)
        {
            foreach (var b in new[] { BtnSummaryTxt, BtnSummaryPdf, BtnDiagZip, BtnBattery, BtnSystemInfo, BtnProblem }) b.IsEnabled = !busy;
            if (busy) StatusService.SetBusy(message ?? ""); else StatusService.SetIdle(LanguageService.T("Ready"));
        }

        private void Toast(string text) => (Window.GetWindow(this) as MainWindow)?.ShowToast(text);

        private async void BtnSummaryTxt_Click(object sender, RoutedEventArgs e) => await ExportSummaryAsync("txt");
        private async void BtnSummaryPdf_Click(object sender, RoutedEventArgs e) => await ExportSummaryAsync("pdf");

        // Ίδια λογική με το Κέντρο Συντήρησης (MaintenanceCenterWindow.ExportReportAsync) - ίδιο SystemReportService.
        private async Task ExportSummaryAsync(string ext)
        {
            SetBusy(true, LanguageService.T("Center_ReportBuilding"));
            TxtReportsStatus.Text = LanguageService.T("Center_ReportBuilding");
            try
            {
                var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "";
                var content = await SystemReportService.GenerateAsync(version);
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    $"GearWin_SystemReport_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
                await Task.Run(() => SystemReportService.Save(path, content));
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                TxtReportsStatus.Text = LanguageService.T("Center_ReportSaved") + path;
            }
            catch (Exception ex) { TxtReportsStatus.Text = LanguageService.T("Center_ReportFailed") + ex.Message; }
            finally { SetBusy(false); }
        }

        private async void BtnDiagZip_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true, LanguageService.T("Health_GeneratingDiagnosticsZip"));
            try
            {
                var path = await HealthCleanupService.GenerateDiagnosticsZipAsync();
                TxtReportsStatus.Text = path != null
                    ? $"{LanguageService.T("Health_DiagnosticsZipSavedPrefix")}{path}"
                    : LanguageService.T("Health_DiagnosticsZipFailed");
                if (path != null) Toast(LanguageService.T("Health_DiagnosticsZipToast"));
            }
            finally { SetBusy(false); }
        }

        private async void BtnBattery_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true, LanguageService.T("Health_GeneratingBatteryReport"));
            try
            {
                var ok = await HealthCleanupService.GenerateBatteryReportAsync();
                TxtReportsStatus.Text = LanguageService.T(ok ? "Health_BatteryReportOpened" : "Health_BatteryReportFailed");
                if (ok) Toast(LanguageService.T("Health_BatteryReportOpened"));
            }
            finally { SetBusy(false); }
        }

        private async void BtnProblem_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true, LanguageService.T("Reports_ProblemBundle"));
            try
            {
                var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "";
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"GearWin_ProblemReport_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                await ProblemReportService.CreateAsync(path, version);
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                TxtReportsStatus.Text = LanguageService.T("Reports_ProblemBundleDone") + path;
            }
            catch (Exception ex) { TxtReportsStatus.Text = ex.Message; }
            finally { SetBusy(false); }
        }

        private void BtnSystemInfo_Click(object sender, RoutedEventArgs e)
        {
            try { AdvancedToolsService.CreateSystemReport(); }
            catch (Exception ex) { TxtReportsStatus.Text = ex.Message; }
        }
    }
}
