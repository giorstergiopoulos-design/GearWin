using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // ΝΕΟ (6.1.0) - αναφορά συστήματος με ένα κλικ (.txt ή .pdf): υλικό, λειτουργικό, δίσκοι με
    // S.M.A.R.T., ασφάλεια, σημεία επαναφοράς, ρυθμίσεις που έχουν αλλάξει σε αυτή τη συνεδρία.
    // Το Build είναι καθαρή συνάρτηση (παίρνει έτοιμα δεδομένα) ώστε να ελέγχεται χωρίς WMI.
    public static class SystemReportService
    {
        public static string Build(DeviceSummary? d, IReadOnlyList<SecurityItem> security,
            IReadOnlyList<RestorePointInfo> restorePoints, IReadOnlyList<ChangeEntry> changes, DateTime now, string appVersion)
        {
            var sb = new StringBuilder();
            string T(string k) => LanguageService.T(k);
            void H(string title) { sb.AppendLine(); sb.AppendLine($"=== {title} ==="); }

            sb.AppendLine($"GearWin {appVersion}");
            sb.AppendLine($"{T("Report_Generated")}: {now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"{T("Report_Machine")}: {Environment.MachineName}");

            if (d != null)
            {
                H(T("Report_Hardware"));
                if (d.Cpu.Name != null) sb.AppendLine($"CPU: {d.Cpu.Name} ({d.Cpu.Cores}C/{d.Cpu.Threads}T)");
                sb.AppendLine($"RAM: {d.Ram.TotalGb:0.#} GB");
                foreach (var g in d.Gpus) sb.AppendLine($"GPU: {g.Name}{(g.DriverVersion != null ? $" (driver {g.DriverVersion})" : "")}");
                var mb = d.Motherboard;
                if (mb.BoardModel != null || mb.SystemModel != null)
                    sb.AppendLine($"{T("Report_Board")}: {mb.BoardManufacturer} {mb.BoardModel} / {mb.SystemManufacturer} {mb.SystemModel}".Replace("  ", " "));
                if (mb.BiosVersion != null) sb.AppendLine($"BIOS: {mb.BiosVendor} {mb.BiosVersion} ({mb.BiosDate})");

                H("Windows");
                sb.AppendLine($"{d.Os.Caption} {d.Os.Version} (build {d.Os.Build}) {d.Os.Architecture}");
                if (d.Os.LastBoot is DateTime lb) sb.AppendLine($"{T("Report_LastBoot")}: {lb:yyyy-MM-dd HH:mm}");

                H(T("Report_Storage"));
                foreach (var dr in d.Drives)
                    sb.AppendLine($"{dr.Letter}: {dr.Model} | {dr.BusType} | {dr.FileSystem} | {dr.FreeGb:0.#}/{dr.TotalGb:0.#} GB | S.M.A.R.T.: {dr.Health}");
            }

            if (security.Count > 0)
            {
                H(T("Report_Security"));
                foreach (var s in security)
                    sb.AppendLine($"{(s.Level == SecurityLevel.Warning ? "[!]" : "[ ]")} {s.Label}: {s.Detail}");
            }

            H(T("Report_RestorePoints"));
            if (restorePoints.Count == 0) sb.AppendLine(T("Report_None"));
            foreach (var p in restorePoints.Take(10)) sb.AppendLine($"{p.CreationTime:yyyy-MM-dd HH:mm}  {p.Description}");

            H(T("Report_Changes"));
            if (changes.Count == 0) sb.AppendLine(T("Report_None"));
            foreach (var c in changes) sb.AppendLine(c.ToString());

            return sb.ToString();
        }

        public static async Task<string> GenerateAsync(string appVersion)
        {
            DeviceSummary? d = null;
            try { d = await MyDeviceService.GetSummaryAsync(); } catch { }
            var sec = await SecurityStatusService.GetAsync();
            IReadOnlyList<RestorePointInfo> rp;
            try { rp = await SystemService.ListRestorePointsAsync(); } catch { rp = Array.Empty<RestorePointInfo>(); }
            return Build(d, sec, rp, ChangeJournalService.Entries, DateTime.Now, appVersion);
        }

        public static void Save(string path, string content)
        {
            if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                PdfExportService.ExportTextReport(path, "GearWin - " + LanguageService.T("Report_Title"), content);
            else
                File.WriteAllText(path, content, new UTF8Encoding(true));
        }
    }
}
