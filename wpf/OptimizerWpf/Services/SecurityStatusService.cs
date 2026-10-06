using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    public enum SecurityLevel { Good, Warning, Info }

    // Label/Detail είναι ήδη μεταφρασμένα κείμενα (το UI δεν χρειάζεται κανένα άλλο mapping).
    public record SecurityItem(string Key, string Label, SecurityLevel Level, string Detail);

    // ΝΕΟ (6.1.0) - πίνακας κατάστασης ασφάλειας: ένα σημείο όπου φαίνονται Defender/Antivirus,
    // Firewall, UAC, Secure Boot, TPM, BitLocker και εκκρεμής επανεκκίνηση. Δεν ξαναμετράει ό,τι
    // ήδη μαζεύει το MyDeviceService (Secure Boot/BitLocker/Defender/Pending reboot) - τα
    // επαναχρησιμοποιεί, και προσθέτει ΜΟΝΟ ό,τι έλειπε (Firewall ανά προφίλ, UAC) από το μητρώο.
    // Ειλικρίνεια: ο πίνακας ΑΝΑΦΕΡΕΙ κατάσταση, δεν τη διορθώνει· "Warning" σημαίνει "αξίζει έλεγχο".
    public static class SecurityStatusService
    {
        // enabled: null = δεν διαβάστηκε. Επιστρέφει true μόνο αν ΟΛΑ τα γνωστά προφίλ είναι ενεργά.
        public static bool? AllEnabled(IEnumerable<int?> profileValues)
        {
            var known = profileValues.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (known.Count == 0) return null;
            return known.All(v => v == 1);
        }

        public static IReadOnlyList<SecurityItem> Build(OsInfo? os, string? tpmStatus, bool? firewallAllOn, bool? uacOn)
        {
            string T(string k) => LanguageService.T(k);
            var list = new List<SecurityItem>();

            if (os?.DefenderStatus is { Length: > 0 } def)
            {
                // ΔΙΟΡΘΩΣΗ: "Ανενεργό" (χωρίς τρίτο AV) σημαίνει πραγματικά καμία προστασία - Warning.
                // Οτιδήποτε άλλο (Ενεργό, ή "Ενεργό μέσω X" όταν ανιχνεύεται τρίτο AV) είναι Good, όχι
                // ουδέτερο Info - η προηγούμενη έκδοση δεν έκανε ποτέ αυτή τη διάκριση.
                var level = def == T("Sys_Disabled") ? SecurityLevel.Warning : SecurityLevel.Good;
                list.Add(new SecurityItem("Defender", T("Sec_Antivirus"), level, def));
            }
            else
                list.Add(new SecurityItem("Defender", T("Sec_Antivirus"), SecurityLevel.Warning, T("Sec_Unknown")));

            list.Add(firewallAllOn switch
            {
                true => new SecurityItem("Firewall", T("Sec_Firewall"), SecurityLevel.Good, T("Sec_On")),
                false => new SecurityItem("Firewall", T("Sec_Firewall"), SecurityLevel.Warning, T("Sec_FirewallOff")),
                _ => new SecurityItem("Firewall", T("Sec_Firewall"), SecurityLevel.Info, T("Sec_Unknown")),
            });

            list.Add(uacOn switch
            {
                true => new SecurityItem("Uac", "UAC", SecurityLevel.Good, T("Sec_On")),
                false => new SecurityItem("Uac", "UAC", SecurityLevel.Warning, T("Sec_UacOff")),
                _ => new SecurityItem("Uac", "UAC", SecurityLevel.Info, T("Sec_Unknown")),
            });

            if (os != null)
                list.Add(new SecurityItem("SecureBoot", "Secure Boot", os.SecureBoot ? SecurityLevel.Good : SecurityLevel.Warning,
                    os.SecureBoot ? T("Sec_On") : T("Sec_Off")));

            if (!string.IsNullOrWhiteSpace(tpmStatus))
                list.Add(new SecurityItem("Tpm", "TPM", SecurityLevel.Info, tpmStatus!));

            if (!string.IsNullOrWhiteSpace(os?.BitLocker))
                list.Add(new SecurityItem("BitLocker", "BitLocker", SecurityLevel.Info, os!.BitLocker!));

            if (os != null)
                list.Add(new SecurityItem("Reboot", T("Sec_PendingReboot"), os.PendingReboot ? SecurityLevel.Warning : SecurityLevel.Good,
                    os.PendingReboot ? T("Sec_Yes") : T("Sec_No")));

            return list;
        }

        public static Task<IReadOnlyList<SecurityItem>> GetAsync() => Task.Run(async () =>
        {
            OsInfo? os = null; string? tpm = null;
            try
            {
                var summary = await MyDeviceService.GetSummaryAsync();
                os = summary.Os; tpm = summary.Motherboard.TpmStatus;
            }
            catch { }
            return Build(os, tpm, ReadFirewallAllOn(), ReadUacOn());
        });

        private static bool? ReadFirewallAllOn()
        {
            try
            {
                var values = new List<int?>();
                foreach (var profile in new[] { "StandardProfile", "PublicProfile", "DomainProfile" })
                {
                    using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\{profile}");
                    values.Add(key?.GetValue("EnableFirewall") is int v ? v : null);
                }
                return AllEnabled(values);
            }
            catch { return null; }
        }

        private static bool? ReadUacOn()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
                return key?.GetValue("EnableLUA") is int v ? v == 1 : null;
            }
            catch { return null; }
        }
    }
}
