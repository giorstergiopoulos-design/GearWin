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
            ["GitHub Dark"] = "Aero7Glass",
            ["VS Code Dark+"] = "SolarizedFlat",
            ["Terminal DOS Green"] = "MatrixRain",
            ["Discord Blurple"] = "VistaAurora",
            ["Steam Deck Dark"] = "OfficeGradient",
            ["RGB Gaming Rig"] = "CyberGrid",
            ["Circuit Board PCB"] = "XPHills",
            ["Synthwave Outrun"] = "MacGradient",
            ["Windows 11 Fluent"] = "MacGradient",
            ["Retro DOS Blue"] = "Retro98Teal",
            ["Microsoft PC Manager"] = "StandardDark",
        };

        public static string For(string themeDisplayName) => Map.TryGetValue(themeDisplayName, out var style) ? style : "StandardDark";
    }
}
