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
            // REQ-570-10: δεν υπάρχει επίσημο feed για ViVeTool IDs (κανένα δεν υπάρχει, δημόσια
            // τεκμηρίωση/κοινότητα είναι η μόνη πηγή παντού) - "sync" σημαίνει περιοδική ενημέρωση
            // αυτής της curated λίστας από αξιόπιστη δημόσια πηγή (pureinfotech.com/vivetool-codes,
            // ενημερώθηκε 2026-09-29), ΟΧΙ αυτόματο live feed. Επιλέχθηκαν GA-track (όχι μόνο
            // Insider-only) χαρακτηριστικά, μεγαλύτερης εφαρμοσιμότητας.
            new ViveFeature(LanguageService.T("Vive_F11Title"), new[] { "59213768" }, LanguageService.T("Vive_F11Tip")),
            new ViveFeature(LanguageService.T("Vive_F12Title"), new[] { "61090762" }, LanguageService.T("Vive_F12Tip")),
            new ViveFeature(LanguageService.T("Vive_F13Title"), new[] { "60813048" }, LanguageService.T("Vive_F13Tip")),
            new ViveFeature(LanguageService.T("Vive_F14Title"), new[] { "58989092", "60716524", "48433719", "61391826" }, LanguageService.T("Vive_F14Tip")),
            new ViveFeature(LanguageService.T("Vive_F15Title"), new[] { "59728252" }, LanguageService.T("Vive_F15Tip")),
        };

        // ΔΙΟΡΘΩΣΗ: το ViVeTool κατεβαίνει πλέον σε δικό του φάκελο χρήστη (%LocalAppData%\OptimizerWpf\tools\vivetool).
        // Πριν αντιγραφόταν στον φάκελο εγκατάστασης της ίδιας της εφαρμογής (Program Files — χωρίς δικαιώματα εγγραφής
        // σε μη-admin) ΜΑΖΙ με ΟΛΑ τα .dll του zip, που μπορούσαν να αντικαταστήσουν ομώνυμα assemblies του GearWin
        // (π.χ. System.*.dll) και να το σπάσουν.
        // ΑΣΦΑΛΕΙΑ (έλεγχος ευπαθειών): το vivetool.exe τρέχει ΜΕ ΥΨΗΛΑ ΔΙΚΑΙΩΜΑΤΑ. Ο φάκελος στο %LocalAppData% είναι
        // εγγράψιμος από κάθε μη-elevated διεργασία του χρήστη (θα μπορούσε να αντικαταστήσει το exe → privilege
        // escalation χωρίς UAC). Τώρα ζει στο %ProgramData%\GearWin με ACL μόνο-Administrators/SYSTEM εγγραφής
        // (οι Users έχουν μόνο ανάγνωση/εκτέλεση), και το zip ΔΕΝ περνά ποτέ από το %TEMP% (εξαγωγή από τη μνήμη).
        private static string RootDir => SecureStorage.RootDir;
        private static string ToolsDir => Path.Combine(RootDir, "tools", "vivetool");
        private static void SecureDirectory(string dir) => SecureStorage.SecureDirectory(dir);
        private static string ExePath => Path.Combine(ToolsDir, "vivetool.exe");
        private static string LegacyExePath => Path.Combine(AppContext.BaseDirectory, "vivetool.exe");

        public static async Task<string?> EnsureAvailableAsync(IProgress<string>? progress = null)
        {
            if (File.Exists(ExePath))
            {
                try { SecureDirectory(RootDir); } catch { return null; } // ποτέ εκτέλεση από φάκελο που δεν μπορεί να κλειδωθεί
                return ExePath;
            }
            if (File.Exists(LegacyExePath)) return LegacyExePath; // εγκατάσταση από παλιότερη έκδοση

            progress?.Report(LanguageService.T("Vive_Downloading"));
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) }; // χωρίς timeout η λήψη μπορούσε να κρεμάσει για πάντα
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
                // Μόνο HTTPS προς το github.com (ή τα δικά του asset hosts) — το URL προέρχεται από απάντηση API.
                if (!Uri.TryCreate(assetUrl, UriKind.Absolute, out var assetUri) || assetUri.Scheme != Uri.UriSchemeHttps ||
                    !(assetUri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase) || assetUri.Host.EndsWith("githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException(LanguageService.T("Vive_NoAssetFound"));

                var bytes = await http.GetByteArrayAsync(assetUrl);
                using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
                var exeEntry = archive.Entries.FirstOrDefault(en => string.Equals(en.Name, "ViVeTool.exe", StringComparison.OrdinalIgnoreCase));
                if (exeEntry == null) throw new InvalidOperationException(LanguageService.T("Vive_ExeNotFound"));

                SecureDirectory(RootDir);
                Directory.CreateDirectory(ToolsDir);
                // Αντιγράφουμε ΜΟΝΟ τα αρχεία του φακέλου όπου βρίσκεται το ViVeTool.exe (όχι υποφάκελοι), με όνομα
                // αρχείου χωρίς διαδρομή (Path.GetFileName) - καμία δυνατότητα "zip slip" εκτός του φακέλου εργαλείου.
                var prefix = exeEntry.FullName[..^exeEntry.Name.Length];
                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.Length == 0 || !entry.FullName.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    if (entry.FullName.Length != prefix.Length + entry.Name.Length) continue;
                    entry.ExtractToFile(Path.Combine(ToolsDir, Path.GetFileName(entry.Name)), true);
                }
                if (!File.Exists(ExePath)) throw new InvalidOperationException(LanguageService.T("Vive_ExeNotFound"));

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
            var errTask = process.StandardError.ReadToEndAsync(); // drain
            var output = await process.StandardOutput.ReadToEndAsync();
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException) { try { process.Kill(true); } catch { } return null; }
            _ = errTask;
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
