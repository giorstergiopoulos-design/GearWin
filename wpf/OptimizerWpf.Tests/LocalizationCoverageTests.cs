using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests
{
    public class LocalizationCoverageTests
    {
        private static string ViewsDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "OptimizerWpf", "Views"))) dir = dir.Parent;
            return dir == null ? throw new DirectoryNotFoundException("Views folder not found") : Path.Combine(dir.FullName, "OptimizerWpf");
        }

        [Fact]
        public void EveryTranslationKeyUsedInXamlExistsInAllLanguages()
        {
            var keys = new SortedSet<string>();
            foreach (var file in Directory.EnumerateFiles(ViewsDir(), "*.xaml", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
                foreach (Match m in Regex.Matches(File.ReadAllText(file), @"\{(?:tr|local):Tr\s+([A-Za-z0-9_]+)\}"))
                    keys.Add(m.Groups[1].Value);
            Assert.True(keys.Count > 200, "XAML scan found too few keys: " + keys.Count);

            var original = LanguageService.Current;
            var missing = new List<string>();
            try
            {
                foreach (var code in new[] { "el", "en", "de", "fr", "es", "ko", "zh", "it", "ru", "ja", "pt", "tr", "ar", "hi" })
                {
                    LanguageService.SetLanguage(code);
                    foreach (var key in keys)
                        if (LanguageService.T(key) == key && !(code == "en" && !key.Contains('_'))) missing.Add($"{code}:{key}");
                }
            }
            finally { LanguageService.SetLanguage(original); }
            Assert.True(missing.Count == 0, "Missing translations: " + string.Join(", ", missing.Take(40)));
        }

        [Fact]
        public void EveryRecommendedAppHasALogoSource()
        {
            var without = BloatwareService.RecommendedApps.Where(a => !LogoCacheService.HasLogoSource(a.WingetId)).Select(a => a.WingetId).ToList();
            Assert.True(without.Count == 0, "Recommended apps without a logo domain: " + string.Join(", ", without));
        }
    }
}
