using System.Collections.Generic;

namespace OptimizerWpf.Services
{
    public record SuggestedFont(string Name, string Category, string License, string Url);

    // REQ-580-02: curated list of well-known, free/open-license fonts. Links to each font's official
    // Google Fonts page rather than hosting/redistributing the font files ourselves - Google Fonts only
    // hosts open-license fonts (OFL/Apache 2.0), so licensing is respected automatically by pointing
    // there instead of us bundling and having to track each font's license ourselves.
    public static class FontSuggestionService
    {
        public static IReadOnlyList<SuggestedFont> SuggestedFonts => new[]
        {
            new SuggestedFont("Inter", "Sans-serif", "OFL", "https://fonts.google.com/specimen/Inter"),
            new SuggestedFont("Roboto", "Sans-serif", "Apache 2.0", "https://fonts.google.com/specimen/Roboto"),
            new SuggestedFont("Open Sans", "Sans-serif", "OFL", "https://fonts.google.com/specimen/Open+Sans"),
            new SuggestedFont("Montserrat", "Sans-serif", "OFL", "https://fonts.google.com/specimen/Montserrat"),
            new SuggestedFont("Poppins", "Sans-serif", "OFL", "https://fonts.google.com/specimen/Poppins"),
            new SuggestedFont("Lato", "Sans-serif", "OFL", "https://fonts.google.com/specimen/Lato"),
            new SuggestedFont("Nunito", "Sans-serif", "OFL", "https://fonts.google.com/specimen/Nunito"),
            new SuggestedFont("Source Sans 3", "Sans-serif", "OFL", "https://fonts.google.com/specimen/Source+Sans+3"),
            new SuggestedFont("Merriweather", "Serif", "OFL", "https://fonts.google.com/specimen/Merriweather"),
            new SuggestedFont("Playfair Display", "Serif", "OFL", "https://fonts.google.com/specimen/Playfair+Display"),
            new SuggestedFont("JetBrains Mono", "Monospace", "OFL", "https://fonts.google.com/specimen/JetBrains+Mono"),
            new SuggestedFont("Fira Code", "Monospace", "OFL", "https://fonts.google.com/specimen/Fira+Code"),
        };
    }
}
