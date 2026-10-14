using System;
using System.Text;

namespace OptimizerWpf.Services
{
    // ΑΣΦΑΛΕΙΑ: η εφαρμογή τρέχει ΠΑΝΤΑ ως Administrator, οπότε κάθε PowerShell που ξεκινά κληρονομεί υψηλά
    // δικαιώματα. Δύο κλάσεις προβλημάτων αποφεύγονται με το -EncodedCommand (Base64 UTF-16LE):
    //  1) command injection - με "-Command \"...{δεδομένα}...\"" ένα " ή \ μέσα σε όνομα εφαρμογής/κανόνα/
    //     adapter έσπαγε τα εισαγωγικά της γραμμής εντολών· το Base64 δεν αφήνει κανένα ειδικό χαρακτήρα.
    //  2) race στο %TEMP% - το "-File temp.ps1" γράφει script σε φάκελο όπου γράφει και μη-elevated κώδικας
    //     του ίδιου χρήστη, που θα μπορούσε να το αντικαταστήσει πριν εκτελεστεί elevated (παράκαμψη UAC).
    public static class PowerShellArgs
    {
        // Όριο CreateProcess ~32767 χαρ. - ό,τι ξεπερνά το MaxEncodedLength πρέπει να χρησιμοποιήσει αρχείο.
        public const int MaxEncodedLength = 28000;

        public static string Encode(string script) => Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        public static bool Fits(string script) => Encode(script).Length <= MaxEncodedLength;

        public static string Command(string script, bool hidden = false) =>
            $"-NoProfile -ExecutionPolicy Bypass {(hidden ? "-WindowStyle Hidden " : "")}-EncodedCommand {Encode(script)}";
    }
}
