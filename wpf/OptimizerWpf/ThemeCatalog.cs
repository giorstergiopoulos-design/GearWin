using System.Collections.Generic;
using System.Windows.Media;

namespace OptimizerWpf
{
    // Direct port of every theme branch in Get-ThemeColors (Optimizer.ps1, ~line 4651) - same
    // names, same RGB values. Only "Windows 11 Fluent" has a confirmed Light variant ported from
    // the PowerShell app's light-mode switch block; every other theme is dark-only here (matches
    // what Optimizer.ps1 itself has fully fleshed out - most of its other themes are dark-focused
    // too). Selecting a dark-only theme while "Light Mode" is active just keeps using its one
    // palette - see ThemeManager.
    public static class ThemeCatalog
    {
        public static readonly ThemeColors WindowsVista = new(
            "Windows Vista (Οθόνη Υποδοχής Aero Black)",
            Color.FromRgb(10, 14, 20), Color.FromRgb(16, 22, 30), Color.FromRgb(28, 42, 58),
            Color.FromRgb(225, 240, 250), Color.FromRgb(135, 175, 205), Color.FromRgb(110, 190, 220),
            Color.FromRgb(14, 20, 28), Color.FromRgb(45, 110, 150), Color.FromRgb(24, 40, 55),
            Color.FromRgb(38, 65, 88), Color.FromRgb(0, 160, 220));

        public static readonly ThemeColors Office2007 = new(
            "Office 2007 (Dark Aurora)",
            Color.FromRgb(28, 38, 52), Color.FromRgb(38, 50, 68), Color.FromRgb(55, 72, 98),
            Color.FromRgb(225, 238, 255), Color.FromRgb(155, 185, 220), Color.FromRgb(140, 180, 220),
            Color.FromRgb(33, 45, 60), Color.FromRgb(70, 100, 140), Color.FromRgb(48, 65, 90),
            Color.FromRgb(215, 180, 60), Color.FromRgb(80, 140, 200));

        public static readonly ThemeColors WindowsXpLuna = new(
            "Windows XP Luna (Dark)",
            Color.FromRgb(20, 40, 80), Color.FromRgb(30, 55, 105), Color.FromRgb(45, 75, 140),
            Color.FromRgb(235, 243, 255), Color.FromRgb(160, 195, 240), Color.FromRgb(180, 210, 255),
            Color.FromRgb(25, 48, 92), Color.FromRgb(0, 100, 200), Color.FromRgb(38, 68, 125),
            Color.FromRgb(230, 170, 50), Color.FromRgb(30, 110, 240));

        public static readonly ThemeColors Cyberpunk = new(
            "Cyberpunk (Dark)",
            Color.FromRgb(15, 15, 25), Color.FromRgb(22, 22, 38), Color.FromRgb(40, 20, 60),
            Color.FromRgb(0, 255, 204), Color.FromRgb(180, 100, 255), Color.FromRgb(255, 0, 127),
            Color.FromRgb(28, 28, 48), Color.FromRgb(255, 0, 127), Color.FromRgb(45, 30, 70),
            Color.FromRgb(65, 40, 100), Color.FromRgb(255, 0, 127));

        public static readonly ThemeColors Matrix = new(
            "Matrix (Dark)",
            Color.FromRgb(5, 15, 5), Color.FromRgb(10, 25, 10), Color.FromRgb(20, 50, 20),
            Color.FromRgb(0, 255, 65), Color.FromRgb(100, 180, 100), Color.FromRgb(50, 200, 50),
            Color.FromRgb(15, 35, 15), Color.FromRgb(0, 255, 65), Color.FromRgb(20, 45, 20),
            Color.FromRgb(30, 70, 30), Color.FromRgb(0, 255, 65));

        public static readonly ThemeColors NordicNight = new(
            "Nordic Night (Dark)",
            Color.FromRgb(35, 39, 46), Color.FromRgb(40, 44, 52), Color.FromRgb(53, 59, 72),
            Color.FromRgb(171, 178, 191), Color.FromRgb(120, 130, 145), Color.FromRgb(97, 175, 239),
            Color.FromRgb(47, 52, 63), Color.FromRgb(97, 175, 239), Color.FromRgb(58, 63, 75),
            Color.FromRgb(75, 82, 98), Color.FromRgb(97, 175, 239));

        public static readonly ThemeColors Windows7Aero = new(
            "Windows 7 Aero (Dark Glass)",
            Color.FromRgb(20, 34, 58), Color.FromRgb(28, 46, 74), Color.FromRgb(45, 75, 115),
            Color.FromRgb(225, 238, 250), Color.FromRgb(140, 180, 215), Color.FromRgb(110, 195, 245),
            Color.FromRgb(24, 40, 65), Color.FromRgb(70, 130, 190), Color.FromRgb(35, 60, 95),
            Color.FromRgb(50, 85, 130), Color.FromRgb(60, 160, 235));

        public static readonly ThemeColors Windows98Retro = new(
            "Windows 98 Retro (Dark)",
            Color.FromRgb(30, 40, 40), Color.FromRgb(38, 50, 50), Color.FromRgb(52, 90, 88),
            Color.FromRgb(220, 235, 230), Color.FromRgb(140, 175, 170), Color.FromRgb(90, 200, 190),
            Color.FromRgb(34, 46, 46), Color.FromRgb(60, 140, 130), Color.FromRgb(45, 70, 68),
            Color.FromRgb(60, 95, 90), Color.FromRgb(0, 180, 160));

        public static readonly ThemeColors MacOsMonterey = new(
            "macOS Monterey (Dark)",
            Color.FromRgb(28, 28, 32), Color.FromRgb(36, 36, 40), Color.FromRgb(52, 52, 58),
            Color.FromRgb(235, 235, 240), Color.FromRgb(160, 160, 168), Color.FromRgb(10, 132, 255),
            Color.FromRgb(40, 40, 45), Color.FromRgb(70, 70, 78), Color.FromRgb(50, 50, 56),
            Color.FromRgb(65, 65, 72), Color.FromRgb(10, 132, 255));

        public static readonly ThemeColors SolarizedDark = new(
            "Solarized Dark",
            Color.FromRgb(0, 43, 54), Color.FromRgb(7, 54, 66), Color.FromRgb(25, 80, 95),
            Color.FromRgb(147, 161, 161), Color.FromRgb(88, 110, 117), Color.FromRgb(181, 137, 0),
            Color.FromRgb(7, 54, 66), Color.FromRgb(38, 139, 210), Color.FromRgb(15, 65, 78),
            Color.FromRgb(25, 80, 95), Color.FromRgb(42, 161, 152));

        public static readonly ThemeColors GitHubDark = new(
            "GitHub Dark",
            Color.FromRgb(13, 17, 23), Color.FromRgb(22, 27, 34), Color.FromRgb(33, 38, 45),
            Color.FromRgb(230, 237, 243), Color.FromRgb(139, 148, 158), Color.FromRgb(63, 185, 80),
            Color.FromRgb(22, 27, 34), Color.FromRgb(48, 54, 61), Color.FromRgb(33, 38, 45),
            Color.FromRgb(48, 54, 61), Color.FromRgb(63, 185, 80));

        public static readonly ThemeColors VsCodeDarkPlus = new(
            "VS Code Dark+",
            Color.FromRgb(30, 30, 30), Color.FromRgb(37, 37, 38), Color.FromRgb(51, 51, 51),
            Color.FromRgb(212, 212, 212), Color.FromRgb(128, 128, 128), Color.FromRgb(206, 145, 120),
            Color.FromRgb(37, 37, 38), Color.FromRgb(60, 60, 60), Color.FromRgb(45, 45, 48),
            Color.FromRgb(62, 62, 66), Color.FromRgb(206, 145, 120));

        public static readonly ThemeColors TerminalDosGreen = new(
            "Terminal DOS Green",
            Color.FromRgb(0, 0, 0), Color.FromRgb(5, 10, 5), Color.FromRgb(10, 25, 10),
            Color.FromRgb(51, 255, 51), Color.FromRgb(20, 150, 20), Color.FromRgb(51, 255, 51),
            Color.FromRgb(0, 10, 0), Color.FromRgb(0, 120, 0), Color.FromRgb(0, 20, 0),
            Color.FromRgb(0, 40, 0), Color.FromRgb(51, 255, 51));

        public static readonly ThemeColors DiscordBlurple = new(
            "Discord Blurple",
            Color.FromRgb(30, 31, 34), Color.FromRgb(43, 45, 49), Color.FromRgb(49, 51, 56),
            Color.FromRgb(242, 243, 245), Color.FromRgb(148, 155, 164), Color.FromRgb(88, 101, 242),
            Color.FromRgb(43, 45, 49), Color.FromRgb(30, 31, 34), Color.FromRgb(49, 51, 56),
            Color.FromRgb(66, 69, 77), Color.FromRgb(88, 101, 242));

        public static readonly ThemeColors SteamDeckDark = new(
            "Steam Deck Dark",
            Color.FromRgb(9, 14, 20), Color.FromRgb(18, 26, 36), Color.FromRgb(25, 36, 48),
            Color.FromRgb(199, 214, 224), Color.FromRgb(107, 133, 153), Color.FromRgb(160, 213, 80),
            Color.FromRgb(18, 26, 36), Color.FromRgb(42, 56, 74), Color.FromRgb(22, 32, 44),
            Color.FromRgb(37, 50, 64), Color.FromRgb(160, 213, 80));

        public static readonly ThemeColors RgbGamingRig = new(
            "RGB Gaming Rig",
            Color.FromRgb(8, 8, 10), Color.FromRgb(14, 14, 18), Color.FromRgb(24, 18, 30),
            Color.FromRgb(240, 240, 255), Color.FromRgb(150, 150, 170), Color.FromRgb(255, 0, 150),
            Color.FromRgb(14, 14, 18), Color.FromRgb(130, 0, 255), Color.FromRgb(18, 14, 24),
            Color.FromRgb(35, 20, 45), Color.FromRgb(255, 0, 150));

        public static readonly ThemeColors CircuitBoardPcb = new(
            "Circuit Board PCB",
            Color.FromRgb(6, 20, 10), Color.FromRgb(10, 30, 15), Color.FromRgb(15, 45, 22),
            Color.FromRgb(212, 175, 55), Color.FromRgb(90, 150, 100), Color.FromRgb(212, 175, 55),
            Color.FromRgb(8, 26, 13), Color.FromRgb(60, 140, 70), Color.FromRgb(12, 35, 18),
            Color.FromRgb(20, 55, 28), Color.FromRgb(212, 175, 55));

        public static readonly ThemeColors SynthwaveOutrun = new(
            "Synthwave Outrun",
            Color.FromRgb(30, 10, 50), Color.FromRgb(45, 15, 70), Color.FromRgb(65, 20, 95),
            Color.FromRgb(255, 225, 255), Color.FromRgb(200, 120, 220), Color.FromRgb(0, 255, 245),
            Color.FromRgb(40, 12, 62), Color.FromRgb(255, 46, 151), Color.FromRgb(50, 18, 75),
            Color.FromRgb(70, 25, 100), Color.FromRgb(255, 46, 151));

        public static readonly ThemeColors Windows11FluentDark = new(
            "Windows 11 Fluent",
            Color.FromRgb(32, 32, 32), Color.FromRgb(40, 40, 40), Color.FromRgb(50, 50, 50),
            Color.FromRgb(255, 255, 255), Color.FromRgb(180, 180, 180), Color.FromRgb(0, 120, 212),
            Color.FromRgb(40, 40, 40), Color.FromRgb(60, 60, 60), Color.FromRgb(45, 45, 45),
            Color.FromRgb(60, 60, 60), Color.FromRgb(0, 120, 212));

        public static readonly ThemeColors Windows11FluentLight = new(
            "Windows 11 Fluent (Light)",
            Color.FromRgb(243, 243, 243), Color.FromRgb(255, 255, 255), Color.FromRgb(235, 235, 235),
            Color.FromRgb(27, 27, 27), Color.FromRgb(96, 96, 96), Color.FromRgb(0, 120, 212),
            Color.FromRgb(255, 255, 255), Color.FromRgb(220, 220, 220), Color.FromRgb(240, 240, 240),
            Color.FromRgb(225, 225, 225), Color.FromRgb(0, 120, 212));

        public static readonly ThemeColors RetroDosBlue = new(
            "Retro DOS Blue",
            Color.FromRgb(0, 0, 170), Color.FromRgb(0, 0, 140), Color.FromRgb(0, 0, 200),
            Color.FromRgb(255, 255, 85), Color.FromRgb(170, 170, 255), Color.FromRgb(255, 255, 255),
            Color.FromRgb(0, 0, 150), Color.FromRgb(255, 255, 255), Color.FromRgb(0, 0, 120),
            Color.FromRgb(0, 0, 180), Color.FromRgb(255, 255, 85));

        public static readonly ThemeColors MicrosoftPcManager = new(
            "Microsoft PC Manager",
            Color.FromRgb(18, 18, 20), Color.FromRgb(26, 27, 30), Color.FromRgb(38, 40, 45),
            Color.FromRgb(240, 240, 242), Color.FromRgb(150, 152, 158), Color.FromRgb(66, 165, 245),
            Color.FromRgb(28, 29, 33), Color.FromRgb(45, 47, 52), Color.FromRgb(36, 38, 43),
            Color.FromRgb(48, 50, 56), Color.FromRgb(41, 151, 255));

        // Display order matches $themeNamesList in Optimizer.ps1 (ComboTheme items) - "Windows 11
        // Fluent" listed once here; the separate Light Mode toggle swaps it to Windows11FluentLight.
        public static readonly IReadOnlyList<ThemeColors> All = new[]
        {
            WindowsVista, Office2007, WindowsXpLuna, Cyberpunk, Matrix, NordicNight, Windows7Aero,
            Windows98Retro, MacOsMonterey, SolarizedDark, GitHubDark, VsCodeDarkPlus,
            TerminalDosGreen, DiscordBlurple, SteamDeckDark, RgbGamingRig, CircuitBoardPcb,
            SynthwaveOutrun, Windows11FluentDark, RetroDosBlue, MicrosoftPcManager,
        };
    }
}
