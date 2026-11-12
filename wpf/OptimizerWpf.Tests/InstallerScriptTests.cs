using System;
using System.IO;
using System.Linq;
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

        // 6.9.9: το in-app update ξεκινά τον installer ως παιδί του GearWin.exe: το taskkill /T του installer θα τον σκότωνε μαζί με την εφαρμογή
        [Fact]
        public void SilentSetupClosesTheAppWithoutKillingItsOwnProcessTree()
        {
            var iss = Iss();
            var line = iss.Split((char)10).First(l => l.Contains("taskkill.exe"));
            Assert.DoesNotContain("/T", line.Replace("/F", "").Replace("/IM", ""));
            Assert.Contains("/F", line);
        }

        [Fact]
        public void InAppUpdateKeepsTheInstallerLogAndRelaunches()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "installer", "OptimizerWpf.iss"))) dir = dir.Parent;
            var src = File.ReadAllText(Path.Combine(dir!.FullName, "wpf", "OptimizerWpf", "Services", "SelfUpdateService.cs"));
            Assert.Contains("last-update.log", src);
            Assert.Contains("/LOG=", src);
            Assert.Contains("/RELAUNCH=1", src);
            Assert.Contains("/CLOSEAPPLICATIONS", src);
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
