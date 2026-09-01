using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public record WingetUpdate(string Name, string Id, string CurrentVersion, string AvailableVersion);

    // Port of the winget-scanning half of Optimizer.ps1's Update-WingetProgressBar/
    // Complete-WingetListLoad/Parse-WingetTableSection (line ~11607). Only the plain
    // "winget upgrade" table is parsed in this increment - the separate msstore-source call and
    // the other-package-manager (pip/npm/choco/...) JSON section from the PowerShell version
    // aren't ported yet, so results will be a subset of what Optimizer.ps1 shows.
    public static class WingetService
    {
        public static async Task<IReadOnlyList<WingetUpdate>> ScanAsync()
        {
            var (output, _) = await RunWingetAsync("upgrade --include-unknown --accept-source-agreements --disable-interactivity");
            return ParseTable(output);
        }

        // ΝΕΟ (χρήστης ανέφερε: "δεν βγαίνει κανένα μήνυμα επιτυχούς εγκατάστασης, έλεγξέ το") - πριν
        // το "success" ήταν πάντα true εκτός αν πετάχτηκε exception (σχεδόν ποτέ), άρα ένα πραγματικό
        // αποτυχημένο winget (π.χ. exit code μη-μηδενικό - πακέτο κλειδωμένο/χρειάζεται επανεκκίνηση)
        // ΔΕΝ θα αναφερόταν ποτέ ως αποτυχία. Τώρα ελέγχεται το πραγματικό exit code της διεργασίας.
        public static async Task<bool> UpgradeAsync(string id)
        {
            var (_, exitCode) = await RunWingetAsync($"upgrade --id \"{id}\" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity");
            return exitCode == 0;
        }

        private static async Task<(string Output, int ExitCode)> RunWingetAsync(string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };

            using var process = new Process { StartInfo = psi };
            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (stdout, process.ExitCode);
        }

        private static IReadOnlyList<WingetUpdate> ParseTable(string rawText)
        {
            var result = new List<WingetUpdate>();
            // winget draws its table with ANSI escape/progress codes mixed in on some terminals -
            // strip them so column-position math below isn't thrown off (same reasoning as
            // Optimizer.ps1's ANSI-stripping fix mentioned in its changelog).
            var cleaned = Regex.Replace(rawText, @"\x1B\[[0-9;]*[a-zA-Z]", "");
            var lines = cleaned.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

            var headerIndex = Array.FindIndex(lines, l => Regex.IsMatch(l, @"^\s*Name\s+Id\s+Version"));
            if (headerIndex < 0 || headerIndex + 2 >= lines.Length) return result;

            var headerLine = lines[headerIndex];
            var columns = new[] { "Name", "Id", "Version", "Available", "Source" }
                .Select(name => (Name: name, Start: headerLine.IndexOf(name, StringComparison.Ordinal)))
                .Where(c => c.Start >= 0)
                .OrderBy(c => c.Start)
                .ToList();

            for (var i = headerIndex + 2; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (Regex.IsMatch(line, @"^\d+ upgrades? available") ||
                    line.StartsWith("No installed package found") ||
                    Regex.IsMatch(line, @"^-+$"))
                    continue;

                var fields = new Dictionary<string, string>();
                for (var c = 0; c < columns.Count; c++)
                {
                    var start = columns[c].Start;
                    if (start >= line.Length) continue;
                    var len = c + 1 < columns.Count ? columns[c + 1].Start - start : line.Length - start;
                    if (start + len > line.Length) len = line.Length - start;
                    if (len <= 0) continue;
                    fields[columns[c].Name] = line.Substring(start, len).Trim();
                }

                if (fields.TryGetValue("Id", out var id) && !string.IsNullOrEmpty(id))
                {
                    result.Add(new WingetUpdate(
                        fields.GetValueOrDefault("Name", ""),
                        id,
                        fields.GetValueOrDefault("Version", ""),
                        fields.GetValueOrDefault("Available", "")));
                }
            }

            return result;
        }
    }
}
