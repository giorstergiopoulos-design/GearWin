using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OptimizerWpf.Views
{
    public partial class ThreeGearIcon : UserControl
    {
        // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε: "αναπαρήγαγε ακριβώς το κινούμενο εικονίδιο της 2.8.2") - ακτίνες
        // πλέον στην ΑΚΡΙΒΗ αναλογία 12:8:6 (=1.5:1:0.75) του ps1's title-bar γραναζιού, και δόντια
        // 10/8/8 (ήταν 9/7/7) - ίδιες τιμές με το $global:titleGearBadge.Add_Paint original.
        private const double BigRadius = 24;
        private const double Small1Radius = BigRadius * 8 / 12; // 16
        private const double Small2Radius = BigRadius * 6 / 12; // 12

        public ThreeGearIcon()
        {
            InitializeComponent();
            // Συντεταγμένες ΤΑΥΤΟΣΗΜΕΣ με τα RotateTransform.CenterX/Y στο ThreeGearIcon.xaml - το
            // γρανάζι πρέπει να σχεδιάζεται γύρω από το ΙΔΙΟ σημείο γύρω από το οποίο περιστρέφεται,
            // αλλιώς "ταξιδεύει" σε τροχιά αντί να γυρίζει επί τόπου.
            PathSmall1.Data = BuildGear(52, 24, Small1Radius, 8);
            PathSmall2.Data = BuildGear(64, 48, Small2Radius, 8);
            PathBig.Data = BuildGear(32, 42, BigRadius, 10);
            Loaded += (_, _) => StartSpin();
        }

        // Port του Draw-SimpleGear (Optimizer.ps1 ~5521) - σώμα (κύκλος) + δόντια (ορθογώνια σε ίσες
        // γωνίες γύρω από την περιφέρεια, ίδιοι συντελεστές πλάτους/ύψους 0.34/0.4×radius) + κεντρική
        // τρύπα (μέσω CombinedGeometry Exclude αντί για δεύτερο, σκουρότερο χρώμα - εδώ το γρανάζι
        // είναι πάντα λευκό πάνω σε έγχρωμο φόντο, οπότε μια πραγματική διάφανη τρύπα δείχνει καλύτερα
        // από ένα εικασμένο σκουρότερο χρώμα).
        private static Geometry BuildGear(double cx, double cy, double radius, int teeth)
        {
            var group = new GeometryGroup { FillRule = FillRule.Nonzero };
            group.Children.Add(new EllipseGeometry(new Point(cx, cy), radius, radius));

            var toothW = radius * 0.34;
            var toothH = radius * 0.4;
            for (var i = 0; i < teeth; i++)
            {
                var angle = (360.0 / teeth) * i;
                var rect = new RectangleGeometry(new Rect(cx - toothW / 2, cy - radius - toothH * 0.55, toothW, toothH))
                {
                    Transform = new RotateTransform(angle, cx, cy),
                };
                group.Children.Add(rect);
            }

            var hole = new EllipseGeometry(new Point(cx, cy), radius * 0.38, radius * 0.38);
            return new CombinedGeometry(GeometryCombineMode.Exclude, group, hole);
        }

        // ΔΙΟΡΘΩΣΗ (χρήστης ζήτησε: "αναπαρήγαγε ακριβώς το κινούμενο εικονίδιο της 2.8.2") - πριν, ΚΑΙ
        // τα δύο μικρά γρανάζια περιστρέφονταν αντίθετα από το μεγάλο (σαν να "δαγκώνουν" απευθείας
        // πάνω του και τα δύο). Το ps1 original έχει στην πραγματικότητα μια ΑΛΥΣΙΔΑ Big↔Small1↔Small2
        // (rot2 = -phase*r1/r2, rot3 = +phase*r1/r3 - το ΙΔΙΟ πρόσημο με το Big, όχι αντίθετο, αφού
        // περνάει από δύο "δαγκώματα") - Small1 αντίθετα, Small2 ΙΔΙΑ φορά με το Big. Η ταχύτητα κάθε
        // γραναζιού είναι επίσης ακριβώς ανάλογη της ακτίνας του (μικρότερο γρανάζι = γρηγορότερη
        // περιστροφή), όχι απλώς κατά προσέγγιση. Βασική περίοδος 13s ≈ ps1's 180ms×72 βήματα (5°/βήμα)
        // για μία πλήρη περιστροφή του μεγάλου γραναζιού.
        public void StartSpin()
        {
            const double basePeriod = 13.0;
            Spin(RotBig, basePeriod, clockwise: true);
            Spin(RotSmall1, basePeriod / (BigRadius / Small1Radius), clockwise: false);
            Spin(RotSmall2, basePeriod / (BigRadius / Small2Radius), clockwise: true);
        }

        // Port του ps1's "στατικό γρανάζι όταν δεν τρέχει σάρωση" (Draw-SimpleGear idle state,
        // RotationDeg=0) - χρησιμοποιείται από το DriverScanButton ώστε το γρανάζι να είναι ακίνητο
        // στην αδράνεια και να ζωντανεύει μόνο κατά τη σάρωση.
        public void StopSpin()
        {
            RotBig.BeginAnimation(RotateTransform.AngleProperty, null);
            RotSmall1.BeginAnimation(RotateTransform.AngleProperty, null);
            RotSmall2.BeginAnimation(RotateTransform.AngleProperty, null);
        }

        private static void Spin(RotateTransform rt, double seconds, bool clockwise)
        {
            var anim = new DoubleAnimation(clockwise ? 0 : 360, clockwise ? 360 : 0, new Duration(TimeSpan.FromSeconds(seconds)))
            { RepeatBehavior = RepeatBehavior.Forever };
            rt.BeginAnimation(RotateTransform.AngleProperty, anim);
        }
    }
}
