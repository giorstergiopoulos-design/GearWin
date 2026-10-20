using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using OptimizerWpf.Services;
using Xunit;

namespace OptimizerWpf.Tests
{
    public class ProblemReportTests
    {
        [Fact]
        public async Task BundleContainsOnlyWhitelistedFilesPlusInfo()
        {
            var dir = Path.Combine(Path.GetTempPath(), "pr_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "crash.log"), "boom");
            File.WriteAllText(Path.Combine(dir, "AppSettings.json"), "{}");
            File.WriteAllText(Path.Combine(dir, "ClipboardHistory.json"), "SECRET");   // ΔΕΝ πρέπει να μπει
            File.WriteAllText(Path.Combine(dir, "passwords.db"), "SECRET");            // ΔΕΝ πρέπει να μπει
            var zipPath = Path.Combine(dir, "out.zip");

            await ProblemReportService.CreateAsync(zipPath, "9.9.9", dir);

            using var zip = ZipFile.OpenRead(zipPath);
            var names = zip.Entries.Select(e => e.FullName).OrderBy(n => n).ToList();
            Assert.Equal(new[] { "AppSettings.json", "crash.log", "info.txt" }, names);
            using var r = new StreamReader(zip.GetEntry("info.txt")!.Open());
            Assert.Contains("GearWin 9.9.9", r.ReadToEnd());
        }

        [Fact]
        public async Task MissingLogsDoNotBreakTheBundle()
        {
            var dir = Path.Combine(Path.GetTempPath(), "pr_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var zipPath = Path.Combine(dir, "out.zip");
            await ProblemReportService.CreateAsync(zipPath, "1.0.0", dir);
            using var zip = ZipFile.OpenRead(zipPath);
            Assert.Single(zip.Entries);
        }
    }
}
