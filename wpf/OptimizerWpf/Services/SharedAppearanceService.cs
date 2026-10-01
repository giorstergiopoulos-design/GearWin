using System;
using System.IO;
using System.Text.Json;

namespace OptimizerWpf.Services
{
    public record SharedAppearance(string Source, string ThemeName, bool IsDarkMode, DateTime UpdatedUtc);

    // ΝΕΟ (6.1.0) - κοινή εμφάνιση με το MotionDesk Studio: ένα μικρό αρχείο JSON σε κοινό φάκελο
    // (%APPDATA%\MotionDeskShared\appearance.json) όπου κάθε εφαρμογή γράφει το τρέχον θέμα/σκοτεινή
    // λειτουργία της. Αν ο χρήστης ενεργοποιήσει "κοινή εμφάνιση", το GearWin διαβάζει το αρχείο στην
    // εκκίνηση και αν το έγραψε ΑΛΛΗ εφαρμογή πιο πρόσφατα, ακολουθεί. ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: το
    // GearWin γράφει/διαβάζει το αρχείο· το MotionDesk το διαβάζει μόνο αν ενημερωθεί να το κάνει.
    public static class SharedAppearanceService
    {
        public const string AppName = "GearWin";

        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MotionDeskShared", "appearance.json");

        public static bool Write(SharedAppearance value, string? path = null)
        {
            try
            {
                path ??= DefaultPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(value));
                File.Move(tmp, path, overwrite: true);
                return true;
            }
            catch { return false; }
        }

        public static SharedAppearance? Read(string? path = null)
        {
            try
            {
                path ??= DefaultPath;
                if (!File.Exists(path)) return null;
                var v = JsonSerializer.Deserialize<SharedAppearance>(File.ReadAllText(path));
                return v is { ThemeName.Length: > 0 } ? v : null;
            }
            catch { return null; }
        }

        // Ακολουθούμε ΜΟΝΟ αν το έγραψε άλλη εφαρμογή (όχι εμείς) και διαφέρει από την τρέχουσα επιλογή.
        public static bool ShouldFollow(SharedAppearance? shared, string currentTheme, bool currentDark) =>
            shared != null && shared.Source != AppName && (shared.ThemeName != currentTheme || shared.IsDarkMode != currentDark);
    }
}
