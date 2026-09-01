using System.Diagnostics;

namespace OptimizerWpf.Services
{
    // Port of the powercfg half of Optimizer.ps1's Office Mode / Gaming Mode toggles (line ~10861+).
    // ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: only the power-plan switch is ported in this increment - the WinForms
    // version's full Office Mode also disables Start menu suggestions/ads and restarts Explorer;
    // its full Gaming Mode also disables Network Throttling, enables HAGS, and temporarily disables
    // VBS/Hypervisor Code Integrity. None of that extra registry/VBS work is wired up here yet.
    public static class PowerModeService
    {
        public static bool SetBalancedPlan() => RunPowercfg("/setactive SCHEME_BALANCED");

        public static bool SetHighPerformancePlan() => RunPowercfg("/setactive SCHEME_MIN");

        private static bool RunPowercfg(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var process = Process.Start(psi);
                process?.WaitForExit(5000);
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
