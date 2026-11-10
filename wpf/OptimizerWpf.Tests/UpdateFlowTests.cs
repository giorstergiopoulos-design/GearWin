using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OptimizerWpf.Services;
using OptimizerWpf.Views;

namespace OptimizerWpf.Tests
{
    // 6.9.8: ροή ενημέρωσης (επανασάρωση, ενιαίο τελικό μήνυμα), εικονίδια Edge/Teams, ελαφρύτερες σαρώσεις αρχείων.
    public class UpdateFlowTests
    {
        [Theory]
        [InlineData(5, false, false, false, true)]   // αποτελέσματα, χωρίς rescan: μόνο Ενημέρωση
        [InlineData(5, false, false, true, false)]   // μετά την ενημέρωση (rescan): μόνο το γρανάζι, ενεργό
        [InlineData(0, false, false, true, false)]   // καμία εγγραφή και rescan: μόνο γρανάζι
        [InlineData(5, true, false, true, false)]    // σάρωση πάνω από rescan: γρανάζι σε πρόοδο (ανενεργό)
        [InlineData(5, false, true, true, true)]     // νέα ενημέρωση ξεκινά: μόνο Ενημέρωση σε πρόοδο
        public void RescanModeShowsOnlyTheGear(int results, bool scanning, bool installing, bool rescan, bool updateVisible)
        {
            var s = OptimizationView.DecideActionButtons(results, scanning, installing, rescan);
            Assert.Equal(updateVisible, s.UpdateVisible);
            Assert.NotEqual(s.ScanVisible, s.UpdateVisible);
            if (!scanning && !installing && rescan) { Assert.True(s.ScanEnabled); Assert.False(s.UpdateEnabled); }
        }

        [Fact]
        public void OneSummaryHasCorrectCountsPerCategory()
        {
            var original = LanguageService.Current;
            try
            {
                LanguageService.SetLanguage("en");
                var text = OptimizationView.BuildUpdateSummary(new[] { "A", "B" }, new[] { "C [x]" }, new[] { "KB1" }, Array.Empty<string>(), Array.Empty<string>(), new[] { "Drv" }, 1, true);
                var lines = text.Split(Environment.NewLine);
                Assert.Contains(lines, l => l.StartsWith("Apps: 2 / 3") && l.Contains("C [x]"));
                Assert.Contains(lines, l => l.StartsWith("Windows: 1 / 1"));
                Assert.Contains(lines, l => l.StartsWith("Drivers: 0 / 1") && l.Contains("Drv"));
                Assert.DoesNotContain(lines, l => l.StartsWith("Apps: 0"));
            }
            finally { LanguageService.SetLanguage(original); }
        }

        [Fact]
        public void OnlyCategoriesThatRanAppearInTheSummary()
        {
            var text = OptimizationView.BuildUpdateSummary(Array.Empty<string>(), Array.Empty<string>(), new[] { "KB1", "KB2" }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), 0, false);
            Assert.Single(text.Split(Environment.NewLine));
            Assert.Contains("2 / 2", text);
        }

        [Fact]
        public void EdgeAndTeamsDoNotShareTheGenericMicrosoftLogoSource()
        {
            Assert.True(LogoCacheService.HasLogoSource("Microsoft.Edge"));
            Assert.True(LogoCacheService.HasLogoSource("Microsoft.Teams"));
            var f = typeof(LogoCacheService).GetField("DomainByKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            var map = (Dictionary<string, string>)f.GetValue(null)!;
            Assert.NotEqual("microsoft.com", map["Microsoft.Edge"]);
            Assert.NotEqual("microsoft.com", map["Microsoft.Teams"]);
            Assert.NotEqual(map["Microsoft.Edge"], map["Microsoft.Teams"]);
        }

        [Fact]
        public async System.Threading.Tasks.Task LargeFileScanKeepsOnlyTheFiftyBiggestInOrder()
        {
            var dir = Path.Combine(Path.GetTempPath(), "gw_large_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, "sub"));
            try
            {
                for (var i = 1; i <= 60; i++) File.WriteAllBytes(Path.Combine(dir, i % 2 == 0 ? "sub" : "", $"f{i}.bin"), new byte[i * 1024]);
                var top = await SystemService.FindLargeFilesInFolderAsync(dir);
                Assert.Equal(50, top.Count);
                Assert.Equal(60 * 1024 / 1024.0 / 1024.0, top[0].SizeMb, 2);
                Assert.True(top.Zip(top.Skip(1), (a, b) => a.SizeMb >= b.SizeMb).All(x => x));
                Assert.DoesNotContain(top, t => t.Path.EndsWith("f1.bin") || t.Path.EndsWith("f10.bin"));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public async System.Threading.Tasks.Task DuplicateScanSeparatesFilesThatOnlyDifferAfterTheFirstBlock()
        {
            var dir = Path.Combine(Path.GetTempPath(), "gw_dup_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var a = new byte[200 * 1024]; new Random(1).NextBytes(a);
                var b = (byte[])a.Clone(); b[^1] ^= 0xFF;   // same size, same first 64 KB, different tail
                File.WriteAllBytes(Path.Combine(dir, "a1.bin"), a);
                File.WriteAllBytes(Path.Combine(dir, "a2.bin"), a);
                File.WriteAllBytes(Path.Combine(dir, "b.bin"), b);
                var dups = await SystemService.FindDuplicatesInFolderAsync(dir);
                Assert.Equal(2, dups.Count);
                Assert.DoesNotContain(dups, d => d.Path.EndsWith("b.bin"));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
