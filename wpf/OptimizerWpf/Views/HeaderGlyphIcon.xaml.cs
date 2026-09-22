using System.Windows;
using System.Windows.Controls;

namespace OptimizerWpf.Views
{
    public enum GlyphKind
    {
        Monitor, Disk, SignalBars, Battery, Tools, Wrench, Broom,
        Shield, Lock, Dial, Brain, Gamepad, Package, Phone,
        Gear, Home, Bolt, Heart, Globe, Check, Palette,
        Question, Clock, Document, Puzzle
    }

    public partial class HeaderGlyphIcon : UserControl
    {
        public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
            nameof(Kind), typeof(GlyphKind), typeof(HeaderGlyphIcon),
            new PropertyMetadata(GlyphKind.Monitor, OnKindChanged));

        public GlyphKind Kind
        {
            get => (GlyphKind)GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        public HeaderGlyphIcon()
        {
            InitializeComponent();
            ApplyKind();
        }

        private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((HeaderGlyphIcon)d).ApplyKind();

        private void ApplyKind()
        {
            PartMonitor.Visibility = Kind == GlyphKind.Monitor ? Visibility.Visible : Visibility.Collapsed;
            PartDisk.Visibility = Kind == GlyphKind.Disk ? Visibility.Visible : Visibility.Collapsed;
            PartSignalBars.Visibility = Kind == GlyphKind.SignalBars ? Visibility.Visible : Visibility.Collapsed;
            PartBattery.Visibility = Kind == GlyphKind.Battery ? Visibility.Visible : Visibility.Collapsed;
            PartTools.Visibility = Kind == GlyphKind.Tools ? Visibility.Visible : Visibility.Collapsed;
            PartWrench.Visibility = Kind == GlyphKind.Wrench ? Visibility.Visible : Visibility.Collapsed;
            PartBroom.Visibility = Kind == GlyphKind.Broom ? Visibility.Visible : Visibility.Collapsed;
            PartShield.Visibility = Kind == GlyphKind.Shield ? Visibility.Visible : Visibility.Collapsed;
            PartLock.Visibility = Kind == GlyphKind.Lock ? Visibility.Visible : Visibility.Collapsed;
            PartDial.Visibility = Kind == GlyphKind.Dial ? Visibility.Visible : Visibility.Collapsed;
            PartBrain.Visibility = Kind == GlyphKind.Brain ? Visibility.Visible : Visibility.Collapsed;
            PartGamepad.Visibility = Kind == GlyphKind.Gamepad ? Visibility.Visible : Visibility.Collapsed;
            PartPackage.Visibility = Kind == GlyphKind.Package ? Visibility.Visible : Visibility.Collapsed;
            PartPhone.Visibility = Kind == GlyphKind.Phone ? Visibility.Visible : Visibility.Collapsed;
            PartGear.Visibility = Kind == GlyphKind.Gear ? Visibility.Visible : Visibility.Collapsed;
            PartHome.Visibility = Kind == GlyphKind.Home ? Visibility.Visible : Visibility.Collapsed;
            PartBolt.Visibility = Kind == GlyphKind.Bolt ? Visibility.Visible : Visibility.Collapsed;
            PartHeart.Visibility = Kind == GlyphKind.Heart ? Visibility.Visible : Visibility.Collapsed;
            PartGlobe.Visibility = Kind == GlyphKind.Globe ? Visibility.Visible : Visibility.Collapsed;
            PartCheck.Visibility = Kind == GlyphKind.Check ? Visibility.Visible : Visibility.Collapsed;
            PartPalette.Visibility = Kind == GlyphKind.Palette ? Visibility.Visible : Visibility.Collapsed;
            PartQuestion.Visibility = Kind == GlyphKind.Question ? Visibility.Visible : Visibility.Collapsed;
            PartClock.Visibility = Kind == GlyphKind.Clock ? Visibility.Visible : Visibility.Collapsed;
            PartDocument.Visibility = Kind == GlyphKind.Document ? Visibility.Visible : Visibility.Collapsed;
            PartPuzzle.Visibility = Kind == GlyphKind.Puzzle ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
