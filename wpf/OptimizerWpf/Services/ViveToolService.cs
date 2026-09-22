using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public record ViveFeature(string Title, string[] Ids, string Tooltip);
    public enum ViveFeatureState { Enabled, Disabled, NotFound, Unknown }

    // Port του Optimizer.ps1's ViVeTool integration (~17378-17709) - "Κρυφές/Πειραματικές
    // Λειτουργίες Windows". Κατεβάζει αυτόματα το vivetool.exe από το επίσημο GitHub repo
    // (thebookisclosed/ViVe) αν δεν υπάρχει ήδη τοπικά.
    public static class ViveToolService
    {
        // Ίδια curated λίστα με το ps1 original (10 feature groups, ~17655-17664). Property αντί για
        // readonly field ώστε να ξαναχτίζεται (με τρέχουσα γλώσσα) κάθε φορά που ανοίγει το
        // ViveToolWindow (ρητό αίτημα χρήστη: "μετάφρασε τα όλα" - συμπεριλαμβανομένου αυτού).
        public static IReadOnlyList<ViveFeature> CuratedFeatures => new[]
        {
            new ViveFeature(LanguageService.T("Vive_F1Title"), new[] { "57645315" }, LanguageService.T("Vive_F1Tip")),
            new ViveFeature(LanguageService.T("Vive_F2Title"), new[] { "57048231", "47205210", "56328729", "48433719" }, LanguageService.T("Vive_F2Tip")),
            new ViveFeature(LanguageService.T("Vive_F3Title"), new[] { "48433719" }, LanguageService.T("Vive_F3Tip")),
            new ViveFeature(LanguageService.T("Vive_F4Title"), new[] { "41356296", "48433719" }, LanguageService.T("Vive_F4Tip")),
            new ViveFeature(LanguageService.T("Vive_F5Title"), new[] { "57703775" }, LanguageService.T("Vive_F5Tip")),
            new ViveFeature(LanguageService.T("Vive_F6Title"), new[] { "55994763", "59162732" }, LanguageService.T("Vive_F6Tip")),
            new ViveFeature(LanguageService.T("Vive_F7Title"), new[] { "57857165", "57994323", "48433719", "49453572", "58383338", "59270880", "59203365" }, LanguageService.T("Vive_F7Tip")),
            new ViveFeature(LanguageService.T("Vive_F8Title"), new[] { "45172197", "51406324" }, LanguageService.T("Vive_F8Tip")),
            new ViveFeature(LanguageService.T("Vive_F9Title"), new[] { "57741219" }, LanguageService.T("Vive_F9Tip")),
            new ViveFeature(LanguageService.T("Vive_F10Title"), new[] { "54792954", "55345819" }, LanguageService.T("Vive_F10Tip")),
        };

        private static string ExePath => Path.Combine(AppContext.BaseDirectory, "vivetool.exe");

        public static async Task<string?> EnsureAvailableAsync(IProgress<string>? progress = null)
        {
            if (File.Exists(ExePath)) return ExePath;

            progress?.Report(LanguageService.T("Vive_Downloading"));
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "OptimizerWpf");
                var json = await http.GetStringAsync("https://api.github.com/repos/thebookisclosed/ViVe/releases/latest");
                var root = JsonDocument.Parse(json).RootElement;
                string? assetUrl = null;
                foreach (var asset in root.GetProperty("assets").EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.Contains("IntelAmd", StringComparison.OrdinalIgnoreCase)) { assetUrl = asset.GetProperty("browser_download_url").GetString(); break; }
                }
                if (assetUrl == null)
                {
                    foreach (var asset in root.GetProperty("assets").EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) { assetUrl = asset.GetProperty("browser_download_url").GetString(); break; }
                    }
                }
                if (assetUrl == null) throw new InvalidOperationException(LanguageService.T("Vive_NoAssetFound"));

                var zipPath = Path.Combine(Path.GetTempPath(), "vivetool_dl.zip");
                var bytes = await http.GetByteArrayAsync(assetUrl);
                await File.WriteAllBytesAsync(zipPath, bytes);

                var extractDir = Path.Combine(Path.GetTempPath(), "vivetool_extract");
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                var foundExe = Directory.EnumerateFiles(extractDir, "ViVeTool.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (foundExe == null) throw new InvalidOperationException(LanguageService.T("Vive_ExeNotFound"));
                File.Copy(foundExe, ExePath, true);
                foreach (var dll in Directory.EnumerateFiles(extractDir, "*.dll", SearchOption.AllDirectories))
                    File.Copy(dll, Path.Combine(AppContext.BaseDirectory, Path.GetFileName(dll)), true);

                File.Delete(zipPath);
                Directory.Delete(extractDir, true);
                progress?.Report(LanguageService.T("Vive_DownloadSuccess"));
                return ExePath;
            }
            catch (Exception ex)
            {
                progress?.Report($"{LanguageService.T("Vive_DownloadFailed")}{ex.Message}");
                return null;
            }
        }

        public static async Task<string?> RunCommandAsync(string arguments)
        {
            var exe = await EnsureAvailableAsync();
            if (exe == null) return null;
            using var process = Process.Start(new ProcessStartInfo(exe, arguments)
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true });
            if (process == null) return null;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }

        // Ίδια λογική με το ps1's Test-ViveFeatureState - αριθμητικός EnabledState κωδικός πρώτα
        // (0/3=disabled, 1/2=enabled), λεκτικό fallback δεύτερο.
        public static async Task<ViveFeatureState> QueryStateAsync(string firstId)
        {
            var output = await RunCommandAsync($"/query /id:{firstId}");
            if (output == null) return ViveFeatureState.Unknown;
            if (output.Contains("could not be found", StringComparison.OrdinalIgnoreCase) || output.Contains("No feature configurations", StringComparison.OrdinalIgnoreCase))
                return ViveFeatureState.NotFound;

            var m = System.Text.RegularExpressions.Regex.Match(output, @"(?im)^\s*EnabledState\s*[:=]?\s*(\d)");
            if (m.Success)
            {
                return m.Groups[1].Value switch
                {
                    "1" or "2" => ViveFeatureState.Enabled,
                    "0" or "3" => ViveFeatureState.Disabled,
                    _ => ViveFeatureState.Unknown,
                };
            }
            if (output.Contains("Enabled", StringComparison.OrdinalIgnoreCase)) return ViveFeatureState.Enabled;
            if (output.Contains("Disabled", StringComparison.OrdinalIgnoreCase)) return ViveFeatureState.Disabled;
            return ViveFeatureState.Unknown;
        }

        public static Task<string?> EnableAsync(IEnumerable<string> ids) => RunCommandAsync("/enable " + string.Join(" ", ids.Select(id => $"/id:{id}")));
        public static Task<string?> DisableAsync(IEnumerable<string> ids) => RunCommandAsync("/disable " + string.Join(" ", ids.Select(id => $"/id:{id}")));
    }
}
