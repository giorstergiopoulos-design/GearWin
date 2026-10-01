using System.Security.Principal;

namespace OptimizerWpf.Services
{
    // ΝΕΟ (6.1.0) - ένδειξη "απαιτεί διαχειριστή". Η εφαρμογή ζητά ήδη elevation από το manifest, άρα
    // το IsElevated είναι συνήθως true· η ένδειξη ανά tweak (SimpleTweak.RequiresAdmin) ενημερώνει
    // ποιες αλλαγές είναι σε επίπεδο συστήματος (HKLM/υπηρεσίες/powercfg) και θα αποτύχουν αν η
    // εφαρμογή τρέξει ποτέ χωρίς δικαιώματα (π.χ. dev εκτέλεση).
    public static class AdminService
    {
        private static bool? _isElevated;

        public static bool IsElevated
        {
            get
            {
                if (_isElevated is bool b) return b;
                try
                {
                    using var id = WindowsIdentity.GetCurrent();
                    _isElevated = new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
                }
                catch { _isElevated = false; }
                return _isElevated.Value;
            }
        }
    }
}
