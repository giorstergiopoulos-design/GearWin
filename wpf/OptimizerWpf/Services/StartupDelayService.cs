using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // ΝΕΟ (6.1.0) - καθυστέρηση εκκίνησης προγραμμάτων: αντί να ξεκινούν όλα μαζί με την είσοδο στα
    // Windows, ένα πρόγραμμα της λίστας εκκίνησης μπορεί να ξεκινά N δευτερόλεπτα αργότερα. Τεχνική:
    // δημιουργείται προγραμματισμένη εργασία (schtasks, ONLOGON + /DELAY) που τρέχει την ίδια εντολή και
    // η αρχική καταχώρηση εκκίνησης απενεργοποιείται (SystemService.SetStartupItemEnabled - αναστρέψιμο).
    // Η "Αφαίρεση" σβήνει την εργασία και ξαναενεργοποιεί την αρχική καταχώρηση.
    public static class StartupDelayService
    {
        public const string TaskFolder = "GearWinDelayed";

        public static string FormatDelay(int seconds)
        {
            seconds = Math.Clamp(seconds, 5, 599 * 60 + 59);
            return $"{seconds / 60:0000}:{seconds % 60:00}";
        }

        public static string BuildTaskName(string startupItemName)
        {
            var safe = Regex.Replace(startupItemName, @"[^\w\-\. ]", "_").Trim();
            if (safe.Length == 0) safe = "item";
            return $@"{TaskFolder}\{safe}";
        }

        public static IReadOnlyList<string> BuildCreateArgs(string taskName, string command, int seconds) => new[]
        {
            "/Create", "/TN", taskName, "/TR", command, "/SC", "ONLOGON", "/DELAY", FormatDelay(seconds), "/RL", "HIGHEST", "/F",
        };

        public static IReadOnlyList<string> BuildDeleteArgs(string taskName) => new[] { "/Delete", "/TN", taskName, "/F" };

        private static async Task<bool> RunSchtasksAsync(IReadOnlyList<string> args)
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var a in args) psi.ArgumentList.Add(a);
                using var p = Process.Start(psi);
                if (p == null) return false;
                await p.WaitForExitAsync();
                return p.ExitCode == 0;
            }
            catch { return false; }
        }

        public static IReadOnlyDictionary<string, int> Delays => AppSettingsService.Current.DelayedStartupSeconds;

        public static async Task<bool> ApplyDelayAsync(StartupItem item, int seconds)
        {
            var task = BuildTaskName(item.Name);
            if (!await RunSchtasksAsync(BuildCreateArgs(task, item.Command, seconds))) return false;
            // Απενεργοποίηση της αρχικής καταχώρησης ΜΟΝΟ αφού η εργασία δημιουργήθηκε (αλλιώς το πρόγραμμα χάνεται).
            if (!item.IsDisabled && !SystemService.SetStartupItemEnabled(item, false))
            {
                await RunSchtasksAsync(BuildDeleteArgs(task));
                return false;
            }
            AppSettingsService.Current.DelayedStartupSeconds[item.Name] = seconds;
            AppSettingsService.Save();
            return true;
        }

        public static async Task<bool> RemoveDelayAsync(StartupItem item)
        {
            await RunSchtasksAsync(BuildDeleteArgs(BuildTaskName(item.Name)));
            var ok = SystemService.SetStartupItemEnabled(item, true);
            if (ok)
            {
                AppSettingsService.Current.DelayedStartupSeconds.Remove(item.Name);
                AppSettingsService.Save();
            }
            return ok;
        }
    }
}
