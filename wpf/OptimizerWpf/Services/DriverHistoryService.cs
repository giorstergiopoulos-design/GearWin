using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OptimizerWpf.Services
{
    public sealed record DriverHistoryEntry(DateTime Time, string Title, string Source, string Result);

    // 6.9.0 - ιστορικό εγκαταστάσεων οδηγών από την εφαρμογή (τελευταίες 100): πότε, ποιος οδηγός, από ποια πηγή, αποτέλεσμα.
    public static class DriverHistoryService
    {
        public const int MaxEntries = 100;
        private static readonly object Gate = new();

        private static string DefaultPath() => Path.Combine(OncePerVersionStore.AppDataDir(), "driver-history.json");

        public static List<DriverHistoryEntry> Load(string? path = null)
        {
            try
            {
                path ??= DefaultPath();
                if (!File.Exists(path) || new FileInfo(path).Length > 512 * 1024) return new();
                return JsonSerializer.Deserialize<List<DriverHistoryEntry>>(File.ReadAllText(path)) ?? new();
            }
            catch { return new(); }
        }

        public static void Record(string title, string source, string result, string? path = null, DateTime? now = null)
        {
            lock (Gate)
            {
                try
                {
                    path ??= DefaultPath();
                    var list = Load(path);
                    list.Add(new DriverHistoryEntry(now ?? DateTime.Now, Clip(title), Clip(source), Clip(result)));
                    if (list.Count > MaxEntries) list = list.Skip(list.Count - MaxEntries).ToList();
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, JsonSerializer.Serialize(list));
                }
                catch { /* το ιστορικό δεν πρέπει ποτέ να εμποδίζει την εγκατάσταση */ }
            }
        }

        private static string Clip(string s) => s.Length > 200 ? s[..200] : s;
    }
}
