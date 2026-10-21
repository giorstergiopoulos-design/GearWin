using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // Απελευθέρωση RAM με τους ίδιους μηχανισμούς των Windows που χρησιμοποιεί το RAMMap της Microsoft (Sysinternals):
    // "Empty Working Sets" (μόνο διεργασίες παρασκηνίου) + "Empty Modified Page List" + "Empty Standby List".
    // Καλούνται απευθείας από το ίδιο το λειτουργικό (NtSetSystemInformation / SystemMemoryListInformation) - ΔΕΝ κατεβάζεται
    // ούτε εκτελείται εξωτερικό αρχείο. Απαιτεί δικαιώματα διαχειριστή (SeProfileSingleProcessPrivilege)· χωρίς αυτά γίνεται
    // μόνο το κλάδεμα των working sets. Τα Windows ξαναγεμίζουν την cache κατά τη χρήση - δεν χάνεται κανένα δεδομένο.
    public static class MemoryCleanerService
    {
        public readonly record struct MemoryStats(long TotalBytes, long AvailableBytes, long FreeBytes, long StandbyBytes, long ModifiedBytes)
        {
            public double FreeGb => FreeBytes / 1073741824.0;
            public double AvailableGb => AvailableBytes / 1073741824.0;
        }

        public sealed record MemoryCleanResult(MemoryStats Before, MemoryStats After, int TrimmedProcesses, bool ListsPurged)
        {
            // "Ελεύθερη" = μνήμη που δεν χρησιμοποιείται ούτε για cache (Zero+Free list) - αυτή είναι που αυξάνει πραγματικά.
            public double FreedGb => Math.Max(0, (After.FreeBytes - Before.FreeBytes) / 1073741824.0);
        }

        private const int SystemMemoryListInformation = 80;
        private const int MemoryFlushModifiedList = 3;
        private const int MemoryPurgeStandbyList = 4;

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemMemoryList
        {
            public UIntPtr ZeroPageCount, FreePageCount, ModifiedPageCount, ModifiedNoWritePageCount, BadPageCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public UIntPtr[] PageCountByPriority;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public UIntPtr[] RepurposedPagesByPriority;
            public UIntPtr ModifiedPageCountPageFile;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint Length, MemoryLoad;
            public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TokenPrivileges { public int Count; public long Luid; public int Attributes; }

        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx s);
        [DllImport("ntdll.dll")] private static extern int NtQuerySystemInformation(int cls, ref SystemMemoryList info, int length, out int returned);
        [DllImport("ntdll.dll")] private static extern int NtSetSystemInformation(int cls, ref int info, int length);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool LookupPrivilegeValue(string? system, string name, out long luid);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TokenPrivileges p, int len, IntPtr prev, IntPtr ret);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);

        private const int PageSize = 4096;

        public static MemoryStats Snapshot()
        {
            var ms = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (!GlobalMemoryStatusEx(ref ms)) return default;
            long free = 0, standby = 0, modified = 0;
            try
            {
                var info = new SystemMemoryList { PageCountByPriority = new UIntPtr[8], RepurposedPagesByPriority = new UIntPtr[8] };
                if (NtQuerySystemInformation(SystemMemoryListInformation, ref info, Marshal.SizeOf<SystemMemoryList>(), out _) == 0)
                {
                    free = (long)((ulong)info.ZeroPageCount + (ulong)info.FreePageCount) * PageSize;
                    modified = (long)(ulong)info.ModifiedPageCount * PageSize;
                    foreach (var p in info.PageCountByPriority) standby += (long)(ulong)p * PageSize;
                }
            }
            catch { /* οι αριθμοί λιστών είναι πληροφοριακοί - δεν πρέπει να ακυρώσουν τη δράση */ }
            return new MemoryStats((long)ms.TotalPhys, (long)ms.AvailPhys, free, standby, modified);
        }

        private static bool EnableProfilePrivilege()
        {
            if (!OpenProcessToken(GetCurrentProcess(), 0x20 | 0x8, out var token)) return false; // ADJUST_PRIVILEGES | QUERY
            try
            {
                if (!LookupPrivilegeValue(null, "SeProfileSingleProcessPrivilege", out var luid)) return false;
                var tp = new TokenPrivileges { Count = 1, Luid = luid, Attributes = 2 };
                return AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero) && Marshal.GetLastWin32Error() == 0;
            }
            finally { CloseHandle(token); }
        }

        public static Task<MemoryCleanResult> CleanAsync() => Task.Run(async () =>
        {
            var before = Snapshot();
            var (trimmed, _) = await SystemService.FreeBackgroundMemoryAsync();
            var purged = false;
            if (AdminService.IsElevated && EnableProfilePrivilege())
            {
                var flush = MemoryFlushModifiedList;
                var purge = MemoryPurgeStandbyList;
                var ok1 = NtSetSystemInformation(SystemMemoryListInformation, ref flush, sizeof(int)) == 0;
                var ok2 = NtSetSystemInformation(SystemMemoryListInformation, ref purge, sizeof(int)) == 0;
                purged = ok1 || ok2;
            }
            return new MemoryCleanResult(before, Snapshot(), trimmed, purged);
        });
    }
}
