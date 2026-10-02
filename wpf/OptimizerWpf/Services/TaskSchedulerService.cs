using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // 6.1.0 - ΚΟΙΝΗ υποδομή Task Scheduler (XML) για: εκκίνηση με τα Windows, καθυστέρηση εκκίνησης
    // προγραμμάτων, εβδομαδιαία συντήρηση.
    //
    // ΓΙΑΤΙ ΟΧΙ registry Run key / schtasks CLI ορίσματα: (1) το app.manifest ζητά
    // requireAdministrator - τα Windows ΔΕΝ εκκινούν σιωπηλά στην είσοδο προγράμματα που απαιτούν
    // elevation από Run key (παραλείπονται χωρίς κανένα μήνυμα) - ΑΥΤΟ ήταν το "δεν λειτουργεί η
    // εκκίνηση με τα Windows". Μια εργασία με RunLevel=HighestAvailable ξεκινά elevated χωρίς UAC
    // prompt. (2) Οι εργασίες που φτιάχνει το `schtasks /Create` με ορίσματα έχουν ΠΡΟΕΠΙΛΟΓΗ
    // "ξεκίνα μόνο με ρεύμα" + "σταμάτα με μπαταρία" - σε laptop δεν θα έτρεχαν ποτέ στην μπαταρία.
    // Το XML ορίζει ρητά DisallowStartIfOnBatteries=false, StopIfGoingOnBatteries=false.
    public static class TaskSchedulerService
    {
        private const string Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        public static string BuildLogonTaskXml(string exePath, string arguments, int delaySeconds, string userId, string description)
        {
            var delay = delaySeconds > 0 ? $"<Delay>PT{delaySeconds}S</Delay>" : "";
            return Wrap(
                $"<LogonTrigger><Enabled>true</Enabled><UserId>{Esc(userId)}</UserId>{delay}</LogonTrigger>",
                exePath, arguments, userId, description);
        }

        // Εβδομαδιαία, Κυριακή στις 03:00 (ίδια ώρα με την προηγούμενη υλοποίηση).
        public static string BuildWeeklyTaskXml(string exePath, string arguments, string userId, string description) =>
            Wrap(
                "<CalendarTrigger><StartBoundary>2026-01-04T03:00:00</StartBoundary><Enabled>true</Enabled>" +
                "<ScheduleByWeek><DaysOfWeek><Sunday /></DaysOfWeek><WeeksInterval>1</WeeksInterval></ScheduleByWeek></CalendarTrigger>",
                exePath, arguments, userId, description);

        private static string Wrap(string trigger, string exePath, string arguments, string userId, string description) =>
            $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.4"" xmlns=""{Ns}"">
  <RegistrationInfo><Description>{Esc(description)}</Description></RegistrationInfo>
  <Triggers>{trigger}</Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{Esc(userId)}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec><Command>{Esc(exePath)}</Command>{(string.IsNullOrWhiteSpace(arguments) ? "" : $"<Arguments>{Esc(arguments)}</Arguments>")}</Exec>
  </Actions>
</Task>";

        private static string Esc(string s) => SecurityElement.Escape(s) ?? "";

        // Χωρίζει μια γραμμή εντολής εκκίνησης ("C:\a b\x.exe" -min  |  C:\x.exe /s) σε exe + ορίσματα.
        public static (string Exe, string Args) SplitCommand(string command)
        {
            command = (command ?? "").Trim();
            if (command.Length == 0) return ("", "");
            if (command[0] == '"')
            {
                var end = command.IndexOf('"', 1);
                if (end > 0) return (command[1..end], command[(end + 1)..].Trim());
            }
            var idx = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                var exeEnd = idx + 4;
                return (command[..exeEnd], command[exeEnd..].Trim());
            }
            var sp = command.IndexOf(' ');
            return sp < 0 ? (command, "") : (command[..sp], command[(sp + 1)..].Trim());
        }

        public static string CurrentUserId()
        {
            try { return WindowsIdentity.GetCurrent().Name; } catch { return Environment.UserName; }
        }

        public static async Task<bool> CreateAsync(string taskName, string xml)
        {
            var tmp = Path.Combine(Path.GetTempPath(), $"gearwin_task_{Guid.NewGuid():N}.xml");
            try
            {
                File.WriteAllText(tmp, xml, Encoding.Unicode); // UTF-16, όπως δηλώνει το XML
                return await RunAsync("/Create", "/TN", taskName, "/XML", tmp, "/F");
            }
            finally { try { File.Delete(tmp); } catch { } }
        }

        public static Task<bool> DeleteAsync(string taskName) => RunAsync("/Delete", "/TN", taskName, "/F");

        public static async Task<bool> ExistsAsync(string taskName) => await RunAsync("/Query", "/TN", taskName);

        private static async Task<bool> RunAsync(params string[] args)
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var a in args) psi.ArgumentList.Add(a);
                using var p = Process.Start(psi);
                if (p == null) return false;
                // Αδειάζουμε τα pipes ώστε να μη μπλοκάρει η διεργασία σε μεγάλη έξοδο.
                var o = p.StandardOutput.ReadToEndAsync();
                var e = p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync();
                await Task.WhenAll(o, e);
                return p.ExitCode == 0;
            }
            catch { return false; }
        }
    }
}
