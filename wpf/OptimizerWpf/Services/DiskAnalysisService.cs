using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // Roots (ρητό αίτημα χρήστη - δευτερεύον παράθυρο "περαιτέρω ανάλυσης"): ίδια πληροφορία με το
    // ps1's $categories[$k].Roots - τα root φακέλους της κατηγορίας, ώστε το drilldown παράθυρο να
    // μπορεί να σαρώσει ΤΟΥΣ ΙΔΙΟΥΣ φακέλους (π.χ. τα Steam/Epic/... roots για "Games") αντί να
    // μαντεύει. "Other" δεν έχει roots (δεν αντιστοιχεί σε συγκεκριμένο φάκελο) - ίδιο με το ps1.
    public record DiskCategory(string Name, double SizeGb, IReadOnlyList<string> Roots);
    public record DiskAnalysisResult(double TotalUsedGb, IReadOnlyList<DiskCategory> Categories);
    public record DiskSubfolderSize(string Name, double SizeGb);

    // C# port of the category-detection scan script embedded in Optimizer.ps1's
    // Start-DiskCategoryAnalysis (line ~18167) - same category roots (Steam/Epic/Origin/EA/GOG/
    // Ubisoft/Riot/Battle.net for Games, Program Files for Apps, the four known-folders for
    // Photos/Videos/Documents/Downloads, \Windows on the system drive), same "Other" = used space
    // minus everything categorized. Runs on a background thread (Task.Run from the caller) since
    // recursively summing file sizes across an entire drive can take a while, same reason the
    // WinForms version runs it as a whole separate headless process.
    public static class DiskAnalysisService
    {
        public static Task<DiskAnalysisResult> AnalyzeAsync(string driveLetter) => Task.Run(() => Analyze(driveLetter));

        private static DiskAnalysisResult Analyze(string driveLetter)
        {
            var driveRoot = driveLetter.TrimEnd('\\') + "\\";
            var categories = new List<DiskCategory>();
            var usedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var gameRoots = new[]
            {
                Path.Combine(driveRoot, "Program Files (x86)", "Steam", "steamapps", "common"),
                Path.Combine(driveRoot, "Program Files", "Steam", "steamapps", "common"),
                Path.Combine(driveRoot, "SteamLibrary", "steamapps", "common"),
                Path.Combine(driveRoot, "XboxGames"),
                Path.Combine(driveRoot, "Program Files", "Epic Games"),
                Path.Combine(driveRoot, "Program Files (x86)", "Origin Games"),
                Path.Combine(driveRoot, "Program Files", "EA Games"),
                Path.Combine(driveRoot, "Program Files (x86)", "GOG Galaxy", "Games"),
                Path.Combine(driveRoot, "Program Files (x86)", "Ubisoft", "Ubisoft Game Launcher", "games"),
                Path.Combine(driveRoot, "Riot Games"),
                Path.Combine(driveRoot, "Program Files (x86)", "Battle.net"),
            }.Where(Directory.Exists).Distinct().ToList();

            var gamesSize = gameRoots.Sum(GetFolderSizeBytes);
            foreach (var r in gameRoots) usedRoots.Add(r);
            categories.Add(new DiskCategory("Games", gamesSize / 1_073_741_824.0, gameRoots));

            var appsSize = 0L;
            var appRoots = new List<string>();
            foreach (var pf in new[] { Path.Combine(driveRoot, "Program Files"), Path.Combine(driveRoot, "Program Files (x86)") })
            {
                if (!Directory.Exists(pf)) continue;
                appRoots.Add(pf);
                foreach (var sub in SafeEnumerateDirectories(pf))
                {
                    if (usedRoots.Contains(sub)) continue;
                    appsSize += GetFolderSizeBytes(sub);
                }
            }
            categories.Add(new DiskCategory("Apps", appsSize / 1_073_741_824.0, appRoots));

            var picturesPath = UserFolderOnDrive(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), driveRoot);
            var videosPath = UserFolderOnDrive(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), driveRoot);
            var docsPath = UserFolderOnDrive(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), driveRoot);
            var dlPath = UserFolderOnDrive(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), driveRoot);

            categories.Add(new DiskCategory("Photos", GetFolderSizeBytes(picturesPath) / 1_073_741_824.0, RootsOf(picturesPath)));
            categories.Add(new DiskCategory("Videos", GetFolderSizeBytes(videosPath) / 1_073_741_824.0, RootsOf(videosPath)));
            categories.Add(new DiskCategory("Documents", GetFolderSizeBytes(docsPath) / 1_073_741_824.0, RootsOf(docsPath)));
            categories.Add(new DiskCategory("Downloads", GetFolderSizeBytes(dlPath) / 1_073_741_824.0, RootsOf(dlPath)));

            var isSystemDrive = string.Equals(driveRoot.TrimEnd('\\'), Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            var winPath = isSystemDrive ? Path.Combine(driveRoot, "Windows") : null;
            var winSize = winPath != null ? GetFolderSizeBytes(winPath) : 0;
            categories.Add(new DiskCategory("Windows", winSize / 1_073_741_824.0, RootsOf(winPath)));

            var drive = new DriveInfo(driveRoot);
            var totalUsedBytes = drive.TotalSize - drive.TotalFreeSpace;
            var categorizedBytes = (long)(categories.Sum(c => c.SizeGb) * 1_073_741_824.0);
            var otherBytes = Math.Max(0, totalUsedBytes - categorizedBytes);
            categories.Add(new DiskCategory("Other", otherBytes / 1_073_741_824.0, Array.Empty<string>()));

            return new DiskAnalysisResult(Math.Round(totalUsedBytes / 1_073_741_824.0, 2), categories);
        }

        private static string? UserFolderOnDrive(string? folderPath, string driveRoot)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return null;
            var root = Path.GetPathRoot(folderPath);
            return string.Equals(root, driveRoot, StringComparison.OrdinalIgnoreCase) ? folderPath : null;
        }

        private static IReadOnlyList<string> RootsOf(string? path) =>
            path != null ? new[] { path } : Array.Empty<string>();

        // Port του Show-DiskCategoryDrilldown scan script (Optimizer.ps1 ~18397-18411): για κάθε root
        // φάκελο της κατηγορίας, αθροίζει το μέγεθος ΚΑΘΕ ΑΜΕΣΟΥ υποφακέλου (recursive άθροισμα
        // αρχείων μέσα του) - όχι το βαθύτερο δέντρο ξανά, μόνο ένα επίπεδο "πόσο χώρο πιάνει το
        // καθένα" - ταξινομημένο φθίνουσα, μέχρι 40 αποτελέσματα, ίδιο όριο με το ps1.
        public static Task<IReadOnlyList<DiskSubfolderSize>> DrilldownAsync(IReadOnlyList<string> roots) =>
            Task.Run(() => Drilldown(roots));

        private static IReadOnlyList<DiskSubfolderSize> Drilldown(IReadOnlyList<string> roots)
        {
            var results = new List<DiskSubfolderSize>();
            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var sub in SafeEnumerateDirectories(root))
                {
                    var sizeGb = GetFolderSizeBytes(sub) / 1_073_741_824.0;
                    results.Add(new DiskSubfolderSize(Path.GetFileName(sub), Math.Round(sizeGb, 2)));
                }
            }
            return results.OrderByDescending(r => r.SizeGb).Take(40).ToList();
        }

        private static long GetFolderSizeBytes(string? path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return 0;
            long sum = 0;
            foreach (var file in SafeEnumerateFiles(path))
            {
                try { sum += new FileInfo(file).Length; } catch { /* file can vanish mid-scan */ }
            }
            return sum;
        }

        // Get-ChildItem -Recurse -ErrorAction SilentlyContinue's equivalent - individual
        // access-denied subfolders (many exist under Program Files/Windows) must not abort the
        // whole scan, matching the PowerShell version's -ErrorAction SilentlyContinue.
        private static IEnumerable<string> SafeEnumerateFiles(string root)
        {
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var dir = stack.Pop();
                IEnumerable<string> files = Array.Empty<string>();
                IEnumerable<string> subDirs = Array.Empty<string>();
                try { files = Directory.EnumerateFiles(dir); } catch { }
                try { subDirs = Directory.EnumerateDirectories(dir); } catch { }
                foreach (var f in files) yield return f;
                foreach (var d in subDirs) stack.Push(d);
            }
        }

        private static IEnumerable<string> SafeEnumerateDirectories(string root)
        {
            try { return Directory.EnumerateDirectories(root); } catch { return Array.Empty<string>(); }
        }
    }
}
