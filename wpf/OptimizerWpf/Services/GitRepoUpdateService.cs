using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // 6.9.0 - ανίχνευση ενημερώσεων για εφαρμογές/εργαλεία που έχουν git repo (π.χ. AI agents ή εργαλεία που κλωνοποίησε ο χρήστης).
    // Ο χρήστης δηλώνει φακέλους (AppSettings.GitRepoRoots)· σαρώνονται repos σε βάθος ≤3. Ανά repo: `git fetch --quiet` και πλήθος commits που
    // λείπουν από το upstream. Η ενημέρωση είναι ΜΟΝΟ `git pull --ff-only` (ποτέ merge/rebase/force). Καμία κλήση shell: ορίσματα μέσω ArgumentList.
    public static class GitRepoUpdateService
    {
        public const string Source = "git";
        private static readonly TimeSpan CmdTimeout = TimeSpan.FromSeconds(40);

        public static IReadOnlyList<string> FindRepos(string root, int maxDepth = 3, int maxRepos = 60)
        {
            var found = new List<string>();
            void Walk(string dir, int depth)
            {
                if (found.Count >= maxRepos) return;
                try
                {
                    if (Directory.Exists(Path.Combine(dir, ".git"))) { found.Add(dir); return; }   // repo: δεν κατεβαίνουμε μέσα του
                    if (depth >= maxDepth) return;
                    foreach (var sub in Directory.EnumerateDirectories(dir))
                    {
                        var name = Path.GetFileName(sub);
                        if (name.StartsWith('.') || name is "node_modules" or "bin" or "obj") continue;
                        if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0) continue;   // όχι junctions/symlinks
                        Walk(sub, depth + 1);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            if (Directory.Exists(root)) Walk(root, 0);
            return found;
        }

        // `git rev-list --count HEAD..@{u}` -> αριθμός (ή null αν δεν είναι έγκυρη έξοδος).
        public static int? ParseBehindCount(string? output)
        {
            var t = output?.Trim();
            return int.TryParse(t, out var n) && n >= 0 ? n : null;
        }

        public static bool IsSafeRepoPath(string? path) =>
            !string.IsNullOrWhiteSpace(path) && path.Length < 260 && !path.Any(c => c == '"' || char.IsControl(c))
            && Path.IsPathRooted(path) && Directory.Exists(Path.Combine(path, ".git"));

        private static async Task<(bool Started, int Exit, string Output)> GitAsync(string repo, CancellationToken ct, params string[] args)
        {
            var psi = new ProcessStartInfo("git.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            psi.ArgumentList.Add("-C"); psi.ArgumentList.Add(repo);
            foreach (var a in args) psi.ArgumentList.Add(a);
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";   // ποτέ αναμονή για κωδικό
            Process? p;
            try { p = Process.Start(psi); } catch (System.ComponentModel.Win32Exception) { return (false, -1, ""); }
            if (p == null) return (false, -1, "");
            using (p)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(CmdTimeout);
                var outTask = p.StandardOutput.ReadToEndAsync(cts.Token);
                var errTask = p.StandardError.ReadToEndAsync(cts.Token);
                try { await p.WaitForExitAsync(cts.Token); }
                catch (OperationCanceledException) { try { p.Kill(true); } catch { } return (true, -2, "timeout"); }
                var o = await outTask; var e = await errTask;
                return (true, p.ExitCode, p.ExitCode == 0 ? o : o + e);
            }
        }

        public static async Task<List<WingetUpdate>> ScanAsync(IEnumerable<string> roots, CancellationToken ct = default)
        {
            var list = new List<WingetUpdate>();
            foreach (var root in roots.Take(10))
            {
                foreach (var repo in FindRepos(root))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!IsSafeRepoPath(repo)) continue;
                    var fetch = await GitAsync(repo, ct, "fetch", "--quiet");
                    if (!fetch.Started) return list;   // git δεν είναι εγκατεστημένο
                    if (fetch.Exit != 0) continue;
                    var behind = await GitAsync(repo, ct, "rev-list", "--count", "HEAD..@{u}");
                    var n = behind.Exit == 0 ? ParseBehindCount(behind.Output) : null;
                    if (n is > 0)
                    {
                        var head = await GitAsync(repo, ct, "rev-parse", "--short", "HEAD");
                        list.Add(new WingetUpdate(Path.GetFileName(repo), repo, head.Exit == 0 ? head.Output.Trim() : "?", $"+{n}", Source));
                    }
                }
            }
            return list;
        }

        // Μόνο fast-forward: αν υπάρχουν τοπικές αλλαγές/αποκλίσεις το git αρνείται και εμφανίζεται ο λόγος.
        public static async Task<(bool Ok, string? Reason)> PullAsync(string repo, CancellationToken ct = default)
        {
            if (!IsSafeRepoPath(repo)) return (false, "Unsafe repository path rejected");
            var r = await GitAsync(repo, ct, "pull", "--ff-only");
            if (!r.Started) return (false, "git not found");
            if (r.Exit == 0) return (true, null);
            var line = r.Output.Split('\n').Select(s => s.Trim()).LastOrDefault(s => s.Length > 0);
            return (false, line);
        }
    }
}
