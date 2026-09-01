using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public record FirewallRule(string Name, string DisplayName, string Direction, string Action, bool Enabled);

    // Port του Optimizer.ps1's Δίκτυο & Ασφάλεια καρτέλα (~13792-14193). Βλ. HANDOFF.md §0.4ιβ.
    public static class NetworkService
    {
        public static Task<string> FullResetAsync() => RunConsoleAsync(
            "netsh winsock reset & netsh int ip reset & ipconfig /release & ipconfig /renew & ipconfig /flushdns");

        public static Task<string> GetIpConfigAllAsync() => RunConsoleAsync("ipconfig /all");
        public static Task<string> GetRoutingTableAsync() => RunConsoleAsync("route print");

        public static Task<bool> RestartWifiAdaptersAsync() =>
            RunPowerShellForSuccessAsync("Restart-NetAdapter -Name '*' -Confirm:$false -ErrorAction SilentlyContinue");

        public static void OpenFirewallManager() =>
            Process.Start(new ProcessStartInfo("control.exe", "/name Microsoft.WindowsFirewall") { UseShellExecute = true });

        // ===== Κανόνες Firewall =====

        public static async Task<System.Collections.Generic.IReadOnlyList<FirewallRule>> LoadFirewallRulesAsync()
        {
            const string script = "Get-NetFirewallRule | Select-Object Name,DisplayName,Direction,Action,Enabled | ConvertTo-Json -Compress";
            var output = await RunPowerShellCaptureAsync(script);
            if (string.IsNullOrWhiteSpace(output)) return Array.Empty<FirewallRule>();
            try
            {
                var root = JsonDocument.Parse(output).RootElement;
                var elements = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : new[] { root }.AsEnumerable();
                var rules = new System.Collections.Generic.List<FirewallRule>();
                foreach (var r in elements)
                {
                    rules.Add(new FirewallRule(
                        r.GetProperty("Name").GetString() ?? "",
                        r.GetProperty("DisplayName").GetString() ?? "",
                        r.GetProperty("Direction").ToString(),
                        r.GetProperty("Action").ToString(),
                        r.GetProperty("Enabled").ToString() is "1" or "True"));
                }
                return rules;
            }
            catch (JsonException) { return Array.Empty<FirewallRule>(); }
        }

        public static Task<bool> SetFirewallRuleEnabledAsync(string ruleName, bool enabled) =>
            RunPowerShellForSuccessAsync($"Set-NetFirewallRule -Name '{ruleName.Replace("'", "''")}' -Enabled {(enabled ? "True" : "False")}");

        public static Task<bool> ExportFirewallPolicyAsync(string path) => RunProcessForSuccessAsync("netsh.exe", $"advfirewall export \"{path}\"");
        public static Task<bool> ImportFirewallPolicyAsync(string path) => RunProcessForSuccessAsync("netsh.exe", $"advfirewall import \"{path}\"");
        public static Task<bool> ResetFirewallAsync() => RunProcessForSuccessAsync("netsh.exe", "advfirewall reset");

        // ===== Hosts editor =====

        public static readonly string HostsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");

        public static string ReadHostsFile()
        {
            try { return File.ReadAllText(HostsFilePath); } catch { return ""; }
        }

        // Backup με timestamp πριν την εγγραφή, ίδιο με το ps1 original. ASCII encoding (ίδιο με ps1).
        public static bool SaveHostsFile(string content)
        {
            try
            {
                File.Copy(HostsFilePath, $"{HostsFilePath}_backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak", overwrite: true);
                File.WriteAllText(HostsFilePath, content, System.Text.Encoding.ASCII);
                return true;
            }
            catch { return false; }
        }

        public const string DefaultHostsContent =
            "# Copyright (c) 1993-2009 Microsoft Corp.\r\n#\r\n# This is a sample HOSTS file used by Microsoft TCP/IP for Windows.\r\n#\r\n" +
            "# 127.0.0.1       localhost\r\n# ::1             localhost\r\n";

        // ===== Windows Defender =====

        public static void StartQuickScan()
        {
            RunPowerShellDetached("try { Start-MpScan -ScanType QuickScan -ErrorAction Stop } catch {}");
            try { Process.Start(new ProcessStartInfo("windowsdefender://scan/?scantype=quick") { UseShellExecute = true }); } catch { }
        }

        // ===== helpers =====

        private static async Task<string> RunConsoleAsync(string command)
        {
            var psi = new ProcessStartInfo("cmd.exe", $"/c {command}") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = System.Text.Encoding.UTF8 };
            using var process = Process.Start(psi);
            if (process == null) return "";
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }

        private static async Task<string> RunPowerShellCaptureAsync(string command)
        {
            var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = System.Text.Encoding.UTF8 };
            using var process = Process.Start(psi);
            if (process == null) return "";
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }

        private static async Task<bool> RunPowerShellForSuccessAsync(string command)
        {
            var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"")
            { UseShellExecute = false, CreateNoWindow = true };
            using var process = Process.Start(psi);
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }

        private static void RunPowerShellDetached(string command)
        {
            try { Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"{command.Replace("\"", "\\\"")}\"") { UseShellExecute = false, CreateNoWindow = true }); }
            catch { }
        }

        private static async Task<bool> RunProcessForSuccessAsync(string fileName, string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
    }
}
