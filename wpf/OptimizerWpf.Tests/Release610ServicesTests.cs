using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OptimizerWpf.Services;

namespace OptimizerWpf.Tests;

// 6.1.0 - καθαρή λογική των νέων υπηρεσιών (χωρίς WMI/μητρώο/tray): αναίρεση αλλαγών, κανόνες
// σημείου επαναφοράς, κανόνες ειδοποιήσεων, καθυστέρηση εκκίνησης, κοινή εμφάνιση, αναφορά συστήματος.
public class Release610ServicesTests
{
    // ── ChangeJournalService ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ChangeJournal_UndoRunsActionOnceAndMarksEntry()
    {
        ChangeJournalService.Clear();
        var calls = 0;
        var entry = ChangeJournalService.Record("tweak: on", () => calls++);

        Assert.True(entry.CanUndo);
        Assert.True(await ChangeJournalService.UndoAsync(entry));
        Assert.Equal(1, calls);
        Assert.True(entry.IsUndone);
        Assert.False(entry.CanUndo);
        Assert.False(await ChangeJournalService.UndoAsync(entry)); // δεύτερη αναίρεση: καμία ενέργεια
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ChangeJournal_FailingUndoReturnsFalseAndStaysUndoable()
    {
        ChangeJournalService.Clear();
        var entry = ChangeJournalService.Record("x", () => throw new InvalidOperationException());
        Assert.False(await ChangeJournalService.UndoAsync(entry));
        Assert.False(entry.IsUndone);
    }

    [Fact]
    public void ChangeJournal_EntriesNewestFirstAndCapped()
    {
        ChangeJournalService.Clear();
        for (var i = 0; i < 250; i++) ChangeJournalService.Record($"c{i}", (Action?)null);
        var entries = ChangeJournalService.Entries;
        Assert.Equal(200, entries.Count);
        Assert.Equal("c249", entries[0].Description);
        Assert.False(entries[0].CanUndo); // χωρίς undo ενέργεια
    }

    // ── RestoreGuardService ─────────────────────────────────────────────────────────────────
    [Fact]
    public void RestoreGuard_NeedsNewPointWhenNoneOrOld()
    {
        var now = new DateTime(2026, 10, 1, 12, 0, 0);
        var max = TimeSpan.FromHours(24);
        Assert.True(RestoreGuardService.NeedsNewPoint(Array.Empty<RestorePointInfo>(), now, max));
        Assert.True(RestoreGuardService.NeedsNewPoint(new[] { new RestorePointInfo(1, "old", now.AddHours(-25)) }, now, max));
        Assert.False(RestoreGuardService.NeedsNewPoint(new[]
        {
            new RestorePointInfo(1, "old", now.AddDays(-9)),
            new RestorePointInfo(2, "recent", now.AddHours(-3)),
        }, now, max));
    }

    // ── AlertService ────────────────────────────────────────────────────────────────────────
    private static readonly IReadOnlyDictionary<string, DateTime> NoHistory = new Dictionary<string, DateTime>();

    [Fact]
    public void Alert_TemperatureBelowThresholdOrUnknownDoesNotFire()
    {
        var now = DateTime.Now;
        Assert.Null(AlertService.EvaluateTemperature("CPU", 84, 85, NoHistory, now, "{0}", "{0} {1} {2}"));
        Assert.Null(AlertService.EvaluateTemperature("CPU", null, 85, NoHistory, now, "{0}", "{0} {1} {2}"));
    }

    [Fact]
    public void Alert_TemperatureAtThresholdFiresThenRespectsCooldown()
    {
        var now = new DateTime(2026, 10, 1, 12, 0, 0);
        var first = AlertService.EvaluateTemperature("GPU", 90, 85, NoHistory, now, "T {0}", "{0}={1}/{2}");
        Assert.NotNull(first);
        Assert.Equal("temp:GPU", first!.Key);
        Assert.Equal("GPU=90/85", first.Body);

        var history = new Dictionary<string, DateTime> { [first.Key] = now };
        Assert.Null(AlertService.EvaluateTemperature("GPU", 95, 85, history, now.AddHours(1), "T {0}", "{0}={1}/{2}"));
        Assert.NotNull(AlertService.EvaluateTemperature("GPU", 95, 85, history, now.AddHours(7), "T {0}", "{0}={1}/{2}"));
    }

    [Fact]
    public void Alert_SmartOnlyFiresOnExplicitFalse()
    {
        var now = DateTime.Now;
        Assert.Null(AlertService.EvaluateSmart("C:", true, NoHistory, now, "{0}", "{0}"));
        Assert.Null(AlertService.EvaluateSmart("C:", null, NoHistory, now, "{0}", "{0}")); // άγνωστο ≠ πρόβλημα
        var a = AlertService.EvaluateSmart("D:", false, NoHistory, now, "{0}", "{0}");
        Assert.NotNull(a);
        Assert.Equal("smart:D:", a!.Key);
    }

    // ── StartupDelayService ─────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(30, "0000:30")]
    [InlineData(90, "0001:30")]
    [InlineData(5, "0000:05")]
    [InlineData(1, "0000:05")]      // κάτω όριο 5s
    [InlineData(3600, "0060:00")]
    public void StartupDelay_FormatsSchtasksDelay(int seconds, string expected) =>
        Assert.Equal(expected, StartupDelayService.FormatDelay(seconds));

    [Fact]
    public void StartupDelay_TaskNameIsSanitizedAndFolderScoped()
    {
        Assert.Equal(@"GearWinDelayed\My App_v2", StartupDelayService.BuildTaskName("My App/v2"));
        Assert.StartsWith(@"GearWinDelayed\", StartupDelayService.BuildTaskName("???"));
    }

    // ── TaskSchedulerService ────────────────────────────────────────────────────────────────
    [Fact]
    public void TaskXml_LogonTaskIsWellFormedElevatedAndBatteryFriendly()
    {
        var xml = TaskSchedulerService.BuildLogonTaskXml(@"C:\Program Files\GearWin\GearWin.exe", "--tray", 15, @"PC\gstrj", "A & B <test>");
        var doc = System.Xml.Linq.XDocument.Parse(xml); // πετά αν δεν είναι well-formed (π.χ. & δεν έγινε escape)
        System.Xml.Linq.XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        Assert.Equal("HighestAvailable", doc.Descendants(ns + "RunLevel").Single().Value);
        Assert.Equal("false", doc.Descendants(ns + "DisallowStartIfOnBatteries").Single().Value);
        Assert.Equal("false", doc.Descendants(ns + "StopIfGoingOnBatteries").Single().Value);
        Assert.Equal("PT15S", doc.Descendants(ns + "Delay").Single().Value);
        Assert.Equal("--tray", doc.Descendants(ns + "Arguments").Single().Value);
        Assert.Equal(@"C:\Program Files\GearWin\GearWin.exe", doc.Descendants(ns + "Command").Single().Value);
        Assert.Equal("A & B <test>", doc.Descendants(ns + "Description").Single().Value);
        Assert.Single(doc.Descendants(ns + "LogonTrigger"));
    }

    [Fact]
    public void TaskXml_WeeklyTaskHasSundayCalendarTriggerAndNoArgumentsElementWhenEmpty()
    {
        var xml = TaskSchedulerService.BuildWeeklyTaskXml(@"C:\x.exe", "", "u", "d");
        var doc = System.Xml.Linq.XDocument.Parse(xml);
        System.Xml.Linq.XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        Assert.Single(doc.Descendants(ns + "Sunday"));
        Assert.Empty(doc.Descendants(ns + "Arguments"));
        Assert.Empty(doc.Descendants(ns + "LogonTrigger"));
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\App\\app.exe\" -min", "C:\\Program Files\\App\\app.exe", "-min")]
    [InlineData("C:\\Tools\\x.exe /silent /x", "C:\\Tools\\x.exe", "/silent /x")]
    [InlineData("C:\\Program Files\\App\\app.exe --flag", "C:\\Program Files\\App\\app.exe", "--flag")]
    [InlineData("notepad", "notepad", "")]
    [InlineData("", "", "")]
    public void TaskScheduler_SplitCommand(string command, string exe, string args) =>
        Assert.Equal((exe, args), TaskSchedulerService.SplitCommand(command));

    // ── WindowOpacityService ────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(100, 255)]
    [InlineData(60, 153)]
    [InlineData(10, 153)]   // κάτω όριο 60% - ποτέ αόρατο παράθυρο
    [InlineData(250, 255)]
    public void WindowOpacity_AlphaIsClamped(int percent, int alpha) =>
        Assert.Equal((byte)alpha, WindowOpacityService.ToAlpha(percent));

    // ── SharedAppearanceService ─────────────────────────────────────────────────────────────
    [Fact]
    public void SharedAppearance_RoundTripAndFollowRules()
    {
        var path = Path.Combine(Path.GetTempPath(), $"appearance_{Guid.NewGuid():N}", "appearance.json");
        try
        {
            Assert.Null(SharedAppearanceService.Read(path));
            Assert.True(SharedAppearanceService.Write(new SharedAppearance("MotionDesk", "Cyberpunk", false, DateTime.UtcNow), path));
            var read = SharedAppearanceService.Read(path);
            Assert.NotNull(read);
            Assert.Equal("Cyberpunk", read!.ThemeName);

            Assert.True(SharedAppearanceService.ShouldFollow(read, "Matrix", true));         // άλλη εφαρμογή, διαφορετικό θέμα
            Assert.False(SharedAppearanceService.ShouldFollow(read, "Cyberpunk", false));    // ίδιο ήδη
            Assert.False(SharedAppearanceService.ShouldFollow(read with { Source = SharedAppearanceService.AppName }, "Matrix", true)); // το έγραψα εγώ
            Assert.False(SharedAppearanceService.ShouldFollow(null, "Matrix", true));
        }
        finally { try { Directory.Delete(Path.GetDirectoryName(path)!, true); } catch { } }
    }

    [Fact]
    public void SharedAppearance_CorruptFileReturnsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), $"appearance_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json");
        try { Assert.Null(SharedAppearanceService.Read(path)); }
        finally { File.Delete(path); }
    }

    // ── SecurityStatusService ───────────────────────────────────────────────────────────────
    [Fact]
    public void Security_AllEnabledNeedsAllKnownProfilesOn()
    {
        Assert.True(SecurityStatusService.AllEnabled(new int?[] { 1, 1, 1 }));
        Assert.False(SecurityStatusService.AllEnabled(new int?[] { 1, 0, 1 }));
        Assert.True(SecurityStatusService.AllEnabled(new int?[] { 1, null, 1 })); // άγνωστο προφίλ δεν μετράει ως off
        Assert.Null(SecurityStatusService.AllEnabled(new int?[] { null, null }));
    }

    [Fact]
    public void Security_BuildFlagsOffFirewallAndUac()
    {
        var items = SecurityStatusService.Build(null, null, firewallAllOn: false, uacOn: false);
        Assert.Equal(SecurityLevel.Warning, items.First(i => i.Key == "Firewall").Level);
        Assert.Equal(SecurityLevel.Warning, items.First(i => i.Key == "Uac").Level);
        Assert.Equal(SecurityLevel.Warning, items.First(i => i.Key == "Defender").Level); // χωρίς δεδομένα => ελέγξτε
        var good = SecurityStatusService.Build(null, null, true, true);
        Assert.Equal(SecurityLevel.Good, good.First(i => i.Key == "Firewall").Level);
        Assert.Equal(SecurityLevel.Good, good.First(i => i.Key == "Uac").Level);
    }

    // ── SystemReportService ─────────────────────────────────────────────────────────────────
    [Fact]
    public void SystemReport_ContainsVersionSecurityRestorePointsAndChanges()
    {
        ChangeJournalService.Clear();
        var change = ChangeJournalService.Record("Fast Startup: on", (Action?)null);
        var security = new[] { new SecurityItem("Firewall", "Firewall", SecurityLevel.Warning, "off") };
        var points = new[] { new RestorePointInfo(7, "Before cleanup", new DateTime(2026, 9, 30, 8, 15, 0)) };

        var text = SystemReportService.Build(null, security, points, new[] { change }, new DateTime(2026, 10, 1, 9, 0, 0), "6.1.0");

        Assert.Contains("GearWin 6.1.0", text);
        Assert.Contains("[!] Firewall: off", text);
        Assert.Contains("Before cleanup", text);
        Assert.Contains("Fast Startup: on", text);
        Assert.Contains("2026-10-01 09:00", text);
    }

    [Fact]
    public void SystemReport_SaveWritesTextFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"report_{Guid.NewGuid():N}.txt");
        try
        {
            SystemReportService.Save(path, "hello");
            Assert.Equal("hello", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    // ── ThemeBackgroundStyles ───────────────────────────────────────────────────────────────
    [Fact]
    public void ThemeBackgrounds_EveryCatalogThemeHasItsOwnStyle()
    {
        var themes = new[]
        {
            "Windows Vista", "Office 2007 (Aurora)", "Windows XP Luna", "Cyberpunk", "Matrix", "Nordic Night", "Windows 7 Aero",
            "Windows 98 Retro", "macOS Monterey", "Solarized Dark", "GitHub Dark", "VS Code Dark+", "Terminal DOS Green",
            "Discord Blurple", "Steam Deck Dark", "RGB Gaming Rig", "Circuit Board PCB", "Synthwave Outrun", "Windows 11 Fluent", "Retro DOS Blue",
        };
        var styles = themes.Select(ThemeBackgroundStyles.For).ToList();
        Assert.Equal(themes.Length, styles.Distinct().Count()); // κανένα θέμα δεν μοιράζεται στυλ
    }
}

// Όλα τα νέα κλειδιά 6.1.0: ύπαρξη σε 14 γλώσσες + ίδια placeholders ({0},{1},{2}) σε κάθε γλώσσα,
// αλλιώς το string.Format θα πετούσε/έβγαζε λάθος κείμενο μόνο σε κάποιες γλώσσες.
public class Release610LanguageTests
{
    private static readonly string[] Keys =
    {
        "Center_Title", "Center_TabSecurity", "Center_TabChanges", "Center_TabAlerts", "Center_TabStartup", "Center_TabReport",
        "Center_SecurityWarnings", "Alert_TempTitle", "Alert_TempBody", "Alert_SmartTitle", "Alert_SmartBody",
        "DryRun_Title", "DryRun_Intro", "DryRun_Footer", "Health_ToolCancelled", "Guard_PointFailedContinue",
        "Journal_On", "Journal_Off", "Tweak_RequiresAdminTip", "Report_Title", "Sec_Firewall", "Sec_UacOff",
        "VerHist_610Title", "VerHist_610Desc", "Home_AnalysisDriveTip", "Appr_ThemeSettingsTab",
    };

    [Fact]
    public void NewKeys_ExistInAllLanguages()
    {
        foreach (var (lang, dict) in LanguageService.AllTranslations)
            foreach (var key in Keys)
                Assert.True(dict.ContainsKey(key) && !string.IsNullOrWhiteSpace(dict[key]), $"{lang}: missing {key}");
    }

    [Theory]
    [InlineData("Center_SecurityWarnings")]
    [InlineData("Alert_TempTitle")]
    [InlineData("Alert_TempBody")]
    [InlineData("Alert_SmartTitle")]
    [InlineData("Alert_SmartBody")]
    public void FormatKeys_HaveSamePlaceholdersInEveryLanguage(string key)
    {
        string Placeholders(string s) => string.Join(",", System.Text.RegularExpressions.Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x));
        var expected = Placeholders(LanguageService.AllTranslations["en"][key]);
        foreach (var (lang, dict) in LanguageService.AllTranslations)
            Assert.True(Placeholders(dict[key]) == expected, $"{lang}: {key} placeholders differ");
        // Και ότι η μορφοποίηση πράγματι δουλεύει.
        Assert.Contains("CPU", string.Format(LanguageService.AllTranslations["en"][key], "CPU", 90, 85, "x"));
    }
}
