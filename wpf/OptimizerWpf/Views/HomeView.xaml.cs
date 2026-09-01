using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class HomeView : UserControl
    {
        // Matches the WinForms app's periodic-refresh pattern (a Timer ticking on the UI thread,
        // reading already-computed values) - PerformanceCounter.NextValue() and
        // GlobalMemoryStatusEx are both fast, synchronous, safe to call directly on the Dispatcher
        // tick without a background thread.
        private readonly DispatcherTimer _refreshTimer;
        private PerformanceCounter? _cpuCounter;

        public HomeView()
        {
            InitializeComponent();
            PopulateDriveList();
            TryInitCpuCounter();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _refreshTimer.Tick += (_, _) => RefreshMetrics();
            _refreshTimer.Start();

            RefreshMetrics();
            _ = RefreshHealthScoreAsync();
        }

        private void TryInitCpuCounter()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // first call always returns 0 - prime it
            }
            catch
            {
                // Counter category can be missing/disabled on some systems (locked-down policy,
                // corrupted counter database) - degrade to "--%" instead of crashing the tab.
                _cpuCounter = null;
            }
        }

        private void PopulateDriveList()
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                CboHomeDrive.Items.Add(drive.Name.TrimEnd('\\'));
            }
            if (CboHomeDrive.Items.Count > 0) CboHomeDrive.SelectedIndex = 0;
        }

        private void RefreshMetrics()
        {
            if (_cpuCounter != null)
            {
                try
                {
                    var cpu = (int)Math.Round(_cpuCounter.NextValue());
                    TxtCpuPercent.Text = $"{cpu}%";
                    BarCpu.Value = cpu;
                }
                catch { /* counter can throw if it becomes invalid mid-run - skip this tick */ }
            }

            if (NativeMethods.GlobalMemoryStatusEx(out var memStatus))
            {
                var totalGb = memStatus.ullTotalPhys / 1024.0 / 1024.0 / 1024.0;
                var usedGb = totalGb - (memStatus.ullAvailPhys / 1024.0 / 1024.0 / 1024.0);
                var pct = (int)memStatus.dwMemoryLoad; // already an integer 0-100 percentage
                TxtRamPercent.Text = $"{pct}%";
                BarRam.Value = pct;
                TxtRamDetail.Text = $"{usedGb:0.0} / {totalGb:0.0} GB";
            }

            if (CboHomeDrive.SelectedItem is string driveName)
            {
                try
                {
                    var drive = new DriveInfo(driveName);
                    var totalGb = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
                    var freeGb = drive.TotalFreeSpace / 1024.0 / 1024.0 / 1024.0;
                    var usedPct = (int)Math.Round(100.0 * (1 - drive.TotalFreeSpace / (double)drive.TotalSize));
                    TxtDiskPercent.Text = $"{usedPct}%";
                    BarDisk.Value = usedPct;
                    TxtDiskDetail.Text = $"{freeGb:0.0} GB ελεύθερα από {totalGb:0.0} GB";
                }
                catch { /* drive can become unready (removable media ejected mid-run) */ }
            }
        }

        private async void BtnRefreshHealth_Click(object sender, System.Windows.RoutedEventArgs e) => await RefreshHealthScoreAsync();

        private async void BtnFixAllHealth_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Port of the "Διόρθωση Όλων" button in Optimizer.ps1 (only the safe, reversible fixes -
            // startup apps/pending restart are surfaced but never touched automatically, same as the
            // WinForms version).
            var result = await Task.Run(HealthScoreService.Compute);
            var fixedSomething = false;

            foreach (var issue in result.Issues)
            {
                switch (issue.FixType)
                {
                    case "Storage":
                        try
                        {
                            var temp = Environment.GetEnvironmentVariable("TEMP");
                            if (!string.IsNullOrEmpty(temp))
                            {
                                foreach (var f in Directory.EnumerateFileSystemEntries(temp))
                                {
                                    try { if (Directory.Exists(f)) Directory.Delete(f, true); else File.Delete(f); } catch { }
                                }
                            }
                            fixedSomething = true;
                        }
                        catch { }
                        break;
                    case "Defender":
                        // Toggling Defender real-time protection needs elevation + the Defender
                        // PowerShell/WMI provider - not wired up in this slice yet.
                        break;
                }
            }

            if (fixedSomething)
            {
                TxtHealthLabel.Text = "Ολοκληρώθηκαν οι διαθέσιμες αυτόματες διορθώσεις.";
            }
            await RefreshHealthScoreAsync();
        }

        // Category -> bar color, matching a reasonable visually-distinct palette (Optimizer.ps1
        // draws its own bar+legend colors per category - not ported 1:1 here, just kept distinct).
        private static readonly Dictionary<string, Color> CategoryColors = new()
        {
            ["Games"] = Color.FromRgb(90, 140, 255),
            ["Apps"] = Color.FromRgb(170, 110, 255),
            ["Photos"] = Color.FromRgb(90, 200, 120),
            ["Videos"] = Color.FromRgb(255, 150, 60),
            ["Documents"] = Color.FromRgb(60, 190, 190),
            ["Downloads"] = Color.FromRgb(230, 200, 60),
            ["Windows"] = Color.FromRgb(220, 90, 90),
            ["Other"] = Color.FromRgb(140, 140, 140),
        };

        private async void BtnAnalyzeDisk_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (CboHomeDrive.SelectedItem is not string driveName) return;

            TxtDiskAnalysisStatus.Text = $"Ανάλυση σε εξέλιξη για {driveName} - μπορεί να διαρκέσει λίγα λεπτά ανάλογα με το πλήθος αρχείων...";
            ListDiskCategories.ItemsSource = null;

            var result = await DiskAnalysisService.AnalyzeAsync(driveName);

            TxtDiskAnalysisStatus.Text = $"Σύνολο χρησιμοποιημένου χώρου: {result.TotalUsedGb:0.0} GB";
            var maxGb = Math.Max(0.01, result.Categories.Max(c => c.SizeGb));
            ListDiskCategories.ItemsSource = result.Categories
                .OrderByDescending(c => c.SizeGb)
                .Select(c => new DiskCategoryRow(
                    c.Name,
                    $"{c.SizeGb:0.0} GB",
                    280.0 * c.SizeGb / maxGb,
                    new SolidColorBrush(CategoryColors.GetValueOrDefault(c.Name, Color.FromRgb(150, 150, 150)))))
                .ToList();
        }

        private async Task RefreshHealthScoreAsync()
        {
            TxtHealthLabel.Text = "Υπολογισμός...";
            // HealthScoreService.Compute() does several WMI queries (Defender status, AV product,
            // restore points) which can take a noticeable moment - runs off the UI thread so the
            // window stays responsive while it's working (see the async-UI rule this project follows).
            var result = await Task.Run(HealthScoreService.Compute);

            TxtHealthScore.Text = result.Score.ToString();
            var scoreColor = result.Score >= 80
                ? new SolidColorBrush(Color.FromRgb(90, 200, 120))
                : result.Score >= 50
                    ? new SolidColorBrush(Color.FromRgb(230, 170, 60))
                    : new SolidColorBrush(Color.FromRgb(220, 80, 80));

            TxtHealthScore.Foreground = scoreColor;
            TxtHealthLabel.Foreground = scoreColor;
            TxtHealthLabel.Text = result.Score >= 80 ? "Καλή Κατάσταση" : result.Score >= 50 ? "Μέτρια Κατάσταση" : "Χρειάζεται Προσοχή";

            if (result.Issues.Count == 0)
            {
                ListHealthIssues.ItemsSource = new[] { new HealthIssueRow("Δεν εντοπίστηκαν προβλήματα - το σύστημά σας λειτουργεί καλά!", scoreColor) };
            }
            else
            {
                ListHealthIssues.ItemsSource = result.Issues.Select(i => new HealthIssueRow(i.Title, scoreColor)).ToList();
            }
        }
    }

    // Bindable row for the ItemsControl in HomeView.xaml (Title + the dot color, matching the
    // score-colored dot per issue that Optimizer.ps1 draws next to each issue label).
    public record HealthIssueRow(string Title, Brush DotColor);

    // Bindable row for the Disk Analysis category list/bar chart.
    public record DiskCategoryRow(string Name, string SizeText, double BarWidth, Brush BarColor);

    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", EntryPoint = "GlobalMemoryStatusEx", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool NativeGlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        public static bool GlobalMemoryStatusEx(out MEMORYSTATUSEX status)
        {
            status = new MEMORYSTATUSEX();
            status.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
            return NativeGlobalMemoryStatusEx(ref status);
        }
    }
}
