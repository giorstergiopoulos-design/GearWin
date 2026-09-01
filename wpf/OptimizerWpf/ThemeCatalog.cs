using System.Collections.Generic;
using System.Windows.Media;

namespace OptimizerWpf
{
    // Direct port of every theme branch in Get-ThemeColors (Optimizer.ps1, ~line 4651, both the
    // $isDarkMode-true and -false switch blocks) - same names, same RGB values. Every theme has a
    // real Dark AND Light palette (Get-ThemeColors branches on isDarkMode first, theme name
    // second, for all 21) - earlier revision of this file only had Light data for Windows 11
    // Fluent; that was wrong, corrected here per user report.
    public static class ThemeCatalog
    {
        private static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

        public static readonly ThemePair WindowsVista = new("Windows Vista",
            new("Windows Vista (Οθόνη Υποδοχής Aero Black)",
                C(10, 14, 20), C(16, 22, 30), C(28, 42, 58), C(225, 240, 250), C(135, 175, 205),
                C(110, 190, 220), C(14, 20, 28), C(45, 110, 150), C(24, 40, 55), C(38, 65, 88), C(0, 160, 220)),
            new("Windows Vista (Aero Light Blue)",
                C(190, 220, 240), C(222, 238, 250), C(255, 255, 255), C(10, 45, 75), C(35, 85, 120),
                C(25, 75, 110), C(205, 230, 245), C(110, 165, 200), C(212, 233, 247), C(235, 247, 253), C(0, 120, 180)));

        public static readonly ThemePair Office2007 = new("Office 2007 (Aurora)",
            new("Office 2007 (Dark Aurora)",
                C(28, 38, 52), C(38, 50, 68), C(55, 72, 98), C(225, 238, 255), C(155, 185, 220),
                C(140, 180, 220), C(33, 45, 60), C(70, 100, 140), C(48, 65, 90), C(215, 180, 60), C(80, 140, 200)),
            new("Office 2007 (Aurora Light)",
                C(198, 211, 229), C(227, 237, 247), C(255, 255, 255), C(21, 66, 139), C(60, 100, 150),
                C(40, 80, 130), C(214, 228, 242), C(151, 180, 216), C(226, 236, 247), C(255, 242, 157), C(64, 115, 162)));

        public static readonly ThemePair WindowsXpLuna = new("Windows XP Luna",
            new("Windows XP Luna (Dark)",
                C(20, 40, 80), C(30, 55, 105), C(45, 75, 140), C(235, 243, 255), C(160, 195, 240),
                C(180, 210, 255), C(25, 48, 92), C(0, 100, 200), C(38, 68, 125), C(230, 170, 50), C(30, 110, 240)),
            new("Windows XP Luna (Light)",
                C(83, 114, 176), C(222, 231, 247), C(255, 255, 255), C(0, 45, 150), C(50, 90, 160),
                C(230, 240, 255), C(204, 218, 240), C(0, 60, 140), C(226, 237, 252), C(255, 204, 102), C(0, 84, 227)));

        public static readonly ThemePair Cyberpunk = new("Cyberpunk",
            new("Cyberpunk (Dark)",
                C(15, 15, 25), C(22, 22, 38), C(40, 20, 60), C(0, 255, 204), C(180, 100, 255),
                C(255, 0, 127), C(28, 28, 48), C(255, 0, 127), C(45, 30, 70), C(65, 40, 100), C(255, 0, 127)),
            new("Cyberpunk (Light)",
                C(220, 220, 235), C(240, 240, 250), C(255, 255, 255), C(20, 20, 40), C(100, 20, 150),
                C(200, 0, 100), C(230, 230, 245), C(200, 0, 100), C(210, 210, 230), C(255, 200, 220), C(200, 0, 100)));

        public static readonly ThemePair Matrix = new("Matrix",
            new("Matrix (Dark)",
                C(5, 15, 5), C(10, 25, 10), C(20, 50, 20), C(0, 255, 65), C(100, 180, 100),
                C(50, 200, 50), C(15, 35, 15), C(0, 255, 65), C(20, 45, 20), C(30, 70, 30), C(0, 255, 65)),
            new("Matrix (Light)",
                C(200, 230, 200), C(225, 245, 225), C(255, 255, 255), C(0, 80, 20), C(20, 120, 40),
                C(0, 100, 30), C(210, 240, 210), C(0, 150, 40), C(190, 225, 190), C(160, 215, 160), C(0, 150, 40)));

        public static readonly ThemePair NordicNight = new("Nordic Night",
            new("Nordic Night (Dark)",
                C(35, 39, 46), C(40, 44, 52), C(53, 59, 72), C(171, 178, 191), C(120, 130, 145),
                C(97, 175, 239), C(47, 52, 63), C(97, 175, 239), C(58, 63, 75), C(75, 82, 98), C(97, 175, 239)),
            new("Nordic Night (Light)",
                C(220, 225, 232), C(238, 242, 246), C(255, 255, 255), C(40, 45, 55), C(90, 100, 115),
                C(30, 110, 190), C(230, 235, 242), C(30, 110, 190), C(210, 218, 228), C(190, 202, 218), C(30, 110, 190)));

        public static readonly ThemePair Windows7Aero = new("Windows 7 Aero",
            new("Windows 7 Aero (Dark Glass)",
                C(20, 34, 58), C(28, 46, 74), C(45, 75, 115), C(225, 238, 250), C(140, 180, 215),
                C(110, 195, 245), C(24, 40, 65), C(70, 130, 190), C(35, 60, 95), C(50, 85, 130), C(60, 160, 235)),
            new("Windows 7 Aero (Light Glass)",
                C(205, 225, 245), C(225, 238, 250), C(255, 255, 255), C(15, 40, 70), C(45, 90, 130),
                C(20, 100, 170), C(215, 232, 248), C(90, 150, 205), C(200, 222, 245), C(225, 240, 253), C(20, 120, 200)));

        public static readonly ThemePair Windows98Retro = new("Windows 98 Retro",
            new("Windows 98 Retro (Dark)",
                C(30, 40, 40), C(38, 50, 50), C(52, 90, 88), C(220, 235, 230), C(140, 175, 170),
                C(90, 200, 190), C(34, 46, 46), C(60, 140, 130), C(45, 70, 68), C(60, 95, 90), C(0, 180, 160)),
            new("Windows 98 Retro (Light)",
                C(195, 210, 205), C(215, 228, 224), C(255, 255, 255), C(20, 45, 42), C(50, 90, 85),
                C(0, 110, 100), C(205, 220, 216), C(70, 140, 130), C(190, 210, 205), C(215, 232, 227), C(0, 130, 115)));

        public static readonly ThemePair MacOsMonterey = new("macOS Monterey",
            new("macOS Monterey (Dark)",
                C(28, 28, 32), C(36, 36, 40), C(52, 52, 58), C(235, 235, 240), C(160, 160, 168),
                C(10, 132, 255), C(40, 40, 45), C(70, 70, 78), C(50, 50, 56), C(65, 65, 72), C(10, 132, 255)),
            new("macOS Monterey (Light)",
                C(246, 246, 248), C(255, 255, 255), C(233, 233, 238), C(28, 28, 32), C(110, 110, 118),
                C(0, 110, 230), C(255, 255, 255), C(220, 220, 226), C(236, 236, 240), C(222, 222, 228), C(0, 110, 230)));

        public static readonly ThemePair SolarizedDark = new("Solarized Dark",
            new("Solarized Dark",
                C(0, 43, 54), C(7, 54, 66), C(25, 80, 95), C(147, 161, 161), C(88, 110, 117),
                C(181, 137, 0), C(7, 54, 66), C(38, 139, 210), C(15, 65, 78), C(25, 80, 95), C(42, 161, 152)),
            new("Solarized Light",
                C(253, 246, 227), C(238, 232, 213), C(255, 255, 255), C(101, 123, 131), C(147, 161, 161),
                C(181, 137, 0), C(238, 232, 213), C(38, 139, 210), C(225, 218, 196), C(213, 205, 180), C(42, 161, 152)));

        public static readonly ThemePair GitHubDark = new("GitHub Dark",
            new("GitHub Dark",
                C(13, 17, 23), C(22, 27, 34), C(33, 38, 45), C(230, 237, 243), C(139, 148, 158),
                C(63, 185, 80), C(22, 27, 34), C(48, 54, 61), C(33, 38, 45), C(48, 54, 61), C(63, 185, 80)),
            new("GitHub Light",
                C(255, 255, 255), C(246, 248, 250), C(234, 238, 242), C(31, 35, 40), C(101, 109, 118),
                C(26, 127, 55), C(246, 248, 250), C(208, 215, 222), C(238, 241, 244), C(220, 225, 230), C(26, 127, 55)));

        public static readonly ThemePair VsCodeDarkPlus = new("VS Code Dark+",
            new("VS Code Dark+",
                C(30, 30, 30), C(37, 37, 38), C(51, 51, 51), C(212, 212, 212), C(128, 128, 128),
                C(206, 145, 120), C(37, 37, 38), C(60, 60, 60), C(45, 45, 48), C(62, 62, 66), C(206, 145, 120)),
            new("VS Code Light+",
                C(255, 255, 255), C(243, 243, 243), C(230, 230, 230), C(30, 30, 30), C(110, 110, 110),
                C(196, 110, 50), C(243, 243, 243), C(215, 215, 215), C(230, 230, 230), C(214, 214, 214), C(196, 110, 50)));

        public static readonly ThemePair TerminalDosGreen = new("Terminal DOS Green",
            new("Terminal DOS Green",
                C(0, 0, 0), C(5, 10, 5), C(10, 25, 10), C(51, 255, 51), C(20, 150, 20),
                C(51, 255, 51), C(0, 10, 0), C(0, 120, 0), C(0, 20, 0), C(0, 40, 0), C(51, 255, 51)),
            new("Terminal Amber (Light)",
                C(250, 247, 235), C(240, 235, 215), C(230, 220, 190), C(140, 90, 10), C(170, 130, 40),
                C(180, 110, 10), C(245, 240, 222), C(200, 160, 60), C(235, 225, 200), C(220, 205, 170), C(180, 110, 10)));

        public static readonly ThemePair DiscordBlurple = new("Discord Blurple",
            new("Discord Blurple",
                C(30, 31, 34), C(43, 45, 49), C(49, 51, 56), C(242, 243, 245), C(148, 155, 164),
                C(88, 101, 242), C(43, 45, 49), C(30, 31, 34), C(49, 51, 56), C(66, 69, 77), C(88, 101, 242)),
            new("Discord Light",
                C(255, 255, 255), C(242, 243, 245), C(235, 236, 240), C(6, 6, 7), C(116, 127, 141),
                C(88, 101, 242), C(242, 243, 245), C(225, 226, 230), C(235, 236, 240), C(220, 222, 228), C(88, 101, 242)));

        public static readonly ThemePair SteamDeckDark = new("Steam Deck Dark",
            new("Steam Deck Dark",
                C(9, 14, 20), C(18, 26, 36), C(25, 36, 48), C(199, 214, 224), C(107, 133, 153),
                C(160, 213, 80), C(18, 26, 36), C(42, 56, 74), C(22, 32, 44), C(37, 50, 64), C(160, 213, 80)),
            new("Steam Light",
                C(240, 245, 248), C(255, 255, 255), C(220, 240, 205), C(27, 40, 56), C(90, 110, 125),
                C(95, 140, 40), C(255, 255, 255), C(200, 215, 225), C(228, 240, 210), C(210, 230, 185), C(95, 140, 40)));

        public static readonly ThemePair RgbGamingRig = new("RGB Gaming Rig",
            new("RGB Gaming Rig",
                C(8, 8, 10), C(14, 14, 18), C(24, 18, 30), C(240, 240, 255), C(150, 150, 170),
                C(255, 0, 150), C(14, 14, 18), C(130, 0, 255), C(18, 14, 24), C(35, 20, 45), C(255, 0, 150)),
            new("RGB Gaming (Light)",
                C(248, 248, 250), C(255, 255, 255), C(240, 235, 250), C(30, 20, 40), C(120, 90, 140),
                C(200, 0, 120), C(255, 255, 255), C(200, 150, 220), C(240, 230, 248), C(225, 205, 242), C(200, 0, 120)));

        public static readonly ThemePair CircuitBoardPcb = new("Circuit Board PCB",
            new("Circuit Board PCB",
                C(6, 20, 10), C(10, 30, 15), C(15, 45, 22), C(212, 175, 55), C(90, 150, 100),
                C(212, 175, 55), C(8, 26, 13), C(60, 140, 70), C(12, 35, 18), C(20, 55, 28), C(212, 175, 55)),
            new("Circuit Board (Light)",
                C(240, 248, 240), C(255, 255, 255), C(220, 240, 220), C(30, 70, 35), C(90, 130, 95),
                C(150, 110, 20), C(255, 255, 255), C(150, 190, 155), C(225, 242, 225), C(205, 232, 208), C(150, 110, 20)));

        public static readonly ThemePair SynthwaveOutrun = new("Synthwave Outrun",
            new("Synthwave Outrun",
                C(30, 10, 50), C(45, 15, 70), C(65, 20, 95), C(255, 225, 255), C(200, 120, 220),
                C(0, 255, 245), C(40, 12, 62), C(255, 46, 151), C(50, 18, 75), C(70, 25, 100), C(255, 46, 151)),
            new("Synthwave (Light)",
                C(252, 240, 250), C(255, 255, 255), C(245, 220, 245), C(90, 20, 90), C(170, 90, 170),
                C(0, 180, 175), C(255, 255, 255), C(255, 150, 200), C(248, 225, 248), C(240, 200, 240), C(220, 20, 140)));

        public static readonly ThemePair Windows11Fluent = new("Windows 11 Fluent",
            new("Windows 11 Fluent",
                C(32, 32, 32), C(40, 40, 40), C(50, 50, 50), C(255, 255, 255), C(180, 180, 180),
                C(0, 120, 212), C(40, 40, 40), C(60, 60, 60), C(45, 45, 45), C(60, 60, 60), C(0, 120, 212)),
            new("Windows 11 Fluent (Light)",
                C(243, 243, 243), C(255, 255, 255), C(235, 235, 235), C(27, 27, 27), C(96, 96, 96),
                C(0, 120, 212), C(255, 255, 255), C(220, 220, 220), C(240, 240, 240), C(225, 225, 225), C(0, 120, 212)));

        public static readonly ThemePair RetroDosBlue = new("Retro DOS Blue",
            new("Retro DOS Blue",
                C(0, 0, 170), C(0, 0, 140), C(0, 0, 200), C(255, 255, 85), C(170, 170, 255),
                C(255, 255, 255), C(0, 0, 150), C(255, 255, 255), C(0, 0, 120), C(0, 0, 180), C(255, 255, 85)),
            new("Retro DOS (Light Grey)",
                C(192, 192, 192), C(220, 220, 220), C(230, 230, 230), C(0, 0, 140), C(80, 80, 120),
                C(0, 0, 170), C(200, 200, 200), C(0, 0, 140), C(210, 210, 210), C(225, 225, 225), C(0, 0, 170)));

        public static readonly ThemePair MicrosoftPcManager = new("Microsoft PC Manager",
            new("Microsoft PC Manager",
                C(18, 18, 20), C(26, 27, 30), C(38, 40, 45), C(240, 240, 242), C(150, 152, 158),
                C(66, 165, 245), C(28, 29, 33), C(45, 47, 52), C(36, 38, 43), C(48, 50, 56), C(41, 151, 255)),
            new("Microsoft PC Manager",
                C(253, 254, 255), C(230, 243, 253), C(255, 255, 255), C(32, 33, 36), C(96, 96, 100),
                C(0, 95, 184), C(255, 255, 255), C(225, 229, 235), C(240, 242, 245), C(225, 230, 238), C(0, 95, 184)));

        // Keeps direct access for code that only ever wants the dark palette (e.g. the app's
        // startup default) without going through the Dark/Light pair.
        public static ThemeColors Windows11FluentDark => Windows11Fluent.Dark;
        public static ThemeColors Windows11FluentLight => Windows11Fluent.Light;

        // Display order matches $themeNamesList in Optimizer.ps1 (ComboTheme items).
        public static readonly IReadOnlyList<ThemePair> All = new[]
        {
            WindowsVista, Office2007, WindowsXpLuna, Cyberpunk, Matrix, NordicNight, Windows7Aero,
            Windows98Retro, MacOsMonterey, SolarizedDark, GitHubDark, VsCodeDarkPlus,
            TerminalDosGreen, DiscordBlurple, SteamDeckDark, RgbGamingRig, CircuitBoardPcb,
            SynthwaveOutrun, Windows11Fluent, RetroDosBlue, MicrosoftPcManager,
        };
    }
}
