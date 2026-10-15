using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace OptimizerWpf.Tests
{
    // Τα tests (ιδίως το live UI-Automation click-through που πατά ΚΑΘΕ διακόπτη) ΔΕΝ πρέπει ποτέ να αγγίζουν τα
    // πραγματικά δεδομένα του χρήστη. Εκτελείται πριν από οποιοδήποτε test, πριν φορτωθεί οποιοδήποτε singleton.
    // Οι ρυθμίσεις του χρήστη ΑΝΤΙΓΡΑΦΟΝΤΑΙ (ανάγνωση μόνο) στον προσωρινό φάκελο ώστε τα tests να ξεκινούν από ρεαλιστική
    // κατάσταση (π.χ. HasSeenOnboarding=true) αλλά κάθε αποθήκευση να καταλήγει εκεί και όχι στο πραγματικό αρχείο.
    internal static class TestIsolation
    {
        [ModuleInitializer]
        internal static void Init()
        {
            var dir = Path.Combine(Path.GetTempPath(), "GearWinTests_" + Environment.ProcessId);
            Directory.CreateDirectory(dir);
            try
            {
                var real = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf", "AppSettings.json");
                if (File.Exists(real)) File.Copy(real, Path.Combine(dir, "AppSettings.json"), overwrite: true);
            }
            catch { /* χωρίς αντίγραφο ξεκινά με προεπιλογές */ }
            Environment.SetEnvironmentVariable("GEARWIN_DATA_DIR", dir);
            Environment.SetEnvironmentVariable("GEARWIN_TEST_HOST", "1");
        }
    }
}
