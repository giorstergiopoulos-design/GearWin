using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace OptimizerWpf.Services
{
    // ΝΕΟ - ρητό αίτημα χρήστη: "η εξαγωγή των πληροφοριών συστήματος να γίνεται σε αρχείο PDF" (πριν
    // ήταν απλό .txt - βλ. SystemView.xaml.cs's BtnExportDevice_Click). Το PDFsharp (empira, MIT από
    // την v6, χωρίς εξωτερικό PDF εκτυπωτή/driver) χρησιμοποιεί ΔΙΚΟ ΤΟΥ font resolver από την v6 και
    // πάνω (δεν "βλέπει" αυτόματα τις γραμματοσειρές των Windows πια) - το WindowsFontResolver
    // παρακάτω διαβάζει απευθείας τα πραγματικά αρχεία .ttf από C:\Windows\Fonts, επιβεβαιωμένο με
    // ζωντανό test build ότι λειτουργεί (παράγει έγκυρο PDF 1.7).
    public static class PdfExportService
    {
        private static bool _resolverRegistered;

        private static void EnsureFontResolver()
        {
            if (_resolverRegistered) return;
            GlobalFontSettings.FontResolver = new WindowsFontResolver();
            _resolverRegistered = true;
        }

        // Δέχεται ΤΟ ΙΔΙΟ format που ήδη παράγουν οι υπάρχουσες "εξαγωγή σε .txt" μέθοδοι - γραμμές
        // "=== Τίτλος Ενότητας ===" σηματοδοτούν νέα ενότητα (έντονα, με λίγο επιπλέον κενό από πάνω),
        // κενές γραμμές γίνονται μικρό κενό, οτιδήποτε άλλο είναι απλό κείμενο (με αναδίπλωση αν δεν
        // χωράει σε μία γραμμή). Σελιδοποίηση αυτόματη όταν το περιεχόμενο ξεπερνά τη σελίδα.
        public static void ExportTextReport(string path, string title, string content)
        {
            EnsureFontResolver();

            var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);

            var fontTitle = new XFont("Arial", 16, XFontStyleEx.Bold);
            var fontHeader = new XFont("Arial", 12, XFontStyleEx.Bold);
            var fontBody = new XFont("Arial", 9.5, XFontStyleEx.Regular);

            const double margin = 40;
            double pageWidth = page.Width.Point;
            double pageHeight = page.Height.Point;
            double contentWidth = pageWidth - margin * 2;
            double y = margin;

            void NewPageIfNeeded(double neededHeight)
            {
                if (y + neededHeight <= pageHeight - margin) return;
                page = document.AddPage();
                gfx = XGraphics.FromPdfPage(page);
                y = margin;
            }

            void DrawLine(string text, XFont font, double lineHeight, double extraTopGap = 0)
            {
                foreach (var wrapped in WrapText(gfx, text, font, contentWidth))
                {
                    NewPageIfNeeded(lineHeight + extraTopGap);
                    y += extraTopGap;
                    gfx.DrawString(wrapped, font, XBrushes.Black, new XRect(margin, y, contentWidth, lineHeight), XStringFormats.TopLeft);
                    y += lineHeight;
                    extraTopGap = 0;
                }
            }

            NewPageIfNeeded(28);
            gfx.DrawString(title, fontTitle, XBrushes.Black, new XRect(margin, y, contentWidth, 24), XStringFormats.TopLeft);
            y += 30;

            foreach (var rawLine in content.Replace("\r\n", "\n").Split('\n'))
            {
                var line = rawLine.TrimEnd();
                if (line.StartsWith("=== ") && line.EndsWith(" ==="))
                {
                    var header = line[4..^4];
                    DrawLine(header, fontHeader, 18, extraTopGap: 10);
                }
                else if (string.IsNullOrWhiteSpace(line))
                {
                    NewPageIfNeeded(6);
                    y += 6;
                }
                else
                {
                    DrawLine(line, fontBody, 14);
                }
            }

            document.Save(path);
        }

        // Απλή αναδίπλωση λέξη-προς-λέξη με βάση το πραγματικό μετρημένο πλάτος (XGraphics.
        // MeasureString) - όχι απλή διαίρεση σε σταθερό αριθμό χαρακτήρων, ώστε να δουλεύει σωστά ΚΑΙ
        // με αναλογικές γραμματοσειρές ΚΑΙ με ελληνικό/λατινικό κείμενο ανάμεικτα.
        private static IEnumerable<string> WrapText(XGraphics gfx, string text, XFont font, double maxWidth)
        {
            var words = text.Split(' ');
            var line = "";
            foreach (var word in words)
            {
                var candidate = line.Length == 0 ? word : $"{line} {word}";
                if (gfx.MeasureString(candidate, font).Width > maxWidth && line.Length > 0)
                {
                    yield return line;
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }
            if (line.Length > 0) yield return line;
        }

        private sealed class WindowsFontResolver : IFontResolver
        {
            private static readonly string FontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

            public byte[] GetFont(string faceName)
            {
                var fileName = faceName switch
                {
                    "Arial#b" => "arialbd.ttf",
                    "Arial#i" => "ariali.ttf",
                    "Arial#bi" => "arialbi.ttf",
                    _ => "arial.ttf",
                };
                return File.ReadAllBytes(Path.Combine(FontsDir, fileName));
            }

            public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
            {
                var key = (isBold, isItalic) switch
                {
                    (true, true) => "Arial#bi",
                    (true, false) => "Arial#b",
                    (false, true) => "Arial#i",
                    _ => "Arial",
                };
                return new FontResolverInfo(key);
            }
        }
    }
}
