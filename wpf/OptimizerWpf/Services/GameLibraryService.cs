using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    // Launcher = "Steam" | "Epic" | "GOG" | "Ubisoft" | "Xbox". LaunchTarget: URI (steam://, epic) ή διαδρομή .exe.
    public record GameEntry(string Launcher, string Id, string Name, string InstallDir, long SizeBytes,
        DateTime? LastPlayed, bool UpdatePending, string? LaunchTarget, string? UninstallTarget, string? VerifyTarget);

    // 6.1.0 - βιβλιοθήκη παιχνιδιών: εντοπίζει τοπικά εγκατεστημένα παιχνίδια από Steam, Epic Games, GOG, Ubisoft
    // Connect και Xbox/Game Pass (φάκελος XboxGames), σε μία λίστα - ΜΟΝΟ ανάγνωση τοπικών αρχείων/μητρώου,
    // καμία σύνδεση σε λογαριασμό/δίκτυο. Ό,τι ξέρει ο ίδιος ο launcher (π.χ. εκκρεμής ενημέρωση Steam) το
    // διαβάζουμε από τα δικά του manifests· ενημέρωση/εγκατάσταση γίνεται από τον launcher (ανοίγουμε το
    // steam:// URI) - δεν παριστάνουμε ότι κατεβάζουμε οι ίδιοι.
    public static class GameLibraryService
    {
        // ── Steam ────────────────────────────────────────────────────────────────────────────

        // Ελάχιστος parser για το Valve KeyValues (VDF): "key" "value" και "key" { ... } εμφωλευμένα.
        public static Dictionary<string, object> ParseVdf(string text)
        {
            var i = 0;
            return ParseBlock(text, ref i, topLevel: true);
        }

        private static Dictionary<string, object> ParseBlock(string s, ref int i, bool topLevel)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) return result;
                if (s[i] == '}') { i++; if (!topLevel) return result; continue; }
                if (s[i] != '"') { i++; continue; }
                var key = ReadQuoted(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == '{') { i++; result[key] = ParseBlock(s, ref i, false); }
                else if (i < s.Length && s[i] == '"') result[key] = ReadQuoted(s, ref i);
            }
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length)
            {
                if (char.IsWhiteSpace(s[i])) i++;
                else if (s[i] == '/' && i + 1 < s.Length && s[i + 1] == '/') { while (i < s.Length && s[i] != '\n') i++; }
                else break;
            }
        }

        private static string ReadQuoted(string s, ref int i)
        {
            i++; // αρχικό "
            var sb = new System.Text.StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length) { i++; sb.Append(s[i] == 'n' ? '\n' : s[i] == 't' ? '\t' : s[i]); }
                else sb.Append(s[i]);
                i++;
            }
            i++; // τελικό "
            return sb.ToString();
        }

        private static string? Str(Dictionary<string, object> d, string key) => d.TryGetValue(key, out var v) ? v as string : null;

        // Φάκελοι βιβλιοθήκης Steam από το libraryfolders.vdf (η ίδια η εγκατάσταση + πρόσθετοι δίσκοι).
        public static IReadOnlyList<string> ParseSteamLibraryPaths(string libraryFoldersVdf)
        {
            var paths = new List<string>();
            var root = ParseVdf(libraryFoldersVdf);
            if (root.Values.FirstOrDefault() is not Dictionary<string, object> folders) return paths;
            foreach (var entry in folders.Values.OfType<Dictionary<string, object>>())
                if (Str(entry, "path") is { Length: > 0 } p) paths.Add(p.Replace(@"\\", @"\"));
            return paths;
        }

        public static GameEntry? ParseSteamManifest(string acfText, string libraryPath)
        {
            var root = ParseVdf(acfText);
            if (root.Values.FirstOrDefault() is not Dictionary<string, object> app) return null;
            var id = Str(app, "appid");
            var name = Str(app, "name");
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name)) return null;
            // Τα "Steamworks Common Redistributables" κ.λπ. δεν είναι παιχνίδια.
            if (name.StartsWith("Steamworks Common", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Steam Linux Runtime", StringComparison.OrdinalIgnoreCase)) return null;

            long.TryParse(Str(app, "SizeOnDisk"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size);
            long.TryParse(Str(app, "LastPlayed"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lastPlayedUnix);
            long.TryParse(Str(app, "BytesToDownload"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytesToDownload);
            int.TryParse(Str(app, "StateFlags"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var flags);

            // StateFlags: 4 = πλήρως εγκατεστημένο· bit 2 (0x2) = απαιτείται ενημέρωση· bit 0x100 = ενημέρωση σε εξέλιξη.
            var updatePending = (flags & 0x2) != 0 || (flags & 0x100) != 0 || (flags != 4 && bytesToDownload > 0);
            var installDir = Path.Combine(libraryPath, "steamapps", "common", Str(app, "installdir") ?? name);
            DateTime? lastPlayed = lastPlayedUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(lastPlayedUnix).LocalDateTime : null;
            return new GameEntry("Steam", id, name, installDir, size, lastPlayed, updatePending,
                $"steam://rungameid/{id}", $"steam://uninstall/{id}", $"steam://validate/{id}");
        }

        private static string? SteamRoot()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (k?.GetValue("SteamPath") is string p && Directory.Exists(p)) return p.Replace('/', '\\');
            }
            catch { }
            foreach (var cand in new[] { @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam" })
                if (Directory.Exists(cand)) return cand;
            return null;
        }

        private static IEnumerable<GameEntry> ScanSteam()
        {
            var root = SteamRoot();
            if (root == null) yield break;
            var libraries = new List<string> { root };
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            try { if (File.Exists(vdf)) libraries.AddRange(ParseSteamLibraryPaths(File.ReadAllText(vdf))); } catch { }
            foreach (var lib in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var apps = Path.Combine(lib, "steamapps");
                if (!Directory.Exists(apps)) continue;
                string[] files;
                try { files = Directory.GetFiles(apps, "appmanifest_*.acf"); } catch { continue; }
                foreach (var f in files)
                {
                    GameEntry? g = null;
                    try { g = ParseSteamManifest(File.ReadAllText(f), lib); } catch { }
                    if (g != null) yield return g;
                }
            }
        }

        // ── Epic Games ───────────────────────────────────────────────────────────────────────

        public static GameEntry? ParseEpicManifest(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                string? S(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                var name = S("DisplayName");
                var appName = S("AppName");
                var location = S("InstallLocation");
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(appName)) return null;
                // Τα DLC/εργαλεία (π.χ. Unreal) έχουν bIsIncompleteInstall ή δεν έχουν δική τους εκτελέσιμη εκκίνηση.
                if (r.TryGetProperty("bIsIncompleteInstall", out var inc) && inc.ValueKind == JsonValueKind.True) return null;
                long size = r.TryGetProperty("InstallSize", out var sz) && sz.TryGetInt64(out var n) ? n : 0;
                var ns = S("CatalogNamespace");
                var item = S("CatalogItemId");
                var uri = ns != null && item != null
                    ? $"com.epicgames.launcher://apps/{Uri.EscapeDataString(ns)}%3A{Uri.EscapeDataString(item)}%3A{Uri.EscapeDataString(appName)}?action=launch&silent=true"
                    : null;
                return new GameEntry("Epic", appName, name, location ?? "", size, null, false, uri, null, null);
            }
            catch { return null; }
        }

        private static IEnumerable<GameEntry> ScanEpic()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (!Directory.Exists(dir)) yield break;
            string[] files;
            try { files = Directory.GetFiles(dir, "*.item"); } catch { yield break; }
            foreach (var f in files)
            {
                GameEntry? g = null;
                try { g = ParseEpicManifest(File.ReadAllText(f)); } catch { }
                if (g != null) yield return g;
            }
        }

        // ── GOG / Ubisoft / Xbox ─────────────────────────────────────────────────────────────

        private static IEnumerable<GameEntry> ScanGog()
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games");
            if (key == null) yield break;
            foreach (var sub in key.GetSubKeyNames())
            {
                GameEntry? g = null;
                try
                {
                    using var k = key.OpenSubKey(sub);
                    var name = k?.GetValue("gameName") as string;
                    var path = k?.GetValue("path") as string;
                    var exe = k?.GetValue("exe") as string;
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(path))
                        g = new GameEntry("GOG", sub, name, path, 0, null, false, exe, null, null);
                }
                catch { }
                if (g != null) yield return g;
            }
        }

        private static IEnumerable<GameEntry> ScanUbisoft()
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs");
            if (key == null) yield break;
            foreach (var sub in key.GetSubKeyNames())
            {
                GameEntry? g = null;
                try
                {
                    using var k = key.OpenSubKey(sub);
                    var dir = (k?.GetValue("InstallDir") as string)?.Replace('/', '\\').TrimEnd('\\');
                    if (!string.IsNullOrEmpty(dir))
                        g = new GameEntry("Ubisoft", sub, Path.GetFileName(dir), dir, 0, null, false, $"uplay://launch/{sub}/0", null, null);
                }
                catch { }
                if (g != null) yield return g;
            }
        }

        // Game Pass: κάθε υποφάκελος του X:\XboxGames\<Παιχνίδι> (η προεπιλεγμένη τοποθεσία εγκατάστασης).
        private static IEnumerable<GameEntry> ScanXbox()
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                var root = Path.Combine(drive.Name, "XboxGames");
                if (!Directory.Exists(root)) continue;
                string[] dirs;
                try { dirs = Directory.GetDirectories(root); } catch { continue; }
                foreach (var d in dirs)
                {
                    var name = Path.GetFileName(d);
                    if (name.Equals("GameSave", StringComparison.OrdinalIgnoreCase) || name.StartsWith("$", StringComparison.Ordinal)) continue;
                    yield return new GameEntry("Xbox", name, name, d, 0, null, false, null, null, null);
                }
            }
        }

        // ── Σύνολο ──────────────────────────────────────────────────────────────────────────

        public static Task<IReadOnlyList<GameEntry>> ScanAllAsync() => Task.Run(() =>
        {
            var all = new List<GameEntry>();
            foreach (var scan in new Func<IEnumerable<GameEntry>>[] { ScanSteam, ScanEpic, ScanGog, ScanUbisoft, ScanXbox })
            {
                try { all.AddRange(scan()); } catch { /* ένας launcher που αποτυγχάνει δεν πρέπει να κρύψει τους άλλους */ }
            }
            return (IReadOnlyList<GameEntry>)all.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        });

        // Μέγεθος φακέλου για launchers που δεν το δίνουν στο manifest (GOG/Ubisoft/Xbox) - υπολογίζεται ΜΟΝΟ όταν
        // το ζητήσει ο χρήστης (μπορεί να είναι αργό σε μεγάλα παιχνίδια).
        public static long FolderSize(string dir, System.Threading.CancellationToken ct = default)
        {
            long sum = 0;
            if (!Directory.Exists(dir)) return 0;
            var stack = new Stack<string>();
            stack.Push(dir);
            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var d = stack.Pop();
                try { foreach (var f in Directory.EnumerateFiles(d)) { try { sum += new FileInfo(f).Length; } catch { } } } catch { }
                try
                {
                    foreach (var sub in Directory.EnumerateDirectories(d))
                    {
                        try { if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0) continue; } catch { continue; }
                        stack.Push(sub);
                    }
                }
                catch { }
            }
            return sum;
        }
    }
}
