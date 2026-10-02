using System.Collections.Generic;

namespace OptimizerWpf
{
    // Port του $themeNamesList → BgStyle mapping μέσα στο Get-ThemeColors (Optimizer.ps1 ~4651-5506,
    // κάθε theme block έχει ΤΟ ΙΔΙΟ BgStyle σε Dark ΚΑΙ Light) - ίδια αντιστοίχιση ονόματος θέματος →
    // στυλ κινούμενου φόντου, χρησιμοποιείται από το ThemedBackgroundControl.
    public static class ThemeBackgroundStyles
    {
        private static readonly Dictionary<string, string> Map = new()
        {
            ["Windows Vista"] = "VistaAurora",
            ["Office 2007 (Aurora)"] = "OfficeGradient",
            ["Windows XP Luna"] = "XPHills",
            ["Cyberpunk"] = "CyberGrid",
            ["Matrix"] = "MatrixRain",
            ["Nordic Night"] = "NordicWaves",
            ["Windows 7 Aero"] = "Aero7Glass",
            ["Windows 98 Retro"] = "Retro98Teal",
            ["macOS Monterey"] = "MacGradient",
            ["Solarized Dark"] = "SolarizedFlat",
            ["GitHub Dark"] = "GitHubCommits",
            ["VS Code Dark+"] = "VsCodeLines",
            ["Terminal DOS Green"] = "TerminalScan",
            ["Discord Blurple"] = "DiscordBubbles",
            ["Steam Deck Dark"] = "SteamDeckPulse",
            ["RGB Gaming Rig"] = "RgbRainbow",
            ["Circuit Board PCB"] = "PcbTraces",
            ["Synthwave Outrun"] = "SynthwaveSun",
            ["Windows 11 Fluent"] = "FluentAcrylic",
            ["Retro DOS Blue"] = "DosBlueBlocks",
            ["Microsoft PC Manager"] = "StandardDark",
            ["Windows 11 Settings"] = "StandardDark",
            ["Office Ribbon"] = "StandardDark",
            ["Gaming Hub"] = "GridPulse",
        };

        public static string For(string themeDisplayName) => Map.TryGetValue(themeDisplayName, out var style) ? style : "StandardDark";
    }
}
