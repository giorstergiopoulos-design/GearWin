using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public record FirewallRule(string Name, string DisplayName, string Direction, string Action, bool Enabled);
    public record ListeningPort(int Port, string ProcessName, int Pid);

    // Port του Optimizer.ps1's Δίκτυο & Ασφάλεια καρτέλα (~13792-14193). Βλ. HANDOFF.md §0.4ιβ.
    public static class NetworkService
    {
        // ===== Κατάσταση Σύνδεσης (v3.2.0, Αρχική) =====
        // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε: "δείκτης κατάστασης δικτύου στην Αρχική") - το
        // NetworkInterface.GetIsNetworkAvailable() ελέγχει μόνο αν υπάρχει ΤΟΠΙΚΗ σύνδεση (καλώδιο/
        // Wi-Fi συνδεδεμένο), όχι αν υπάρχει πραγματικά Internet (π.χ. router συνδεδεμένο αλλά χωρίς
        // WAN) - ping σε γνωστό, σταθερό host (1.1.1.1 - Cloudflare DNS, καμία εξάρτηση από DNS
        // resolution του ίδιου του ping) με ΣΥΝΤΟΜΟ timeout (1.5s) είναι ο μόνος αξιόπιστος τρόπος.
        public static async Task<(bool Connected, int? LatencyMs)> CheckInternetAsync()
        {
            try
            {
                if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return (false, null);
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync("1.1.1.1", 1500);
                return reply.Status == System.Net.NetworkInformation.IPStatus.Success
                    ? (true, (int)reply.RoundtripTime)
                    : (false, null);
            }
            catch { return (false, null); }
        }

        // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε: "βάλε ένα graph δίπλα στο δίκτυο, όπως στη Διαχείριση Εργασιών") -
        // ΜΟΝΟ τοπική ανάγνωση των ήδη υπαρχόντων μετρητών του λειτουργικού (GetIPv4Statistics) -
        // ΚΑΝΕΝΑ δικτυακό αίτημα, ΚΑΜΙΑ σχέση με το ξεχωριστό CheckInternetAsync (ping) παραπάνω. Γι'
        // αυτό είναι ασφαλές να διαβάζεται κάθε 1s (ίδιο ρυθμό με το CPU/RAM tile) αντί για το αραιό
        // 20s timer - ένα graph χρειάζεται συχνή δειγματοληψία για να δείχνει σαν "ζωντανό".
        public static long GetTotalNetworkBytes()
        {
            long total = 0;
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    var stats = nic.GetIPv4Statistics();
                    total += stats.BytesReceived + stats.BytesSent;
                }
                catch { }
            }
            return total;
        }

        public static Task<string> FullResetAsync() => RunConsoleAsync(
            "netsh winsock reset & netsh int ip reset & ipconfig /release & ipconfig /renew & ipconfig /flushdns");

        public static Task<string> GetIpConfigAllAsync() => RunConsoleAsync("ipconfig /all");
        public static Task<string> GetRoutingTableAsync() => RunConsoleAsync("route print");

        public static Task<bool> RestartWifiAdaptersAsync() =>
            RunPowerShellForSuccessAsync("Restart-NetAdapter -Name '*' -Confirm:$false -ErrorAction SilentlyContinue");

        // ΝΕΟ - roadmap "Γρήγορη εναλλαγή DNS" - Set-DnsClientServerAddress εφαρμόζεται σε ΚΑΘΕ
        // ενεργό προσαρμογέα (Status -eq 'Up'), ίδια λογική με το ήδη υπάρχον RestartWifiAdaptersAsync
        // παραπάνω - χωρίς να χρειάζεται να μαντέψει η εφαρμογή ΠΟΙΟΣ είναι ο "κύριος" προσαρμογέας.
        public static Task<bool> SetDnsAsync(string primary, string? secondary = null)
        {
            var servers = secondary is null ? $"'{primary}'" : $"'{primary}','{secondary}'";
            return RunPowerShellForSuccessAsync(
                $"Get-NetAdapter | Where-Object Status -eq 'Up' | Set-DnsClientServerAddress -ServerAddresses {servers}");
        }

        public static Task<bool> ResetDnsAsync() =>
            RunPowerShellForSuccessAsync("Get-NetAdapter | Where-Object Status -eq 'Up' | Set-DnsClientServerAddress -ResetServerAddresses");

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

        // ΝΕΟ - roadmap "εντοπισμός ενεργού DoH" (τεχνική βελτίωση από έρευνα ανταγωνιστικών εργαλείων) -
        // το DNS-over-HTTPS παρακάμπτει ΕΝΤΕΛΩΣ το hosts file (βλ. Net_TelemetryBlockBtn/HostsEditor
        // παραπάνω) - αν είναι ενεργό για τον DNS server που χρησιμοποιεί ο χρήστης, ο αποκλεισμός
        // τηλεμετρίας μέσω hosts δεν κάνει ουσιαστικά τίποτα, χωρίς καμία ένδειξη γι' αυτό μέχρι τώρα.
        // Get-DnsClientDohServerAddress (Windows 11, ενσωματωμένο) - AutoUpgrade=True σημαίνει ενεργό DoH.
        public static async Task<bool> IsDohEnabledAsync()
        {
            const string script = "(Get-DnsClientDohServerAddress -ErrorAction SilentlyContinue | Where-Object { $_.AutoUpgrade -eq $true -or $_.DohFlags -ne 0 } | Measure-Object).Count";
            var output = await RunPowerShellCaptureAsync(script);
            return int.TryParse(output?.Trim(), out var count) && count > 0;
        }

        public static Task<bool> SetFirewallRuleEnabledAsync(string ruleName, bool enabled) =>
            RunPowerShellForSuccessAsync($"Set-NetFirewallRule -Name '{ruleName.Replace("'", "''")}' -Enabled {(enabled ? "True" : "False")}");

        public static Task<bool> ExportFirewallPolicyAsync(string path) => RunProcessForSuccessAsync("netsh.exe", $"advfirewall export \"{path}\"");
        public static Task<bool> ImportFirewallPolicyAsync(string path) => RunProcessForSuccessAsync("netsh.exe", $"advfirewall import \"{path}\"");
        public static Task<bool> ResetFirewallAsync() => RunProcessForSuccessAsync("netsh.exe", "advfirewall reset");

        // ΝΕΟ - roadmap "Έλεγχος τείχους προστασίας/ανοιχτών θυρών": οι κανόνες τείχους προστασίας
        // (παραπάνω) υπήρχαν ήδη, αλλά όχι το "ποιες θύρες είναι πραγματικά ανοιχτές/σε ακρόαση αυτή
        // τη στιγμή" - καθαρά ενημερωτικό, καμία ενέργεια/αλλαγή, ίδιο μοτίβο parsing με
        // LoadFirewallRulesAsync (Get-NetTCPConnection είναι το σύγχρονο PowerShell ισοδύναμο του
        // netstat -ano, με δομημένη JSON έξοδο αντί για parsing κειμένου σταθερού πλάτους).
        public static async Task<System.Collections.Generic.IReadOnlyList<ListeningPort>> LoadListeningPortsAsync()
        {
            const string script = "Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | " +
                "Select-Object LocalPort,OwningProcess,@{N='ProcessName';E={(Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).ProcessName}} | " +
                "Sort-Object LocalPort -Unique | ConvertTo-Json -Compress";
            var output = await RunPowerShellCaptureAsync(script);
            if (string.IsNullOrWhiteSpace(output)) return Array.Empty<ListeningPort>();
            try
            {
                var root = JsonDocument.Parse(output).RootElement;
                var elements = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : new[] { root }.AsEnumerable();
                var ports = new System.Collections.Generic.List<ListeningPort>();
                foreach (var p in elements)
                {
                    var pid = p.GetProperty("OwningProcess").GetInt32();
                    var name = p.TryGetProperty("ProcessName", out var n) ? n.GetString() ?? "" : "";
                    ports.Add(new ListeningPort(p.GetProperty("LocalPort").GetInt32(), string.IsNullOrEmpty(name) ? "?" : name, pid));
                }
                return ports;
            }
            catch (JsonException) { return Array.Empty<ListeningPort>(); }
        }

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

        // ===== Αποκλεισμός Τηλεμετρίας (v3.2.0, ρητό αίτημα χρήστη μετά από web research: "one-click
        // preset μπλοκαρίσματος τηλεμετρίας", ίδιο πνεύμα με WindowsSpyBlocker/O&O ShutUp10) - curated
        // λίστα γνωστών endpoints τηλεμετρίας/διαγνωστικών της Microsoft, μπλοκαρισμένα μέσω hosts file
        // (ίδιος μηχανισμός με τον ήδη υπάρχοντα Επεξεργαστή Αρχείου Hosts, όχι νέα υποδομή). Το μπλοκ
        // οριοθετείται με ΣΑΦΗ markers ώστε να μπορεί να αφαιρεθεί καθαρά (toggle), χωρίς να αγγίξει
        // τυχόν δικές του καταχωρήσεις του χρήστη γύρω του.
        private const string TelemetryBlockStart = "# --- OptimizerWpf: Αποκλεισμός Τηλεμετρίας Microsoft (BEGIN) ---";
        private const string TelemetryBlockEnd = "# --- OptimizerWpf: Αποκλεισμός Τηλεμετρίας Microsoft (END) ---";

        public static readonly string[] TelemetryDomains =
        {
            "vortex.data.microsoft.com", "vortex-win.data.microsoft.com", "telecommand.telemetry.microsoft.com",
            "telecommand.telemetry.microsoft.com.nsatc.net", "oca.telemetry.microsoft.com", "oca.telemetry.microsoft.com.nsatc.net",
            "sqm.telemetry.microsoft.com", "sqm.telemetry.microsoft.com.nsatc.net", "watson.telemetry.microsoft.com",
            "watson.telemetry.microsoft.com.nsatc.net", "redir.metaservices.microsoft.com", "choice.microsoft.com",
            "choice.microsoft.com.nsatc.net", "df.telemetry.microsoft.com", "reports.wes.df.telemetry.microsoft.com",
            "wes.df.telemetry.microsoft.com", "services.wes.df.telemetry.microsoft.com", "sqm.df.telemetry.microsoft.com",
            "telemetry.microsoft.com", "watson.ppe.telemetry.microsoft.com", "telemetry.appex.bing.net",
            "telemetry.urs.microsoft.com", "settings-sandbox.data.microsoft.com", "vortex-sandbox.data.microsoft.com",
            "survey.watson.microsoft.com", "watson.live.com", "watson.microsoft.com", "statsfe2.ws.microsoft.com",
            "corpext.msitadfs.glbdns2.microsoft.com", "compatexchange.cloudapp.net", "cs1.wpc.v0cdn.net",
            "a-0001.a-msedge.net", "statsfe2.update.microsoft.com.akadns.net", "sls.update.microsoft.com.akadns.net",
            "fe2.update.microsoft.com.akadns.net", "diagnostics.support.microsoft.com", "settings-win.data.microsoft.com",
            "adnexus.net", "adnxs.com", "az361816.vo.msecnd.net", "az512334.vo.msecnd.net",
        };

        // Επιστρέφει true αν το μπλοκ βρίσκεται ήδη στο hosts (για να δείχνει το UI σωστά την
        // τρέχουσα κατάσταση - toggle, όχι μόνο "εφάρμοσε").
        public static bool IsTelemetryBlocked() => ReadHostsFile().Contains(TelemetryBlockStart);

        public static bool BlockTelemetry()
        {
            var current = ReadHostsFile();
            if (current.Contains(TelemetryBlockStart)) return true; // ήδη εφαρμοσμένο
            var block = "\r\n" + TelemetryBlockStart + "\r\n" +
                        string.Join("\r\n", Array.ConvertAll(TelemetryDomains, d => $"0.0.0.0 {d}")) +
                        "\r\n" + TelemetryBlockEnd + "\r\n";
            return SaveHostsFile(current.TrimEnd() + "\r\n" + block);
        }

        public static bool UnblockTelemetry()
        {
            var current = ReadHostsFile();
            var startIdx = current.IndexOf(TelemetryBlockStart, StringComparison.Ordinal);
            if (startIdx < 0) return true; // δεν υπήρχε καν
            var endIdx = current.IndexOf(TelemetryBlockEnd, StringComparison.Ordinal);
            if (endIdx < 0) return false;
            endIdx += TelemetryBlockEnd.Length;
            var updated = current.Remove(startIdx, endIdx - startIdx);
            return SaveHostsFile(updated);
        }

        // ===== Windows Defender =====

        public static void StartQuickScan()
        {
            RunPowerShellDetached("try { Start-MpScan -ScanType QuickScan -ErrorAction Stop } catch {}");
            try { Process.Start(new ProcessStartInfo("windowsdefender://scan/?scantype=quick") { UseShellExecute = true }); } catch { }
        }

        // ΝΕΟ - roadmap "Ανίχνευση δημόσιου Wi-Fi" - Get-NetConnectionProfile.NetworkCategory είναι η
        // ίδια κατηγοριοποίηση που δείχνουν οι Ρυθμίσεις Windows (Δημόσιο/Ιδιωτικό/Domain) - καμία
        // δική μας ανίχνευση, απλή ανάγνωση της ήδη υπάρχουσας απόφασης των Windows.
        public static async Task<bool> IsOnPublicNetworkAsync()
        {
            var output = await RunPowerShellCaptureAsync(
                "(Get-NetConnectionProfile | Where-Object { $_.NetworkCategory -eq 'Public' }).Count");
            return int.TryParse(output.Trim(), out var count) && count > 0;
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
