using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    public record UsbHistoryEntry(string FriendlyName, string DeviceId, DateTime? LastConnected);

    // ΝΕΟ - roadmap "ιστορικό συσκευών USB" (από έρευνα ανταγωνιστικών εργαλείων, βλ. NirSoft
    // USBDeview) - καθαρά ΕΝΗΜΕΡΩΤΙΚΟ, διαβάζει απευθείας το ίδιο registry subtree
    // (HKLM\SYSTEM\CurrentControlSet\Enum\USBSTOR) που διατηρούν οι Windows για ΚΑΘΕ συσκευή
    // αποθήκευσης USB που έχει συνδεθεί ποτέ σε αυτόν τον υπολογιστή - καμία ενέργεια/αλλαγή.
    public static class UsbHistoryService
    {
        public static IReadOnlyList<UsbHistoryEntry> GetHistory()
        {
            var results = new List<UsbHistoryEntry>();
            try
            {
                using var usbstor = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USBSTOR");
                if (usbstor == null) return results;

                foreach (var deviceTypeName in usbstor.GetSubKeyNames())
                {
                    using var deviceTypeKey = usbstor.OpenSubKey(deviceTypeName);
                    if (deviceTypeKey == null) continue;

                    foreach (var instanceName in deviceTypeKey.GetSubKeyNames())
                    {
                        using var instanceKey = deviceTypeKey.OpenSubKey(instanceName);
                        if (instanceKey == null) continue;

                        var friendlyName = instanceKey.GetValue("FriendlyName") as string
                            ?? deviceTypeName.Replace("&", " ").Replace("_", " ");
                        var lastWrite = GetKeyLastWriteTime(instanceKey);
                        results.Add(new UsbHistoryEntry(friendlyName, instanceName, lastWrite));
                    }
                }
            }
            catch { }

            results.Sort((a, b) => (b.LastConnected ?? DateTime.MinValue).CompareTo(a.LastConnected ?? DateTime.MinValue));
            return results;
        }

        // Το .NET Registry API δεν εκθέτει απευθείας το LastWriteTime ενός κλειδιού - χρειάζεται η
        // εγγενής RegQueryInfoKey (Win32) πάνω στο handle. Επιστρέφει null αν αποτύχει - εμφανίζεται
        // ως "άγνωστο" στο UI αντί για ψευδή ημερομηνία.
        [System.Runtime.InteropServices.DllImport("advapi32.dll")]
        private static extern int RegQueryInfoKey(
            Microsoft.Win32.SafeHandles.SafeRegistryHandle hKey, System.Text.StringBuilder? lpClass, IntPtr lpcchClass,
            IntPtr lpReserved, out int lpcSubKeys, IntPtr lpcbMaxSubKeyLen, IntPtr lpcbMaxClassLen,
            out int lpcValues, IntPtr lpcbMaxValueNameLen, IntPtr lpcbMaxValueLen, IntPtr lpSecurityDescriptor,
            out long lpftLastWriteTime);

        private static DateTime? GetKeyLastWriteTime(RegistryKey key)
        {
            try
            {
                var result = RegQueryInfoKey(key.Handle, null, IntPtr.Zero, IntPtr.Zero, out _, IntPtr.Zero, IntPtr.Zero,
                    out _, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out var fileTime);
                if (result != 0) return null;
                return DateTime.FromFileTime(fileTime);
            }
            catch { return null; }
        }
    }
}
