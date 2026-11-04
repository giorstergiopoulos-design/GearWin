using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests
{
    // 6.9.6: κάθε έκδοση του CHANGELOG (από 6.7.0) υπάρχει στο in-app Ιστορικό Εκδόσεων σε ΟΛΕΣ τις 14 γλώσσες, εμφανίζεται στο παράθυρο Βοήθειας, και τα
    // μη-αγγλικά κείμενα δεν είναι αγγλικά leftovers.
    public class VersionHistoryCoverageTests
    {
        private static readonly string[] Languages = { "el", "en", "de", "fr", "es", "ko", "zh", "it", "ru", "ja", "pt", "tr", "ar", "hi" };

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CHANGELOG.md"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }

        private static List<string> ChangelogKeys() =>
            Regex.Matches(File.ReadAllText(Path.Combine(Root(), "CHANGELOG.md")), @"^## v(\d+)\.(\d+)\.(\d+)", RegexOptions.Multiline)
                .Select(m => (maj: int.Parse(m.Groups[1].Value), min: int.Parse(m.Groups[2].Value), pat: int.Parse(m.Groups[3].Value)))
                .Where(v => (v.maj, v.min, v.pat).CompareTo((6, 7, 0)) >= 0)
                .Select(v => $"v{v.maj}{v.min}{v.pat}").Distinct().ToList();

        [Fact]
        public void EveryChangelogVersionFrom670IsInTheInAppHistoryInAllLanguagesAndShownInHelp()
        {
            var keys = ChangelogKeys();
            Assert.Contains("v696", keys);
            var xaml = File.ReadAllText(Path.Combine(Root(), "wpf", "OptimizerWpf", "Views", "HelpWindow.xaml"));
            var all = LanguageService.AllTranslations;
            var problems = new List<string>();
            foreach (var k in keys)
            {
                foreach (var part in new[] { "Title", "Desc" })
                {
                    var key = $"VerHist_{k}{part}";
                    if (!xaml.Contains("{tr:Tr " + key + "}")) problems.Add($"{key}: not shown in HelpWindow.xaml");
                    foreach (var lang in Languages)
                        if (!all[lang].TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text)) problems.Add($"{lang}: {key} missing");
                }
                // titel starts with the localized word for "Version" + the version number
                var ver = k.Substring(1);
                var dotted = $"{ver[0]}.{ver[1]}.{ver.Substring(2)}";
                foreach (var lang in Languages)
                    if (all[lang].TryGetValue($"VerHist_{k}Title", out var t) && !t.Contains(dotted)) problems.Add($"{lang}: VerHist_{k}Title lacks {dotted}");
            }
            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Take(30)));
        }

        [Fact]
        public void HistoryAndHelpTextsAreTranslatedNotEnglishLeftovers()
        {
            var all = LanguageService.AllTranslations;
            var keys = ChangelogKeys().SelectMany(k => new[] { $"VerHist_{k}Title", $"VerHist_{k}Desc" })
                .Concat(new[] { "Help_UpdatesTitle", "Help_UpdatesDesc", "Help_PerfTitle", "Help_PerfDesc" }).ToList();
            var problems = new List<string>();
            foreach (var key in keys)
                foreach (var lang in Languages.Where(l => l != "en"))
                    if (!all[lang].TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text)) problems.Add($"{lang}: {key} missing");
                    else if (text == all["en"][key]) problems.Add($"{lang}: {key} is identical to the English text");
            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Take(30)));
        }
    }
}
