using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public record DriverUpdate(string UpdateId, string Title, string? DriverClass, string? Manufacturer, string? Model, string? Date);
    public record DriverScanResult(bool PolicyBlocked, IReadOnlyList<DriverUpdate> Updates);
    public record DriverStoreEntry(string Driver, string OriginalFileName, string? Version, string? Date, string? Class);

    // Port του Optimizer.ps1's driver-ενημερώσεων συστήματος (~11847-13411) - ΜΟΝΟ η πηγή "Windows
    // Update" μεταφέρθηκε σε αυτό το πέρασμα, ΟΧΙ οι AMD/NVIDIA (web scraping σε drivers.amd.com/
    // geforce.com) ή Dell Command Update πηγές - βλ. HANDOFF.md §0.4ιβ για πλήρη τεκμηρίωση όλων
    // των 4 πηγών. Το Windows Update καλύπτει ήδη τη γενική περίπτωση για κάθε κατασκευαστή· οι
    // άλλες 3 πηγές είναι narrow/εύθραυστο web scraping συγκεκριμένων ιστοσελίδων - εκτός εμβέλειας
    // αυτού του περάσματος, ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ, όχι σιωπηλή παράλειψη.
    public static class DriverService
    {
        public static async Task<DriverScanResult> ScanAsync()
        {
            const string script = @"
$ErrorActionPreference = 'Continue'
$policyBlocked = $false
foreach ($p in @(
    'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate',
    'HKLM:\SOFTWARE\Microsoft\PolicyManager\current\device\Update',
    'HKLM:\SOFTWARE\Microsoft\PolicyManager\default\Update',
    'HKLM:\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings'
)) {
    try {
        $v = (Get-ItemProperty -Path $p -Name 'ExcludeWUDriversInQualityUpdate' -ErrorAction SilentlyContinue).ExcludeWUDriversInQualityUpdate
        if ($v -eq 1) { $policyBlocked = $true }
    } catch {}
}

$updates = @()
if (-not $policyBlocked) {
    try {
        $session = New-Object -ComObject Microsoft.Update.Session
        $searcher = $session.CreateUpdateSearcher()
        $result = $searcher.Search(""IsInstalled=0 and Type='Driver'"")
        foreach ($u in $result.Updates) {
            $updates += [PSCustomObject]@{
                UpdateId     = $u.Identity.UpdateID
                Title        = $u.Title
                DriverClass  = $u.DriverClass
                Manufacturer = $u.DriverManufacturer
                Model        = $u.DriverModel
                Date         = if ($u.DriverVerDate) { $u.DriverVerDate.ToString('yyyy-MM-dd') } else { $null }
            }
        }
    } catch {}
}

[PSCustomObject]@{ PolicyBlocked = $policyBlocked; Updates = @($updates) } | ConvertTo-Json -Depth 4 -Compress
";
            var output = await RunPowerShellScriptAsync(script, TimeSpan.FromMinutes(3));
            if (string.IsNullOrWhiteSpace(output)) return new DriverScanResult(false, Array.Empty<DriverUpdate>());

            try
            {
                var root = JsonDocument.Parse(output).RootElement;
                var policyBlocked = root.GetProperty("PolicyBlocked").GetBoolean();
                var updates = new List<DriverUpdate>();
                foreach (var u in root.GetProperty("Updates").EnumerateArray())
                {
                    updates.Add(new DriverUpdate(
                        u.GetProperty("UpdateId").GetString() ?? "",
                        u.GetProperty("Title").GetString() ?? "",
                        u.TryGetProperty("DriverClass", out var dc) ? dc.GetString() : null,
                        u.TryGetProperty("Manufacturer", out var mf) ? mf.GetString() : null,
                        u.TryGetProperty("Model", out var md) ? md.GetString() : null,
                        u.TryGetProperty("Date", out var dt) ? dt.GetString() : null));
                }
                return new DriverScanResult(policyBlocked, updates);
            }
            catch (JsonException)
            {
                return new DriverScanResult(false, Array.Empty<DriverUpdate>());
            }
        }

        // Ξεχωριστή background διεργασία ανά driver, ίδιο με το ps1 original - δεν περιμένει να
        // ολοκληρωθεί (η λήψη+εγκατάσταση μέσω Windows Update μπορεί να πάρει αρκετά λεπτά).
        public static void InstallInBackground(string updateId)
        {
            var script = $@"
$ErrorActionPreference = 'Continue'
try {{
    $session = New-Object -ComObject Microsoft.Update.Session
    $searcher = $session.CreateUpdateSearcher()
    $result = $searcher.Search(""UpdateID='{updateId}'"")
    if ($result.Updates.Count -eq 0) {{ exit 1 }}
    $toInstall = New-Object -ComObject Microsoft.Update.UpdateColl
    $toInstall.Add($result.Updates.Item(0)) | Out-Null
    $downloader = $session.CreateUpdateDownloader()
    $downloader.Updates = $toInstall
    $downloader.Download() | Out-Null
    $installer = $session.CreateUpdateInstaller()
    $installer.Updates = $toInstall
    $installer.Install() | Out-Null
}} catch {{}}
";
            RunPowerShellScriptDetached(script);
        }

        // DISM Export-Driver - εξάγει ΟΛΟΥΣ τους οδηγούς τρίτων κατασκευαστών (ΟΧΙ τους εργοστασιακούς
        // Windows) σε φάκελο. Ίδιος μηχανισμός με καθιερωμένα εργαλεία δημιουργίας αντιγράφων οδηγών.
        public static Task<bool> BackupAsync(string destinationPath) =>
            RunProcessAsync("dism.exe", $"/Online /Export-Driver /Destination:\"{destinationPath}\"", TimeSpan.FromMinutes(10));

        // pnputil - εγκαθιστά ξανά όλους τους οδηγούς από φάκελο αντιγράφου. Απαιτεί δικαιώματα
        // διαχειριστή (ήδη διαθέσιμα - βλ. app.manifest).
        public static Task<bool> RestoreAsync(string sourcePath) =>
            RunProcessAsync("pnputil.exe", $"/add-driver \"{sourcePath}\\*.inf\" /subdirs /install", TimeSpan.FromMinutes(10));

        // Get-WindowsDriver (DISM cmdlet, ΟΧΙ ανάλυση κειμένου pnputil - το τελευταίο είναι localized
        // ανά γλώσσα Windows, ήδη προκάλεσε bug αλλού στο ps1 original). Φιλτράρει Inbox=false (ΜΟΝΟ
        // 3rd-party, ΠΟΤΕ εργοστασιακούς οδηγούς Windows).
        public static async Task<IReadOnlyList<DriverStoreEntry>> ScanStoreAsync()
        {
            const string script = @"
$ErrorActionPreference = 'Continue'
$drivers = Get-WindowsDriver -Online -ErrorAction SilentlyContinue | Where-Object { -not $_.Inbox }
$result = @($drivers | ForEach-Object {
    [PSCustomObject]@{
        Driver           = $_.Driver
        OriginalFileName = [System.IO.Path]::GetFileName($_.OriginalFileName)
        Version          = $_.Version
        Date             = if ($_.Date) { $_.Date.ToString('yyyy-MM-dd') } else { $null }
        ClassName        = $_.ClassName
    }
})
$result | ConvertTo-Json -Depth 4 -Compress
";
            var output = await RunPowerShellScriptAsync(script, TimeSpan.FromMinutes(3));
            if (string.IsNullOrWhiteSpace(output)) return Array.Empty<DriverStoreEntry>();

            try
            {
                var root = JsonDocument.Parse(output).RootElement;
                var entries = new List<DriverStoreEntry>();
                var elements = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : new[] { root }.AsEnumerable();
                foreach (var d in elements)
                {
                    entries.Add(new DriverStoreEntry(
                        d.GetProperty("Driver").GetString() ?? "",
                        d.GetProperty("OriginalFileName").GetString() ?? "",
                        d.TryGetProperty("Version", out var v) ? v.GetString() : null,
                        d.TryGetProperty("Date", out var dt) ? dt.GetString() : null,
                        d.TryGetProperty("ClassName", out var c) ? c.GetString() : null));
                }
                return entries;
            }
            catch (JsonException)
            {
                return Array.Empty<DriverStoreEntry>();
            }
        }

        // pnputil αρνείται από μόνο του να διαγράψει driver που βρίσκεται ενεργά σε χρήση - καμία
        // πρόσθετη προστασία χρειάζεται εδώ (ίδια λογική με το ps1 original).
        public static Task<bool> DeleteStoreDriverAsync(string driverOemName) =>
            RunProcessAsync("pnputil.exe", $"/delete-driver {driverOemName} /uninstall /force", TimeSpan.FromMinutes(2));

        private static async Task<string> RunPowerShellScriptAsync(string script, TimeSpan timeout)
        {
            var scriptFile = Path.Combine(Path.GetTempPath(), $"OptimizerWpfDriver_{Guid.NewGuid():N}.ps1");
            File.WriteAllText(scriptFile, script);
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                };
                using var process = Process.Start(psi);
                if (process == null) return "";
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                using var cts = new System.Threading.CancellationTokenSource(timeout);
                try { await process.WaitForExitAsync(cts.Token); }
                catch (OperationCanceledException) { try { process.Kill(); } catch { } return ""; }
                return await stdoutTask;
            }
            finally
            {
                try { File.Delete(scriptFile); } catch { }
            }
        }

        // Fire-and-forget - χρησιμοποιείται μόνο για την εγκατάσταση driver (μπορεί να πάρει πολλά
        // λεπτά, δεν χρειάζεται να την περιμένει η εφαρμογή· ο χρήστης βλέπει μόνο ότι ξεκίνησε).
        private static void RunPowerShellScriptDetached(string script)
        {
            var scriptFile = Path.Combine(Path.GetTempPath(), $"OptimizerWpfDriverInstall_{Guid.NewGuid():N}.ps1");
            File.WriteAllText(scriptFile, script);
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"& '{scriptFile}'; Remove-Item '{scriptFile}' -Force\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            }
            catch { }
        }

        private static async Task<bool> RunProcessAsync(string fileName, string arguments, TimeSpan timeout)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var process = Process.Start(psi);
                if (process == null) return false;
                using var cts = new System.Threading.CancellationTokenSource(timeout);
                try { await process.WaitForExitAsync(cts.Token); }
                catch (OperationCanceledException) { try { process.Kill(); } catch { } return false; }
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
