using System.Windows.Data;
using System.Windows.Markup;
using OptimizerWpf.Services;

namespace OptimizerWpf
{
    // Σύντομη MarkupExtension για μεταφρασμένο κείμενο - {loc:Tr KeyName} αντί για το πολύ πιο
    // αναλυτικό {Binding [KeyName], Source={x:Static services:LanguageService.Instance}} σε κάθε
    // μεμονωμένο TextBlock. Ενσωματώνεται πάνω στο ήδη υπάρχον indexer-binding μηχανισμό του
    // LanguageService (βλ. LanguageService.cs) - ζωντανή ενημέρωση όταν αλλάζει η γλώσσα, καμία
    // ανάγκη code-behind ανά παράθυρο.
    public class TrExtension : MarkupExtension
    {
        public string Key { get; set; } = "";

        public TrExtension() { }
        public TrExtension(string key) => Key = key;

        // ΔΙΟΡΘΩΣΗ (πραγματικό σφάλμα εκτέλεσης: "Set property TextBlock.Text threw an exception") -
        // η επιστροφή ΤΟΥ ΙΔΙΟΥ του Binding αντικειμένου από το ProvideValue ΔΕΝ ενεργοποιεί αυτόματα
        // δέσμευση δεδομένων· το WPF απλά προσπαθεί να αναθέσει το ίδιο το Binding object απευθείας
        // στην ιδιότητα TextBlock.Text (τύπου string) - αποτυχία τύπου. Η σωστή τεχνική για μια
        // MarkupExtension "περιτύλιγμα" γύρω από ένα Binding είναι να καλέσει η ίδια το ΔΙΚΟ ΤΟΥ
        // ProvideValue (περνώντας τον ίδιο serviceProvider) - αυτό είναι που πραγματικά συνδέει το
        // Binding με τη σωστή ιδιότητα-στόχο μέσω IProvideValueTarget.
        public override object ProvideValue(System.IServiceProvider serviceProvider) =>
            new Binding($"[{Key}]") { Source = LanguageService.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
    }
}
