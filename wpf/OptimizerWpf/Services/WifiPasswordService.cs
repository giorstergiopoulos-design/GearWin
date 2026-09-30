using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OptimizerWpf.Services
{
    public record WifiProfile(string Name, string? Password);

    // Νέο (πρόταση χρήστη, ίδιο πνεύμα με το ήδη υπάρχον PasswordVaultService - βλ. Network_
    // PasswordManagerTitle): εμφανίζει τους αποθηκευμένους κωδικούς Wi-Fi ΑΥΤΟΥ του υπολογιστή.
    // Χρησιμοποιεί απευθείας το native WLAN API (wlanapi.dll) αντί για ανάλυση κειμένου εξόδου του
    // netsh.exe - το netsh's "Key Content"/"Περιεχόμενο κλειδιού" κ.λπ. αλλάζει ανά γλώσσα του
    // λειτουργικού συστήματος (η εφαρμογή υποστηρίζει 14 γλώσσες UI, αλλά το ΛΕΙΤΟΥΡΓΙΚΟ μπορεί να
    // είναι σε οποιαδήποτε γλώσσα - ανάλυση αγγλικού κειμένου θα έσπαγε σιωπηλά σε ελληνικά/άλλα
    // Windows). Το XML profile (WLAN_PROFILE_GET_PLAINTEXT_KEY) έχει ΣΤΑΘΕΡΑ ονόματα ετικετών
    // ανεξαρτήτως γλώσσας OS - αξιόπιστο. Απαιτεί Administrator (η εφαρμογή το έχει ήδη).
    public static class WifiPasswordService
    {
        private const int ERROR_SUCCESS = 0;
        private const uint WLAN_PROFILE_GET_PLAINTEXT_KEY = 4;

        [DllImport("wlanapi.dll")]
        private static extern int WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

        [DllImport("wlanapi.dll")]
        private static extern int WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        private static extern int WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll")]
        private static extern int WlanGetProfileList(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pReserved, out IntPtr ppProfileList);

        [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
        private static extern int WlanGetProfile(IntPtr hClientHandle, ref Guid pInterfaceGuid, string strProfileName, IntPtr pReserved,
            out IntPtr pstrProfileXml, ref uint pdwFlags, out uint pdwGrantedAccess);

        [DllImport("wlanapi.dll")]
        private static extern void WlanFreeMemory(IntPtr pMemory);

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_INTERFACE_INFO_LIST_HEADER
        {
            public uint dwNumberOfItems;
            public uint dwIndex;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_INTERFACE_INFO
        {
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strInterfaceDescription;
            public uint isState;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_PROFILE_INFO_LIST_HEADER
        {
            public uint dwNumberOfItems;
            public uint dwIndex;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_PROFILE_INFO
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;
            public uint dwFlags;
        }

        public static Task<List<WifiProfile>> GetProfilesAsync() => Task.Run(() =>
        {
            var results = new List<WifiProfile>();
            if (WlanOpenHandle(2, IntPtr.Zero, out _, out var clientHandle) != ERROR_SUCCESS) return results;

            try
            {
                if (WlanEnumInterfaces(clientHandle, IntPtr.Zero, out var interfaceListPtr) != ERROR_SUCCESS) return results;
                try
                {
                    var header = Marshal.PtrToStructure<WLAN_INTERFACE_INFO_LIST_HEADER>(interfaceListPtr);
                    var itemPtr = IntPtr.Add(interfaceListPtr, Marshal.SizeOf<WLAN_INTERFACE_INFO_LIST_HEADER>());
                    var itemSize = Marshal.SizeOf<WLAN_INTERFACE_INFO>();

                    for (var i = 0; i < header.dwNumberOfItems; i++)
                    {
                        var iface = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(IntPtr.Add(itemPtr, i * itemSize));
                        CollectProfilesForInterface(clientHandle, iface.InterfaceGuid, results);
                    }
                }
                finally { WlanFreeMemory(interfaceListPtr); }
            }
            finally { WlanCloseHandle(clientHandle, IntPtr.Zero); }

            return results;
        });

        private static void CollectProfilesForInterface(IntPtr clientHandle, Guid interfaceGuid, List<WifiProfile> results)
        {
            if (WlanGetProfileList(clientHandle, ref interfaceGuid, IntPtr.Zero, out var profileListPtr) != ERROR_SUCCESS) return;
            try
            {
                var header = Marshal.PtrToStructure<WLAN_PROFILE_INFO_LIST_HEADER>(profileListPtr);
                var itemPtr = IntPtr.Add(profileListPtr, Marshal.SizeOf<WLAN_PROFILE_INFO_LIST_HEADER>());
                var itemSize = Marshal.SizeOf<WLAN_PROFILE_INFO>();

                for (var i = 0; i < header.dwNumberOfItems; i++)
                {
                    var profile = Marshal.PtrToStructure<WLAN_PROFILE_INFO>(IntPtr.Add(itemPtr, i * itemSize));
                    var name = profile.strProfileName;
                    if (results.Exists(p => p.Name == name)) continue; // ίδιο δίκτυο ορατό από πολλαπλές κάρτες Wi-Fi

                    var flags = WLAN_PROFILE_GET_PLAINTEXT_KEY;
                    if (WlanGetProfile(clientHandle, ref interfaceGuid, name, IntPtr.Zero, out var xmlPtr, ref flags, out _) != ERROR_SUCCESS)
                    {
                        results.Add(new WifiProfile(name, null));
                        continue;
                    }
                    try
                    {
                        var xml = Marshal.PtrToStringUni(xmlPtr);
                        results.Add(new WifiProfile(name, ExtractKeyMaterial(xml)));
                    }
                    finally { WlanFreeMemory(xmlPtr); }
                }
            }
            finally { WlanFreeMemory(profileListPtr); }
        }

        private static string? ExtractKeyMaterial(string? profileXml)
        {
            if (string.IsNullOrEmpty(profileXml)) return null;
            try
            {
                var doc = XDocument.Parse(profileXml);
                var ns = doc.Root!.GetDefaultNamespace();
                return doc.Descendants(ns + "keyMaterial").FirstOrDefault()?.Value;
            }
            catch { return null; }
        }
    }
}
