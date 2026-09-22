using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    public record RecommendedApp(string Group, string Title, string WingetId);
    public record InstalledApp(string DisplayName, string? Publisher, string UninstallString);
    public record UwpAppInfo(string Name, string PackageFullName);

    // Port του Optimizer.ps1's Εφαρμογές & Bloat καρτέλα (~14193-15000). Βλ. HANDOFF.md §0.4ιβ.
    public static class BloatwareService
    {
        // ===== Ενσωματωμένα Στοιχεία Windows =====

        public static Task<bool> InstallAppxAsync(string namePattern, string wingetId) => RunPsForSuccessAsync(
            $"try {{ Add-AppxPackage -Register \"$(Get-AppxPackage -AllUsers | Where {{$_.Name -like '*{namePattern}*'}} | Select -First 1 -ExpandProperty InstallLocation)\\AppxManifest.xml\" -DisableDevelopmentMode -ErrorAction Stop }} " +
            $"catch {{ winget install --id {wingetId} --exact --silent --accept-source-agreements --accept-package-agreements }}");

        public static Task<bool> RemoveAppxAsync(string namePattern) => RunPsForSuccessAsync(
            $"Get-AppxPackage -Name '*{namePattern}*' -AllUsers | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue");

        // ΝΕΟ (εντοπίστηκε σε audit παλαιότητας WPF-έναντι-ps1 v2.8.2): το ps1 original δεν κάνει ΜΟΝΟ
        // enable του optional feature - ορίζει επίσης το WMP Legacy ως προεπιλογή για αρχεία μουσικής
        // ΚΑΙ το καρφιτσώνει στη γραμμή εργασιών (best-effort, βλ. Set-DefaultAppAssociations/
        // Add-TaskbarPin ~10817-10853). Και τα δύο βήματα είναι best-effort (δεν αποτυγχάνει η συνολική
        // εγκατάσταση αν αποτύχουν - ίδιο "μη-κρίσιμο" πνεύμα με το original).
        public static async Task<bool> InstallWindowsMediaPlayerAsync()
        {
            var ok = await RunPsForSuccessAsync(
                "Enable-WindowsOptionalFeature -Online -FeatureName MediaPlayback -All -NoRestart -ErrorAction SilentlyContinue; " +
                "Enable-WindowsOptionalFeature -Online -FeatureName WindowsMediaPlayer -All -NoRestart -ErrorAction SilentlyContinue");
            if (!ok) return false;
            await SetDefaultAppAssociationsAsync(new Dictionary<string, string>
            {
                [".mp3"] = "Applications\\wmplayer.exe", [".wma"] = "Applications\\wmplayer.exe",
                [".wav"] = "Applications\\wmplayer.exe", [".m4a"] = "Applications\\wmplayer.exe",
            });
            await AddTaskbarPinAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "wmplayer.exe"));
            return true;
        }

        // Port του K-Lite install script (~14731) - winget install + ορισμός WMP Legacy για μουσική,
        // MPC-HC για βίντεο (αν εντοπιστεί το exe του, η θέση εγκατάστασης διαφέρει ανά αρχιτεκτονική),
        // με καρφίτσωμα στη γραμμή εργασιών και για τα δύο (όλα best-effort, ίδιο με WMP παραπάνω).
        public static async Task<bool> InstallKLiteAsync()
        {
            var ok = await WingetInstallAsync("CodecGuide.K-LiteCodecPack.Mega");
            if (!ok) return false;

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var mpcCandidates = new[]
            {
                Path.Combine(programFiles, "K-Lite Codec Pack", "MPC-HC64", "mpc-hc64.exe"),
                Path.Combine(programFilesX86, "K-Lite Codec Pack", "MPC-HC", "mpc-hc.exe"),
                Path.Combine(programFiles, "K-Lite Codec Pack", "MPC-HC", "mpc-hc.exe"),
            };
            var mpcPath = mpcCandidates.FirstOrDefault(File.Exists);

            var associations = new Dictionary<string, string>
            {
                [".mp3"] = "Applications\\wmplayer.exe", [".wma"] = "Applications\\wmplayer.exe",
                [".wav"] = "Applications\\wmplayer.exe", [".flac"] = "Applications\\wmplayer.exe",
            };
            if (mpcPath != null)
            {
                var mpcExeName = Path.GetFileName(mpcPath);
                foreach (var ext in new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv" }) associations[ext] = $"Applications\\{mpcExeName}";
            }
            await SetDefaultAppAssociationsAsync(associations);
            await AddTaskbarPinAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "wmplayer.exe"));
            if (mpcPath != null) await AddTaskbarPinAsync(mpcPath);
            return true;
        }

        // Port του Set-DefaultAppAssociations (~10817) - DISM /Import-DefaultAppAssociations μέσω
        // προσωρινού XML, best-effort (δεν ρίχνει exception σε αποτυχία, απλά επιστρέφει false).
        private static async Task<bool> SetDefaultAppAssociationsAsync(Dictionary<string, string> associations)
        {
            var xmlLines = string.Join("\r\n", associations.Select(kv =>
                $"  <Association Identifier=\"{kv.Key}\" ProgId=\"{kv.Value}\" ApplicationName=\"\" />"));
            var xmlContent = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<DefaultAssociations>\r\n{xmlLines}\r\n</DefaultAssociations>";
            var xmlPath = Path.Combine(Path.GetTempPath(), $"OptimizerWpfDefaultAppAssoc_{Guid.NewGuid():N}.xml");
            try
            {
                await File.WriteAllTextAsync(xmlPath, xmlContent, System.Text.Encoding.UTF8);
                return await RunProcessForSuccessAsync("dism.exe", $"/Online /Import-DefaultAppAssociations:\"{xmlPath}\"");
            }
            catch { return false; }
            finally { try { File.Delete(xmlPath); } catch { } }
        }

        // Port του Add-TaskbarPin (~10837) - η Microsoft δεν προσφέρει επίσημο API για προγραμματιστικό
        // pin στη γραμμή εργασιών των Windows 11· χρησιμοποιείται το ίδιο "verb" του Explorer (shell
        // context-menu action) όπως το ps1 original - λειτουργεί σε πολλά builds, όχι εγγυημένο σε όλα.
        private static async Task<bool> AddTaskbarPinAsync(string exePath)
        {
            if (!File.Exists(exePath)) return false;
            var script = $@"
try {{
    $folder = Split-Path '{exePath}' -Parent
    $fileName = Split-Path '{exePath}' -Leaf
    $shellApp = New-Object -ComObject Shell.Application
    $ns = $shellApp.Namespace($folder)
    $item = $ns.ParseName($fileName)
    if (-not $item) {{ exit 1 }}
    $pinVerb = $item.Verbs() | Where-Object {{ ($_.Name -replace '&','') -match 'στη γραμμή εργασιών|to Tas' }}
    if ($pinVerb) {{ $pinVerb.DoIt(); exit 0 }} else {{ exit 1 }}
}} catch {{ exit 1 }}
";
            var scriptFile = Path.Combine(Path.GetTempPath(), $"OptimizerWpfTaskbarPin_{Guid.NewGuid():N}.ps1");
            try
            {
                await File.WriteAllTextAsync(scriptFile, script);
                using var process = Process.Start(new ProcessStartInfo("powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptFile}\"")
                { UseShellExecute = false, CreateNoWindow = true });
                if (process == null) return false;
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch { return false; }
            finally { try { File.Delete(scriptFile); } catch { } }
        }

        public static Task<bool> RemoveWindowsMediaPlayerAsync() => RunPsForSuccessAsync(
            "Disable-WindowsOptionalFeature -Online -FeatureName WindowsMediaPlayer -NoRestart -ErrorAction SilentlyContinue");

        public static void InstallPhotoViewer()
        {
            using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows Photo Viewer\Capabilities\FileAssociations", writable: true);
            key?.SetValue(".jpg", "PhotoViewer.FileAssoc.Tiff");
        }

        public static void RemovePhotoViewer()
        {
            try { Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows Photo Viewer\Capabilities\FileAssociations", throwOnMissingSubKey: false); } catch { }
        }

        // ===== Προτεινόμενες Εφαρμογές (winget) =====

        // Group είναι πλέον ένα σταθερό, ουδέτερο-ως-προς-γλώσσα κλειδί (όχι το ελληνικό εμφανιζόμενο
        // όνομα) - το GroupHeaderIconConverter στο BloatwareView.xaml.cs μεταφράζει το κλειδί σε
        // εμφανιζόμενο κείμενο τη στιγμή της εμφάνισης, ώστε η ομαδοποίηση/κεφαλίδα να ακολουθεί την
        // τρέχουσα γλώσσα (ρητό αίτημα χρήστη: "μετάφρασε τα όλα").
        public static readonly IReadOnlyList<RecommendedApp> RecommendedApps = new[]
        {
            new RecommendedApp("Browsers", "Google Chrome", "Google.Chrome"),
            new RecommendedApp("Browsers", "Mozilla Firefox", "Mozilla.Firefox"),
            new RecommendedApp("Browsers", "Opera", "Opera.Opera"),
            new RecommendedApp("Browsers", "Microsoft Edge", "Microsoft.Edge"),
            new RecommendedApp("Compression", "7-Zip", "7zip.7zip"),
            new RecommendedApp("Compression", "WinRAR", "RARLab.WinRAR"),
            new RecommendedApp("Multimedia", "VLC", "VideoLAN.VLC"),
            new RecommendedApp("Multimedia", "Spotify", "Spotify.Spotify"),
            new RecommendedApp("Multimedia", "GOM Player", "GOMLab.GOMPlayer"),
            new RecommendedApp("Multimedia", "Winamp", "Winamp.Winamp"),
            new RecommendedApp("Multimedia", "K-Lite Codec Pack", "CodecGuide.K-LiteCodecPack.Mega"),
            new RecommendedApp("Communication", "Zoom", "Zoom.Zoom"),
            new RecommendedApp("Communication", "Discord", "Discord.Discord"),
            new RecommendedApp("Communication", "Microsoft Teams", "Microsoft.Teams"),
            // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "του viber θέλει διόρθωση") - "Viber.Viber" δεν υπάρχει στο
            // winget repository, η εγκατάσταση απέτυχε πάντα σιωπηλά - το σωστό ID είναι "Rakuten.Viber".
            new RecommendedApp("Communication", "Viber", "Rakuten.Viber"),
            new RecommendedApp("Tools", "AnyDesk", "AnyDeskSoftwareGmbH.AnyDesk"),
            new RecommendedApp("Tools", "TeamViewer", "TeamViewer.TeamViewer"),
            new RecommendedApp("Tools", "PowerToys", "Microsoft.PowerToys"),
            new RecommendedApp("Documents", "OpenOffice", "Apache.OpenOffice"),
            new RecommendedApp("Documents", "Adobe Acrobat Reader", "Adobe.Acrobat.Reader.64-bit"),
            new RecommendedApp("SystemLibs", ".NET Desktop Runtime 8", "Microsoft.DotNet.DesktopRuntime.8"),
            new RecommendedApp("SystemLibs", "DirectX Runtime", "Microsoft.DirectX"),
            new RecommendedApp("SystemLibs", "Visual C++ Redistributable x64", "Microsoft.VCRedist.2015+.x64"),
        };

        public static async Task<bool> IsWingetAppInstalledAsync(string wingetId)
        {
            using var process = Process.Start(new ProcessStartInfo("winget.exe", $"list --id {wingetId} --exact --accept-source-agreements")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
            if (process == null) return false;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return process.ExitCode == 0 && !output.Contains("No installed package found");
        }

        public static Task<bool> WingetInstallAsync(string wingetId) => RunProcessForSuccessAsync("winget.exe",
            $"install --id {wingetId} --exact --silent --accept-source-agreements --accept-package-agreements");

        public static Task<bool> WingetUninstallAsync(string wingetId) => RunProcessForSuccessAsync("winget.exe",
            $"uninstall --id {wingetId} --exact --silent");

        // ===== Διαχείριση UWP (Store apps only) =====

        public static async Task<IReadOnlyList<UwpAppInfo>> ScanUwpAppsAsync()
        {
            const string script = "Get-AppxPackage | Where-Object { -not $_.IsFramework -and -not $_.IsResourcePackage -and $_.SignatureKind -eq 'Store' } | " +
                                   "Select-Object Name, PackageFullName | Sort-Object Name | ConvertTo-Json -Compress";
            var output = await RunPsCaptureAsync(script);
            if (string.IsNullOrWhiteSpace(output)) return Array.Empty<UwpAppInfo>();
            try
            {
                var root = System.Text.Json.JsonDocument.Parse(output).RootElement;
                var elements = root.ValueKind == System.Text.Json.JsonValueKind.Array ? root.EnumerateArray() : new[] { root }.AsEnumerable();
                return elements.Select(e => new UwpAppInfo(e.GetProperty("Name").GetString() ?? "", e.GetProperty("PackageFullName").GetString() ?? "")).ToList();
            }
            catch { return Array.Empty<UwpAppInfo>(); }
        }

        public static Task<bool> RemoveUwpAppAsync(string packageFullName) =>
            RunPsForSuccessAsync($"Remove-AppxPackage -Package '{packageFullName}'");

        // ===== Βαθιά Απεγκατάσταση =====

        public static Task<IReadOnlyList<InstalledApp>> LoadInstalledAppsAsync() => Task.Run(() =>
        {
            var apps = new List<InstalledApp>();
            foreach (var (hive, subPath) in new[]
                     {
                         (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                         (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                         (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                     })
            {
                using var uninstallKey = hive.OpenSubKey(subPath);
                if (uninstallKey == null) continue;
                foreach (var name in uninstallKey.GetSubKeyNames())
                {
                    using var sub = uninstallKey.OpenSubKey(name);
                    var displayName = sub?.GetValue("DisplayName") as string;
                    var uninstallString = sub?.GetValue("UninstallString") as string;
                    if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(uninstallString)) continue;
                    if (sub!.GetValue("SystemComponent") != null) continue;
                    if (apps.Any(a => a.DisplayName == displayName)) continue;
                    apps.Add(new InstalledApp(displayName, sub.GetValue("Publisher") as string, uninstallString));
                }
            }
            return (IReadOnlyList<InstalledApp>)apps.OrderBy(a => a.DisplayName).ToList();
        });

        public static Task<int> UninstallAppAsync(string uninstallString) => Task.Run(() =>
        {
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{uninstallString}\"") { UseShellExecute = false });
            process?.WaitForExit();
            return process?.ExitCode ?? -1;
        });

        public static IReadOnlyList<string> FindResidualFolders(string appName)
        {
            var safeName = string.Concat(appName.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetEnvironmentVariable("ProgramData") ?? "",
            };
            var results = new List<string>();
            foreach (var root in roots.Where(Directory.Exists))
            {
                try
                {
                    results.AddRange(Directory.EnumerateDirectories(root).Where(d => Path.GetFileName(d).Contains(safeName, StringComparison.OrdinalIgnoreCase)));
                }
                catch { }
            }
            return results;
        }

        public static bool DeleteResidualFolder(string path)
        {
            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                return true;
            }
            catch { return false; }
        }

        // ===== helpers =====

        private static async Task<bool> RunPsForSuccessAsync(string command)
        {
            using var process = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"")
            { UseShellExecute = false, CreateNoWindow = true });
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }

        private static async Task<string> RunPsCaptureAsync(string command)
        {
            using var process = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = System.Text.Encoding.UTF8 });
            if (process == null) return "";
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }

        private static async Task<bool> RunProcessForSuccessAsync(string fileName, string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false, CreateNoWindow = true });
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
    }
}
