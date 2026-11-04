using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace OptimizerWpf.Tests
{
    // 6.9.7: εκδόσεις χωρίς ίχνη προσωπικών δεδομένων/διαδρομών build και με πλήρεις πληροφορίες έκδοσης σε κάθε exe/dll του προγράμματος.
    public class ReleaseHygieneTests
    {
        // D:\a\_work\... inside GearWin.exe is the public build path of Microsoft's .NET apphost, not ours; only our own CI checkout path (D:\a\GearWin) is a trace.
        private static readonly string[] Forbidden = { @"C:\Users\", @"C:\w\", @"D:\a\GearWin", "gstrj", "LOGISTIRIO", "giorstergiopoulos@gmail", "/Users/" };

        private static string AppDir() => Path.GetDirectoryName(typeof(OptimizerWpf.MainWindow).Assembly.Location)!;

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CHANGELOG.md"))) dir = dir.Parent;
            return dir!.FullName;
        }

        private static IEnumerable<string> Hits(string file)
        {
            var b = File.ReadAllBytes(file);
            var latin = Encoding.Latin1.GetString(b);
            var utf16 = Encoding.Unicode.GetString(b);
            foreach (var p in Forbidden)
                if (latin.Contains(p, StringComparison.OrdinalIgnoreCase) || utf16.Contains(p, StringComparison.OrdinalIgnoreCase)) yield return $"{Path.GetFileName(file)}: contains '{p}'";
        }

        [Fact]
        public void ProgramAssembliesCarryFullVersionInfo()
        {
            foreach (var f in new[] { "GearWin.dll", "GearWin.exe" }.Select(n => Path.Combine(AppDir(), n)).Where(File.Exists))
            {
                var v = FileVersionInfo.GetVersionInfo(f);
                Assert.Equal("giorstergiopoulos-design", v.CompanyName);
                Assert.Equal("GearWin - Complete PC Care", v.ProductName);
                Assert.Contains("giorstergiopoulos-design", v.LegalCopyright);
                Assert.False(string.IsNullOrWhiteSpace(v.FileDescription));
                Assert.Matches(@"^\d+\.\d+\.\d+\.0$", v.FileVersion);
                Assert.StartsWith(v.FileVersion!.Substring(0, v.FileVersion.Length - 2), v.ProductVersion);
            }
        }

        [Fact]
        public void BuildOutputHasNoPersonalDataOrLocalPaths()
        {
            var problems = new List<string>();
            foreach (var f in new[] { "GearWin.dll", "GearWin.exe", "GearWin.deps.json", "GearWin.runtimeconfig.json" }.Select(n => Path.Combine(AppDir(), n)).Where(File.Exists))
                problems.AddRange(Hits(f));
            // der publish-Ordner (falls vorhanden): exe/dll/json, keine PDB
            var publish = Path.Combine(Root(), "wpf", "OptimizerWpf", "publish", "win-x64");
            if (Directory.Exists(publish))
            {
                problems.AddRange(Directory.EnumerateFiles(publish, "GearWin*.*", SearchOption.TopDirectoryOnly).Where(f => !f.EndsWith(".pdb")).SelectMany(Hits));
                Assert.Empty(Directory.EnumerateFiles(publish, "*.pdb", SearchOption.AllDirectories));
            }
            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct().Take(20)));
        }

        [Fact]
        public void TrackedTextFilesHaveNoPersonalPaths()
        {
            var root = Root();
            var psi = new ProcessStartInfo("git", "ls-files") { WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi)!;
            var files = p.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            p.WaitForExit();
            if (files.Length == 0) return;   // not a git checkout
            var text = new HashSet<string> { ".md", ".cs", ".xaml", ".csproj", ".iss", ".ps1", ".yml", ".json", ".txt", ".toml", ".bat" };
            var self = "ReleaseHygieneTests.cs";
            var problems = new List<string>();
            foreach (var rel in files.Select(f => f.Trim()).Where(f => text.Contains(Path.GetExtension(f).ToLowerInvariant()) && !f.EndsWith(self) && !f.StartsWith("release/")))
            {
                var path = Path.Combine(root, rel);
                if (!File.Exists(path)) continue;
                var s = File.ReadAllText(path);
                foreach (var pat in new[] { @"C:\w\", "gstrj", "LOGISTIRIO", "giorstergiopoulos@gmail" })
                    if (s.Contains(pat, StringComparison.OrdinalIgnoreCase)) problems.Add($"{rel}: '{pat}'");
                // C:\Users\<name>\ : only the neutral placeholders test / Public are allowed
                if (System.Text.RegularExpressions.Regex.IsMatch(s, @"C:\\Users\\(?!test\\|Public\\)\w", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) problems.Add($"{rel}: personal C:\\Users path");
            }
            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Take(20)));
        }
    }
}
