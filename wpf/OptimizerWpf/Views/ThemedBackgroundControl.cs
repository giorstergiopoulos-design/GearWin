using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace OptimizerWpf.Views
{
    // WPF-equivalent port του New-ThemedBackgroundBitmap (Optimizer.ps1 ~5908) - κάθε BgStyle έχει το
    // δικό του ελαφρύ σχέδιο, με έναν κυκλικό "Phase" (0-360) για προαιρετική αργή κίνηση, ίδια λογική
    // με το πρωτότυπο. ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: τα σχήματα είναι απλοποιημένα WPF ισοδύναμα (DrawingContext
    // αντί για GDI+ Graphics), ΟΧΙ pixel-perfect αναπαραγωγή - ίδιο πνεύμα/χρώματα/κίνηση ανά στυλ.
    public class ThemedBackgroundControl : FrameworkElement
    {
        private string _style = "StandardDark";
        private bool _isDark = true;
        private bool _animated;
        private double _phase;
        private readonly DispatcherTimer _timer;

        // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε μέτρηση: "δες αν βαραίνει η εφαρμογή πολύ") - επιβεβαιώθηκε
        // ζωντανά ~10% CPU ΣΥΝΕΧΩΣ ακόμα και σε πλήρη αδράνεια, επειδή αυτό το Tick έτρεχε ασταμάτητα
        // ανεξάρτητα από το αν το παράθυρο ήταν ελαχιστοποιημένο/χωρίς focus. 60ms->120ms - τα στυλ
        // εδώ είναι όλα αργές, ομαλές κινήσεις (gradients/κύματα/τροχιές) - η διαφορά είναι οπτικά
        // αδιόρατη, το CPU κόστος περίπου μισό. Συνδυάζεται με Pause()/Resume() (βλ. ThemeManager.
        // AttachBackground) που σταματά ΤΕΛΕΙΩΣ το animation όποτε το παράθυρο δεν είναι ορατό/focused.
        public ThemedBackgroundControl()
        {
            IsHitTestVisible = false;
            _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(120) };
            _timer.Tick += (_, _) => { _phase = (_phase + 1.2) % 360; InvalidateVisual(); };
            Unloaded += (_, _) => _timer.Stop();
        }

        public void ApplyTheme(string style, bool isDark, bool animated)
        {
            _style = style;
            _isDark = isDark;
            _animated = animated;
            if (animated) _timer.Start(); else { _timer.Stop(); _phase = 0; }
            InvalidateVisual();
        }

        // Παύει/συνεχίζει το animation χωρίς να αγγίζει την επιλογή του χρήστη (_animated) - το
        // Resume() σέβεται ΠΑΝΤΑ αν ο χρήστης έχει απενεργοποιήσει τα κινούμενα φόντα καθόλου.
        public void Pause() => _timer.Stop();
        public void Resume() { if (_animated) _timer.Start(); }

        // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε: "τα κινούμενα φόντα να έχουν μέγεθος μέχρι και εκεί που τελειώνουν
        // οι καρτέλες") - το FrameworkElement.OnRender ΔΕΝ περικόπτει αυτόματα ό,τι ζωγραφίζεται εκτός
        // ορίων (σε αντίθεση με τα περισσότερα Panels) - στυλ όπως το Orbit (μεγάλες ελλείψεις με
        // ακτίνα ανάλογη του πλάτους w, βλ. DrawOrbit) ζωγράφιζαν τμήματά τους ΚΑΤΩ από τη λωρίδα
        // καρτελών, μέσα στην περιοχή περιεχομένου (επιβεβαιώθηκε ζωντανά: μπλε κουκκίδα ορατή κάτω
        // από τον τίτλο "Πληροφορίες Συστήματος"). PushClip στα ΑΚΡΙΒΗ όρια του control (0,0,w,h) -
        // ΚΑΝΕΝΑ στυλ δεν μπορεί πλέον να ζωγραφίσει έξω από την περιοχή menu+τίτλος+καρτέλες.
        protected override void OnRender(DrawingContext dc)
        {
            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0) return;
            var rad = _phase * Math.PI / 180.0;

            dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));
            switch (_style)
            {
                case "VistaAurora": DrawVistaAurora(dc, w, h, rad); break;
                case "OfficeGradient": DrawOfficeGradient(dc, w, h, rad); break;
                case "XPHills": DrawXpHills(dc, w, h); break;
                case "CyberGrid": DrawCyberGrid(dc, w, h); break;
                case "MatrixRain": DrawMatrixRain(dc, w, h); break;
                case "NordicWaves": DrawNordicWaves(dc, w, h, rad); break;
                case "Aero7Glass": DrawAero7Glass(dc, w, h, rad); break;
                case "Retro98Teal": DrawRetro98Teal(dc, w, h); break;
                case "MacGradient": DrawMacGradient(dc, w, h, rad); break;
                case "Waves": DrawNordicWaves(dc, w, h, rad); break;
                case "GridPulse": DrawCyberGrid(dc, w, h); break;
                case "Gears": DrawGears(dc, w, h, rad); break;
                case "Orbit": DrawOrbit(dc, w, h, rad); break;
                case "Particles": DrawParticles(dc, w, h, rad); break;
                case "SolarizedFlat": DrawSolarizedFlat(dc, w, h, rad); break;
                default: DrawStandard(dc, w, h); break;
            }
            dc.Pop();
        }

        private static SolidColorBrush B(byte a, byte r, byte g, byte b) => new(Color.FromArgb(a, r, g, b));
        private static LinearGradientBrush Grad(Color c1, Color c2, Point start, Point end) =>
            new(c1, c2, start, end) { MappingMode = BrushMappingMode.Absolute };

        private void DrawVistaAurora(DrawingContext dc, double w, double h, double rad)
        {
            var c1 = _isDark ? Color.FromRgb(18, 30, 45) : Color.FromRgb(210, 232, 248);
            var c2 = _isDark ? Color.FromRgb(8, 12, 18) : Color.FromRgb(165, 205, 235);
            dc.DrawRectangle(Grad(c1, c2, new Point(0, 0), new Point(w, h)), null, new Rect(0, 0, w, h));

            var (r, g, b) = _isDark ? (60, 170, 220) : (210, 235, 255);
            var pen = new Pen(B(50, (byte)r, (byte)g, (byte)b), Math.Max(30, h * 0.08)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var dy = Math.Sin(rad) * (h * 0.03);
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(-w * 0.04, h * 0.19 + dy), false, false);
                ctx.PolyBezierTo(new[] { new Point(w * 0.27, h * 0.06 - dy), new Point(w * 0.62, h * 0.25 + dy), new Point(w, h * 0.12 - dy) }, true, false);
            }
            dc.DrawGeometry(null, pen, geo);
        }

        private void DrawOfficeGradient(DrawingContext dc, double w, double h, double rad)
        {
            var c1 = _isDark ? Color.FromRgb(18, 28, 48) : Color.FromRgb(200, 218, 240);
            var c2 = _isDark ? Color.FromRgb(35, 50, 85) : Color.FromRgb(235, 240, 250);
            dc.DrawRectangle(Grad(c1, c2, new Point(0, 0), new Point(w, h)), null, new Rect(0, 0, w, h));

            // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "κάποια κινούμενα φόντα δεν εμφανίζονται σωστά ή πάρα πολύ
            // αχνά") - το Dark alpha ήταν 25/255 (~10% αδιαφάνεια), σχεδόν αόρατο. Εδώ και στα
            // υπόλοιπα στυλ παρακάτω: περίπου διπλασιασμός των πολύ χαμηλών alpha τιμών.
            byte alpha = (byte)(_isDark ? 45 : 60);
            var dx = Math.Sin(rad) * (w * 0.05);
            dc.DrawEllipse(B(alpha, 212, 175, 55), null, new Point(w * 0.5 + dx, h * 0.83), w * 0.55, h * 0.16);
        }

        private void DrawXpHills(DrawingContext dc, double w, double h)
        {
            var c1 = _isDark ? Color.FromRgb(15, 35, 75) : Color.FromRgb(110, 160, 220);
            var c2 = _isDark ? Color.FromRgb(25, 50, 100) : Color.FromRgb(180, 210, 245);
            dc.DrawRectangle(Grad(c1, c2, new Point(0, 0), new Point(0, h)), null, new Rect(0, 0, w, h));

            var sunX = ((_phase / 360.0) * (w + 200)) - 100;
            byte sunAlpha = (byte)(_isDark ? 55 : 90);
            dc.DrawEllipse(B(sunAlpha, 255, 255, 230), null, new Point(sunX, h * 0.13), w * 0.05, w * 0.05);

            var hillColor = _isDark ? Color.FromRgb(20, 55, 30) : Color.FromRgb(90, 165, 80);
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(0, h), true, true);
                ctx.LineTo(new Point(0, h * 0.72), true, false);
                ctx.PolyBezierTo(new[] { new Point(w * 0.27, h * 0.59), new Point(w * 0.64, h * 0.66), new Point(w, h * 0.55) }, true, false);
                ctx.LineTo(new Point(w, h), true, false);
            }
            dc.DrawGeometry(new SolidColorBrush(hillColor), null, geo);
        }

        private void DrawCyberGrid(DrawingContext dc, double w, double h)
        {
            var bg = _isDark ? Color.FromRgb(15, 15, 25) : Color.FromRgb(220, 220, 235);
            dc.DrawRectangle(new SolidColorBrush(bg), null, new Rect(0, 0, w, h));
            var (r, g, b) = _isDark ? (255, 0, 127) : (0, 100, 150);
            var pen = new Pen(B(50, (byte)r, (byte)g, (byte)b), 1);
            var offset = (_phase / 360.0) * 40;
            for (var x = -40 + offset; x < w; x += 40) dc.DrawLine(pen, new Point(x, 0), new Point(x, h));
            for (var y = -40 + offset; y < h; y += 40) dc.DrawLine(pen, new Point(0, y), new Point(w, y));
        }

        private void DrawMatrixRain(DrawingContext dc, double w, double h)
        {
            var bg = _isDark ? Color.FromRgb(5, 15, 5) : Color.FromRgb(200, 230, 200);
            dc.DrawRectangle(new SolidColorBrush(bg), null, new Rect(0, 0, w, h));
            var (g, b) = _isDark ? (255, 65) : (150, 40);
            var pen = new Pen(B(75, 0, (byte)g, (byte)b), 2);
            var rnd = new Random(42);
            var fallOffset = (_phase / 360.0) * (h + 300);
            for (var i = 0; i < 30; i++)
            {
                var rx = rnd.Next(0, (int)Math.Max(1, w));
                var ryBase = rnd.Next(0, (int)h + 300);
                var ry = ((ryBase + fallOffset) % (h + 300)) - 300;
                dc.DrawLine(pen, new Point(rx, ry), new Point(rx, ry + rnd.Next(100, 300)));
            }
        }

        private void DrawNordicWaves(DrawingContext dc, double w, double h, double rad)
        {
            var c1 = _isDark ? Color.FromRgb(30, 35, 42) : Color.FromRgb(210, 218, 228);
            var c2 = _isDark ? Color.FromRgb(45, 52, 63) : Color.FromRgb(235, 240, 245);
            dc.DrawRectangle(Grad(c1, c2, new Point(0, 0), new Point(0, h)), null, new Rect(0, 0, w, h));

            var waveY = h * 0.6;
            var pen = new Pen(B(50, 140, 190, 220), Math.Max(20, h * 0.05));
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                var first = true;
                for (var px = -50.0; px <= w + 50; px += 100)
                {
                    var py = waveY + Math.Sin(px / 150.0 + rad) * (h * 0.05);
                    if (first) { ctx.BeginFigure(new Point(px, py), false, false); first = false; }
                    else ctx.LineTo(new Point(px, py), true, true);
                }
            }
            dc.DrawGeometry(null, pen, geo);
        }

        private void DrawAero7Glass(DrawingContext dc, double w, double h, double rad)
        {
            var c1 = _isDark ? Color.FromRgb(15, 28, 50) : Color.FromRgb(195, 220, 245);
            var c2 = _isDark ? Color.FromRgb(35, 65, 105) : Color.FromRgb(230, 242, 253);
            dc.DrawRectangle(Grad(c1, c2, new Point(0, 0), new Point(w, h)), null, new Rect(0, 0, w, h));

            byte glossAlpha = (byte)(_isDark ? 55 : 90);
            var glossDx = Math.Sin(rad) * (w * 0.06);
            dc.DrawEllipse(B(glossAlpha, 255, 255, 255), null, new Point(w * 0.5 + glossDx, h * -0.05), w * 0.65, h * 0.22);
        }

        private void DrawRetro98Teal(DrawingContext dc, double w, double h)
        {
            var bg = _isDark ? Color.FromRgb(24, 38, 38) : Color.FromRgb(180, 205, 200);
            dc.DrawRectangle(new SolidColorBrush(bg), null, new Rect(0, 0, w, h));
            // ΔΙΟΡΘΩΣΗ: το Light alpha ήταν 14/255 (~5.5%), σχεδόν αόρατο.
            var lineColor = _isDark ? Color.FromArgb(40, 20, 38, 40) : Color.FromArgb(35, 255, 255, 255);
            var pen = new Pen(new SolidColorBrush(lineColor), 1);
            var lineOffset = (_phase / 360.0) * 4;
            for (var ly = lineOffset; ly < h; ly += 4) dc.DrawLine(pen, new Point(0, ly), new Point(w, ly));
        }

        private void DrawMacGradient(DrawingContext dc, double w, double h, double rad)
        {
            var c1 = _isDark ? Color.FromRgb(24, 24, 28) : Color.FromRgb(240, 240, 244);
            var c2 = _isDark ? Color.FromRgb(34, 34, 40) : Color.FromRgb(250, 250, 252);
            var gx2 = w + Math.Sin(rad) * (w * 0.05);
            dc.DrawRectangle(Grad(c1, c2, new Point(0, 0), new Point(gx2, h)), null, new Rect(0, 0, w, h));
        }

        private void DrawSolarizedFlat(DrawingContext dc, double w, double h, double rad)
        {
            var bg = _isDark ? Color.FromRgb(0, 43, 54) : Color.FromRgb(253, 246, 227);
            dc.DrawRectangle(new SolidColorBrush(bg), null, new Rect(0, 0, w, h));
            var pulseAlpha = (byte)Math.Clamp(45 + Math.Sin(rad) * 20, 0, 255);
            dc.DrawRectangle(B(pulseAlpha, 42, 161, 152), null, new Rect(0, 0, 6, h));
        }

        private void DrawStandard(DrawingContext dc, double w, double h)
        {
            var c = _isDark ? Color.FromRgb(35, 35, 35) : Color.FromRgb(240, 240, 240);
            dc.DrawRectangle(new SolidColorBrush(c), null, new Rect(0, 0, w, h));
        }

        // 3 νέα, καθολικά (ανεξάρτητα θέματος) στυλ κινούμενου φόντου (ρητό αίτημα χρήστη, screenshot
        // Ρυθμίσεων Εμφάνισης - "Γρανάζια"/"Τροχιές"/"Σωματίδια").
        private void DrawGears(DrawingContext dc, double w, double h, double rad)
        {
            DrawStandard(dc, w, h);
            var (r, g, b) = _isDark ? (255, 255, 255) : (30, 30, 30);
            // ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "κάποια κινούμενα φόντα... πάρα πολύ αχνά") - 25/35 (out of
            // 255, ~10-14% αδιαφάνεια) ήταν από τα πιο αχνά στυλ, ειδικά αφού είναι το ΜΟΝΟ ορατό
            // στοιχείο πάνω σε ένα επίπεδο DrawStandard φόντο (όχι απλά μια λεπτομέρεια πάνω σε ήδη
            // έγχρωμο gradient, όπως τα άλλα στυλ).
            byte alpha = (byte)(_isDark ? 55 : 65);
            void Gear(double cx, double cy, double radius, int teeth, double phaseOffset)
            {
                var angle = rad * 180 / Math.PI + phaseOffset;
                var body = new EllipseGeometry(new Point(cx, cy), radius, radius);
                var toothW = radius * 0.34; var toothH = radius * 0.4;
                var group = new GeometryGroup { FillRule = FillRule.Nonzero };
                group.Children.Add(body);
                for (var i = 0; i < teeth; i++)
                {
                    var a = (360.0 / teeth) * i + angle;
                    group.Children.Add(new RectangleGeometry(new Rect(cx - toothW / 2, cy - radius - toothH * 0.55, toothW, toothH)) { Transform = new RotateTransform(a, cx, cy) });
                }
                var hole = new EllipseGeometry(new Point(cx, cy), radius * 0.38, radius * 0.38);
                dc.DrawGeometry(B(alpha, (byte)r, (byte)g, (byte)b), null, new CombinedGeometry(GeometryCombineMode.Exclude, group, hole));
            }
            Gear(w * 0.2, h * 0.3, Math.Min(w, h) * 0.16, 9, 0);
            Gear(w * 0.75, h * 0.65, Math.Min(w, h) * 0.22, 10, 15);
        }

        // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε: "οι τροχιές να πιάνουν όλο το μήκος του παραθύρου και να είναι υπό
        // γωνία, όχι σαν να τις βλέπω από πάνω") - πριν η ακτίνα ήταν Math.Min(w,h)*0.12*ring (πολύ
        // μικρή/κεντραρισμένη σε πλατύ παράθυρο, ΔΕΝ έφτανε στις άκρες) και ο δακτύλιος ζωγραφιζόταν ως
        // ΤΕΛΕΙΟΣ κύκλος ενώ η κουκκίδα κινούνταν σε πεπλατυσμένη έλλειψη (*0.6 στο Y) - η ασυμφωνία
        // δακτυλίου/κουκκίδας ΚΑΙ η συμμετρική κάθετη σμίκρυνση χωρίς καμία περιστροφή είναι ακριβώς η
        // "κάθετη προβολή από πάνω" εμφάνιση που ανέφερε ο χρήστης. Τώρα: η ακτίνα Χ κλιμακώνεται με το
        // w (το τρίτο δαχτυλίδι φτάνει σχεδόν σε όλο το πλάτος του παραθύρου) ΚΑΙ ο δακτύλιος
        // περιστρέφεται με RotateTransform (πραγματική "υπό γωνία" τροχιά, όχι απλή κάθετη σμίκρυνση) -
        // η κουκκίδα υπολογίζεται με τον ΙΔΙΟ μετασχηματισμό ώστε να παραμένει ακριβώς πάνω στο δαχτυλίδι.
        private void DrawOrbit(DrawingContext dc, double w, double h, double rad)
        {
            DrawStandard(dc, w, h);
            var cx = w / 2; var cy = h / 2;
            var (r, g, b) = _isDark ? (100, 190, 255) : (0, 100, 200);
            const double tiltDeg = -20.0;
            const double squash = 0.4;
            var tiltRad = tiltDeg * Math.PI / 180.0;
            var cosT = Math.Cos(tiltRad); var sinT = Math.Sin(tiltRad);
            // Το ύψος εδώ (menu+τίτλος+καρτέλες) είναι πολύ μικρότερο από το πλάτος - χωρίς αυτό το
            // Min, η ακτίνα Y του 3ου δαχτυλιδιού (βασισμένη καθαρά στο πλάτος) θα ξεπερνούσε κατά
            // πολύ το διαθέσιμο ύψος και θα περικοπτόταν σχεδόν εντελώς από το PushClip του OnRender.
            var maxRadiusY = h * 0.46;

            for (var ring = 1; ring <= 3; ring++)
            {
                var radiusX = w * 0.145 * ring;
                var radiusY = Math.Min(radiusX * squash, maxRadiusY * ring / 3.0);

                dc.PushTransform(new RotateTransform(tiltDeg, cx, cy));
                dc.DrawEllipse(null, new Pen(B(65, (byte)r, (byte)g, (byte)b), 1), new Point(cx, cy), radiusX, radiusY);
                dc.Pop();

                var angle = rad + ring * 2.1;
                var ex = radiusX * Math.Cos(angle);
                var ey = radiusY * Math.Sin(angle);
                var dotX = cx + ex * cosT - ey * sinT;
                var dotY = cy + ex * sinT + ey * cosT;
                dc.DrawEllipse(B(220, (byte)r, (byte)g, (byte)b), null, new Point(dotX, dotY), 5, 5);
            }
        }

        private void DrawParticles(DrawingContext dc, double w, double h, double rad)
        {
            DrawStandard(dc, w, h);
            var (r, g, b) = _isDark ? (255, 255, 255) : (60, 60, 60);
            var rnd = new Random(7);
            var drift = rad * 30;
            for (var i = 0; i < 40; i++)
            {
                var baseX = rnd.NextDouble() * w;
                var baseY = (rnd.NextDouble() * (h + 60) + drift) % (h + 60) - 30;
                var size = 1.5 + rnd.NextDouble() * 2.5;
                dc.DrawEllipse(B((byte)(60 + rnd.Next(100)), (byte)r, (byte)g, (byte)b), null, new Point(baseX, baseY), size, size);
            }
        }
    }
}
