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
    // δημιουργείται προγραμματισμένη εργασία (Task Scheduler XML, LogonTrigger + Delay, χωρίς περιορισμούς μπαταρίας) που τρέχει την ίδια εντολή και
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

        public static IReadOnlyDictionary<string, int> Delays => AppSettingsService.Current.DelayedStartupSeconds;

        public static async Task<bool> ApplyDelayAsync(StartupItem item, int seconds)
        {
            var task = BuildTaskName(item.Name);
            var (exe, args) = TaskSchedulerService.SplitCommand(item.Command);
            if (exe.Length == 0) return false;
            var xml = TaskSchedulerService.BuildLogonTaskXml(exe, args, Math.Clamp(seconds, 5, 3600), TaskSchedulerService.CurrentUserId(),
                "GearWin - delayed startup: " + item.Name);
            if (!await TaskSchedulerService.CreateAsync(task, xml)) return false;
            // Απενεργοποίηση της αρχικής καταχώρησης ΜΟΝΟ αφού η εργασία δημιουργήθηκε (αλλιώς το πρόγραμμα χάνεται).
            if (!item.IsDisabled && !SystemService.SetStartupItemEnabled(item, false))
            {
                await TaskSchedulerService.DeleteAsync(task);
                return false;
            }
            AppSettingsService.Current.DelayedStartupSeconds[item.Name] = seconds;
            AppSettingsService.Save();
            return true;
        }

        public static async Task<bool> RemoveDelayAsync(StartupItem item)
        {
            await TaskSchedulerService.DeleteAsync(BuildTaskName(item.Name));
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
