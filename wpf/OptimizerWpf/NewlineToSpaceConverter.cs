using System;
using System.Globalization;
using System.Windows.Data;

namespace OptimizerWpf
{
    // Οι ετικέτες του SidebarShortcuts.All έχουν σκόπιμα "\n" ενσωματωμένο (2 γραμμές, σχεδιασμένο
    // για το ΚΑΘΕΤΟ πλευρικό μενού) - στην ΟΡΙΖΟΝΤΙΑ λωρίδα (horizModernStrip) αυτό έδειχνε άσχημα
    // (μία καρτούλα ξαφνικά διπλή σε ύψος) - εδώ γίνεται μονογραμμικό αντικαθιστώντας με κενό.
    public class NewlineToSpaceConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
            (value as string)?.Replace("\n", " ") ?? value ?? "";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
