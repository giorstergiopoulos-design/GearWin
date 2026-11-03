using System;
using System.IO;
using Xunit;

namespace OptimizerWpf.Tests
{
    // 6.9.5: ο σιωπηλός uninstall έμενε για πάντα σε ένα MsgBox (installer-e2e). Το script του installer δεν πρέπει να ρωτά όταν είναι σιωπηλό.
    public class InstallerScriptTests
    {
        private static string Iss()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "installer", "OptimizerWpf.iss"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return File.ReadAllText(Path.Combine(dir!.FullName, "installer", "OptimizerWpf.iss"));
        }

        [Fact]
        public void SilentUninstallNeverShowsAMessageBox()
        {
            var iss = Iss();
            var start = iss.IndexOf("function InitializeUninstall", StringComparison.Ordinal);
            Assert.True(start >= 0);
            var body = iss.Substring(start, iss.IndexOf("function ShouldResetTweaks", start, StringComparison.Ordinal) - start);
            Assert.Contains("UninstallSilent", body);
            Assert.True(body.IndexOf("UninstallSilent", StringComparison.Ordinal) < body.IndexOf("MsgBox", StringComparison.Ordinal), "the silent check must come before the MsgBox");
        }
    }
}
