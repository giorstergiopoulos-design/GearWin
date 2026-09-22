# Contributing / Συνεισφορά

*English below the Greek section.*

## Ελληνικά

Ευχαριστούμε για το ενδιαφέρον! Το project είναι μικρό και ανοιχτό σε συνεισφορές — μερικές βασικές
οδηγίες πριν ανοίξεις ένα PR:

### Πριν ξεκινήσεις

- Η ενεργή εφαρμογή είναι στο `wpf/OptimizerWpf/` (.NET 8, WPF/C#). Το `Optimizer.ps1` (PowerShell/
  WinForms) είναι το αρχικό εργαλείο, διατηρείται μόνο ως ιστορική αναφορά — **μην** προσθέτεις νέες
  λειτουργίες εκεί.
- Για γενική προσανατολισμό στη δομή/λογική του κώδικα, δες το `HANDOFF.md` (κυρίως ελληνικά,
  αγγλική σύνοψη στην κορυφή) και το `README.md`.
- Άνοιξε πρώτα ένα issue αν σκοπεύεις μια μεγάλη αλλαγή, ώστε να συζητηθεί η προσέγγιση πριν επενδύσεις
  χρόνο σε κώδικα.

### Μεταφράσεις

Κάθε string που φαίνεται στον χρήστη περνάει από `LanguageService.T("Key")` — ορίζεται ταυτόχρονα και
στις 14 γλώσσες (Ελληνικά, Αγγλικά, Γερμανικά, Γαλλικά, Ισπανικά, Ιταλικά, Ρωσικά, Κινέζικα, Ιαπωνικά,
Πορτογαλικά, Κορεατικά, Τουρκικά, Αραβικά, Χίντι) στο `Services/LanguageService.cs`. Αν προσθέτεις νέο
UI κείμενο, χρειάζεται νέο κλειδί **και στις 14** language blocks - ένα PR που προσθέτει κλειδί μόνο
σε ένα block δεν θα γίνει merge. Αν δεν μιλάς όλες τις γλώσσες, μια μηχανική μετάφραση είναι καλύτερη
από τίποτα — θα ελεγχθεί/διορθωθεί στο review.

### Εικονίδια

Νέα εικονίδια στην εφαρμογή θα πρέπει να είναι διανυσματικά (μέσω του `Views/HeaderGlyphIcon.xaml`,
προσθέτοντας νέο `GlyphKind`), όχι emoji - το WPF `TextBlock` δεν αποδίδει πάντα σωστά έγχρωμα emoji σε
αυτό το περιβάλλον.

### Δοκιμές

`dotnet test wpf/OptimizerWpf.Tests` πριν από κάθε PR. Νέα λογική σε `Services/*.cs` με μη-τετριμμένη
λογική (parsing, υπολογισμοί) θα πρέπει ιδανικά να συνοδεύεται από test.

### Pull requests

- Μικρά, εστιασμένα PRs προτιμώνται από μεγάλα PRs που αλλάζουν πολλά ταυτόχρονα.
- Περιέγραψε ΤΙ αλλάζει και ΓΙΑΤΙ (όχι μόνο "fix bug") στην περιγραφή του PR.

## English

Thanks for your interest! This is a small project, open to contributions — a few basics before opening
a PR:

### Before you start

- The active app lives in `wpf/OptimizerWpf/` (.NET 8, WPF/C#). `Optimizer.ps1` (PowerShell/WinForms)
  is the original tool, kept only as historical reference — **do not** add new features there.
- For orientation on the codebase, see `HANDOFF.md` (mostly Greek, English summary at the top) and
  `README.md`.
- Open an issue first for anything large, so the approach can be discussed before you invest time.

### Translations

Every user-facing string goes through `LanguageService.T("Key")`, defined identically across all 14
language blocks (Greek, English, German, French, Spanish, Italian, Russian, Chinese, Japanese,
Portuguese, Korean, Turkish, Arabic, Hindi) in `Services/LanguageService.cs`. New UI text needs a
new key added to **all 14** blocks — a PR adding a key to only one block won't be merged. A machine
translation is better than nothing if you don't speak all the languages; it'll be reviewed/fixed.

### Icons

New icons should be vector-based (via `Views/HeaderGlyphIcon.xaml`, adding a new `GlyphKind`), not
emoji — WPF's `TextBlock` doesn't reliably render color emoji fonts in this environment.

### Tests

Run `dotnet test wpf/OptimizerWpf.Tests` before every PR. New non-trivial logic in `Services/*.cs`
(parsing, calculations) should ideally come with a test.

### Pull requests

- Small, focused PRs are preferred over large PRs that change many things at once.
- Describe WHAT changes and WHY (not just "fix bug") in the PR description.
