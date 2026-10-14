using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace OptimizerWpf.Services
{
    // ΑΣΦΑΛΕΙΑ: η εφαρμογή τρέχει ως Administrator. Κάθε αρχείο που γράφεται και ΜΕΤΑ εκτελείται/διαβάζεται από
    // elevated διεργασία (XML scheduled task, πακέτο οδηγού, εξαγόμενα εργαλεία, associations XML) δεν πρέπει να
    // περνά από φάκελο εγγράψιμο από μη-elevated κώδικα του ίδιου χρήστη (%TEMP%, %LocalAppData%) - αλλιώς το
    // αρχείο μπορεί να αντικατασταθεί ανάμεσα στη δημιουργία και στη χρήση (privilege escalation χωρίς UAC).
    // Εδώ: %ProgramData%\GearWin με ACL μόνο-Administrators/SYSTEM εγγραφής, Users μόνο ανάγνωση/εκτέλεση. Οι
    // Users δεν μπορούν ούτε να μετονομάσουν/διαγράψουν τον φάκελο (δεν έχουν δικαίωμα στον γονέα να σβήσουν παιδιά).
    public static class SecureStorage
    {
        public static string RootDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GearWin");

        public static void SecureDirectory(string dir)
        {
            Directory.CreateDirectory(dir);
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            void Allow(WellKnownSidType sid, FileSystemRights rights) =>
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid, null), rights, inherit,
                    PropagationFlags.None, AccessControlType.Allow));
            Allow(WellKnownSidType.LocalSystemSid, FileSystemRights.FullControl);
            Allow(WellKnownSidType.BuiltinAdministratorsSid, FileSystemRights.FullControl);
            Allow(WellKnownSidType.BuiltinUsersSid, FileSystemRights.ReadAndExecute);
            new DirectoryInfo(dir).SetAccessControl(security);
        }

        // true μόνο αν ΚΑΝΕΝΑΣ μη-διαχειριστής (Users, Authenticated Users, Everyone, ο τρέχων χρήστης) δεν έχει δικαίωμα
        // εγγραφής/δημιουργίας/διαγραφής στον φάκελο. Χρησιμοποιείται πριν καταχωρηθεί εργασία που ξεκινά ένα exe με
        // ΥΨΗΛΑ δικαιώματα χωρίς UAC: αν το exe ζει σε εγγράψιμο από τον χρήστη φάκελο (Downloads, Desktop, φάκελος
        // ανάπτυξης), κάθε μη-elevated διεργασία θα μπορούσε να το αντικαταστήσει και να αποκτήσει admin σε κάθε σύνδεση.
        public static bool IsDirectoryAdminOnlyWritable(string? dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
                const FileSystemRights writeRights = FileSystemRights.WriteData | FileSystemRights.AppendData |
                    FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories | FileSystemRights.Delete |
                    FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions |
                    FileSystemRights.TakeOwnership | FileSystemRights.WriteAttributes | FileSystemRights.WriteExtendedAttributes;
                var risky = new[]
                {
                    new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                    new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    WindowsIdentity.GetCurrent().User!,
                };
                var rules = new DirectoryInfo(dir).GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier));
                foreach (FileSystemAccessRule r in rules)
                {
                    if (r.AccessControlType != AccessControlType.Allow) continue;
                    if ((r.FileSystemRights & writeRights) == 0) continue;
                    if (Array.Exists(risky, s => s.Equals(r.IdentityReference))) return false;
                }
                return true;
            }
            catch { return false; }
        }

        public static void EnsureRoot() => SecureDirectory(RootDir);

        // Προσωρινός φάκελος εργασίας για αρχεία που θα χρησιμοποιηθούν από elevated εργαλεία. Επιστρέφει νέο μοναδικό
        // υποφάκελο (κληρονομεί το κλειδωμένο ACL). Ρίχνει εξαίρεση αν ο φάκελος δεν μπορεί να κλειδωθεί - ποτέ
        // "σιωπηλή" υποβάθμιση σε μη ασφαλή διαδρομή.
        public static string NewPrivateTempDir(string prefix)
        {
            EnsureRoot();
            PurgeOldTemp();
            var dir = Path.Combine(RootDir, "tmp", $"{prefix}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        // Υπολείμματα (π.χ. διεργασία που τερματίστηκε πριν τον καθαρισμό) - διαγράφονται όσα είναι παλιότερα από 1 μέρα.
        private static void PurgeOldTemp()
        {
            try
            {
                var tmp = Path.Combine(RootDir, "tmp");
                if (!Directory.Exists(tmp)) return;
                foreach (var d in Directory.GetDirectories(tmp))
                    if (DateTime.UtcNow - Directory.GetCreationTimeUtc(d) > TimeSpan.FromDays(1))
                        try { Directory.Delete(d, true); } catch { }
            }
            catch { }
        }

        public static string NewPrivateTempFile(string prefix, string extension) =>
            Path.Combine(NewPrivateTempDir(prefix), "file" + extension);
    }
}
