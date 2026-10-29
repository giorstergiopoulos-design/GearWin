using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests
{
    public class GitRepoUpdateTests
    {
        private static string Temp() => Path.Combine(Path.GetTempPath(), "gwgit_" + Guid.NewGuid().ToString("N"));

        [Theory]
        [InlineData("3\n", 3)]
        [InlineData("0", 0)]
        [InlineData("fatal: no upstream", null)]
        [InlineData("-1", null)]
        [InlineData("", null)]
        public void BehindCountParsing(string output, int? expected) => Assert.Equal(expected, GitRepoUpdateService.ParseBehindCount(output));

        [Fact]
        public void FindsReposWithinDepthAndSkipsInsideRepos()
        {
            var root = Temp();
            Directory.CreateDirectory(Path.Combine(root, "a", ".git"));
            Directory.CreateDirectory(Path.Combine(root, "a", "inner", ".git"));   // inside a repo: not listed
            Directory.CreateDirectory(Path.Combine(root, "x", "y", "b", ".git"));
            Directory.CreateDirectory(Path.Combine(root, "x", "y", "z", "w", "deep", ".git"));   // too deep
            Directory.CreateDirectory(Path.Combine(root, "node_modules", "c", ".git"));
            var found = GitRepoUpdateService.FindRepos(root).Select(p => Path.GetRelativePath(root, p)).OrderBy(p => p).ToArray();
            Assert.Equal(new[] { "a", Path.Combine("x", "y", "b") }, found);
        }

        [Fact]
        public void UnsafePathsAreRejected()
        {
            var root = Temp();
            Directory.CreateDirectory(Path.Combine(root, ".git"));
            Assert.True(GitRepoUpdateService.IsSafeRepoPath(root));
            Assert.False(GitRepoUpdateService.IsSafeRepoPath(root + "\" & calc & \""));
            Assert.False(GitRepoUpdateService.IsSafeRepoPath(@"relative\path"));
            Assert.False(GitRepoUpdateService.IsSafeRepoPath(Path.GetTempPath()));   // no .git
            Assert.False(WingetService.IsSafePackageId(@"C:\nope", "git"));
        }

        private static int Git(string dir, params string[] args)
        {
            var psi = new ProcessStartInfo("git.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = dir };
            foreach (var a in new[] { "-c", "user.email=t@t", "-c", "user.name=t", "-c", "commit.gpgsign=false" }.Concat(args)) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode;
        }

        [Fact]
        public async Task DetectsAndFastForwardsARealRepo()
        {
            try { Process.Start(new ProcessStartInfo("git.exe", "--version") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!.WaitForExit(); }
            catch (System.ComponentModel.Win32Exception) { return; }   // git is not installed on this machine: nothing to verify

            var root = Temp();
            var origin = Path.Combine(root, "origin"); var clones = Path.Combine(root, "clones");
            Directory.CreateDirectory(origin); Directory.CreateDirectory(clones);
            Assert.Equal(0, Git(origin, "init", "-q", "-b", "main"));
            File.WriteAllText(Path.Combine(origin, "a.txt"), "1");
            Git(origin, "add", "."); Assert.Equal(0, Git(origin, "commit", "-q", "-m", "one"));
            Assert.Equal(0, Git(clones, "clone", "-q", origin, "tool"));

            Assert.Empty(await GitRepoUpdateService.ScanAsync(new[] { clones }));   // up to date

            File.WriteAllText(Path.Combine(origin, "a.txt"), "2");
            Git(origin, "commit", "-q", "-am", "two");
            var updates = await GitRepoUpdateService.ScanAsync(new[] { clones });
            var u = Assert.Single(updates);
            Assert.Equal("git", u.Source);
            Assert.Equal("+1", u.AvailableVersion);

            var (ok, reason) = await WingetService.UpgradeDetailedAsync(u.Id, "git");
            Assert.True(ok, reason);
            Assert.Equal("2", File.ReadAllText(Path.Combine(clones, "tool", "a.txt")));
            Assert.Empty(await GitRepoUpdateService.ScanAsync(new[] { clones }));
        }
    }
}
