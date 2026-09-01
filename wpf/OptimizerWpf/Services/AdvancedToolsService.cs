using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public record WindowsFeatureInfo(string FeatureName, string DisplayName, bool Enabled);
    public record GhostDeviceInfo(string InstanceId, string FriendlyName, string Class);

    // Port του Optimizer.ps1's Προηγμένα Εργαλεία καρτέλα (~15001-15621). Βλ. HANDOFF.md §0.4ιβ.
    public static class AdvancedToolsService
    {
        // ===== Συντήρηση & Διάγνωση Συστήματος (10 ενέργειες) =====

        public static void FullMaintenance() => RunPsDetached(
            "Remove-Item \\\"$env:TEMP\\*\\\" -Recurse -Force -ErrorAction SilentlyContinue; sfc /scannow; DISM /Online /Cleanup-Image /RestoreHealth");

        public static bool EnablePrivilege(string privilege) => TokenPrivilege.Enable(privilege);

        public static void NetworkReset() => RunPsDetached("netsh winsock reset; netsh int ip reset; ipconfig /flushdns");

        public static int ClearEventLogs()
        {
            var count = 0;
            try
            {
                using var session = new EventLogSession();
                foreach (var logName in session.GetLogNames().ToList())
                {
                    try { session.ClearLog(logName); count++; } catch { }
                }
            }
            catch { }
            return count;
        }

        public static void CollectGarbage() { GC.Collect(); GC.WaitForPendingFinalizers(); }

        public static string WriteDeepDiagnosticsReport()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Deep_Diagnostics.txt");
            var text = $"OS: {Environment.OSVersion}\nMachine: {Environment.MachineName}\nCPU count: {Environment.ProcessorCount}\n" +
                       $"64-bit OS: {Environment.Is64BitOperatingSystem}\n.NET: {Environment.Version}\nDate: {DateTime.Now}\n";
            File.WriteAllText(path, text);
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
            return path;
        }

        public static void OpenDeviceManager() => Process.Start(new ProcessStartInfo("devmgmt.msc") { UseShellExecute = true });
        public static void OpenWindowsUpdateTroubleshooter() => Process.Start(new ProcessStartInfo("ms-settings:troubleshoot") { UseShellExecute = true });

        public static void CreateSystemReport()
        {
            var path = Path.Combine(Path.GetTempPath(), "OptimizerWpfSystemReport.txt");
            try
            {
                using var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c systeminfo > \"{path}\"") { UseShellExecute = false, CreateNoWindow = true });
                p?.WaitForExit();
            }
            catch { }
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
        }

        public static void ResetWindowsUpdateServices() => RunPsDetached("net stop wuauserv; net stop bits; net start wuauserv; net start bits");

        public static void SetDotNetRollForward() =>
            Environment.SetEnvironmentVariable("DOTNET_ROLL_FORWARD", "LatestMajor", EnvironmentVariableTarget.Machine);

        public static int CleanXboxCredentials()
        {
            var count = 0;
            try
            {
                using var p = Process.Start(new ProcessStartInfo("cmdkey.exe", "/list") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
                var output = p?.StandardOutput.ReadToEnd() ?? "";
                p?.WaitForExit();
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(output, @"(Xbl\S+)"))
                {
                    using var del = Process.Start(new ProcessStartInfo("cmdkey.exe", $"/delete:{m.Groups[1].Value}") { UseShellExecute = false, CreateNoWindow = true });
                    del?.WaitForExit();
                    count++;
                }
            }
            catch { }
            return count;
        }

        public static void InstallGroupPolicyEditor() => RunPsDetached(
            "Get-ChildItem \\\"$env:SystemRoot\\servicing\\Packages\\\" -Filter '*GroupPolicy*' | ForEach-Object { dism /online /norestart /add-package:$($_.FullName) }");

        // ===== Εργαλειοθήκη (launchers) =====

        public static void OpenSnippingTool() => Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true });
        public static void OpenNotepad() => Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true });
        public static void OpenCalculator() => Process.Start(new ProcessStartInfo("calc.exe") { UseShellExecute = true });
        public static void OpenControlPanel() => Process.Start(new ProcessStartInfo("control.exe") { UseShellExecute = true });
        public static void OpenTaskManager() => Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
        public static void OpenRegistryEditor() => Process.Start(new ProcessStartInfo("regedit.exe") { UseShellExecute = true });

        [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, System.UIntPtr dwExtraInfo);
        private const int VK_LWIN = 0x5B, VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_B = 0x42, KEYEVENTF_KEYUP = 0x2;

        // Προσομοίωση Win+Ctrl+Shift+B - επίσημη συντόμευση Windows για GPU driver soft reset.
        public static void GpuDriverSoftReset()
        {
            keybd_event(VK_LWIN, 0, 0, System.UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, 0, System.UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, 0, System.UIntPtr.Zero);
            keybd_event(VK_B, 0, 0, System.UIntPtr.Zero);
            keybd_event(VK_B, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
        }

        // ===== Windows Optional Features =====

        public static readonly (string FeatureName, string DisplayName)[] CuratedFeatures =
        {
            ("NetFx3", ".NET Framework 3.5"),
            ("Containers-DisposableClientVM", "Windows Sandbox"),
            ("Microsoft-Hyper-V-All", "Hyper-V"),
            ("Microsoft-Windows-Subsystem-Linux", "WSL"),
            ("TelnetClient", "Telnet Client"),
            ("SMB1Protocol", "SMB 1.0/CIFS (legacy, ανασφαλές)"),
        };

        public static Task<bool> IsFeatureEnabledAsync(string featureName) => Task.Run(async () =>
        {
            var output = await RunPsCaptureAsync($"(Get-WindowsOptionalFeature -Online -FeatureName {featureName}).State");
            return output.Trim() == "Enabled";
        });

        public static Task<bool> SetFeatureEnabledAsync(string featureName, bool enabled) => RunPsForSuccessAsync(
            enabled
                ? $"Enable-WindowsOptionalFeature -Online -FeatureName {featureName} -All -NoRestart"
                : $"Disable-WindowsOptionalFeature -Online -FeatureName {featureName} -NoRestart");

        // Εξαιρεί τα 6 curated + WindowsMediaPlayer (δικιά της λογική στο Bloatware tab) - ίδιο με ps1.
        public static async Task<System.Collections.Generic.IReadOnlyList<WindowsFeatureInfo>> ScanAllFeaturesAsync()
        {
            var excluded = CuratedFeatures.Select(f => f.FeatureName).Append("MediaPlayback").Append("WindowsMediaPlayer").ToHashSet();
            const string script = "Get-WindowsOptionalFeature -Online | Select-Object FeatureName, State | ConvertTo-Json -Compress";
            var output = await RunPsCaptureAsync(script);
            if (string.IsNullOrWhiteSpace(output)) return Array.Empty<WindowsFeatureInfo>();
            try
            {
                var root = JsonDocument.Parse(output).RootElement;
                var elements = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : new[] { root }.AsEnumerable();
                return elements
                    .Select(e => e.GetProperty("FeatureName").GetString() ?? "")
                    .Where(n => n != "" && !excluded.Contains(n))
                    .Select(n => new WindowsFeatureInfo(n, n, false))
                    .ToList();
            }
            catch (JsonException) { return Array.Empty<WindowsFeatureInfo>(); }
        }

        // ===== Συσκευές-Φαντάσματα =====

        public static async Task<System.Collections.Generic.IReadOnlyList<GhostDeviceInfo>> ScanGhostDevicesAsync()
        {
            const string script = "Get-PnpDevice -PresentOnly:$false | Where-Object { -not $_.Present -and $_.FriendlyName } | " +
                                   "Select-Object InstanceId, FriendlyName, Class | ConvertTo-Json -Compress";
            var output = await RunPsCaptureAsync(script);
            if (string.IsNullOrWhiteSpace(output)) return Array.Empty<GhostDeviceInfo>();
            try
            {
                var root = JsonDocument.Parse(output).RootElement;
                var elements = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : new[] { root }.AsEnumerable();
                return elements.Select(e => new GhostDeviceInfo(
                    e.GetProperty("InstanceId").GetString() ?? "",
                    e.GetProperty("FriendlyName").GetString() ?? "",
                    e.TryGetProperty("Class", out var c) ? c.GetString() ?? "" : "")).ToList();
            }
            catch (JsonException) { return Array.Empty<GhostDeviceInfo>(); }
        }

        // ΔΙΟΡΘΩΣΗ (βλ. HANDOFF.md §0.4ιβ - ο ps1 original ΔΕΝ έχει confirm dialog εδώ, το πιο
        // επικίνδυνο, ανεπιβεβαίωτο σημείο όλης της καρτέλας) - το confirm dialog είναι στο UI layer.
        public static Task<bool> RemoveGhostDeviceAsync(string instanceId) =>
            RunPsForSuccessAsync($"Remove-PnpDevice -InstanceId '{instanceId}' -Confirm:$false -ErrorAction Stop");

        // ===== helpers =====

        private static void RunPsDetached(string command)
        {
            try { Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"{command}\"") { UseShellExecute = false, CreateNoWindow = true }); }
            catch { }
        }

        private static async Task<string> RunPsCaptureAsync(string command)
        {
            using var process = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = System.Text.Encoding.UTF8 });
            if (process == null) return "";
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }

        private static async Task<bool> RunPsForSuccessAsync(string command)
        {
            using var process = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"")
            { UseShellExecute = false, CreateNoWindow = true });
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
    }

    // Ελάχιστο P/Invoke wrapper για AdjustTokenPrivileges - port του .NET custom interop type που
    // χρησιμοποιεί το ps1 original (NativeSysInterop.TokenManipulator).
    internal static class TokenPrivilege
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES { public LUID Luid; public uint Attributes; }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES { public uint PrivilegeCount; public LUID_AND_ATTRIBUTES Privilege; }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr h, uint access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool LookupPrivilegeValue(string? host, string name, out LUID luid);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES newState, uint bufLen, IntPtr prev, IntPtr ret);

        private const uint TOKEN_ADJUST_PRIVILEGES = 0x20, TOKEN_QUERY = 0x8, SE_PRIVILEGE_ENABLED = 0x2;

        public static bool Enable(string privilege)
        {
            try
            {
                if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var token)) return false;
                if (!LookupPrivilegeValue(null, privilege, out var luid)) return false;
                var tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Privilege = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED } };
                return AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            catch { return false; }
        }
    }
}
