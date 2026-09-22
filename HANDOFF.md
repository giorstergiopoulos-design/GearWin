# PC Performance & Maintenance Suite — Project Handoff

## English Summary (for non-Greek-reading contributors)

*Added per roadmap item "HANDOFF.md summary in English" — this section is a short orientation, not a
translation of the document below (which stays in Greek: it is the working development log for this
project, written and read primarily by Greek-speaking contributors/AI sessions across many long
sessions). If you don't read Greek, start here and use README.md for everything user-facing.*

**What this repository is:** a free, open-source Windows 11 maintenance/optimization tool. It shipped
originally as a single-file PowerShell + WinForms script (`Optimizer.ps1`, kept for historical
reference and no longer the active codebase) and has since been fully ported to a native WPF/C#
(.NET 8) application, at `wpf/OptimizerWpf/`. That WPF app is the current, actively developed product —
all 8 tabs described in `README.md` are implemented there.

**Where things live:**
- `wpf/OptimizerWpf/` — the app itself. `Views/*.xaml` + `*.xaml.cs` per tab/window, `Services/*.cs`
  for the actual system logic (registry, WMI, process calls), `Services/LanguageService.cs` for the
  4-language (el/en/de/fr) translation dictionary used by every string in the UI.
- `wpf/OptimizerWpf.Tests/` — xUnit unit tests.
- `installer/` — the Inno Setup script (`OptimizerWpf.iss`) and per-language license/description text
  used to build the distributed installer (`OptimizerWpf-Setup-<version>.exe`).
- `Optimizer.ps1` — the original PowerShell/WinForms tool. Not touched anymore; retained as historical
  reference and as the functional spec the WPF port was built against (feature parity, then beyond).
- `HANDOFF.md` (this file, below this section) — a long, mostly-chronological Greek-language
  development log covering both eras (PowerShell and WPF). It records design decisions, bugs found and
  fixed, and user feedback that shaped the app — useful for understanding *why* something is built the
  way it is, not just *what* it does. `README.md` covers the *what* (features, install, build) in both
  languages already.
- Version history shown inside the app itself (Version History window) is the authoritative,
  user-facing changelog — check `Services/LanguageService.cs`'s `VerHist_*` keys for the source text.

**Conventions worth knowing before contributing:**
- Every user-facing string goes through `LanguageService.T("Key")`, defined identically across all 4
  language blocks in `LanguageService.cs` — a new UI string needs a new key added to all four.
- New icons use the vector `Views/HeaderGlyphIcon.xaml` control (a `Kind` enum of glyphs), not emoji —
  WPF's `TextBlock` does not reliably render color emoji fonts in this environment; the app switched
  away from emoji entirely for this reason.
- Dialogs use `ThemedMessageBox` (root `OptimizerWpf` namespace), not the native
  `System.Windows.MessageBox` — the native one ignores the app's light/dark theme.
- Tweaks that toggle a Windows setting are expected to detect and reflect the *real* current state on
  load (see `SimpleTweak.DetectState` in `Services/TweakService.cs`), not assume "off" by default.

## 0.1 ΤΡΕΧΩΝ ROADMAP (μετά την v4.4.2) — αλλαγές, βελτιώσεις & νέες προσθήκες

*Νέο roadmap, γραμμένο από την τρέχουσα, πραγματική κατάσταση του WPF port (v4.4.2) - το παλιό §4
("Πλήρης Λίστα Εκκρεμοτήτων") παρακάτω αφορά ΑΠΟΚΛΕΙΣΤΙΚΑ την εγκαταλελειμμένη `Optimizer.ps1`/WinForms
εποχή και διατηρείται μόνο ως ιστορικό αρχείο - ΜΗΝ το συγχέεις με ενεργές εκκρεμότητες. ΚΑΝΕΝΑ από τα
παρακάτω δεν έχει ξεκινήσει ακόμα εκτός αν σημειώνεται ρητά - πρόκειται για προτάσεις προς αξιολόγηση/
έγκριση, όχι δεσμευτικό πλάνο.*

### Α. Διορθώσεις / τεχνικό χρέος που εντοπίστηκε αλλά δεν καλύφθηκε πλήρως
1. **Registry Cleaner - περισσότερες ασφαλείς κατηγορίες**: το `HealthCleanupService.ScanRegistry()`
   καλύπτει προς το παρόν 4 κατηγορίες (MissingUninstaller, ObsoleteMuiCache, OrphanedAppPath,
   OrphanedStartupEntry), όλες με το ΙΔΙΟ αυστηρό κριτήριο "flag μόνο αν η διαδρομή που δείχνει η
   καταχώρηση πραγματικά δεν υπάρχει". Υποψήφιες επόμενες κατηγορίες με το ΙΔΙΟ κριτήριο: Shared DLLs
   (`SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs`), ορφανά File Association ProgIDs
   (`SOFTWARE\Classes\*`), ορφανά COM/ActiveX CLSID entries. ΚΑΘΕ νέα κατηγορία χρειάζεται το ίδιο
   αυστηρό "Exists() πριν το flag" τεστ - καμία heuristic διαγραφή.
2. **HealthScoreService ευρήματα - μόνο ενημερωτικά, καμία ενέργεια διόρθωσης**: η νέα κάρτα "επιπλέον
   ευρήματα υγείας" στον Πλήρη Έλεγχο Υγείας (v4.4.1) δείχνει προβλήματα (Defender off, εκκρεμής
   επανεκκίνηση, πολλά προγράμματα εκκίνησης, χωρίς πρόσφατο σημείο επαναφοράς) αλλά ΔΕΝ προσφέρει
   κουμπί ενέργειας - ο χρήστης πρέπει να πάει χειροκίνητα στην αντίστοιχη καρτέλα. Θα μπορούσε να πάρει
   το ΙΔΙΟ μοτίβο με την κάρτα "Apps" (κουμπί που πηγαίνει κατευθείαν στη σωστή καρτέλα/ρύθμιση).
3. **Backup registry - καμία διεπαφή επαναφοράς**: κάθε διαγραφή registry παίρνει αυτόματο `.reg` backup
   (`%LocalAppData%\OptimizerWpf\RegistryBackups\`) αλλά δεν υπάρχει ΚΑΜΙΑ οθόνη μέσα στην εφαρμογή να
   δεις/επαναφέρεις παλιά backups - μόνο χειροκίνητο διπλό-κλικ στο .reg αρχείο απ' έξω. Μια απλή λίστα
   "Ιστορικό Backup Registry" (Υγεία & Συντήρηση) με κουμπί "Επαναφορά" θα έκλεινε αυτό το κενό.

### Β. Βελτιώσεις σε ήδη υπάρχουσες λειτουργίες
4. **Startup impact, όχι μόνο πλήθος**: το `HealthScoreService.CheckStartupCount` μετράει ΜΟΝΟ αριθμό
   εγγραφών Run (>15 = εύρημα) - το Task Manager δείχνει πραγματικό "impact" (Low/Medium/High) ανά
   πρόγραμμα. Θα χρειαζόταν μέτρηση πραγματικού χρόνου εκκίνησης ανά διεργασία (Boot Timeline της
   καρτέλας Προηγμένα Εργαλεία ήδη μετράει κάτι σχετικό - πιθανή επαναχρησιμοποίηση/σύνδεση).
5. **Προγραμματισμένος (background) Πλήρης Έλεγχος Υγείας**: ήδη υπάρχει υποδομή background scheduling
   (`AutoGamingModeService`) - θα μπορούσε να επεκταθεί σε προαιρετικό εβδομαδιαίο auto-scan με toast
   ειδοποίηση αν βρεθούν σημαντικά ευρήματα, χωρίς να ανοίγει αυτόματα το παράθυρο (καθαρά ενημερωτικό,
   καμία αυτόματη ενέργεια χωρίς επιβεβαίωση χρήστη - ίδια φιλοσοφία ασφάλειας με όλη την εφαρμογή).
6. **Περισσότερες γλώσσες**: το `LanguageService` υποστηρίζει ήδη 4 γλώσσες (el/en/de/fr) με πλήρη
   κάλυψη κλειδιών (επιβεβαιωμένο μέσω `LanguageServiceCompletenessTests`) - προσθήκη π.χ. Ισπανικών/
   Ιταλικών θα ακολουθούσε το ίδιο, ήδη αποδεδειγμένο μοτίβο (νέο language block, ίδια keys, ίδιο test).
7. **Αυτόματη ενημέρωση εφαρμογής**: προς το παρόν ο χρήστης πρέπει να κατεβάσει χειροκίνητα νέο
   installer (βλ. αυτή τη συνομιλία) - ένας απλός "έλεγχος για ενημέρωση" (σύγκριση `<Version>` με GitHub
   Releases API, ΧΩΡΙΣ αυτόματη λήψη/εγκατάσταση, μόνο ειδοποίηση + link) θα έκλεινε αυτό το κενό χωρίς
   να προσθέσει ρίσκο σιωπηλής αυτο-ενημέρωσης.

### Γ. Νέες προσθήκες (δεν υπάρχουν καθόλου ακόμα)
8. **Εύρεση διπλότυπων αρχείων**: κοινό χαρακτηριστικό ανταγωνιστικών εργαλείων (CCleaner/PC Manager) -
   σάρωση επιλεγμένου φακέλου/δίσκου, hash-based σύγκριση, χειροκίνητη επιβεβαίωση πριν διαγραφή (ΠΟΤΕ
   αυτόματη - ίδια αρχή με όλες τις υπόλοιπες καταστροφικές ενέργειες της εφαρμογής).
9. **Mini widget / system tray live view**: μικρό αναδυόμενο πλαίσιο από το tray icon με ζωντανό
   CPU/RAM/δίκτυο, χωρίς να χρειάζεται άνοιγμα ολόκληρου του κύριου παραθύρου - το `NetworkTrafficService`
   (ETW-based, v4.3.5) ήδη παρέχει τα δεδομένα δικτύου που θα χρειαζόταν.
10. **Έλεγχος τείχους προστασίας/ανοιχτών θυρών**: επέκταση της καρτέλας Δίκτυο & Ασφάλεια με λίστα
    ενεργών κανόνων τείχους προστασίας που επιτρέπουν εισερχόμενη κίνηση (`netsh advfirewall firewall
    show rule`) - καθαρά ενημερωτικό, ίδιο πνεύμα ασφάλειας με τα υπόλοιπα Network εργαλεία.
11. **Εξαγωγή/εισαγωγή ΠΛΗΡΟΥΣ προφίλ ρυθμίσεων**: υπάρχει ήδη export/import προφίλ tweaks
    (`Tweaks_ExportProfile`/`ImportProfile`) - θα μπορούσε να επεκταθεί σε ΠΛΗΡΕΣ προφίλ εφαρμογής
    (καρφιτσωμένες συντομεύσεις, θέμα, γλώσσα, ρυθμίσεις sidebar) για εύκολη μεταφορά σε νέο PC.

---


**Τρέχουσα έκδοση:** v2.8.0 Final (βλ. `$global:versionHistory["2.8.0"]` - **ΠΡΩΤΗ ζωντανή, στιγμιότυπο-επιβεβαιωμένη διόρθωση PC Manager skin**: το περιεχόμενο κάθε καρτέλας πλέον επεκτείνεται δυναμικά να γεμίζει το διαθέσιμο πλάτος όταν το rail είναι ενεργό, αντί για σταθερό πλάτος (βλ. `Update-SidebarDockLayout`, μεταβλητή `$pcMgrContentW`). Επίσης: νέο πλαίσιο στις πρώτες ρυθμίσεις της καρτέλας Επιπλέον Ρυθμίσεις (`$cardMainTweaks`), διόρθωση περιθωρίου κουμπιού "Προσθήκη Νέας Επιλογής". Προηγούμενη: v2.7.0 Final - επαλήθευση/σκλήρυνση 4 σημείων χωρίς πρόσβαση σε live Windows (SDI timeout, letterbox MinimumSize, PC Manager rail label MeasureString, glass-background crop math). Πριν: v2.6.0 Final - ανίχνευση Lenovo/HP OEM. Πριν: v2.5.0 Final - **letterbox μεγιστοποίηση/resize** (μεγάλη αλλαγή αρχιτεκτονικής - βλ. ΟΠΩΣΔΗΠΟΤΕ ενότητα 0.8 πριν αγγίξεις `$mainForm`/sidebar). §4.Α έχει την ΠΛΗΡΗ τρέχουσα λίστα εκκρεμοτήτων)
**Αρχείο:** `Optimizer.ps1` (~18.300 γραμμές, ~2.34MB, PowerShell + WinForms, single-file GUI εφαρμογή)
**Launcher:** `Run_Optimizer.bat` (elevation + auto-patch μηχανισμός με backup/validation) — επιβεβαιωμένο ότι δεν χρειάζεται αλλαγές για καμία προσθήκη μέχρι v2.1.1 (καμία νέα εξωτερική εξάρτηση πέρα από winget/PowerShell 5.1 built-ins)
**Κώδικας αναφοράς:** `reference/WMT-GUI.ps1` (44.500+ γραμμές — **WPF/XAML, ΟΧΙ WinForms** — βλ. ενότητα "Κρίσιμα Τεχνικά Ευρήματα")

Αυτό το έγγραφο είναι η συμπύκνωση μιας πολύ μεγάλης, πολυ-συνεδριακής συνομιλίας ανάπτυξης (από v1.0.2 έως v2.1.1). Σκοπός του είναι να επιτρέψει σε νέο Claude instance (ιδανικά μέσω Claude Code) να συνεχίσει τη δουλειά χωρίς να χρειαστεί να ξανανακαλύψει το πλαίσιο, τις αποφάσεις, και τα λάθη που έχουν ήδη γίνει και διορθωθεί.

**ΝΕΟ σε v2.0.8/v2.1.0/v2.1.1** (πλήρης λεπτομέρεια στο in-app Ιστορικό Εκδόσεων): πλατύτερο mainForm (1150→1280px) με πλήρες κεντράρισμα περιεχομένου σε ΟΛΕΣ τις καρτέλες, θεματισμένα dropdown popups (`Set-ThemedComboBoxDrawing`), χειροκίνητα σχεδιασμένες σημαίες γλωσσών, staggered startup timers (WinRE/Health Score) για γρήγορη εκκίνηση, anchored/embedded πλευρικό μενού με πραγματική δέσμευση χώρου (`Update-SidebarDockLayout`, καλείται ΚΑΙ στην εκκίνηση ΚΑΙ σε live αλλαγή ρυθμίσεων) + σκληρό clamp θέσης/ύψους (ποτέ πλέον εκτός ορίων mainForm), νέο κεντραρισμένο `$global:scrollPanelHome` wrapper (Αρχική δεν ήταν ΠΟΤΕ κεντραρισμένη πριν), νέα λειτουργία **Ανάλυση Χώρου Δίσκου ανά Κατηγορία** (Αρχική, segmented bar + drilldown, συνδεδεμένη με το ήδη υπάρχον dropdown δίσκου), νέα κάρτα **Όλες οι Πρόσθετες Λειτουργίες Windows** (Προηγμένα Εργαλεία - πλήρης αναζητήσιμη λίστα, όχι μόνο τα 6 hardcoded). Όλα πλήρως μεταφρασμένα EN/FR/DE.

## 0.3 Οδηγία χρήστη: ΜΕΤΑ την v2.2.0, ανάλυση μεγέθους αρχείου (έφτασε ~2MB)

Ρητή οδηγία: αφού ολοκληρωθεί η v2.2.0 (ΟΧΙ πριν - δεν πρέπει να διακόψει/καθυστερήσει την τρέχουσα
δουλειά ενσωματώσεων/διορθώσεων), να γίνει ανάλυση του `Optimizer.ps1` επειδή έχει φτάσει ~2MB μέγεθος
αρχείου, για να εξεταστεί αν μπορεί να μειωθεί το μέγεθος ΧΩΡΙΣ να επηρεαστεί η λειτουργικότητα ("η
εφαρμογή να λειτουργεί τέλεια"). Πιθανές κατευθύνσεις προς εξέταση τότε (ΔΕΝ έχει ξεκινήσει καμία ακόμα):
- Έλεγχος για διπλότυπο/νεκρό κώδικα που έχει συσσωρευτεί σε τόσες πολλές παρτίδες αλλαγών.
- Πιθανή ενοποίηση επαναλαμβανόμενων μοτίβων (π.χ. τα πολλά σχεδόν-πανομοιότυπα async scan scripts -
  Start-X/Complete-X ζεύγη με Process+Timer polling - μπορεί να μοιράζονται περισσότερο κοινό κώδικα).
- Σχετίζεται με το ήδη καταγεγραμμένο σημείο 0.5 παρακάτω (πιθανή μετάβαση σε modules/assets αντί για
  ένα ενιαίο αρχείο) - ίδιο γενικό ζήτημα "βαριάς εφαρμογής", δύο σχετικές αλλά ξεχωριστές οδηγίες χρήστη.
- ΠΡΟΣΟΧΗ: οποιαδήποτε μείωση μεγέθους ΔΕΝ πρέπει να θυσιάσει ευανάγνωστα σχόλια/τεκμηρίωση εντός του
  κώδικα που έχουν αποδειχτεί κρίσιμα σε αυτό το project (βλ. πολλά "ΔΙΟΡΘΩΣΗ ΡΙΖΙΚΗ" σχόλια σε όλο το
  αρχείο) - η εξοικονόμηση χώρου δεν πρέπει να έρθει σε βάρος της μελλοντικής συντηρησιμότητας.

**ΑΠΟΤΕΛΕΣΜΑΤΑ ανάλυσης (v2.3.0, AST-based, μέσω `[System.Management.Automation.Language.Parser]::ParseFile`)**:
Συνολικό μέγεθος αρχείου: 2.239.289 bytes (~2,14MB), 17.891 γραμμές. Ανάλυση byte-μεγέθους ανά μεγάλο block:
- `$global:stringTranslations` (το μεγάλο EN/FR/DE λεξικό TT() - κυρίως μεταφράσεις του ιστορικού εκδόσεων
  συν μεμονωμένων strings): **1.038.541 bytes (~0,99MB) = 46,4% ΤΟΥ ΣΥΝΟΛΙΚΟΥ ΑΡΧΕΙΟΥ**, 3.587 γραμμές.
- `$global:versionHistory` (το ελληνικό κείμενο του ιστορικού - οι μεταφράσεις του είναι ΜΕΣΑ στο
  `stringTranslations` παραπάνω, όχι εδώ): 126.454 bytes (~0,12MB) = 5,6% του αρχείου, 488 γραμμές.
- `$global:translations` (μικρό λεξικό T() για ονομαστικά keys - μενού/καρτέλες): 6.652 bytes = 0,3%, αμελητέο.
- **Συνδυασμένα, μεταφράσεις + ιστορικό εκδόσεων = 1.171.647 bytes (~1,12MB) = 52,3% ΤΟΥ ΣΥΝΟΛΙΚΟΥ ΑΡΧΕΙΟΥ.**
  Ο πραγματικός κώδικας εφαρμογής/UI logic είναι μόνο ~1,02MB (47,7%).

**Συμπέρασμα**: το ~52% του μεγέθους του αρχείου ΔΕΝ είναι κώδικας εφαρμογής - είναι σωρευμένο κείμενο
ιστορικού εκδόσεων (60+ versions, ελληνικά) και οι 3 πλήρεις μεταφράσεις του (EN/FR/DE). Αυτό μεγαλώνει
ΓΡΑΜΜΙΚΑ με κάθε νέα έκδοση/changelog bullet - είναι ο μεγαλύτερος μοχλός μελλοντικής μείωσης μεγέθους,
ΠΟΛΥ μεγαλύτερος από οποιοδήποτε νεκρό κώδικα (το μόνο που εντοπίστηκε/αφαιρέθηκε αυτή τη φορά ήταν
~130 γραμμές SDI-window κώδικα - αμελητέο μπροστά στο 1MB+ των μεταφράσεων).
**ΚΑΜΙΑ ΔΟΜΙΚΗ ΑΛΛΑΓΗ ΔΕΝ έγινε σε αυτό το πέρασμα** (μόνο ανάλυση, όπως ζητήθηκε) - μόνο 3 πραγματικά
orphaned μεταφράσεις καθαρίστηκαν (βλ. §Α παρακάτω). Πιθανές μελλοντικές κατευθύνσεις προς εξέταση
(ΚΑΜΙΑ δεν έχει αποφασιστεί/ξεκινήσει):
- Αρχειοθέτηση παλιών version-history entries (π.χ. πριν από 1-2 χρόνια) σε ξεχωριστό αρχείο, με το
  in-app Ιστορικό Εκδόσεων να δείχνει μόνο πρόσφατες + link/σημείωση για το πλήρες ιστορικό. ΡΙΣΚΟ: ο
  χρήστης ίσως θέλει να βλέπει ΟΛΟ το ιστορικό μέσα στην ίδια εφαρμογή - να ΜΗΝ γίνει χωρίς ρητή έγκριση.
- Εξωτερικά .json/.resx resource αρχεία για τις μεταφράσεις αντί για inline hashtable - θα μείωνε το
  μέγεθος του .ps1 αλλά θα έσπαγε το "single-file, καμία εξωτερική εξάρτηση" σχέδιο του launcher
  (`Run_Optimizer.bat`) - ΜΕΓΑΛΗ αλλαγή αρχιτεκτονικής, σχετίζεται με το ήδη καταγεγραμμένο σημείο 0.5.

## 0.6 v2.3.0: trust/stability scoring, toast notifications, καθαρισμός

**Trust/stability scoring (`Complete-DriverScan`, ~γραμμή 11787)**: όταν σαρώνονται οδηγοί, το ΙΔΙΟ GPU
μπορεί να εμφανιστεί ΚΑΙ στα αποτελέσματα Windows Update ΚΑΙ σε αυτά του amd.com/nvidia.com (2 ανεξάρτητες
πηγές, βλ. ενότητα 0 για την αρχιτεκτονική πολλαπλών πηγών). Η λίστα `$confirmedGpuNames` συλλέγει τα
ονόματα GPU που το AMD/NVIDIA block επιβεβαίωσε ανεξάρτητα ότι έχουν νεότερο οδηγό (μέσα στους ήδη
υπάρχοντες `foreach` βρόχους, μετά το `if (-not $amdNewer/$nvNewer) { continue }`). Στον βρόχο του Windows
Update, το `Test-DriverSourceOverlap` (νέα function, ~γραμμή 11764, δίπλα στο `Get-DriverClassIcon`) κάνει
απλή σύγκριση επικάλυψης ονόματος (`-like`, ίδιο πνεύμα με το ήδη υπάρχον best-effort matching στο
`$match`) μεταξύ του ονόματος συσκευής Windows Update και της λίστας `$confirmedGpuNames` - μόνο για
`DriverClass -match "Display|Video"` (GPU). Όταν συμφωνούν, η ετικέτα πηγής αναβαθμίζεται από
"✓ Πηγή: Windows Update (επίσημη)" σε "✓✓ Επιβεβαιωμένο (WU+AMD/NVIDIA)" με επεξηγηματικό tooltip.
ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: η αντιστοίχιση ονόματος είναι best-effort (string overlap, όχι hardware ID) - ίδιος
περιορισμός με το ήδη υπάρχον `$currentVer` matching, δεν είναι νέος κίνδυνος.

**Toast notifications (`Show-ToastNotification`, ήδη υπήρχε πλήρως έτοιμη/δοκιμασμένη function από
προηγούμενο πέρασμα, γραμμή ~5968, απλά χωρίς κανένα call site)**: συνδέθηκε ΤΩΡΑ σε 3 σημεία ολοκλήρωσης
που πριν ενημέρωναν ΜΟΝΟ σιωπηλά τη γραμμή κατάστασης (καμία άλλη ένδειξη): (1) επιτυχής δημιουργία
σημείου επαναφοράς (μέσα στο click handler του `$global:btnCreateRestorePoint`, ~γραμμή 11308), (2)
ολοκλήρωση σάρωσης οδηγών (τέλος του `Complete-DriverScan`), (3) ολοκλήρωση ReTrim SSD (Υγεία & Συντήρηση).
ΣΚΟΠΙΜΑ ΔΕΝ συνδέθηκε παντού (βλ. το ίδιο το σχόλιο σχεδιασμού μέσα στο `Show-ToastNotification` - "όχι
μαζική αντικατάσταση") - σημεία που ήδη έχουν `Show-CustomMessage` modal (π.χ. Health Score Fix All,
Registry/Storage scan completion που απαιτεί επιλογή χρήστη) ΔΕΝ πήραν toast, θα ήταν διπλή/περιττή
ειδοποίηση. Πιθανά επόμενα σημεία (ΔΕΝ έγιναν - SFC/DISM/CHKDSK τρέχουν σε ΕΝΤΕΛΩΣ ξεχωριστή, μη-
παρακολουθούμενη powershell.exe διεργασία μέσω `Invoke-DiagnosticCommand` - η εφαρμογή δεν έχει ΚΑΝΕΝΑ
τρόπο να ξέρει πότε τελειώνουν, θα χρειαζόταν νέο polling μηχανισμό, εκτός πεδίου αυτού του περάσματος).

**Καθαρισμός νεκρού κώδικα + orphaned μεταφράσεων**: αφαιρέθηκε το `Start-SdiDriverCheck`/
`Find-SdioExecutable`/σχετικές μεταβλητές κατάστασης (~130 γραμμές, μηδενικά call sites - επιβεβαιώθηκε
μέσω grep πριν την αφαίρεση). Βρέθηκαν (μέσω AST-based ανάλυσης, ΟΧΙ οπτικό grep - βλ. σημείωση παρακάτω)
και αφαιρέθηκαν 3 orphaned μεταφράσεις (EN/FR/DE) - παλιά διατύπωση ενός Dell-σχετικού changelog bullet
("3η 'πηγή'") που είχε αναδιατυπωθεί αργότερα στο ίδιο το `$global:versionHistory` (αφαιρέθηκε το "3η"
όταν προστέθηκε το NVIDIA ως 4η πηγή) χωρίς να ενημερωθούν οι μεταφράσεις - το TT() πάντα επιστρέφει
ασφαλές fallback στο ελληνικό όταν λείπει μετάφραση, άρα αυτό ΔΕΝ ήταν ορατό bug, μόνο "νεκρό βάρος".
**ΤΕΧΝΙΚΗ ΣΗΜΕΙΩΣΗ για μελλοντικό παρόμοιο έλεγχο**: μια αρχική προσπάθεια με `.StartsWith()` σε
StringConstantExpressionAst values απέτυχε σιωπηλά - το .NET `String.StartsWith(string)` χωρίς ρητό
`StringComparison.Ordinal` χρησιμοποιεί culture-sensitive σύγκριση, και ο χαρακτήρας ➕ (U+2795) αποδείχτηκε
"μηδενικού βάρους" σε αυτήν τη σύγκριση (κάθε string φαινόταν να "ξεκινάει" με αυτόν) - ΠΑΝΤΑ να περνάει
ρητά `[System.StringComparison]::Ordinal` σε `StartsWith`/`EndsWith`/`Contains` όταν συγκρίνονται σύμβολα/
emoji, όχι μόνο αλφαβητικό κείμενο.

## 0.7 v2.4.0: διόρθωση περιθωρίων Αρχικής, glass-background επέκταση, διόρθωση SDI

**Home tab περιθώρια (η πραγματική ρίζα)**: το `$global:scrollPanelHome` είχε πλάτος 1030px - ΑΚΡΙΒΩΣ ίδιο
με το πλάτος των ίδιων των καρτών του (`$cardHealthScore`/`$cardDiskAnalysis` στο X=20 με πλάτος 1030 =>
δεξί άκρο στο X=1050, 20px πέρα από το panel). ΟΛΕΣ οι υπόλοιπες καρτέλες (Health/Optimization/Network/
System) ήδη χρησιμοποιούν panel πλάτους 1070px για κάρτες πλάτους 1030px (1030 + 20 αριστερά + 20 δεξιά).
Διορθώθηκε σε 1070px (Size, Location centering formula, AutoScrollMinSize.Width 1010->1050) ώστε να
ταιριάζει με το ήδη αποδεδειγμένο μοτίβο. Η προηγούμενη διόρθωση (v2.3.0, height 610->600) αφορούσε ΜΟΝΟ
την κάθετη διάσταση - ΔΕΝ είχε αγγίξει αυτό το πλάτος-bug, γι' αυτό ο χρήστης ξανάστειλε το ίδιο screenshot.

**Glass-background επέκταση (`Set-PanelGlassBackground`, ~γραμμή 7007)**: εφαρμόστηκε σε ΜΟΝΟ τα 5 panels
που ήδη έχουν το αποδεδειγμένα ασφαλές SetStyle (OptimizedDoubleBuffer/AllPaintingInWmPaint/UserPaint) -
Home/Health/Optimization/Network/System. Τεχνική: `Add_Paint` handler που κάνει χειροκίνητο
`Graphics.DrawImage` ενός crop του `$psender.Parent.BackgroundImage` (το ήδη υπάρχον, ζωντανά
ανανεούμενο bitmap του $content/"tab page") ΣΤΗ ΣΤΑΘΕΡΗ θέση του panel (`.Left`/`.Top`) - ΣΚΟΠΙΜΑ ΧΩΡΙΣ να
λαμβάνει υπόψη το δικό του `AutoScrollPosition` (το background παραμένει οπτικά "σταθερό" σαν υλικό πίσω
από το panel, ΔΕΝ κυλάει μαζί με το περιεχόμενο - αυτό αποφεύγει εντελώς τον υπολογισμό offset κύλισης).
ΔΙΑΦΟΡΕΤΙΚΟ μηχανισμό από το ήδη αποτυχημένο v1.6.6 (καμία ρύθμιση της ιδιότητας `BackgroundImage` σε
AutoScroll panel, κανένα `BackColor=Transparent`) - τρέχει στον κανονικό `Paint` κύκλο αντί να βασίζεται
στον ημι-αυτόματο μηχανισμό επανασχεδίασης-σε-κύλιση της ιδιότητας `BackgroundImage`.
**ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ (σημαντική)**: ΔΕΝ υπήρχε δυνατότητα ζωντανής οπτικής επιβεβαίωσης σε πραγματικά
Windows γι' αυτή τη συγκεκριμένη αλλαγή σε αυτό το πέρασμα (μόνο `ParseFile`/static analysis). Εφαρμόστηκε
κατόπιν ρητής έγκρισης του χρήστη ("δοκίμασε τη διόρθωση") μετά από ανάλυση ρίσκου. Αν εμφανιστεί ξανά
"φάντασμα"/artifact σε κάποιο από αυτά τα 5 panels: το πρώτο σημείο ελέγχου είναι το `Set-PanelGlassBackground`
Paint handler - πιθανή αιτία θα ήταν κάποιο edge case στο crop math (π.χ. όταν το panel μετατοπίζεται λόγω
docking του πλευρικού μενού EN ΩΡΑ που τρέχει ήδη το animation - το crop διαβάζει `.Left`/`.Top` ζωντανά σε
κάθε Paint, άρα ΘΑ έπρεπε να προσαρμόζεται σωστά, αλλά αυτό ΔΕΝ δοκιμάστηκε ζωντανά).

**SDI "Sum: 0" (η πραγματική ρίζα)**: το προηγούμενο επιχείρημα SDI (`-nogui -autoclose -nostamp
-output_dir -log_dir`) ΠΟΤΕ δεν περιείχε flag που να ζητά λήψη/ενημέρωση του τοπικού index driverpacks -
το SDI συνέκρινε πάντα ενάντια σε ΚΕΝΟ index, άρα "Sum: 0" ήταν αναμενόμενο σε ΚΑΘΕ εκτέλεση, όχι μόνο
στην πρώτη (η προηγούμενη v2.2.5 διάγνωση "πρώτη εκτέλεση" ήταν ημιτελής). Προστέθηκε το επίσημα
τεκμηριωμένο flag `-autoupdate` (πηγή: https://sdi-tool.org/settings/ - "starts downloading automatically"·
επιβεβαιώθηκε ΚΑΙ μέσω WebSearch σε δεύτερη πηγή). Το flag κατεβάζει τον ΚΑΤΑΛΟΓΟ/index (μεταδεδομένα -
ΟΧΙ τα ίδια τα αρχεία οδηγών, αυτά κατεβαίνουν ΜΟΝΟ μέσω ξεχωριστής εντολής `select`+`install` που η
εφαρμογή ΔΕΝ καλεί ποτέ). Χρονικό όριο αυξήθηκε 25 δευτ. -> 3 λεπτά για να χωρέσει η πρώτη λήψη index (το
SDI αποθηκεύει τον index στη ΔΙΚΗ ΤΟΥ προεπιλεγμένη τοποθεσία, ΟΧΙ στον προσωρινό μας φάκελο, άρα θα
έπρεπε να παραμένει μόνιμα - οι επόμενες σαρώσεις αναμένεται να είναι πολύ πιο γρήγορες).
**ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ**: το ΑΚΡΙΒΕΣ μέγεθος/διάρκεια της λήψης index δεν επιβεβαιώθηκε ζωντανά (κανένα
πραγματικό SDI/δίκτυο διαθέσιμο σε sandbox). Αν ΚΑΙ το νέο όριο 3 λεπτών αποδειχτεί ανεπαρκές σε αργή
σύνδεση, το UI ήδη εξηγεί τιμίως ότι μπορεί να χρειαστεί επανάληψη - καμία σιωπηλή αποτυχία.

**"Πλήρης μεγιστοποίηση"**: ΡΗΤΑ αφέθηκε fixed-size προς το παρόν, κατόπιν επιλογής του χρήστη μετά από
ανάλυση ρίσκου (το responsive resize είχε ήδη εγκαταλειφθεί 2 φορές παλιότερα - βλ. ενότητα 0.5 παρακάτω).
Αν ζητηθεί ξανά στο μέλλον, η επιλογή "letterbox" (μεγιστοποίηση με ΣΤΑΘΕΡΟ μέγεθος περιεχομένου
κεντραρισμένο στο μεγαλύτερο παράθυρο, καμία αναδιάταξη) ήταν η προτεινόμενη, χαμηλού ρίσκου εναλλακτική.

## 0.8 v2.5.0: letterbox μεγιστοποίηση/resize (ΔΙΑΒΑΣΕ ΠΡΙΝ ΑΓΓΙΞΕΙΣ `$mainForm`/sidebar)

**Το αίτημα**: ο χρήστης ζήτησε ΕΠΑΝΕΙΛΗΜΜΕΝΑ (2 φορές στο ίδιο πέρασμα) πραγματική δυνατότητα
resize/maximize "όπως όλα τα παράθυρα στα Windows 11". Το πλήρες responsive redesign (αναδιάταξη όλου
του περιεχομένου) είχε ΗΔΗ εγκαταλειφθεί 2 φορές παλιότερα (v2.0.4) ως εκτός εφικτού πεδίου - βλ. ενότητα
0.5 παρακάτω. Αντ' αυτού εφαρμόστηκε η τεχνική **"letterbox"**: το mainForm έγινε πραγματικά
resizable/maximizable, αλλά ΟΛΟ το ήδη υπάρχον περιεχόμενο (κάθε control που ήταν μέχρι τότε άμεσο παιδί
του `$mainForm`) μεταφέρθηκε ΑΥΤΟΥΣΙΟ, ΧΩΡΙΣ ΚΑΜΙΑ αλλαγή θέσης/μεγέθους, μέσα σε ένα νέο, ΣΤΑΘΕΡΟΥ
μεγέθους (1280x800 - ακριβώς το πρώην `$mainForm.Size`) `$global:canvasPanel`. Το canvas απλά
κεντράρεται μέσα στην τρέχουσα ορατή περιοχή σε κάθε αλλαγή μεγέθους (σαν βίντεο 4:3 σε οθόνη 16:9) -
ΜΗΔΕΝΙΚΗ αναδιάταξη οποιουδήποτε ήδη υπάρχοντος στοιχείου.

**Πού είναι ο κώδικας**:
- `$global:canvasPanel` δημιουργείται και γεμίζει (σκουπίζοντας ΟΛΟΚΛΗΡΟ το `$mainForm.Controls`) στο
  ΑΠΟΛΥΤΑ τελευταίο μπλοκ κώδικα πριν το `$mainForm.ShowDialog()` (τέλος του αρχείου) - ΠΡΕΠΕΙ να παραμείνει
  εκεί, αφού σκουπίζει ΚΥΡΙΟΛΕΚΤΙΚΑ ό,τι έχει προστεθεί μέχρι εκείνο το σημείο. Αν προστεθεί ΝΕΟ control
  απευθείας στο `$mainForm.Controls` (αντί στο `$global:canvasPanel.Controls`) ΜΕΤΑ από αυτό το σημείο,
  θα καταλήξει στο εξωτερικό "letterbox" περιθώριο αντί στο πραγματικό περιεχόμενο της εφαρμογής - ΚΑΝΕ
  `$global:canvasPanel.Controls.Add(...)` για οτιδήποτε νέο ΜΟΝΙΜΟ στοιχείο προστεθεί από εδώ και πέρα
  (dynamically-created controls μέσα σε υπάρχουσες functions/event handlers είναι ήδη ασφαλή - προστίθενται
  σε ήδη-υπάρχοντα panels που ΕΙΝΑΙ ήδη μέσα στο canvas, π.χ. `$rowPanel.Controls.Add(...)`).
- `Update-CanvasLetterbox` (νέα function, ίδιο σημείο) κεντράρει το canvas σε κάθε `$mainForm.Add_Resize`.
- `$mainForm.MinimumSize = new(1296, 839)` - αποτρέπει σμίκρυνση κάτω από το μέγεθος του canvas (το
  mainForm ΔΕΝ έχει δικό του AutoScroll - αν γινόταν μικρότερο από το canvas, τμήματα θα ήταν απρόσιτα).

**ΚΡΙΣΙΜΗ διόρθωση που χρειάστηκε παράλληλα**: ο κώδικας του πλευρικού μενού (☰) - `Get-SidebarOpenX`,
`Get-SidebarHiddenX`, `Update-SidebarFormPosition`, `$global:sidebarSlideTimer`, `Show-Sidebar`,
`Hide-Sidebar`, `$global:sidebarBoundsGuardTimer` (όλα γύρω από γραμμή ~7550-7750) - αναφερόταν ΠΑΝΤΟΥ
απευθείας σε `$mainForm.Left`/`.Width`/`.ClientSize.Height/.Width`/`.PointToScreen(...)`, υποθέτοντας ότι
αυτά ΠΑΝΤΑ αντιστοιχούν στα πραγματικά ορατά όρια της εφαρμογής - υπόθεση που έσπαγε αμέσως μόλις το
mainForm έγινε resizable/μεγαλύτερο από το canvas (π.χ. σε μεγιστοποίηση σε μεγάλη οθόνη). **Αυτή ήταν η
περιοχή με το ΜΕΓΑΛΥΤΕΡΟ ιστορικό επαναλαμβανόμενων, επίμονων bugs σε όλο το project** (πολλαπλά
"ΔΙΟΡΘΩΣΗ ΡΙΖΙΚΗ"/"ΘΕΜΕΛΙΩΔΟΥΣ ΑΡΧΙΤΕΚΤΟΝΙΚΗΣ" σχόλια εκεί, v2.0.4 έως v2.2.0) - ΟΛΕΣ αυτές οι αναφορές
εντοπίστηκαν συστηματικά (μέσω grep, ΟΧΙ οπτικού ελέγχου) και αντικαταστάθηκαν με τα ισοδύναμα του
`$global:canvasPanel` (π.χ. `$mainForm.Left` -> `$mainForm.Left + $global:canvasPanel.Left`,
`$mainForm.ClientSize.Width` -> `$global:canvasPanel.Width`, `PointToScreen([Point]::Empty)` ->
`PointToScreen($global:canvasPanel.Location)`). Στην προεπιλεγμένη, μη-μεγιστοποιημένη κατάσταση
`canvasPanel.Left=(0,0)` και μέγεθος 1280x800 ταυτίζονται ΑΚΡΙΒΩΣ με το πρώην mainForm - άρα η συμπεριφορά
παραμένει ΑΚΡΙΒΩΣ ίδια με πριν σε αυτή την (συχνότερη) κατάσταση, και προσαρμόζεται σωστά μόνο όταν το
παράθυρο γίνεται πραγματικά μεγαλύτερο/μετακινείται το letterbox.
**Το `$global:sidebarEmbedPanel`** (το ΝΕΟ, embedded panel του sidebar - ΟΧΙ το παλιό `$global:sidebarForm`)
είναι πλέον ΠΑΙΔΙ του `$global:canvasPanel` (σκουπίστηκε μαζί με όλα τα άλλα), άρα οι δικές του
συντεταγμένες (`.Left`/`.Top`) είναι ήδη canvas-relative από μόνες τους - χρειάστηκε μόνο να αλλάξουν οι
αναφορές `$mainForm.ClientSize.*` σε `$global:canvasPanel.*` στο `Show-Sidebar`/`Hide-Sidebar` (ΧΩΡΙΣ
προσθήκη `.Left` offset, αφού είναι ήδη τοπικές). Το `$global:sidebarForm` (ξεχωριστό, floating Form -
χρησιμοποιείται ΜΟΝΟ στη μη-anchored κατάσταση) παραμένει ξεχωριστό top-level Form (ΔΕΝ μπορεί να γίνει
παιδί ενός Panel) - χρειάστηκε τον πλήρη υπολογισμό οθόνης-συντεταγμένων (mainForm.Left + canvasPanel.Left).

**ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ (πολύ σημαντική)**: ΔΕΝ υπήρχε ΚΑΜΙΑ δυνατότητα ζωντανής οπτικής επιβεβαίωσης σε
πραγματικά Windows για αυτή τη συγκεκριμένη αλλαγή (μόνο `ParseFile`/static analysis + πολύ προσεκτική,
συστηματική ιχνηλάτηση κάθε αναφοράς `$mainForm.Left/.Width/.Height/.ClientSize` σε όλο το αρχείο μέσω
grep). Αν μετά από αυτό αναφερθεί ΞΑΝΑ κάποιο πρόβλημα θέσης/μεγέθους του sidebar (η ΠΙΟ πιθανή περιοχή
για regressions, δεδομένου του ιστορικού της): **πρώτο σημείο ελέγχου είναι αυτή η ενότητα** - έλεγξε αν
όλες οι αναφορές `$mainForm.*` σε αυτές τις 6-7 συναρτήσεις/timers έχουν πράγματι γίνει `$global:canvasPanel.*`
όπου έπρεπε (grep για `mainForm\.ClientSize|mainForm\.Width\b|mainForm\.Height\b` θα έπρεπε να επιστρέφει
0 αποτελέσματα σε όλο το αρχείο - αν επιστρέφει κάτι, βρέθηκε σημείο που ξέφυγε από αυτό το πέρασμα).
Άλλες πιθανές πηγές προβλημάτων που ΔΕΝ ελέγχθηκαν ζωντανά: Z-order μετά το reparenting (σειρά διατηρήθηκε
βάσει της σειράς στο `$mainForm.Controls` τη στιγμή του reparenting, ΜΕΤΑ από όλα τα `BringToFront()` της
αρχικής κατασκευής - θα ΕΠΡΕΠΕ να διατηρεί το ίδιο οπτικό αποτέλεσμα, δεν επιβεβαιώθηκε).

**ΔΙΟΡΘΩΣΗ v2.6.0 (MinimumSize)**: ζωντανή προσομοίωση (πραγματικό PowerShell + System.Drawing, ΟΧΙ
θεωρητικά) του letterbox-centering μαθηματικού αποκάλυψε ότι το ΣΤΑΤΙΚΟ, μαντεμένο `MinimumSize`
(1296x839) ρίσκαρε να είναι έστω 1px μικρότερο από το πραγματικό border+titlebar chrome ανάλογα με
θέμα/DPI - ΑΚΟΜΑ ΚΑΙ 1px έλλειψη προκαλεί ορατή υπερχείλιση του canvas εκτός ορατής περιοχής (χωρίς
AutoScroll στο ίδιο το mainForm, δεν υπάρχει τρόπος να προσπελαστεί). Διορθώθηκε ώστε το πραγματικό
chrome να ΜΕΤΡΙΕΤΑΙ ζωντανά (`$mainForm.Size - $mainForm.ClientSize`, ΑΦΟΥ το FormBorderStyle έγινε ήδη
Sizable) αντί να μαντεύεται - πιο αξιόπιστο ΓΙΑ ΤΟ ΣΥΓΚΕΚΡΙΜΕΝΟ σύστημα, ΣΥΝ επιπλέον περιθώριο ασφαλείας
(+20/+20) από πάνω, ΣΥΝ κατώφλι (`Math.Max`) στην παλιά τιμή σε περίπτωση που η μέτρηση επιστρέψει κάτι
μη αναμενόμενο πριν εμφανιστεί πραγματικά το παράθυρο. Ακόμα ΔΕΝ επιβεβαιώθηκε ζωντανά σε πραγματικά
Windows (το ίδιο το session δεν έχει δυνατότητα rendering) - αλλά η μέθοδος υπολογισμού είναι πλέον
ουσιωδώς πιο αξιόπιστη από πριν.

## 0.5 Σχέδιο για v2.5.0: πιθανή μετάβαση σε WPF / πιθανή απομάκρυνση από μονολιθικό .ps1

Ρητή οδηγία χρήστη (όχι ακόμα απόφαση, προς εξέταση στην ώρα της): αν μέχρι την v2.5.0 έχουν συσσωρευτεί
πολλές δυσκολίες (οπτικές ή άλλες) που το WinForms genuinely δεν μπορεί να λύσει καθαρά — εξέτασε πλήρη
μετάβαση σε WPF/XAML. ΣΗΜΕΙΩΣΗ: το `reference/WMT-GUI.ps1` είναι ήδη WPF (βλ. ενότητα "Κρίσιμα Τεχνικά
Ευρήματα" παρακάτω) — μια μετάβαση θα μπορούσε ενδεχομένως να αντλήσει ΠΕΡΙΣΣΟΤΕΡΑ από εκεί απευθείας
(layout code, όχι μόνο λογική), κάτι που ΔΕΝ είναι εφικτό τώρα με WinForms. Δεύτερο, ξεχωριστό σημείο:
ΜΕΤΑ το πέρας όλων των ενσωματώσεων (πριν την v2.5.0), αν το ενιαίο `Optimizer.ps1` έχει γίνει πολύ
"βαρύ" (μέγεθος/πολυπλοκότητα), εξέτασε είτε διάσπαση σε modules + assets, είτε μεταγλώττιση σε .exe
(π.χ. μέσω ps2exe ή μεταφορά πυρήνα λογικής σε compiled .NET). Και οι δύο αποφάσεις είναι ΜΕΓΑΛΕΣ
αρχιτεκτονικές αλλαγές - να ΜΗΝ ξεκινήσουν χωρίς ρητή επιβεβαίωση του χρήστη τη στιγμή που θα εξεταστούν,
και σίγουρα όχι πριν ολοκληρωθεί η τρέχουσα δουλειά ενσωματώσεων.

## 0.4 v2.8.2: Η μετάβαση σε WPF ΞΕΚΙΝΗΣΕ — Stage 1 (app shell + Home tab) ολοκληρωμένο

Ρητή απόφαση χρήστη (μετά από επίμονα, δύσκολα προς διάγνωση bugs στο letterbox/scale σύστημα του
WinForms — λάθος τοποθέτηση λωρίδας καρτελών στη μεγιστοποίηση, αναξιόπιστο dark-mode theming
scrollbar παρά από πολλαπλές τεκμηριωμένες τεχνικές): η ενότητα 0.5 παρακάτω ("σχέδιο για v2.5.0,
προς εξέταση") ενεργοποιήθηκε ρητά. Η μετάβαση ΞΕΚΙΝΗΣΕ, με **σταδιακή, ελεγχόμενη προσέγγιση**
(git backup πρώτα, μετά ένα κομμάτι τη φορά με πραγματική επαλήθευση μετά από κάθε κομμάτι) - ΟΧΙ
ένα μονολιθικό, αφύλακτο πέρασμα σε όλη την εφαρμογή.

**Τεχνολογική επιλογή**: πλήρες C# WPF project (`.csproj` + XAML + code-behind), ΟΧΙ το μοτίβο
PowerShell+XamlReader του `reference/WMT-GUI.ps1`. Ρητή επιλογή χρήστη - βλ. συζήτηση στο ίδιο
session. Βρίσκεται στο `wpf/OptimizerWpf/` μέσα στο ίδιο repo - το `Optimizer.ps1` ΔΕΝ αγγίχτηκε,
παραμένει πλήρως λειτουργικό ως fallback μέχρι το WPF να φτάσει σε λειτουργική ισοτιμία.

**Περιβάλλον**: το μηχάνημα είχε ΜΟΝΟ .NET Runtime (8.0.30), καθόλου SDK - εγκαταστάθηκε το .NET 8
SDK μέσω `winget install Microsoft.DotNet.SDK.8` (με ρητή επιβεβαίωση χρήστη πριν την εγκατάσταση).

**Τι υπάρχει ήδη (Stage 1)**:
- `App.xaml`/`ThemeManager.cs`: σύστημα θεματισμού μέσω swappable `ResourceDictionary`
  (`Themes/Dark.xaml`, `Themes/Light.xaml`) - χρώματα αντιγραμμένα από το `Get-ThemeColors`
  "Windows 11 Fluent" (dark+light) του Optimizer.ps1. Χρησιμοποιεί `DynamicResource` παντού (όχι
  `StaticResource`) ώστε η εναλλαγή θέματος να ενημερώνει αυτόματα ΚΑΘΕ control χωρίς το χειροκίνητο
  `allCards`/`allTextLabels` σύστημα παρακολούθησης του WinForms.
- `Themes/Styles.xaml`: κοινά styles (CardStyle, TabPillStyle, FlatButtonStyle, AccentButtonStyle).
- `MainWindow.xaml(.cs)`: το app shell - τίτλος/κουμπιά, οριζόντια λωρίδα 8 καρτελών (RadioButton-based
  pills, GroupName="MainTabs"), content host, γραμμή κατάστασης. Η μεγιστοποίηση/αλλαγή μεγέθους
  χειρίζεται εξ ολοκλήρου το native Grid layout του WPF - ΔΕΝ υπάρχει κανένα αντίστοιχο του
  baseline-capture-and-multiply Set-CanvasScale συστήματος, άρα ΔΟΜΙΚΑ αδύνατο να επαναληφθεί το bug
  της λάθος τοποθετημένης λωρίδας καρτελών στη μεγιστοποίηση.
- `Views/HomeView.xaml(.cs)`: πλήρως χτισμένη καρτέλα Αρχική (κάρτες CPU/RAM/Disk + Βαθμολογία
  Υγείας + Ανάλυση Δίσκου). CPU (`PerformanceCounter`), RAM (`GlobalMemoryStatusEx` P/Invoke) και
  Δίσκος (`DriveInfo`) είναι ΖΩΝΤΑΝΑ δεδομένα, ανανεώνονται κάθε 1 δευτ. μέσω `DispatcherTimer`.
  Ανάλυση Δίσκου: ΔΙΟΡΘΩΘΗΚΕ (χρήστης ανέφερε "δεν φαίνεται να λειτουργεί") - το κουμπί "Ανάλυση" δεν
  είχε καν Click handler σε προηγούμενο πέρασμα. `Services/DiskAnalysisService.cs` είναι πλήρης C#
  port του category-detection script μέσα στο `Start-DiskCategoryAnalysis` (ίδιες κατηγορίες/ρίζες:
  Games [Steam/Epic/Origin/EA/GOG/Ubisoft/Riot/Battle.net], Apps [Program Files], Photos/Videos/
  Documents/Downloads [known folders], Windows [μόνο στον δίσκο συστήματος], Other [υπόλοιπο]) - τρέχει
  σε background thread (`Task.Run`, αργή recursive άθροιση μεγέθους αρχείων).
- `Services/HealthScoreService.cs`: πλήρης C# port του `Get-SystemHealthScore` (ίδιοι έλεγχοι/
  βαθμολογία: χώρος δίσκου συστήματος, εκκρεμής επανεκκίνηση [με το ΙΔΙΟ v2.8.2 fix - χωρίς
  PendingFileRenameOperations], Defender+άλλο AV μέσω WMI, πλήθος εφαρμογών εκκίνησης, ηλικία
  τελευταίου Σημείου Επαναφοράς μέσω WMI `SystemRestore`). Τρέχει μέσω `Task.Run` (ΟΧΙ στο UI thread -
  οι WMI ερωτήσεις μπορεί να αργήσουν αισθητά) και ενημερώνει το `ItemsControl` της κάρτας Βαθμολογίας
  Υγείας στο HomeView όταν ολοκληρωθεί. ΣΗΜΕΙΩΣΗ: η εφαρμογή δεν ζητάει ακόμα elevation (το
  Optimizer.ps1 τρέχει ΠΑΝΤΑ ως Administrator) - κάποιοι έλεγχοι WMI (π.χ. Defender status) μπορεί να
  αποτύχουν σιωπηλά χωρίς αυτό· η elevation δεν έχει προστεθεί ακόμα στο WPF project.
  "Διόρθωση Όλων" κάνει προς το παρόν μόνο τον καθαρισμό temp αρχείων (Storage fix) - η ενεργοποίηση
  Defender/δημιουργία Σημείου Επαναφοράς δεν έχουν μεταφερθεί ακόμα.
- **21 θέματα, ΚΑΘΕ ένα με πραγματική Dark ΚΑΙ Light παραλλαγή** (`ThemeCatalog.cs`/`ThemeColors.cs`/
  `ThemePair` record): ΔΙΟΡΘΩΘΗΚΕ (χρήστης ανέφερε ρητά ότι "όλα τα θέματα είχαν light/dark variations"
  - προηγούμενο πέρασμα είχε λανθασμένα μόνο το "Windows 11 Fluent" με Light παλέτα, λάθος υπόθεση,
  όχι ό,τι πραγματικά υπάρχει στο Get-ThemeColors) - και τα 21×2=42 παλέτες μεταφέρθηκαν αυτούσιες από
  τα δύο switch blocks (`$isDarkMode` true/false) του `Get-ThemeColors`. Το `ThemeManager.SelectTheme`/
  `ToggleLightDark` πλέον διαχωρίζουν σωστά "ποιο θέμα" από "dark ή light" - toggling Light/Dark ΠΑΝΤΑ
  αλλάζει στην αντίστοιχη παραλλαγή του ΤΡΕΧΟΝΤΟΣ θέματος, όχι μόνο του Fluent.
  **UI**: το κουμπί επιλογής θέματος είναι πλέον τετράγωνο εικονίδιο παλέτας (🎨, `BtnThemePicker`) που
  ανοίγει custom `Popup` με λίστα θεμάτων - ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε "οι επιλογές δεν φαίνονται
  καθόλου"): η προηγούμενη εκδοχή ήταν `ComboBox` - το popup ενός ComboBox στο WPF κρατάει ΠΑΝΤΑ λευκό
  φόντο εκτός αν γίνει ρητό retemplate, ενώ το Foreground ήταν το θεματικό TextBrush (π.χ. λευκό σε
  σκούρα θέματα) - λευκό σε λευκό, αόρατο. Το νέο custom `Popup`/`ListBox` ορίζει ρητά δικό του φόντο
  (`CardBgBrush`) μέσω νέου implicit `ListBoxItem` style στο `Themes/Styles.xaml`, άρα δεν μπορεί να
  ξανασυμβεί αυτό το συγκεκριμένο πρόβλημα.
- `Views/OptimizationView.xaml(.cs)`: καρτέλα Βελτιστοποίηση. Office Mode/Gaming Mode αλλάζουν
  πραγματικά το πλάνο ενέργειας Windows (`Services/PowerModeService.cs`, powercfg SCHEME_BALANCED/
  SCHEME_MIN) - ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: μόνο αυτό το κομμάτι μεταφέρθηκε, ΟΧΙ τα Start menu
  suggestions/Explorer restart (Office Mode) ή Network Throttling/HAGS/VBS toggle (Gaming Mode) του
  WinForms. Διαχειριστής Ενημερώσεων Λογισμικού (`Services/WingetService.cs`): πραγματική σάρωση
  `winget upgrade --include-unknown` + επιλεκτική αναβάθμιση - μεταφέρθηκε ΜΟΝΟ η ανάλυση του βασικού
  πίνακα winget (fixed-width column parsing, ίδια λογική με το `Parse-WingetTableSection`)· η
  ξεχωριστή κλήση `--source msstore` και η συγχώνευση pip/npm/choco/κ.λπ. (OTHERPM_JSON section) του
  WinForms δεν έχουν μεταφερθεί ακόμα, άρα η λίστα θα δείχνει λιγότερα αποτελέσματα.
  ΝΕΟ κοινόχρηστο `ToggleSwitchStyle` (Themes/Styles.xaml) - θα ξαναχρησιμοποιηθεί σε κάθε επόμενη
  καρτέλα με tweak toggles.
- `Views/PlaceholderView.xaml(.cs)`: γενικό stand-in για τις 6 καρτέλες που δεν έχουν μεταφερθεί ακόμα
  (Υγεία/Δίκτυο/Επιπλέον Ρυθμίσεις/Bloat/Προηγμένα/Σύστημα) - δείχνει ξεκάθαρα ποια
  καρτέλα λείπει, ώστε το app shell να είναι ΗΔΗ πλήρες/buildable ενώ το περιεχόμενο προστίθεται
  σταδιακά.
- **Κλασικό μενού** (`MainWindow.xaml` `Menu`/`MenuItem`, ρητό αίτημα χρήστη): Εργαλεία/Προβολή/
  Ρυθμίσεις/Βοήθεια, αντιστοιχεί στο `$mainMenu` του Optimizer.ps1. Το "Προβολή" (εναλλαγή καρτέλας)
  είναι ΠΛΗΡΩΣ λειτουργικό - μοιράζεται την ίδια `ShowTabContent` με τη λωρίδα καρτελών, ώστε τα δύο
  ΠΑΝΤΑ να συμφωνούν ποια καρτέλα είναι ενεργή. Τα υπόλοιπα (Κρυφές Λειτουργίες/UWP Manager/Ρυθμίσεις
  Εμφάνισης/Ιστορικό/Βοήθεια) ανοίγουν ένα ειλικρινές MessageBox "δεν έχει μεταφερθεί ακόμα" - τα
  πραγματικά τους παράθυρα δεν υπάρχουν ακόμα στο WPF.
  **ΔΕΝ υλοποιήθηκαν ακόμα σε αυτό το πέρασμα** (ρητό αίτημα χρήστη, εκτός ορίων λόγω όγκου): το
  οριζόντιο μοντέρνο μενού και το πλευρικό (sidebar) μενού με το περιεχόμενό τους - στο WinForms αυτά
  υπήρχαν ΚΥΡΙΩΣ ως εναλλακτικοί τρόποι πλοήγησης στην ΙΔΙΑ λειτουργικότητα (Βοήθεια/Ιστορικό/Κρυφές
  Λειτουργίες/UWP Manager/Ρυθμίσεις), που το κλασικό μενού παραπάνω ΗΔΗ καλύπτει σε αυτό το WPF app -
  σκόπιμα δεν αντιγράφηκαν 1:1, θα χρειαστεί ξεχωριστή συζήτηση αν ο χρήστης τα θέλει ΠΡΑΓΜΑΤΙΚΑ ως
  τρίτο/τέταρτο, παράλληλο σύστημα πλοήγησης ή αν το κλασικό μενού αρκεί.
- **Ενημερωτής Winget**: πρόσθεση `ProgressBar` (indeterminate - το winget CLI δεν εκθέτει πραγματικό
  %, οπότε ένα καθορισμένο ποσοστό θα ήταν ψεύτικο) ορατή μόνο κατά τη σάρωση/αναβάθμιση, ΚΑΙ πραγματικό
  μήνυμα επιτυχίας/αποτυχίας (γραμμή κατάστασης της κάρτας + `MessageBox`) - ΔΙΟΡΘΩΘΗΚΕ ΡΙΖΙΚΑ ΚΑΙ ένα
  δεύτερο, σχετικό bug: το `WingetService.UpgradeAsync` θεωρούσε "επιτυχία" ΟΤΙΔΗΠΟΤΕ εκτός αν πετούσε
  exception (σχεδόν ποτέ) - τώρα ελέγχει το πραγματικό exit code της διεργασίας winget.
- **Πλακίδιο δίσκου - βελάκια αντί για dropdown, εικονίδιο ανά τύπο δίσκου** (ρητό αίτημα χρήστη): το
  `ComboBox` αντικαταστάθηκε από δύο κουμπιά-βελάκια στα άκρα του πλακιδίου (`BtnDrivePrev`/
  `BtnDriveNext`, HomeView.xaml) που κυκλώνουν στη λίστα δίσκων. Νέο `Services/DriveTypeService.cs`
  ανιχνεύει HDD/SSD/USB: αφαιρούμενα (USB) μέσω `DriveInfo.DriveType`, HDD/SSD για σταθερούς δίσκους
  μέσω αλυσίδας WMI associators στο `root\Microsoft\Windows\Storage`
  (`MSFT_Partition`→`MSFT_Disk`→`MSFT_PhysicalDisk.MediaType`, η μόνη αξιόπιστη πηγή αυτής της
  πληροφορίας - το παλιό `Win32_DiskDrive` δεν τη διακρίνει). Το εικονίδιο/χρώμα/ετικέτα του πλακιδίου
  αλλάζουν ανάλογα, χωρίς να καλείται σε κάθε tick του 1-δευτερόλεπτου refresh (μόνο όταν αλλάζει ο
  επιλεγμένος δίσκος).
- **Πιο όμορφα εικονίδια CPU/RAM**: `RadialGradientBrush` (γυαλιστερή, "3D" όψη σφαίρας) αντί για
  επίπεδο χρώμα, με σωστά emoji γλυφή (⚙ για CPU, 💾 για RAM) αντί για απλό κείμενο "CPU"/"RAM".
  ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: "3D" εδώ σημαίνει glossy gradient badge, όχι πραγματικά τρισδιάστατα
  εικονογραφημένα icons (θα χρειάζονταν εξωτερικά assets/εικαστικό σχέδιο, εκτός δυνατοτήτων αυτού
  του περάσματος χωρίς οπτικό έλεγχο).

**Επαλήθευση που έγινε (ΚΑΙ γιατί είναι ΠΙΟ αξιόπιστη από το WinForms/PowerShell)**: `dotnet build`
(0 warnings/errors) ΚΑΙ πραγματική εκτέλεση του .exe (`OptimizerWpf.exe`) με έλεγχο crash/exit code -
ΟΧΙ μόνο static parsing όπως το `ParseFile()` στο Optimizer.ps1. Αυτό ΕΝΤΟΠΙΣΕ ΚΑΙ ΔΙΟΡΘΩΘΗΚΑΝ 3
πραγματικά runtime bugs ΠΡΙΝ παραδοθούν (κάτι αδύνατο να γίνει στο WinForms χωρίς οπτικό έλεγχο):
(1) `DllImport` entry point mismatch (μετονομάστηκε η C# μέθοδος αλλά όχι το `EntryPoint`), (2) C#
method overload σύγκρουση (`ref` vs `out` δεν αρκεί για διαφοροποίηση), (3) `IsChecked="True"` στο
XAML προκαλούσε `NullReferenceException` επειδή το event έτρεχε ΠΡΙΝ ολοκληρωθεί το
`InitializeComponent()` (διορθώθηκε: αρχική επιλογή καρτέλας τίθεται ΜΕΤΑ το `InitializeComponent()`
στον constructor, όχι μέσα στο XAML). ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: ΔΕΝ υπάρχει ακόμα τρόπος να δει κανείς
ζωντανά την ΟΠΤΙΚΗ απόδοση (χρώματα/διάταξη/στοίχιση) - μόνο ότι χτίζεται και τρέχει χωρίς exception.
Ο χρήστης πρέπει να το τρέξει ο ίδιος και να επιβεβαιώσει οπτικά.

**Πώς να το τρέξεις**: `cd wpf/OptimizerWpf && dotnet run` (ή build+τρέξε απευθείας το
`bin/Debug/net8.0-windows/OptimizerWpf.exe`).

**Επόμενα βήματα (ΜΗΝ τα ξεκινήσεις όλα μαζί - ένα-ένα, με έλεγχο μετά από κάθε ένα)**:
1. Ζωντανή Βαθμολογία Υγείας στην Αρχική (μεταφορά λογικής `Get-SystemHealthScore`) + πραγματικά
   δεδομένα δίσκου/Ανάλυσης Δίσκου.
2. Μία-μία οι υπόλοιπες 7 καρτέλες (προτεινόμενη σειρά: Βελτιστοποίηση, μετά Υγεία, μετά οι
   υπόλοιπες - βλ. ενότητα 4 παρακάτω για τη σειρά προτεραιότητας του χρήστη σε άλλα θέματα).
3. Δευτερεύοντα παράθυρα (Βοήθεια, Ιστορικό Εκδόσεων, Ρυθμίσεις Εμφάνισης, ViVeTool, UWP Manager...).
4. Τα υπόλοιπα συστήματα μενού (οριζόντιο μοντέρνο, πλευρικό, PC Manager skin) - ΠΡΟΣΟΧΗ: μπορεί να
   ΜΗΝ χρειάζονται καν χωριστή υλοποίηση στο WPF, αφού το native resizing/layout καλύπτει μέρος του
   λόγου ύπαρξής τους (π.χ. το letterbox/scale σύστημα) - επανεξέτασε την ανάγκη τους όταν φτάσεις εκεί
   αντί να τα αντιγράψεις τυφλά.
5. Git commit μετά από ΚΑΘΕ ολοκληρωμένο, χτισμένο/τρεγμένο κομμάτι (όχι μόνο στο τέλος) - το ίδιο
   δίχτυ ασφαλείας που μόλις αποκτήθηκε για το Optimizer.ps1 ισχύει εξίσου εδώ.

## 0.4β WPF: διόρθωση σκούρου θέματος στο κλασικό μενού, πραγματικές συντομεύσεις, εικονίδια καρτελών, spinner κατάστασης, μοντέρνο scrollbar

Batch διορθώσεων μετά από αναφορά χρήστη με screenshot (κλασικό μενού ανοιχτόχρωμο σε dark mode) +
λίστα επιπλέον, μικρότερων bugs/ελλείψεων. `dotnet build` (0 warnings/errors) ΚΑΙ πραγματική εκτέλεση
`OptimizerWpf.exe` (χωρίς crash) επιβεβαιώθηκαν πριν την παράδοση - βλ. πολιτική ενότητα 0.4 παραπάνω.

**Τι έγινε**:
- **Κλασικό μενού, σκούρο θέμα** (`Themes/Styles.xaml`, νέο implicit `Style TargetType="MenuItem"`):
  ίδια ρίζα με το ήδη διορθωμένο ComboBox του theme picker - το native `Menu`/`MenuItem` submenu
  `Popup` κρατάει ΠΑΝΤΑ ανοιχτόχρωμο φόντο ανεξάρτητα από `DynamicResource` bindings. Πλήρες
  retemplate και για τους δύο ρόλους (`TopLevelHeader`/`SubmenuItem`) με ρητό `CardBgBrush`.
- **Πραγματικές συντομεύσεις πληκτρολογίου**: το `InputGestureText="Ctrl+1"` κ.λπ. στο XAML ήταν ΜΟΝΟ
  κείμενο - δεν συνδέεται αυτόματα με πραγματική συντόμευση στο WPF χωρίς `ICommand`/`InputBinding`.
  Προστέθηκε `Window_PreviewKeyDown` (`MainWindow.xaml.cs`) που χειρίζεται Ctrl+1..6 (εναλλαγή
  καρτέλας) πραγματικά. Το SubmenuItem template τώρα ΔΕΙΧΝΕΙ ΚΑΙ το `InputGestureText` σε ξεχωριστή
  στήλη (το default template δεν το απέδιδε καθόλου οπτικά πριν).
- **Ctrl+M**: νέο, πραγματικό shortcut που κρύβει/δείχνει ολόκληρο το κλασικό μενού (`ClassicMenu`,
  πλέον με `x:Name`). Σκόπιμα togglable (όχι πάντα-ορατό) - θα χρησιμεύσει αργότερα στο σχεδιαζόμενο
  "Windows Classic" skin (τετραγωνισμένο παράθυρο, βλ. αίτημα χρήστη) όπου το κλασικό μενού θα είναι
  μέρος του χαρακτηριστικού chrome, ΟΧΙ μόνιμο στοιχείο του τρέχοντος modern shell.
- **Εικονίδια στις καρτέλες**: κάθε `RadioButton.Content` στη λωρίδα καρτελών είναι πλέον ένα
  StackPanel γλυφή+κείμενο αντί για απλό string (🏠 Αρχική, ⚡ Βελτιστοποίηση, 🩺 Υγεία, 🌐 Δίκτυο, 🔧
  Ρυθμίσεις, 🧹 Bloat, 🛠 Προηγμένα, 🖥 Σύστημα). ΠΡΟΣΟΧΗ αν ξαναπειράξεις `TabButton_Checked`: το
  label για το `PlaceholderView`/`ShowTabContent` τραβιέται πλέον με `GetTabLabel()` helper (διαβάζει
  το δεύτερο `TextBlock` παιδί) - ΟΧΙ `rb.Content.ToString()`, που θα επέστρεφε το όνομα του
  StackPanel type.
- **Κυκλικό εικονίδιο δραστηριότητας στη γραμμή κατάστασης**: νέο `Services/StatusService.cs` -
  static class με `SetBusy(message)`/`SetIdle(message)` + `event Action<string,bool>? Changed`. Κάθε
  view/service το καλεί χωρίς να ξέρει τίποτα για το status bar· το `MainWindow` είναι ο ΜΟΝΑΔΙΚΟΣ
  συνδρομητής, ενημερώνει `TxtStatus` ΚΑΙ παίζει/σταματάει `Storyboard` περιστροφής σε ένα μικρό
  ημικυκλικό `Path` (ορατό μόνο όσο busy=true). Συνδέθηκε ΗΔΗ σε πραγματικές background εργασίες:
  εκκίνηση εφαρμογής, `UpdateDiskIconAsync`, `BtnAnalyzeDisk_Click`, `RefreshHealthScoreAsync`
  (HomeView), σάρωση/αναβάθμιση winget (OptimizationView `SetBusy`). ΔΕΝ έχει συνδεθεί ακόμα σε καμία
  μελλοντική νέα async λειτουργία - κάθε νέο `Task.Run`/βαρύ async call ΠΡΕΠΕΙ να καλεί
  `StatusService.SetBusy/SetIdle` γύρω του, όχι μόνο τοπικά UI state.
- **Μοντέρνο (Windows 11-style) scrollbar** (`Themes/Styles.xaml`, νέο implicit `Style
  TargetType="{x:Type ScrollBar}"`): λεπτή, διάφανη λωρίδα με στρογγυλεμένο thumb, χωρίς βελάκια -
  εφαρμόζεται ΑΥΤΟΜΑΤΑ σε κάθε `ScrollViewer` της εφαρμογής (δεν χρειάστηκε retemplate του ίδιου του
  `ScrollViewer`, μόνο του εσωτερικού `ScrollBar` που χρησιμοποιεί μέσω implicit style lookup).
- **Theme picker, δύο ξεχωριστά bugs**:
  1. *Δεν φαινόταν ποιο θέμα είναι επιλεγμένο*: ο υπάρχων trigger (`TabSelectBrush` φόντο) ήταν οπτικά
     πολύ κοντά στο hover χρώμα. Προστέθηκαν ΤΡΙΑ ανεξάρτητα, ισχυρότερα σήματα στο επιλεγμένο
     στοιχείο: κάθετη λωρίδα `AccentBrush` αριστερά, ✓ δεξιά, έντονη γραφή.
  2. *Το ποντίκι ρολάριζε μόνο πάνω από τη μπάρα κύλισης*: το `ListBox` ήταν τυλιγμένο σε ΔΕΥΤΕΡΟ,
     χειροκίνητο `ScrollViewer` - το `ListBox` έχει ΗΔΗ δικό του εσωτερικό `ScrollViewer` μέσα στο
     default template του, και ο εμφωλευμένος διπλασιασμός μπέρδευε ποιος "κατέχει" το scroll. Λύση:
     αφαιρέθηκε το εξωτερικό `ScrollViewer` - `MaxHeight`/`ScrollViewer.VerticalScrollBarVisibility`
     μπαίνουν απευθείας πάνω στο `ListBox`.

**ΔΕΝ έγιναν σε αυτό το πέρασμα** (ρητά αναφερόμενα από τον χρήστη, εκτός ορίων λόγω όγκου -
ΣΕΙΡΑ που θα ακολουθηθεί στο επόμενο πέρασμα):
1. Ολοκλήρωση Optimization tab (Start menu suggestions/Explorer restart για Office Mode, HAGS/VBS/
   Network Throttling για Gaming Mode) + winget msstore-source/other-package-manager merge.
2. Οριζόντιο μοντέρνο μενού + πλευρικό (αριστερό/δεξί) μενού - με προηγούμενο σχεδιασμό
   ("σκέψου") ώστε το πλευρικό να είναι skin-agnostic (να μην επηρεάζεται από το μελλοντικό "Windows
   Classic" skin ούτε το PC-Manager-equivalent skin, ίδια κατηγοριοποίηση και στα δύο).
3. Ανάλυση Χώρου Δίσκου: δευτερεύον/λεπτομερές παράθυρο με bar chart "όπως στο ps1" + περιγραφή σε
   δύο γραμμές ανά κατηγορία (τώρα μόνο μονογραμμικές μπάρες μέσα στην ίδια κάρτα).
4. Πλακίδιο δίσκου: γράμμα δίσκου + περιγραφή/μάρκα στο κείμενο χρησιμοποιημένου χώρου· θερμοκρασία
   (Κελσίου, με εικονίδιο θερμομέτρου) στο πλακίδιο δίσκου ΚΑΙ στα άλλα πλακίδια (CPU τουλάχιστον) -
   χρειάζεται πρώτα έρευνα, καμία καθολική/αξιόπιστη μέθοδος δεν είναι εγγυημένη σε όλο το hardware.
5. 4ο πλακίδιο GPU (θερμοκρασία + % χρήσης) στην ίδια σειρά με CPU/RAM/Disk, στενεύοντας τα άλλα 3.
6. Εις βάθος σάρωση όλου του Optimizer.ps1 για ανεξάρτητη επανεκτίμηση IA (ποια καρτέλα/κάρτα/
   κατηγορία, αν χρειάζονται νέες καρτέλες/κατηγορίες μενού/κουμπιά κεντρικής οθόνης).
7. Μελλοντικό "Windows Classic" skin (τετραγωνισμένο παράθυρο + κλασικά οπτικά στοιχεία).

## 0.4γ WPF: διόρθωση 3 bugs από screenshot μετά το 0.4β (tab strip clipping, εικονίδιο Υγείας, Ctrl+7/8)

Ο χρήστης έστειλε screenshot της τρέχουσας κατάστασης μετά το 0.4β και εντόπισε 3 προβλήματα:

1. **Η καρτέλα "Σύστημα" κοβόταν στη δεξιά άκρη του παραθύρου** - 8 pills με εικονίδιο+κείμενο δεν
   χωράνε πια σε πλάτος 1000-1280px. Ο απλός οριζόντιος `StackPanel` (`TabStrip`, `MainWindow.xaml`)
   τυλίχτηκε σε `ScrollViewer` (`HorizontalScrollBarVisibility="Auto"`) - αν δεν χωράνε όλες οι
   καρτέλες εμφανίζεται λεπτή οριζόντια μπάρα (το ίδιο μοντέρνο ScrollBar style) αντί να κόβεται
   σιωπηλά η τελευταία, ανεξάρτητα από το πλάτος του παραθύρου.
2. **Το εικονίδιο της καρτέλας "Υγεία & Συντήρηση" δεν φαινόταν** (tofu/κενό κουτάκι) - το U+1FA7A
   (Stethoscope) είναι Emoji 13.1 (πολύ πρόσφατο Unicode block), δεν καλύπτεται αξιόπιστα σε όλες τις
   εκδόσεις Windows 10/11. Αντικαταστάθηκε με ✚ (U+271A, Dingbats - πολύ παλιό, καθολικά
   υποστηριζόμενο), σύμφωνα με την ήδη καθιερωμένη πολιτική "αν ένα glyph δεν είναι εγγυημένα
   ασφαλές, μη χρησιμοποιείται" (βλ. ενότητα Γ, πολιτική Ιστορικού Εκδόσεων - επεκτάθηκε εδώ και στα
   tab icons).
3. **Ctrl+7/Ctrl+8 δεν υπήρχαν** για Προηγμένα Εργαλεία/Σύστημα (μόνο 1-6 είχαν συντόμευση) -
   προστέθηκαν στο `Window_PreviewKeyDown` (`MainWindow.xaml.cs`) ΚΑΙ ως `InputGestureText` στα
   αντίστοιχα `MenuItem` του κλασικού μενού.

`dotnet build` (0/0) + πραγματική εκτέλεση (χωρίς crash) επιβεβαιώθηκαν πριν το commit.

## 0.4δ WPF: πλήρες Office/Gaming Mode + winget msstore/other-package-manager merge (2 από τα deferred items ολοκληρώθηκαν)

Ολοκλήρωση 2 από τα ρητά καταγεγραμμένα deferred items (§0.4β/§0.4γ):

**1. Πλήρες Office Mode / Gaming Mode** (`Services/PowerModeService.cs`, πλήρης port του
`Set-OfficeMode`/`Disable-OfficeMode`/`Set-GamingMode`/`Disable-GamingMode`, ~10855-10946): πέρα από
το powercfg plan-switch που υπήρχε ήδη, τώρα εφαρμόζονται ΚΑΙ όλα τα registry tweaks (οπτικά εφέ,
SmartScreen, Start menu suggestions/tips για Office Mode· Game Mode/Game DVR, Network Throttling,
HAGS, Win32PrioritySeparation, προσωρινή απενεργοποίηση VBS/HVCI για Gaming Mode) ΚΑΙ η επανεκκίνηση
Explorer στην απενεργοποίηση - ίδια ακριβώς registry keys/τιμές με το ps1 original. `SetRegSafe`
(C# helper, `Microsoft.Win32.Registry`) καταπίνει σφάλματα ανά key ακριβώς όπως το `Set-RegSafe` του
ps1 (ένα αποτυχημένο HKLM key λόγω έλλειψης elevation δεν σταματάει τα υπόλοιπα). Το status bar
(`StatusService`) ενημερώνεται τώρα γύρω από κάθε toggle. ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ (αμετάβλητη): η
εφαρμογή δεν ζητάει elevation - τα HKLM keys θα αποτύχουν σιωπηλά χωρίς Administrator, όπως θα
συνέβαινε και στο ps1 αν δεν έτρεχε πάντα elevated.

**2. Winget msstore + 10 άλλοι package managers** (`Services/WingetService.cs`, πλήρης port του
`Complete-WingetListLoad`/`Get-UpgradeCommandForSource`, ~11112-11760): το `WingetUpdate` record
απέκτησε πεδίο `Source`. Η σάρωση τώρα καλεί ΚΑΙ το επίσημο Microsoft Store CLI (`store updates` -
ίδιο parsing με box-drawing `│` separator ή whitespace, ίδιο header-detection) ΚΑΙ τους 10 άλλους
providers (pip/npm[+global]/pnpm[+global]/chocolatey/scoop/gem/cargo/dotnet/psmodule/composer) -
καθένας best-effort (`Get-Command`-equivalent: αν το `Process.Start` πετάξει `Win32Exception`
σημαίνει "δεν είναι εγκατεστημένο", αγνοείται σιωπηλά, ΔΕΝ σταματάει τους υπόλοιπους). Συγχώνευση με
αφαίρεση διπλότυπων ίδια με το ps1 (`HashSet` από Id για winget/msstore, `"$Source:$Id"` για τους
άλλους providers). Το `psmodule` provider καλεί εσωτερικά `powershell.exe` (η αναζήτηση εκδόσεων στο
PSGallery είναι εγγενώς PowerShell tooling, ίδιο σκεπτικό με το γιατί το antίστοιχο upgrade command
ήδη περνούσε από PowerShell). `GetUpgradeCommandForSource` προστέθηκε επίσης - η "Αναβάθμιση
Επιλεγμένων" τώρα δρομολογεί τη ΣΩΣΤΗ εντολή αναβάθμισης ανά provider (π.χ. `pip install --upgrade`,
`npm update`, `choco upgrade`), όχι πάντα `winget upgrade`. Η κάρτα κάθε αναβάθμισης στο UI δείχνει
πλέον και το Source (π.χ. "· npm"). ΠΡΟΣΟΧΗ: η σάρωση τρέχει έως 12 εξωτερικές διεργασίες
διαδοχικά (κάθε μία με ανεξάρτητο timeout 20-60s ώστε μία κολλημένη να μην μπλοκάρει τις υπόλοιπες) -
μπορεί να διαρκέσει αισθητά περισσότερο από πριν σε συστήματα με πολλούς εγκατεστημένους package
managers· δεν δοκιμάστηκε ζωντανά με πραγματικά εγκατεστημένα pip/npm/κ.λπ. (μόνο `dotnet build` +
εκκίνηση εφαρμογής χωρίς crash) - ο χρήστης πρέπει να το δοκιμάσει με "Σάρωση" στην καρτέλα
Βελτιστοποίηση για πλήρη επιβεβαίωση.

`dotnet build` (0/0) + πραγματική εκτέλεση (χωρίς crash) επιβεβαιώθηκαν πριν το commit.

## 0.4ε WPF: λείπον fallback κουμπί Store update-scan (χρήστης ανέφερε "δεν φαίνονται οι ενημερώσεις των metro εφαρμογών")

Διάγνωση: δοκιμάστηκε ζωντανά σε αυτό το μηχάνημα το ΑΚΡΙΒΩΣ ίδιο invocation pattern που χρησιμοποιεί
το `ScanMsStoreAsync` (redirected stdin/stdout, εγγραφή "n") - το πραγματικό `store updates` CLI
επέστρεψε "No updates found." τόσο από interactive shell όσο και από το redirected pattern. Άρα το
parsing του `WingetService` ΔΕΝ έχει bug - λειτουργεί σωστά. Η πραγματική αιτία: το `store` CLI
παραμένει "Preview" tool (επιβεβαιωμένο από το ίδιο `store --help`) και δεν πιάνει πάντα ΟΛΕΣ τις
εκκρεμείς ενημερώσεις Metro/UWP εφαρμογών - ΑΚΡΙΒΩΣ το ίδιο, ήδη τεκμηριωμένο όριο που το ps1 original
περιέγραφε στο σχόλιό του (~11219-11239) γι' αυτό και κρατούσε ΞΕΧΩΡΙΣΤΟ κουμπί
`$global:btnCheckMsStore` ως backup - το κουμπί αυτό απλά δεν είχε μεταφερθεί στο WPF (μόνο η λίστα
μέσα στο Complete-WingetListLoad είχε μεταφερθεί, όχι αυτό το ξεχωριστό, συμπληρωματικό στοιχείο).

**Διόρθωση**: προστέθηκε `WingetService.TriggerMsStoreUpdateScanAndOpen()` - καλεί το επίσημο
`MDM_EnterpriseModernAppManagement_AppManagement01` CIM provider (`root\cimv2\mdm\dmmap`,
`UpdateScanMethod` - το ΙΔΙΟ μηχανισμό που χρησιμοποιεί το ίδιο το Store όταν ο χρήστης πατά "Check
for updates" εκεί) ΚΑΙ ανοίγει `ms-windows-store://downloadsandupdates`. Νέο κουμπί
`BtnCheckMsStore` στην κάρτα Winget του `OptimizationView.xaml`, κάτω από τα υπάρχοντα
Σάρωση/Αναβάθμιση - ΣΗΜΕΙΩΣΗ: αυτή η μέθοδος ΔΕΝ επιστρέφει λίστα (μόνο ενεργοποιεί σάρωση στο
παρασκήνιο, ΙΔΙΑ συμπεριφορά με το ps1 original) - τα αποτελέσματα εμφανίζονται ΜΕΣΑ στο ίδιο το
Store app, όχι στη λίστα της εφαρμογής.

`dotnet build` (0/0) + πραγματική εκτέλεση (χωρίς crash) επιβεβαιώθηκαν. ΔΕΝ επαληθεύτηκε ζωντανά αν
αυτό λύνει πλήρως το αναφερόμενο πρόβλημα του χρήστη (χρειάζεται το ίδιο το Store app να δείξει
πράγματι εκκρεμείς ενημερώσεις μετά το trigger, κάτι που μόνο ο χρήστης μπορεί να επιβεβαιώσει).

## 0.4στ WPF: ΠΡΑΓΜΑΤΙΚΗ ρίζα του "λιγότερες ενημερώσεις msstore από την 2.8.2" - λείπε elevation

Ο χρήστης έστειλε side-by-side screenshot: 2.8.2 = 20 ενημερώσεις (με [msstore] Spotify/Xbox
Identity/Xbox TCUI ορατά), WPF = 7 (μόνο winget). Η αρχική μου υπόθεση ("stale scan, ξανασάρωσε")
απορρίφθηκε ρητά από τον χρήστη ("ειναι live to αποτελεσμα"). Επαλήθευση:
- `winget upgrade --include-unknown ...` απευθείας στο τερματικό (μη-elevated) → 7 εφαρμογές, ΑΚΡΙΒΩΣ
  ίδιες με το WPF - το winget parsing είναι σωστό.
- `store updates` απευθείας (μη-elevated) → "No updates found." - ΙΔΙΟ με το WPF.
- ΡΙΖΑ: το Optimizer.ps1 (2.8.2) ΤΡΕΧΕΙ ΠΑΝΤΑ elevated (self-elevation στην εκκίνηση, ήδη
  τεκμηριωμένο στο §0.4 παραπάνω) - το WPF exe ΔΕΝ ζητούσε ΚΑΘΟΛΟΥ elevation μέχρι τώρα. Το ίδιο
  `store.exe updates` CLI επιστρέφει διαφορετικά (περισσότερα) αποτελέσματα όταν καλείται από
  elevated (Administrator) context - επιβεβαιωμένο συμπέρασμα, ίδια κατηγορία με άλλα ήδη
  τεκμηριωμένα σημεία της εφαρμογής που απαιτούν elevation (Defender/AV WMI queries στο
  HealthScoreService, HKLM registry keys του Gaming Mode).

**Διόρθωση**: νέο `wpf/OptimizerWpf/app.manifest` με
`<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />` + DPI awareness,
συνδεδεμένο στο project μέσω `<ApplicationManifest>app.manifest</ApplicationManifest>` στο
`.csproj`. Κάθε εκκίνηση του `.exe` πλέον εμφανίζει το UAC prompt αυτόματα, όπως το ps1.
`Run_OptimizerWpf.bat` ενημερώθηκε να ελέγχει ρητά (`net session`) αν ήδη τρέχει elevated και, αν
όχι, να ξεκινάει το exe μέσω `Start-Process -Verb RunAs` αντί για απλό `start`.

**ΣΗΜΑΝΤΙΚΗ ΣΥΝΕΠΕΙΑ ΓΙΑ ΤΗ ΔΙΑΔΙΚΑΣΙΑ ΕΠΑΛΗΘΕΥΣΗΣ** (§0.4, "dotnet build + πραγματική εκτέλεση"):
μετά από αυτή την αλλαγή, η εκκίνηση του .exe από μη-elevated PowerShell/Bash session (όπως γίνεται
σε κάθε προηγούμενο πέρασμα επαλήθευσης) μπορεί να ζητήσει UAC consent - αν ζητηθεί ενεργά (secure
desktop prompt), ΔΕΝ μπορεί να απαντηθεί αυτόματα/μη-διαδραστικά. Σε αυτό το μηχάνημα η δοκιμή
έδειξε ότι η διεργασία ξεκίνησε elevated ΧΩΡΙΣ ορατό prompt (πιθανώς λόγω τοπικών ρυθμίσεων UAC) -
ΔΕΝ είναι εγγυημένο σε κάθε περιβάλλον. Επιβεβαιώθηκε ΕΜΜΕΣΑ ότι η ανύψωση δικαιωμάτων όντως
λειτούργησε: το `Stop-Process` από το ίδιο μη-elevated shell απέτυχε με "Access is denied" -
ακριβώς η αναμενόμενη συμπεριφορά όταν μια τυπική διεργασία προσπαθεί να τερματίσει μια elevated.
Από εδώ και πέρα, όποτε χρειαστεί να κλείσει ένα ανοιχτό instance του WPF exe πριν από `dotnet
build`, θα χρειαστεί να το κλείσει ο ΧΡΗΣΤΗΣ χειροκίνητα (δεν μπορεί πλέον να γίνει αξιόπιστα μέσω
`Stop-Process` από μη-elevated context).

`dotnet build` (0/0) επιβεβαιώθηκε. Η ζωντανή εκτέλεση επιβεβαιώθηκε έμμεσα (elevated process
started, access-denied στο kill attempt) αλλά ΔΕΝ επαληθεύτηκε οπτικά ότι το UAC prompt/η ίδια η
εφαρμογή συμπεριφέρεται σωστά σε φυσιολογική, διαδραστική εκκίνηση από τον χρήστη - χρειάζεται τη
δική του δοκιμή.

## 0.4ζ WPF: πλευρικό μενού (skin-agnostic) + ενοποίηση κατηγοριοποίησης - 1 από τα deferred items ολοκληρώθηκε

Deferred item "πλευρικό (αριστερό/δεξιό) μοντέρνο μενού" - ο χρήστης ζήτησε ρητά να σκεφτώ πρώτα
σχεδιαστικά ώστε να μην επηρεάζεται από μελλοντικά skins (Windows Classic, PC Manager-equivalent) και
να μη χρειάζεται διαφορετική κατηγοριοποίηση ανά skin.

**Σχεδιαστική απόφαση**: νέο `NavItem.cs` (root namespace) - `record NavItem(string Tag, string Icon,
string Label)` + στατική λίστα `NavItems.All` (τα 8 στοιχεία, μία και μοναδική φορά). Το κλασικό
μενού ΚΑΙ το νέο πλευρικό μενού αντλούν και τα δύο από αυτή τη λίστα αντί να έχουν το καθένα δικό
του, χειρόγραφο αντίγραφο (πριν: 2 σημεία με 8 hand-written MenuItem/RadioButton το καθένα - ρίσκο
"πρόσθεσα καρτέλα, ξέχασα να την προσθέσω και στο άλλο μενού"). Το `MenuViewParent` (η "Προβολή"
του κλασικού μενού) γεμίζει πλέον από κώδικα (`PopulateViewMenu()`, `MainWindow.xaml.cs`) αντί να
έχει 8 χειρόγραφα `<MenuItem>` στο XAML. Η λωρίδα καρτελών (TabStrip) ΣΚΟΠΙΜΑ ΔΕΝ μετατράπηκε σε
data-driven - ήδη δοκιμασμένη/σταθερή, η μετατροπή θα ρίσκαρε να ξαναδημιουργήσει το IsChecked-timing
bug (§0.4) χωρίς αντίστοιχο όφελος, αφού δεν ήταν αυτή η πηγή του προβλήματος διπλής κατηγοριοποίησης.

**`Views/SidebarNav.xaml(.cs)`** (νέο, αυτόνομο `UserControl`): κάθετη λίστα εικονίδιο+ετικέτα
(RadioButton ανά `NavItem`, δικό του `GroupName="SidebarNav"` - ανεξάρτητο από το "MainTabs" της
λωρίδας καρτελών, άρα δεν μπερδεύονται). Καθαρά presentation + 2 events
(`NavigationRequested(string tag)`, `PositionToggleRequested()`) - ΔΕΝ γνωρίζει τίποτα για
`ShowTabContent`/ποια καρτέλα είναι "ενεργή" εκτός αν του το πει ρητά ο owner μέσω `SetActiveTag`.
Αυτός ο διαχωρισμός είναι ΑΚΡΙΒΩΣ αυτό που το κάνει skin-agnostic: ένα μελλοντικό skin μπορεί να
τοποθετήσει το ΙΔΙΟ control στο δικό του window chrome χωρίς καμία αλλαγή - το layout (θέση/πλάτος)
αποφασίζεται πάντα από τον owner (`MainWindow`), ποτέ από το ίδιο το control. Έχει δικό του κουμπί
σύμπτυξης/ανάπτυξης (236px ↔ 72px, φυσική αποκοπή κειμένου στο στενό πλάτος - όχι πολύπλοκη
introspection του DataTemplate) ΚΑΙ κουμπί ⇄ για εναλλαγή αριστερά/δεξιά.

**`MainWindow.xaml`**: η σειρά περιεχομένου (πρώην απλό `Border`) έγινε 3-στηλο `Grid`
(`ColA`/`ColGap`/`ColB`) που φιλοξενεί το `SidebarNav` (`x:Name="Sidebar"`, κρυφό εξ ορισμού) και το
`ContentHostBorder`. `BtnHamburger` (προϋπήρχε στο title bar, ΔΕΝ ήταν συνδεδεμένο με τίποτα μέχρι
τώρα) ανοίγει/κλείνει το πλευρικό μενού. `ApplySidebarLayout()` (`MainWindow.xaml.cs`) διαχειρίζεται
Grid.Column/πλάτη στηλών ανάλογα με 3 ανεξάρτητα flags (`_sidebarVisible`/`_sidebarExpanded`/
`_sidebarOnRight`). `SelectTab(tag)` είναι η κοινή δρομολόγηση - κλασικό μενού, πλευρικό μενού ΚΑΙ
Ctrl+1..8 όλα καταλήγουν να τσεκάρουν το αντίστοιχο RadioButton στη λωρίδα καρτελών, το οποίο μέσω
`TabButton_Checked` ενημερώνει ΚΑΙ το `ContentHost` ΚΑΙ το πλευρικό μενού (`Sidebar.SetActiveTag`) -
έτσι και οι 3 μηχανισμοί πλοήγησης παραμένουν πάντα σε συμφωνία χωρίς να ξέρουν ο ένας για τον άλλο.

`dotnet build` (0/0) επιβεβαιώθηκε. Η ζωντανή εκτέλεση επιβεβαιώθηκε μόνο ως "δεν κατέρρευσε μέσα
σε 4 δευτερόλεπτα" (το exe πλέον απαιτεί elevation - βλ. §0.4στ, οπότε ο συνήθης βρόχος launch+kill
δεν είναι πλέον αξιόπιστος από μη-elevated context). ΔΕΝ επαληθεύτηκε ΚΑΘΟΛΟΥ οπτικά (άνοιγμα/
κλείσιμο sidebar, σύμπτυξη, εναλλαγή αριστερά/δεξιά, ευθυγράμμιση με το υπόλοιπο layout) - ο χρήστης
πρέπει να το δοκιμάσει διαδραστικά.

**Οριζόντιο μοντέρνο μενού - δεν χτίστηκε ξεχωριστά**: η λωρίδα καρτελών (TabStrip) ΗΔΗ είναι μια
οριζόντια, μοντέρνα navigation bar (pills με εικονίδια, ίδιο ύφος με "modern horizontal menu") -
κρίση αρχιτεκτονικής (μέρος του ρητά ζητηθέντος IA review): δεν έχει νόημα να χτιστεί ΔΕΥΤΕΡΗ,
παράλληλη οριζόντια μπάρα με το ΙΔΙΟ ακριβώς σκοπό. Αν ο χρήστης εννοούσε κάτι οπτικά διαφορετικό
(π.χ. dropdown-style mega-menu αντί για pills), χρειάζεται διευκρίνιση πριν χτιστεί κάτι δεύτερο.

### ΔΙΟΡΘΩΣΗ §0.4ζ (ίδιο πέρασμα, ο χρήστης διόρθωσε το περιεχόμενο πριν προλάβω να κάνω commit)

Ο χρήστης διευκρίνισε: το πλευρικό μενού πρέπει να έχει ΤΟ ΙΔΙΟ περιεχόμενο με το κλασικό μενού
(Εργαλεία/Προβολή/Ρυθμίσεις/Βοήθεια - όχι μόνο τις 8 καρτέλες), ΚΑΙ το μελλοντικό PC Manager skin
θα δείχνει ΜΟΝΟ τις καρτέλες (πιο minimal sidebar εκεί) - άρα ΥΠΑΡΧΕΙ τελικά διαφοροποίηση περιεχομένου
ανά skin, απλά ελεγχόμενη από ΕΝΑ κοινό μοντέλο, όχι από 2 ξεχωριστές λίστες. Επίσης αφαιρέθηκε ρητά
το κουμπί σύμπτυξης ("όχι").

**Αναδιάρθρωση**: νέο `ClassicMenuModel.cs` (root namespace) - `MenuAction`/`MenuLeaf`/`MenuGroup`
records + στατικό `ClassicMenuModel.Groups` (οι 4 ομάδες, η ομάδα "Προβολή" χτίζεται από το
`NavItems.All`). Το κλασικό `Menu` (MainWindow.xaml) γεμίζει πλέον ΕΞ ΟΛΟΚΛΗΡΟΥ από κώδικα
(`PopulateClassicMenu()`) από αυτό το μοντέλο - όχι πια καθόλου χειρόγραφα `<MenuItem>` στο XAML,
ούτε καν για Εργαλεία/Ρυθμίσεις/Βοήθεια (πριν μόνο η "Προβολή" ήταν code-driven). Το `SidebarNav`
ξαναγράφτηκε πλήρως: χτίζει το ΙΔΙΟ περιεχόμενο (group headers + leaves) στο code-behind
(`BuildItems()`) - κάθε leaf γίνεται `RadioButton` (αν αντιστοιχεί σε καρτέλα, μέρος του ενεργού
highlight) ή απλό `Button` (αν είναι "δεν έχει μεταφερθεί ακόμα" ενέργεια). ΤΟ ΚΟΥΜΠΙ ΣΥΜΠΤΥΞΗΣ
ΑΦΑΙΡΕΘΗΚΕ πλήρως (`BtnCollapse` δεν υπάρχει πια) - παρέμεινε μόνο το κουμπί εναλλαγής αριστερά/
δεξιά (⇄, δεν αναφέρθηκε αρνητικά από τον χρήστη). Νέο `bool ShowOnlyNavigationGroup` property
(default `false`, όχι ακόμα συνδεδεμένο σε UI) - όταν `true`, δείχνει ΜΟΝΟ την ομάδα "Προβολή"
(αναγνωρίζεται δομικά: όλα τα leaves της ομάδας έχουν `NavigateTag`, όχι με σύγκριση τίτλου) -
έτοιμο για το μελλοντικό PC Manager skin να το ενεργοποιήσει χωρίς αλλαγές στο ίδιο το control.

**Αποφυγή reentrancy bug**: το `RadioButton.Click` (όχι `Checked`) χρησιμοποιείται για να
πυροδοτήσει το `LeafClicked` - το `Click` ΔΕΝ πυροδοτείται όταν το `IsChecked` τίθεται
προγραμματιστικά (όπως κάνει το `SetActiveTag` όταν το MainWindow ενημερώνει το sidebar από άλλη
πλοήγηση) - αν είχε χρησιμοποιηθεί `Checked`, θα δημιουργούνταν κύκλος
MainWindow→SetActiveTag→Checked→LeafClicked→MainWindow→SetActiveTag→...

`dotnet build` (0/0) + εκκίνηση χωρίς άμεσο crash επιβεβαιώθηκαν. ΔΕΝ επαληθεύτηκε οπτικά (ίδιος
περιορισμός με §0.4στ - elevation μπλοκάρει το UAC prompt από μη-διαδραστικό context).

### ΔΙΟΡΘΩΣΗ #2 §0.4ζ (χρήστης έστειλε screenshot, απέρριψε ΟΛΟΚΛΗΡΟ το πρώτο σχέδιο)

Ο χρήστης απέρριψε ρητά το group/nested σχέδιο ("δεν θελω τα submenus", "εχεις αφησει τις καρτελες
μεσα") και επεσήμανε ότι έπρεπε να χρησιμοποιήσω την 2.8.2 ως reference αντί να επινοήσω δικό μου
σχέδιο. Διερεύνηση στο ΠΡΑΓΜΑΤΙΚΟ Optimizer.ps1 (`Add-SidebarItem`/`Show-Sidebar`/`Hide-Sidebar`,
~7937-8129 - ΔΙΑΦΟΡΕΤΙΚΟ σύστημα από το `$global:pcManagerNavRail`, ~7606-7734, που είναι
αποκλειστικά για το μελλοντικό PC Manager skin) αποκάλυψε: το πραγματικό πλευρικό μενού είναι μια
ΕΠΙΠΕΔΗ λίστα 6 τετραγωνισμένων pills (86x90px, ΧΩΡΙΣ ομάδες/κεφαλίδες, ΧΩΡΙΣ τις 8 καρτέλες μέσα -
αυτές έχουν ήδη τη δική τους λωρίδα) - Οδηγίες & Βοήθεια, Ιστορικό Εκδόσεων, **Ιστορικό Ενεργειών**
(νέα ανακάλυψη - `Show-ActionLogWindow`, δεν είχε αναφερθεί/καταγραφεί ποτέ πριν σε αυτό το
HANDOFF), Κρυφές Λειτουργίες (ViVeTool), Διαχείριση UWP, Ρυθμίσεις Εμφάνισης. Κάθε pill: μεγάλο
εικονίδιο (32px) πάνω-κέντρο + πλήρης ετικέτα (έως 2 γραμμές, ρητό σπάσιμο στο " & " όπου υπάρχει)
από κάτω, στρογγυλεμένη γωνία στο hover.

**Πλήρης επαναγραφή**: `SidebarShortcuts.cs` (νέο, root namespace) - 6 `SidebarShortcut` records με
ΑΚΡΙΒΩΣ τις ετικέτες των "SidebarXxx" translation keys του ps1 (ΣΚΟΠΙΜΑ διαφορετικές από τις
αντίστοιχες του κλασικού μενού - το ίδιο το ps1 έχει ξεχωριστά `$sbKeys`/`$hmKeys` σύνολα). Το
`SidebarNav` ξαναγράφτηκε: απλό `ItemsControl` από `Button`, ΧΩΡΙΣ groups/headers, ΧΩΡΙΣ κουμπί
σύμπτυξης (ήδη αφαιρέθηκε στο προηγούμενο πέρασμα) ΚΑΙ ΧΩΡΙΣ το κουμπί εναλλαγής αριστερά/δεξιά που
είχα προσθέσει (δεν υπάρχει αντίστοιχο στο ps1 - η θέση εκεί ορίζεται από ρύθμιση, όχι live toggle
μέσα στο ίδιο το sidebar· η δυνατότητα ΔΕΞΙΑΣ τοποθέτησης αφαιρέθηκε προς το παρόν, θα προστεθεί αν
χρειαστεί όταν υπάρξει το αντίστοιχο Ρυθμίσεις Εμφάνισης παράθυρο). Αφαιρέθηκε ΚΑΙ το πρώην
`ClassicMenuModel`-based περιεχόμενο/`SetActiveTag`/`ShowOnlyNavigationGroup` από το SidebarNav - ΔΕΝ
χρειάζονται πια, αφού το sidebar δεν αφορά καθόλου πλοήγηση καρτελών. Το `ClassicMenuModel.cs`
ΠΑΡΑΜΕΝΕΙ αμετάβλητο και σωστό - αφορά ΜΟΝΟ το κλασικό `Menu` (που ΣΩΣΤΑ έχει και τα 4 (Εργαλεία/
Προβολή/Ρυθμίσεις/Βοήθεια), δεν άλλαξε.

**Νέα γνωστή εκκρεμότητα**: "Ιστορικό Ενεργειών" (`Show-ActionLogWindow`) προστέθηκε στη λίστα των
"δεν έχει μεταφερθεί ακόμα" δευτερευόντων παραθύρων - ΔΕΝ είχε προηγουμένως καταγραφεί πουθενά σε
αυτό το HANDOFF ως ξεχωριστό, υπαρκτό feature.

`dotnet build` (0/0) + εκκίνηση χωρίς άμεσο crash επιβεβαιώθηκαν. Ίδιος περιορισμός με §0.4στ - ΔΕΝ
επαληθεύτηκε οπτικά ακόμα (elevation μπλοκάρει διαδραστική δοκιμή από μη-elevated context).

## 0.4η WPF: Ανάλυση Δίσκου (δευτερεύον παράθυρο + μπάρα), θερμοκρασίες, 4ο πλακίδιο GPU, drive model, αραιότερη ανανέωση δίσκου, deep IA review

Ολοκλήρωση της τελευταίας παρτίδας deferred items (§0.4 λίστα).

**Ανάλυση Χώρου Δίσκου - πλήρης ξαναγραφή ώστε να ταιριάζει με το ps1 original** (ρητό αίτημα
χρήστη): το προηγούμενο WPF pass είχε ΞΕΧΩΡΙΣΤΗ μπάρα ανά κατηγορία - το πραγματικό ps1
(`Complete-DiskCategoryAnalysis`/`Show-DiskCategoryDrilldown`, ~18280-18481) έχει ΜΙΑ οριζόντια
μπάρα χωρισμένη σε έγχρωμα segments (αναλογικό πλάτος ανά κατηγορία) + legend από κάτω με αναδίπλωση
(WrapPanel) + **δευτερεύον παράθυρο** όταν πατηθεί segment/legend item. Πλήρης port:
- `DiskAnalysisService.cs`: το `DiskCategory` απέκτησε `Roots` (ίδιο με το ps1's `Categories[$k].Roots`
  - τα root φακέλους ανά κατηγορία, "Other" χωρίς roots). Νέα `DrilldownAsync(roots)` - σαρώνει ΚΑΘΕ
  ΑΜΕΣΟ υποφάκελο κάθε root (recursive μέγεθος ο καθένας), ταξινομημένο φθίνουσα, μέχρι 40
  αποτελέσματα - port του ps1's inline scan script (~18397-18411).
- `Views/DiskDrilldownWindow.xaml(.cs)` (ΝΕΟ, το δευτερεύον παράθυρο που ζήτησε ο χρήστης): τίτλος
  "Περαιτέρω Ανάλυση: {κατηγορία}", status text, scrollable λίστα Name/SizeGB, κουμπί Κλείσιμο - port
  του `Show-DiskCategoryDrilldown` (WinForms `Form` -> WPF `Window`).
- `HomeView.xaml`: `DiskBarGrid` (Grid με star-weighted columns, ένα per κατηγορία με SizeGb>0,
  weight=SizeGb - το WPF Star sizing κάνει αυτόματα την αναλογική διαίρεση, όχι χειροκίνητο pixel
  math όπως το ps1) μέσα σε rounded Border. `ListDiskLegend` (ItemsControl+WrapPanel) - κάθε item
  clickable (`DiskLegendItem_Click`) ανοίγει `DiskDrilldownWindow` αν η κατηγορία έχει roots.

**Θερμοκρασίες** (ρητό αίτημα χρήστη, με ρητή ΤΙΜΙΑ σημείωση ήδη στο σχόλιο κώδικα): νέο
`Services/HardwareSensorService.cs` -
`GetDiskTemperatureCelsius` (μέσω `MSFT_PhysicalDisk.GetStorageReliabilityCounter()`,
`root\Microsoft\Windows\Storage` - η ΙΔΙΑ επίσημη μέθοδος πίσω από το PowerShell cmdlet
`Get-StorageReliabilityCounter`) και `GetCpuTemperatureCelsius` (μέσω `MSAcpi_ThermalZoneTemperature`,
`root\WMI` - γνωστό ασυνεπές σε desktop hardware, πιο αξιόπιστο σε laptops). ΚΑΙ οι δύο επιστρέφουν
`null` όταν δεν είναι διαθέσιμες σε αυτό το hardware/BIOS - το UI δείχνει τίμια "—", ΠΟΤΕ ψευδή τιμή.
Το CPU tile και το Disk tile δείχνουν πλέον 🌡+τιμή. Το RAM tile ΔΕΝ πήρε θερμοκρασία - καμία τυπική/
καθολικά προσβάσιμη πηγή θερμοκρασίας RAM υπάρχει σε commodity hardware (μόνο συγκεκριμένα SPD hub
chips σε ορισμένα RGB RAM kits, εκτός εμβέλειας εδώ).

**4ο πλακίδιο GPU** (ρητό αίτημα χρήστη, μικραίνοντας τα άλλα 3 για να χωρέσει - η σειρά έγινε 4
στήλες αντί για 3): νέο `Services/GpuInfoService.cs` - όνομα μέσω `Win32_VideoController`, **ποσοστό
χρήσης** μέσω της ενσωματωμένης κατηγορίας μετρητών Windows "GPU Engine" (Windows 10+, καμία vendor
εξάρτηση - η ΙΔΙΑ πηγή δεδομένων με το tab Απόδοση > GPU του Task Manager, φιλτράρει instances με
"engtype_3D", ίδιο priming-pattern με τον υπάρχοντα μετρητή CPU). **Θερμοκρασία GPU ΔΕΝ είναι
διαθέσιμη** χωρίς NVIDIA NVML/AMD ADL SDK (vendor-specific, εκτός εμβέλειας) - δείχνει πάντα "—",
τίμια σημειωμένο στο σχόλιο κώδικα.

**Μάρκα/περιγραφή δίσκου στο tile** (ξεχασμένο κομμάτι από το ΑΡΧΙΚΟ αίτημα χρήστη -
"δεν αναφέρεις... την περιγραφή του δίσκου"): `DriveTypeService.GetDiskModel(driveLetter)` - το
κλασικό `Win32_DiskDrive.Model` (π.χ. "Samsung SSD 970 EVO Plus 1TB") μέσω της παλιάς, ευρέως
υποστηριζόμενης associator αλυσίδας `Win32_LogicalDisk`→`Win32_DiskPartition`→`Win32_DiskDrive` (πιο
καθολικά διαθέσιμη από τη νεότερη `root\Microsoft\Windows\Storage` που χρησιμοποιεί ήδη το
`DriveTypeService.Detect`). Νέο `TxtDriveModel` στο δίσκου tile.

**Αραιότερη ανανέωση δίσκου** (ρητό αίτημα χρήστη - "η ανανέωση των δίσκων να γίνεται όταν αλλάζει το
ποσοστό, δεν χρειάζεται live gauge, ελάφρυνση εφαρμογής"): ο χώρος δίσκου ΔΕΝ αλλάζει αισθητά μέσα σε
1 δευτερόλεπτο σαν το CPU - διαχωρίστηκε σε 2 timers: `_refreshTimer` (1s, CPU/RAM/GPU - αυτά ΕΙΝΑΙ
πραγματικά live) και νέο `_diskRefreshTimer` (20s, μόνο δίσκος). Επιπλέον διορθώθηκε ΚΑΙ διαρροή
πόρων που υπήρχε ήδη πριν (`HomeView.Unloaded` τώρα σταματάει και τους δύο timers - κάθε επιστροφή
στην Αρχική δημιουργεί ΝΕΟ `HomeView`, χωρίς αυτό οι timers των παλιών instances θα συνέχιζαν να
τρέχουν επ' αόριστον στο παρασκήνιο).

### Deep IA review (ρητό αίτημα χρήστη): πλήρης απογραφή καρτών/κατηγοριών ανά καρτέλα

Πλήρης σάρωση όλων των `Helper-CreateCard` κλήσεων + τίτλων (`Add-IconedLabel`) σε ΟΛΟ το
Optimizer.ps1, οργανωμένη ανά καρτέλα (parent scroll panel):

- **Αρχική**: CPU/RAM/Disk, Βαθμολογία Υγείας, Ανάλυση Χώρου Δίσκου. *(WPF: πλήρες)*
- **Βελτιστοποίηση**: Office Mode, Gaming Mode, Διαχειριστής Ενημερώσεων (winget) *(WPF: πλήρες)* +
  Ενημερώσεις Οδηγών Συσκευών (Drivers, 520px κάρτα), Αντίγραφο Ασφαλείας Οδηγών, Καθαρισμός
  Αποθήκης Οδηγών & Αναφορά *(WPF: ΔΕΝ έχουν μεταφερθεί ακόμα - επόμενη προτεραιότητα σε αυτή την
  καρτέλα)*.
- **Υγεία & Συντήρηση**: Καθαρισμός Μητρώου, Καθαρισμός Cache Περιηγητών, Περιβάλλον Ανάκτησης
  (WinRE). *(WPF: placeholder μόνο)*
- **Δίκτυο & Ασφάλεια**: Διαχείριση Κανόνων Firewall, Εργαλεία Πολιτικής Firewall, Επεξεργαστής
  Αρχείου Hosts, Windows Defender Γρήγορη Σάρωση. *(WPF: placeholder μόνο)*
- **Εφαρμογές & Bloat**: Ενσωματωμένα Στοιχεία Windows, Προτεινόμενες Εφαρμογές (winget install - 7
  υποκατηγορίες: Browsers/Συμπίεση/Πολυμέσα/Επικοινωνία/Εργαλεία/Έγγραφα/Βιβλιοθήκες), Διαχείριση
  Εγκατεστημένων Εφαρμογών (UWP), Βαθιά Απεγκατάσταση. *(WPF: placeholder μόνο)*
- **Προηγμένα Εργαλεία**: Συντήρηση & Διάγνωση (600px, μεγαλύτερη κάρτα όλης της καρτέλας),
  Εργαλειοθήκη Γρήγορης Πρόσβασης, Πρόσθετες Λειτουργίες Windows, Όλες οι Πρόσθετες Λειτουργίες
  Windows (ΞΕΧΩΡΙΣΤΗ από την προηγούμενη - curated λίστα vs πλήρης/αναζητήσιμη λίστα, ΔΕΝ είναι
  διπλότυπο/bug, σκόπιμη UX απόφαση), Διαχείριση Συσκευών-Φαντασμάτων. *(WPF: placeholder μόνο)*
- **Σύστημα**: Εφαρμογές Εκκίνησης, Διεργασίες Συστήματος, Αποθηκευτικός Χώρος (διπλότυπα/μεγάλα
  αρχεία), Σημεία Επαναφοράς, Βελτιστοποίηση Υπηρεσιών, Δοκιμή Ταχύτητας Δίσκου. *(WPF: placeholder
  μόνο)*
- **Επιπλέον Ρυθμίσεις**: Κύρια λίστα tweaks (845px - η ΠΥΚΝΟΤΕΡΗ κάρτα όλης της εφαρμογής, δεκάδες
  μεμονωμένα toggles), AI & Copilot, Απόδοση (HAGS/μπαταρία/USB/PCIe - μερικώς ήδη γνωστό από
  Gaming Mode), Προσαρμοσμένες Επιλογές Δεξιού Κλικ. *(WPF: placeholder μόνο)*
- **ViVeTool** (ξεχωριστό dialog, ΟΧΙ καρτέλα): Κρυφές/Πειραματικές Λειτουργίες, Προσαρμοσμένο
  Feature ID.

**Συμπέρασμα IA review**: η υπάρχουσα δομή 8 καρτελών καλύπτει ΚΑΘΑΡΑ όλο το περιεχόμενο - ΔΕΝ
βρέθηκε ΚΑΜΙΑ κάρτα/λειτουργία που να ταιριάζει καλύτερα αλλού ή να χρειάζεται ολότελα νέα
καρτέλα/κατηγορία μενού/κουμπί κεντρικής οθόνης. Η κατηγοριοποίηση του ps1 original είναι ήδη
λογική και συνεπής (π.χ. τα driver-related κομμάτια σωστά ζουν στη Βελτιστοποίηση, όχι στο Σύστημα ή
τα Προηγμένα - ταιριάζει με το `Get-AppIcon` IconType mapping, που είναι ήδη 1:1 με τις 8 καρτέλες).
Το ΜΟΝΟ πραγματικό εύρημα είναι όγκος δουλειάς που απομένει (6 ολόκληρες καρτέλες + το Drivers κομμάτι
της Βελτιστοποίησης δεν έχουν μεταφερθεί ακόμα), ΟΧΙ κακή αρχιτεκτονική προς διόρθωση.

`dotnet build` (0/0) + εκκίνηση χωρίς άμεσο crash επιβεβαιώθηκαν. ΔΕΝ επαληθεύτηκε οπτικά (elevation
μπλοκάρει διαδραστική δοκιμή από μη-elevated context, ίδιο με §0.4στ/ζ) - ειδικά η Ανάλυση Δίσκου
(μπάρα/legend/δευτερεύον παράθυρο) και το GPU tile χρειάζονται πραγματικό οπτικό έλεγχο από τον
χρήστη.

## 0.4θ WPF: LibreHardwareMonitorLib για πραγματικές θερμοκρασίες (μετά από διαδικτυακή έρευνα), σειρά πλακιδίων, οπτική διόρθωση θερμομέτρου

Ο χρήστης ανέφερε ότι δίσκος/GPU έδειχναν πάντα "—" και ζήτησε ρητά έρευνα μέσω διαδικτύου για
εναλλακτικό τρόπο. `WebSearch` επιβεβαίωσε: (1) η προηγούμενη προσέγγιση WMI
(`MSFT_PhysicalDisk.GetStorageReliabilityCounter`) είναι γνωστό ότι δεν λειτουργεί αξιόπιστα σε
πολλούς NVMe δίσκους (ο δίσκος σε αυτό το μηχάνημα, "WD Blue SN580", είναι ακριβώς τέτοιος), (2) ΔΕΝ
υπάρχει ΚΑΜΙΑ WMI κλάση για GPU θερμοκρασία χωρίς vendor SDK, επιβεβαιωμένο από πολλαπλές πηγές. Η
καθιερωμένη λύση που όλη η βιομηχανία χρησιμοποιεί γι' αυτό ακριβώς είναι το **LibreHardwareMonitorLib**
(MIT license, ανοιχτού κώδικα, ενεργά συντηρούμενο - διαδέχεται το παλιό OpenHardwareMonitor) - διαβάζει
αισθητήρες απευθείας (ίδια τεχνική με HWiNFO/Task Manager), χρειάζεται Administrator (το οποίο η
εφαρμογή ΗΔΗ απαιτεί από το `app.manifest`, §0.4στ - ιδανικό timing).

**Νέο `Services/SensorService.cs`**: κρατάει ΕΝΑ process-wide `Computer` singleton (η δημιουργία/
`Open()` είναι "βαριά" - φορτώνει kernel driver πρόσβαση, ΔΕΝ πρέπει να ξαναγίνεται per-query).
`GetCpuTemperatureCelsius()` (προτιμά sensor με "Package" στο όνομα), `GetGpuTemperatureCelsius()`
(δοκιμάζει `HardwareType.GpuNvidia`→`GpuAmd`→`GpuIntel`), `GetDiskTemperatureCelsius(modelHint)`
(ταιριάζει storage hardware με βάση το ήδη γνωστό μοντέλο δίσκου - τα Windows δεν εκθέτουν άμεσα
"γράμμα δίσκου -> sensor" αντιστοίχιση σε κανένα επίπεδο, οπότε γίνεται ταίριασμα ονόματος, με
fallback στον πρώτο δίσκο που βρέθηκε). Όλα επιστρέφουν `null` (όχι ψευδή τιμή) αν ο driver αποτύχει
να φορτώσει ή δεν βρεθεί κατάλληλος αισθητήρας. Το παλιό `Services/HardwareSensorService.cs`
(καθαρό WMI, αποδεδειγμένα αναξιόπιστο) **αφαιρέθηκε** - αντικαταστάθηκε πλήρως.

**Σειρά πλακιδίων** (ρητό αίτημα χρήστη): CPU/GPU/RAM/Δίσκος αντί για CPU/RAM/Δίσκος/GPU - απλή
εναλλαγή τιμών `Grid.Column` στο `HomeView.xaml` (0/2/4/6), καμία αλλαγή στο ίδιο το περιεχόμενο.

**Οπτική διόρθωση θερμομέτρου** (ρητό αίτημα χρήστη: "μικρά... όχι ευδιάκριτα στο dark mode"): νέο
`TempValueStyle` (`Themes/Styles.xaml`) - πλήρες `TextBrush` (όχι το σκόπιμα χαμηλής αντίθεσης
`SubTextBrush`) + μεγαλύτερο/έντονο μέγεθος (13pt SemiBold) για την τιμή θερμοκρασίας, εικονίδιο 🌡
μεγάλωσε (11pt -> 16pt) - και τα δύο σε όλα τα 3 tiles (CPU/Disk/GPU) μέσω `replace_all`.

**GPU όνομα - εμφανίζει ΟΛΕΣ τις κάρτες γραφικών, όχι μόνο την πρώτη** (ρητό αίτημα χρήστη: "να
δείχνει και τον ενσωματωμένο σε CPU αν υπάρχει"): `GpuInfoService.GetName()` ένωνε πριν μόνο το
`FirstOrDefault()` αποτέλεσμα του `Win32_VideoController` - σε συστήματα με ΚΑΙ ενσωματωμένη (iGPU)
ΚΑΙ διακριτή (dGPU) κάρτα, η σειρά επιστροφής της WMI δεν είναι αξιόπιστη ένδειξη "κύριας" κάρτας.
Τώρα ενώνει ΟΛΑ τα ονόματα (π.χ. "AMD Radeon(TM) Graphics + NVIDIA GeForce RTX ..."). ΣΗΜΕΙΩΣΗ
ΕΙΛΙΚΡΙΝΕΙΑΣ: το ποσοστό χρήσης/θερμοκρασία στο tile παραμένουν ΕΝΙΑΙΑ τιμή (άθροισμα/πρώτο εύρημα
αντίστοιχα) - πλήρης υποστήριξη πολλαπλών ΞΕΧΩΡΙΣΤΩΝ GPU tiles θα χρειαζόταν μεγαλύτερη αναδιάταξη
του layout (δυναμική λίστα αντί για σταθερές 4 στήλες), εκτός εμβέλειας αυτού του περάσματος.

Νέο NuGet dependency: `LibreHardwareMonitorLib` 0.9.4 (`OptimizerWpf.csproj`).

`dotnet build` (0/0) + εκκίνηση χωρίς άμεσο crash επιβεβαιώθηκαν (σημαντικό εδώ - επιβεβαιώνει ότι ο
kernel driver του LibreHardwareMonitorLib φορτώνει χωρίς exception σε αυτό το μηχάνημα, τουλάχιστον
όσο αφορά την εκκίνηση - το `SensorService.GetComputer()` τον φορτώνει lazy/async μέσω
`RefreshCpuTemperatureAsync`, όχι στο startup path). ΔΕΝ επαληθεύτηκε οπτικά αν οι πραγματικές τιμές
θερμοκρασίας (όχι μόνο "χτίζεται/τρέχει") εμφανίζονται σωστά - χρειάζεται τη δοκιμή του χρήστη.

## 0.4ι WPF: διορθώσεις θερμοκρασιών μετά από αναφορά χρήστη (LHM 0.9.6, sensor name matching, RAM γραμμή, disk icon fallback)

Ο χρήστης ανέφερε: δίσκος ΔΕΝ δείχνει τίποτα, CPU/GPU δείχνουν "την ίδια" τιμή (ύποπτο), RAM δεν
δείχνει τίποτα καθόλου (ούτε "—"), και ζήτησε διαφορετικά εικονίδια ανά είδος δίσκου στην εναλλαγή.

- **LibreHardwareMonitorLib 0.9.4 → 0.9.6**: το ίδιο το project καταγράφει πρόσφατες διορθώσεις
  ΑΚΡΙΒΩΣ σε NVMe θερμοκρασία sensors (ο δίσκος σε αυτό το μηχάνημα, "WD Blue SN580", είναι NVMe).
- **`SensorService.GetTemperature`**: το "Package" ως προτιμώμενο όνομα αισθητήρα CPU είναι Intel-
  στυλ ονομασία - σε AMD συστήματα (ο χρήστης έχει "AMD Radeon(TM) Graphics", άρα πιθανότατα AMD
  CPU/APU) ο αντίστοιχος αισθητήρας λέγεται "Tctl"/"Tdie" - ΔΕΝ ταίριαζε ΠΟΤΕ, οπότε έπεφτε σε
  "οποιοσδήποτε αισθητήρας θερμοκρασίας βρεθεί πρώτος" (πιθανή αιτία του "ίδια τιμή σε CPU/GPU" -
  μπορεί να έπιανε τυχαία λάθος/ασήμαντο αισθητήρα). Τώρα δοκιμάζει λίστα γνωστών ονομάτων και για
  τις δύο οικογένειες (`Package`/`Tctl`/`Tdie`/`CPU Die`/`Core Average`/`Core Max` για CPU, `GPU
  Core`/`Core`/`Hot Spot`/`Junction` για GPU) με τη σειρά, πριν καταλήξει σε "πρώτο διαθέσιμο".
- **RAM tile**: δεν είχε ΚΑΘΟΛΟΥ γραμμή θερμοκρασίας πριν (ασυνεπές με τα άλλα 3 tiles - γι' αυτό
  φαινόταν "τίποτα" αντί για τίμιο "—"). Προστέθηκε `TxtRamTemp` + `SensorService.
  GetRamTemperatureCelsius()` (`HardwareType.Memory`, `IsMemoryEnabled=true` στο `Computer`) - σπάνια
  θα βρει τιμή σε commodity hardware, αλλά τουλάχιστον δείχνει τίμια "—" σαν τα υπόλοιπα τώρα.
- **Disk icon fallback bug** (πιθανή αιτία του "δεν άλλαζε το εικονίδιο στην εναλλαγή"): το
  `PhysicalDriveKind.Unknown` (όταν αποτυγχάνει η ανίχνευση τύπου) χρησιμοποιούσε το ΙΔΙΟ glyph+χρώμα
  με το HDD - αν ένας δίσκος ανιχνευόταν Unknown, η εναλλαγή σε/από αυτόν δεν έδειχνε ΚΑΜΙΑ οπτική
  διαφορά. Τώρα ξεχωριστό, ουδέτερο γκρι "❓".

`dotnet build` (0/0) + εκκίνηση χωρίς άμεσο crash επιβεβαιώθηκαν. ΔΕΝ επαληθεύτηκε αν οι
πραγματικές τιμές είναι πλέον σωστές/διαφοροποιημένες - χρειάζεται νέα δοκιμή του χρήστη.

## 0.4ια Σειρά υλοποίησης για τις 6 εναπομείνασες καρτέλες + Drivers (ρητό αίτημα χρήστη)

Βάσει του πλήρους inventory του §0.4η, προτεινόμενη σειρά μεταφοράς (κριτήρια: πρώτα να
ολοκληρωθεί ό,τι έχει ήδη ΞΕΚΙΝΗΣΕΙ, μετά μικρότερο/απλούστερο πριν το μεγαλύτερο/πολυπλοκότερο,
τα πιο ρίσκο/destructive operations τελευταία αφού θα υπάρχει ήδη πιο ώριμο πρότυπο):

1. **Βελτιστοποίηση - ολοκλήρωση Drivers** (Ενημερώσεις Οδηγών, Αντίγραφο Ασφαλείας Οδηγών,
   Καθαρισμός Αποθήκης Οδηγών) - ΗΔΗ ξεκινημένη καρτέλα, τελειώνει πρώτα πριν νέα καρτέλα.
2. **Υγεία & Συντήρηση** - μόνο 3 κάρτες, μικρότερη νέα καρτέλα, γρήγορο κέρδος.
3. **Σύστημα** - 6 κάρτες αλλά κυρίως system-info-driven UI (Εκκίνηση/Διεργασίες/Αποθηκευτικός
   Χώρος/Σημεία Επαναφοράς/Υπηρεσίες/Benchmark) - υψηλή πρακτική αξία, μέτριο μέγεθος.
4. **Δίκτυο & Ασφάλεια** - 4 κάρτες, security-relevant (χειρισμός κανόνων firewall χρειάζεται
   προσοχή).
5. **Εφαρμογές & Bloat** - επαναχρησιμοποιεί το ήδη υπάρχον `WingetService` (Προτεινόμενες Εφαρμογές
   εγκαθίστανται μέσω winget) - η "Προτεινόμενες Εφαρμογές" κάρτα είναι η μεγαλύτερη σε αριθμό
   στοιχείων (7 υποκατηγορίες).
6. **Επιπλέον Ρυθμίσεις** - η ΠΥΚΝΟΤΕΡΗ κάρτα όλης της εφαρμογής (845px, δεκάδες μεμονωμένα toggles)
   - μηχανικά επαναληπτική δουλειά, μεγάλος όγκος.
7. **Προηγμένα Εργαλεία** - τελευταία σκόπιμα: περιλαμβάνει τα πιο "ρίσκο" εργαλεία (Windows
   Optional Features management, Διαχείριση Συσκευών-Φαντασμάτων) - καλύτερα να χτιστούν αφού θα
   υπάρχει ήδη ώριμο, δοκιμασμένο πρότυπο toggle/registry/WMI patterns από τα προηγούμενα.

Ο χρήστης ενέκρινε τη σειρά ("το αφήνεις έτσι για τις καρτέλες") και ζήτησε ρητά βαθύτερη έρευνα
περιεχομένου πριν ξεκινήσει η υλοποίηση ("τι θα βάλεις μέσα όμως ψάξε καλά") - βλ. §0.4ιβ παρακάτω.

## 0.4ιβ Λεπτομερές περιεχόμενο ανά προτεραιότητα (ρητό αίτημα χρήστη: "ψάξε καλά")

Πλήρης ανάγνωση κώδικα (όχι μόνο τίτλοι καρτών) για τα 2 πρώτα στη σειρά προτεραιότητας. Τα
υπόλοιπα 5 (Δίκτυο/Bloatware/Advanced/Σύστημα/Tweaks) ανατέθηκαν σε background research agents -
τα αποτελέσματά τους θα προστεθούν εδώ όταν ολοκληρωθούν.

### 1. Βελτιστοποίηση - ολοκλήρωση "Ενημερώσεις Οδηγών Συσκευών" (cardOpt4, ~11847-13035)

**ΠΟΛΥ μεγαλύτερο σε πραγματικότητα από μία απλή "κάρτα"** - 4 ανεξάρτητες, headless πηγές δεδομένων
(καμία δεν ανοίγει δικό της παράθυρο - ρητή, επαναλαμβανόμενη απαίτηση χρήστη στο ιστορικό, SDIO
ρητά απορρίφθηκε):
1. **Windows Update COM API** (`Microsoft.Update.Session`, φιλτραρισμένο σε `Type='Driver'`) - η
   κύρια πηγή, συνδυάζεται με `Win32_PnPSignedDriver` (εγκατεστημένοι, τοπικό/χωρίς δίκτυο). Ελέγχει
   ΚΑΙ την πολιτική `ExcludeWUDriversInQualityUpdate` (πολλαπλά πιθανά registry paths) πριν πει
   ψευδώς "όλα ενημερωμένα".
2. **AMD** (μόνο για AMD GPUs, `Win32_VideoController -match "AMD|Radeon"`) - server-rendered HTML
   scraping της επίσημης σελίδας `drivers.amd.com` ανά μοντέλο (regex στο HTML για version+ημερομηνία
   λήψης) - σύγκριση με ΗΜΕΡΟΜΗΝΙΕΣ, όχι version strings (ασύμβατη αρίθμηση Windows vs Adrenalin).
3. **NVIDIA** (μόνο για NVIDIA GPUs) - το ΔΙΚΟ ΤΗΣ δημόσιο API (lookupValueSearch.aspx + geforce.com
   AjaxDriverService.php) - ίδιος μηχανισμός με το ανοιχτού κώδικα TinyNvidiaUpdateChecker.
4. **Dell** (μόνο αν `Win32_ComputerSystem.Manufacturer -match "Dell"` ΚΑΙ το επίσημο Dell Command
   Update ήδη εγκατεστημένο) - τρέχει `dcu-cli.exe /scan` αθόρυβα, διαβάζει το XML report του.
   ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ (ήδη στο ίδιο το ps1): ΔΕΝ δοκιμάστηκε ζωντανά με πραγματικό Dell hardware.

Εγκατάσταση (`Install-DriverUpdate`): ξεχωριστή background PowerShell διεργασία ανά driver
(Windows Update Session/Downloader/Installer COM), ΟΧΙ inline. Κυκλικό κουμπί σάρωσης με progress
arc + περιστρεφόμενο γρανάζι (οπτικό στοιχείο - στο WPF θα χρειαστεί δικό του custom-drawn/Path
equivalent, ήδη υπάρχει προηγούμενο από το status bar spinner). Checkbox opt-in "αυτόματη λήψη &
εγκατάσταση" (persisted σε `$appSettings`).

**cardOpt5 - Αντίγραφο Ασφαλείας Οδηγών** (~13037-13134): 2 κουμπιά - "Δημιουργία Αντιγράφου"
(`dism.exe /Online /Export-Driver /Destination:...`, μόνο drivers τρίτων, ΠΟΤΕ Windows native) και
"Επαναφορά" (`pnputil.exe /add-driver *.inf /subdirs /install`, με explicit Yes/No επιβεβαίωση - ΟΧΙ
αναστρέψιμη ενέργεια). Κοινό timer/state και για τις δύο λειτουργίες (async, ΔΕΝ μπλοκάρει το UI -
ήδη διορθωμένο bug στο ps1 ιστορικό, το pnputil μπορεί να πάρει αρκετά λεπτά).

**cardDriverStore - Καθαρισμός Αποθήκης Οδηγών** (~13136+): `Get-WindowsDriver` (DISM cmdlet, ΟΧΙ
ανάλυση κειμένου pnputil - το τελευταίο είναι localized ανά γλώσσα Windows, ήδη προκάλεσε bug
αλλού). Φιλτράρει `Inbox=false` (μόνο 3rd-party, ΠΟΤΕ εργοστασιακούς), ομαδοποιεί κατά
`OriginalFileName`, προτείνει διαγραφή ΜΟΝΟ των παλιότερων εκδόσεων (κρατά πάντα την πιο πρόσφατη).
Το ίδιο το pnputil αρνείται να διαγράψει driver σε ενεργή χρήση - καμία πρόσθετη προστασία
χρειάζεται στο C# port.

### 2. Υγεία & Συντήρηση (πλήρης καρτέλα, ~13300-13792)

**cardRegClean - Καθαρισμός Μητρώου** (~13411-13664): ΣΚΟΠΙΜΑ στενός/ασφαλής, ΟΧΙ επιθετική
σάρωση - μόνο 2 κατηγορίες: (α) `MissingUninstaller` - εγγραφές `...\Uninstall\*` όπου το exe path
του `UninstallString` δεν υπάρχει πια (με προστασία system-folder paths: System32/SysWOW64/WinSxS/
κ.λπ. ΠΟΤΕ δεν προτείνονται), (β) `ObsoleteMuiCache` - παρωχημένα cached ονόματα σε
`HKCU\...\Shell\MuiCache` όπου το path δεν υπάρχει πια. Αυτόματο `.reg` backup ΠΡΙΝ από κάθε
διαγραφή (`Invoke-RegistryCleanDelete`). Background scan process, ίδιο async μοτίβο.

**cardBrowserCache - Καθαρισμός Cache Περιηγητών** (~13665-13699): Δυναμική λίστα (μόνο browsers
που πραγματικά ανιχνεύτηκαν στο σύστημα, `$global:browserCacheCatalog`) - ένα κουμπί ανά browser σε
grid 3 στηλών, κάθε κουμπί καθαρίζει ΜΟΝΟ cache folders (ΟΧΙ ιστορικό/κωδικούς/σελιδοδείκτες), όλα τα
προφίλ. Χρειάζεται πρώτα να βρω/ξαναχτίσω το `$browserCacheCatalog` (ποιοι browsers/paths
υποστηρίζονται) σε επόμενο πέρασμα υλοποίησης.

**cardWinRE - Περιβάλλον Ανάκτησης Windows** (~13701-13791): `reagentc.exe /info` (σχεδόν ακαριαίο,
συγχρονισμένη κλήση OK) για κατάσταση, χρωματισμένη ένδειξη (πράσινο Ενεργό/κόκκινο Ανενεργό).
"Ενεργοποίηση WinRE" ζητά explicit Yes/No επιβεβαίωση (αλλάζει boot configuration data) πριν
`reagentc.exe /enable`. Requires elevation (ήδη διαθέσιμο στο WPF).

### 3. Δίκτυο & Ασφάλεια (~13792-14193)

5 μεμονωμένα κουμπιά πάνω από τις κάρτες: Πλήρης Επαναφορά Δικτύου (winsock/ip reset+ipconfig
release/renew/flushdns), Πληροφορίες Δικτύου (ipconfig /all, read-only), Επανεκκίνηση Wi-Fi
(`Restart-NetAdapter -Name "*"` - ΧΩΡΙΣ επιβεβαίωση παρότι επηρεάζει ΟΛΟΥΣ τους adapters), Άνοιγμα
WF.msc (native Firewall), Πίνακας Δρομολόγησης (read-only).

**cardFirewall - Διαχείριση Κανόνων**: search box (τοπικό φιλτράρισμα σε ήδη φορτωμένα, όριο 50
αποτελέσματα)· "Φόρτωση Κανόνων" = async pattern (temp .ps1 `Get-NetFirewallRule | ConvertTo-Json`,
Timer 700ms poll πάνω σε Start-Process - ΙΔΙΟ μοτίβο με winget, να γίνει `Process.WaitForExitAsync`
στο C#)· κάθε κανόνας toggle switch → `Set-NetFirewallRule -Enabled` ΧΩΡΙΣ επιβεβαίωση.

**cardFirewallTools**: Εξαγωγή/Εισαγωγή πολιτικής (.wfw, `netsh advfirewall export/import` -
εισαγωγή έχει confirm dialog, ΑΝΤΙΚΑΘΙΣΤΑ ολόκληρη την πολιτική)· "Επαναφορά Προεπιλογών" (κόκκινο
κουμπί, `netsh advfirewall reset`, confirm dialog, **ΜΗ αναστρέψιμο** χωρίς προηγούμενη εξαγωγή -
πιο επικίνδυνη ενέργεια της καρτέλας).

**cardHostsEditor**: auto-load στο άνοιγμα καρτέλας· Φόρτωση/Αποθήκευση (backup .bak με timestamp
πριν την εγγραφή, `Encoding.ASCII` - ΠΡΟΣΟΧΗ στο port, non-ASCII hostnames θα χαλάσουν)/Επαναφορά
Προεπιλογών (μόνο textbox, ΔΕΝ γράφει δίσκο)/Άνοιγμα με Σημειωματάριο (bypass του in-app editor -
ΧΩΡΙΣ backup αν αποθηκευτεί από εκεί). Αποθήκευση έχει confirm dialog + backup, επικίνδυνη ενέργεια
(system file, χρειάζεται elevation - ήδη διαθέσιμο).

**cardDefenderScan**: "Έναρξη Γρήγορης Σάρωσης" = fire-and-forget (temp .ps1 `Start-MpScan
-ScanType QuickScan` + ΤΑΥΤΟΧΡΟΝΟ άνοιγμα `windowsdefender://scan/?scantype=quick` URI ώστε ο
χρήστης να βλέπει ζωντανή πρόοδο ΣΤΟ ίδιο το Windows Security app) - **σκόπιμα ΚΑΝΕΝΑ Timer
polling εδώ** (η σάρωση μπορεί να πάρει 15+ λεπτά, το app παραδίδει την προόδο στο native UI).

### 4. Εφαρμογές & Bloat (~14193-15000)

**cardBuiltin** (6 σειρές Install/Uninstall): Copilot/Xbox Gaming Overlay/News&Weather/HEVC
Extensions μέσω `Restore-AppxPackage` (νέο helper: δοκιμάζει provisioned-package re-register →
winget install → άνοιγμα ms-windows-store:// URI ως τελευταία λύση) + `Remove-AppxPackage`· Windows
Media Player Legacy μέσω `Enable/Disable-WindowsOptionalFeature` + auto default-app-association +
taskbar pin· Windows Photo Viewer μέσω απευθείας registry key (`HKLM:\...\Photo
Viewer\Capabilities\FileAssociations`).

**cardRecommended** (7 υποκατηγορίες, ΚΑΘΑΡΑ δηλωτική λίστα Τίτλος+WingetId+Tooltip - Browsers[4]/
Archivers[2]/Media[4+K-Lite ειδική περίπτωση]/Communication[4]/Tools[3]/Documents[2]/System
Libraries[3], ~22 απλές εγγραφές): κοινός μηχανισμός `Start-AppInstallOrUninstall` -
`Test-WingetAppInstalled` (συγχρονισμένο, `winget list`) → `Start-Process winget install/uninstall
--silent` (async) → ΚΟΙΝΟΣ `$appOpsTimer` (600ms) polling πολλαπλών ταυτόχρονων operations. Το
K-Lite Codec Pack έχει ΔΙΚΟ ΤΟΥ timer (μετά την εγκατάσταση: εντοπισμός MPC-HC, ρύθμιση
προεπιλεγμένων εφαρμογών ήχου/βίντεο, taskbar pin και για τα δύο players).

**cardUwpManage**: ένα κουμπί ανοίγει ξεχωριστό dialog (`Show-UwpAppManagerWindow`, 900x700 modal) -
σάρωση `Get-AppxPackage` φιλτραρισμένο σε ΜΟΝΟ πραγματικά Store apps (`SignatureKind -eq "Store"`,
όχι frameworks/resource-packages), friendly names μέσω ~29-εγγραφών hashtable
(`Get-FriendlyAppName`), per-app Uninstall ΜΕ confirm dialog (`Remove-AppxPackage`, ΣΥΓΧΡΟΝΑ στο UI
thread - ασυνέπεια με το Deep Uninstall παρακάτω που ΕΙΝΑΙ async, να διορθωθεί στο port).

**cardDeepUninstall**: τρέχει ΠΑΝΤΑ τον ΕΠΙΣΗΜΟ uninstaller της εφαρμογής (registry
`UninstallString`, ΚΑΝΕΝΑ silent flag injection) - confirm dialog → async (Timer 500ms, ίδιο
μοτίβο) → μετά την ολοκλήρωση σαρώνει για "residual folders" (`%LOCALAPPDATA%`/`%APPDATA%`/
`%ProgramData%`, top-level μόνο) → αν βρεθούν, δεύτερο confirm dialog → διαγραφή ΠΑΝΤΑ στον Κάδο
Ανακύκλωσης (`Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory`, ΠΟΤΕ μόνιμη διαγραφή).

### 5. Προηγμένα Εργαλεία (~15001-15621)

**cardAdvMaintenance** (10 κουμπιά): πλήρης συντήρηση (temp cleanup+SFC+DISM RestoreHealth)·
ενεργοποίηση προνομίων token (P/Invoke `AdjustTokenPrivileges` - χρειάζεται δικό του C# wrapper)·
δικτυακό reset· **καθαρισμός Event Logs** (`EventLogSession.ClearLog` - ΚΑΤΑΣΤΡΕΠΤΙΚΟ, ΧΩΡΙΣ
confirm dialog στο ps1 - πρέπει να προστεθεί στο port)· GC (placebo, μόνο η ίδια η διεργασία)· Deep
Diagnostics report σε αρχείο· άνοιγμα devmgmt.msc· WU troubleshooter (ms-settings: URI)· System
Report· reset υπηρεσιών WU (net stop/start wuauserv+bits)· .NET RollForward env var (ΧΩΡΙΣ
confirm)· καθαρισμός Xbox credentials (`cmdkey`, locale-safe regex)· εγκατάσταση Group Policy
Editor (dism .mum packages).

**cardAdvToolbox** (7 κουμπιά): καθαροί launchers (Snipping Tool/Notepad/Calculator/Control
Panel/Task Manager/regedit) + "Επανεκκίνηση GPU Driver" (προσομοίωση Win+Ctrl+Shift+B keystroke -
ΟΧΙ πραγματικό API, χρειάζεται SendInput equivalent στο C#).

**cardWinFeatures** (6 curated: .NET 3.5/Windows Sandbox/Hyper-V/WSL/Telnet/SMB1) vs
**cardAllWinFeatures** (ΠΛΗΡΗΣ, αναζητήσιμη λίστα ~150-250 features, ΕΞΑΙΡΕΙ ρητά τα 6 curated +
WindowsMediaPlayer [η δικιά της πλουσιότερη λογική ζει στο Bloatware tab] - ΔΕΝ είναι διπλότυπο,
σκόπιμος σχεδιασμός). Κοινό `Invoke-OptionalFeatureToggle`: confirm dialog → async toggle. Η πλήρης
λίστα έχει ΤΟ ΙΔΙΟ temp-script+Timer-polling μοτίβο με winget/firewall/storage.

**cardGhostDevices**: `Get-PnpDevice -PresentOnly:$false` φιλτραρισμένο σε `Present=$false` (ΠΟΤΕ
συνδεδεμένο hardware), async scan (Timer 800ms)· per-row "Αφαίρεση" = `Remove-PnpDevice
-Confirm:$false` **ΧΩΡΙΣ κανένα confirm dialog στο ps1 - η πιο επικίνδυνη, ανεπιβεβαίωτη ενέργεια
όλης της καρτέλας, ΠΡΕΠΕΙ να προστεθεί confirm στο port**.

### 6. Σύστημα (~15622-16518)

**cardStartup**: toggle ανά στοιχείο εκκίνησης, ΠΑΝΤΑ αναστρέψιμο (rename value σε
`OptimizerDisabled_X` αντί για διαγραφή - ΠΟΤΕ χάνεται δεδομένο).

**cardProcesses**: `Get-Process` top 25 κατά μνήμη, hardcoded λίστα κρίσιμων διεργασιών (System/
Idle/csrss/lsass/winlogon/explorer/dwm/κ.λπ.) που ΔΕΝ παίρνουν καν κουμπί Τερματισμού· confirm
dialog πριν από `Stop-Process -Force`.

**cardStorage**: Εύρεση Διπλότυπων (SHA256 hash grouping, μόνο προσωπικοί φάκελοι: Έγγραφα/Λήψεις/
Επιφάνεια/Εικόνες/Βίντεο, ΠΟΤΕ system) / Εύρεση Μεγάλων Αρχείων (ίδιοι φάκελοι, top 50)· "OneDrive
- Ελευθέρωση Χώρου" (`attrib +U -P` κάνει αρχεία online-only, async)· διαγραφή αποτελεσμάτων ΠΑΝΤΑ
στον Κάδο Ανακύκλωσης με confirm.

**cardRestorePoints**: Δημιουργία (`Checkpoint-Computer`, όριο Windows 1/24ωρο)· Άνοιγμα native
Ρυθμίσεων Προστασίας (SystemPropertiesProtection.exe) ως "διέξοδος" για διαγραφή μεμονωμένου
σημείου - **ΣΚΟΠΙΜΑ ΔΕΝ υλοποιείται diagnostic delete-by-sequence** (τεκμηριωμένος περιορισμός: το
WMI SystemRestore ΔΕΝ έχει Delete method, το vssadmin έχει ασύμβατο ID scheme, ρίσκο λάθος
διαγραφής - να ΜΗΝ ξαναπροσπαθηθεί στο port)· "Επαναφορά" = confirm dialog έντονης διατύπωσης
(άμεση επανεκκίνηση) → `Restore-Computer`.

**cardServicesOpt**: curated λίστα ~29 υπηρεσιών (DiagTrack/MapsBroker/WSearch/XblAuthManager/κ.λπ.,
αρκετές με προειδοποιήσεις στην περιγραφή τους) - toggle Αυτόματο↔Χειροκίνητο με backup/restore
μηχανισμό (κοινή "tweak backup" υποδομή, ΙΔΙΑ με άλλα σημεία της εφαρμογής) - μόνο υπηρεσίες που
ήταν ΗΔΗ Αυτόματες αγγίζονται ποτέ.

**cardDriveBenchmark**: 256MB test file write+read timing (Stopwatch), προ-έλεγχος ελεύθερου χώρου
(≥1GB), async (Timer 500ms) - **τεκμηριωμένος περιορισμός**: η ανάγνωση επηρεάζεται από Windows
file-cache (μόλις γραμμένο αρχείο) - ενδεικτικό test, ΟΧΙ CrystalDiskMark-level ακρίβεια, ήδη
τεκμηριωμένο στο ίδιο το ps1.

### 7. Επιπλέον Ρυθμίσεις (~16519-17573)

Η ΠΥΚΝΟΤΕΡΗ καρτέλα - 4 κάρτες + 3 standalone ενότητες. ΟΛΑ τα toggle rows μοιράζονται τον ΙΔΙΟ
`Add-TweakRow` factory (label+ToggleSwitch+OnScript/OffScript κλείσιμο) - το "restore" σημαίνει
"επαναφορά στην ΠΡΑΓΜΑΤΙΚΗ προηγούμενη τιμή του χρήστη" (backup σε κοινό JSON store,
`Backup-RegValueIfNeeded`/`Restore-BackedUpRegValue`), ΟΧΙ hardcoded default - σημαντικό
αρχιτεκτονικό detail να διατηρηθεί στο port (χρειάζεται μια αντίστοιχη C# "tweak backup" υπηρεσία).

**cardMainTweaks** (845px, 14 toggles, όλα registry-based εκτός #7/#8): Storage Sense αυτόματος
καθαρισμός, Fast Startup, Κλασικό Win10 δεξί-κλικ μενού (CLSID key create/delete + explorer
restart), Εμφάνιση επεκτάσεων/κρυφών αρχείων, Delivery Optimization P2P, Απενεργοποίηση επιτάχυνσης
ποντικιού, Αρχείο Αδρανοποίησης (`powercfg /hibernate on/off`), Ultimate Performance πλάνο
(`powercfg -duplicatescheme` + καθαρισμός στην επαναφορά), Αφαίρεση "Προτεινόμενα" από Start,
Ιστορικό Πρόχειρου (Win+V), Απενεργοποίηση Network Throttling, Βελτιστοποίηση System
Responsiveness, Απενεργοποίηση Game Bar/DVR (ΞΕΧΩΡΙΣΤΟ από το Gaming Mode του tab 1 - αγγίζει ΜΟΝΟ
Game DVR), Τηλεμετρία στο ελάχιστο επιτρεπτό επίπεδο (Home/Pro).

**cardAiCopilotBg** (7 toggles, v1.8.9): Απενεργοποίηση Copilot ("μαλακή" - το πακέτο μένει
εγκατεστημένο, πλήρης αφαίρεση ζει στο Bloatware tab), Windows Recall, Click To Do (Windows 11
25H2), αυτόματη εκκίνηση AI service, AI λειτουργίες σε Edge/Paint/Notepad - όλα registry policy
keys.

**cardPerfBg** (4 toggles - ΜΕΡΙΚΩΣ ήδη γνωστό, HAGS/μπαταρία/USB/PCIe - το ΙΔΙΟ HAGS key
(`HwSchMode`) χρησιμοποιείται ΚΑΙ από το πλήρες Gaming Mode που ήδη υλοποιήθηκε στο WPF, πιθανή
επικάλυψη προς εξέταση όταν χτιστεί αυτό το tab ώστε να μη συγκρούονται).

**3 standalone ενότητες (ΕΚΤΟΣ κάρτας)**:
- **Κρυπτογράφηση Συσκευής**: ΣΚΟΠΙΜΑ ΟΧΙ toggle - μόνο άνοιγμα των native Ρυθμίσεων
  (BitLocker applet fallback σε ms-settings: URI) - το ps1 ΠΟΤΕ αγγίζει BitLocker registry
  απευθείας (ρίσκο κλειδώματος δεδομένων χωρίς σωστό recovery key) - **να διατηρηθεί ΑΥΣΤΗΡΑ ίδια
  πολιτική στο port, ΠΟΤΕ registry-based toggle εδώ**.
- **2 hardcoded δεξί-κλικ επιλογές** (Ιδιοκτησία/Take Ownership - `icacls /grant *S-1-3-4:F /t`
  αναδρομικά, **ΧΩΡΙΣ confirm dialog στο ps1**· Άνοιγμα PowerShell εδώ) - μέσω `HKEY_CLASSES_ROOT`,
  ΞΕΧΩΡΙΣΤΑ από το JSON-tracked custom menu builder παρακάτω.
- **3 κουμπιά Οπτικών Εφέ** (Καλύτερη Εμφάνιση/Απόδοση/Ισορροπημένο) - **ΤΑ ΜΟΝΑ 3 σημεία σε ΟΛΗ
  την καρτέλα με πραγματικό Yes/No confirm dialog** (κλείνουν explorer.exe) - ΠΟΤΕ αγγίζουν το
  undocumented binary `UserPreferencesMask` (ρητή προφύλαξη στο ps1 - λάθος bit σπάει οπτικά την
  επιφάνεια εργασίας).

**cardCustomMenu** - πλήρες CRUD (όχι fixed λίστα): JSON store παρακολουθεί ΜΟΝΟ entries που
έφτιαξε η ίδια η εφαρμογή (`OptimizerCustom_<guid>` keys), ώστε να μπορεί να τα αφαιρέσει με
ασφάλεια χωρίς να αγγίξει προϋπάρχοντα shell verbs τρίτων. 3 scopes (Files/Directory/
FolderBackground). Modal dialog προσθήκης (MenuText+Command+Scope, auto-appends `%1`/`%V`
placeholder). **Ρίσκο προς σημείωση**: ουσιαστικά αυθαίρετη εκτέλεση εντολής registered στο μενού
των Windows, καμία επικύρωση πέρα από "μη κενό" - άξιο προσοχής στο port (ίσως προειδοποίηση προς
τον χρήστη).

**Cross-cutting εύρημα ειδικά για αυτή την καρτέλα**: ΚΑΝΕΝΑ από τα 4 tweak/AI/perf cards δεν έχει
async/Timer pattern (όλα συγχρονισμένα registry/powercfg calls, γρήγορα) - deferred `.SendToBack()`
z-order bugs (2 φορές επαναλαμβανόμενο σε cardAiCopilotBg/cardPerfBg) είναι WinForms-specific,
άσχετο στο WPF (declarative layout, όχι z-order dance).

## 0.4ιγ Συνολικό συμπέρασμα μετά την πλήρη έρευνα περιεχομένου

Πέρα από το ΗΔΗ καταγεγραμμένο "καμία αναδιάταξη καρτελών δεν χρειάζεται" (§0.4η), η πλήρης ανάγνωση
περιεχομένου αποκάλυψε ΕΝΑ γενικό, επαναλαμβανόμενο αρχιτεκτονικό μοτίβο σε ΣΧΕΔΟΝ όλες τις 6
εναπομείνασες καρτέλες: εξωτερική powershell.exe διεργασία + Timer polling + JSON handoff, για ΚΑΘΕ
βαριά λειτουργία (σαρώσεις firewall/features/ghost devices/storage/drivers/registry clean). Στο
WPF αυτό ΔΕΝ χρειάζεται να αντιγραφεί κατά γράμμα ανά λειτουργία - ένας ΚΟΙΝΟΣ, επαναχρησιμοποιήσιμος
async helper (ή απευθείας in-process `Task.Run` λογική, χωρίς καν να χρειάζεται να καλεί ξανά
powershell.exe) καλύπτει όλες τις περιπτώσεις - πραγματική ευκαιρία απλοποίησης κατά το port, όχι
μόνο μηχανική μεταφορά.

Επίσης εντοπίστηκαν 5 σημεία σε όλη την εφαρμογή όπου λείπει confirmation dialog παρά τον πραγματικό
κίνδυνο (βλ. λίστα §0.4ιβ σημείο 3-6 + Take Ownership στο §0.4ιβ σημείο 7) - συνιστάται να προστεθούν
στο WPF port ως βελτίωση, όχι απλή 1:1 αντιγραφή της υπάρχουσας (ελλιπούς σε αυτά τα σημεία)
συμπεριφοράς.

### Cross-cutting εύρημα (και τα 6 πάνω tabs): επαναλαμβανόμενο async pattern

Optimization/Health/Network/Bloatware/Advanced/System ΟΛΑ επαναλαμβάνουν το ΙΔΙΟ μοτίβο: temp .ps1
script γράφεται σε TEMP → `Start-Process powershell.exe -WindowStyle Hidden/Minimized -PassThru` →
`System.Windows.Forms.Timer` (500-800ms) polling `Process.HasExited` → ανάγνωση/διαγραφή temp JSON
αρχείου. Στο WPF/C# port, αυτό ΔΕΝ χρειάζεται να ξαναγραφτεί ως ξεχωριστό `DispatcherTimer` ανά
λειτουργία - μπορεί να γίνει ΕΝΑΣ κοινός, επαναχρησιμοποιήσιμος async helper
(`Process.Start`+`await process.WaitForExitAsync()`+`System.Text.Json`), ή ακόμα καλύτερα να
τρέξει η ΙΔΙΑ η λογική in-process μέσω `Task.Run` αντί να καλεί ξανά ξεχωριστό powershell.exe (το
WinForms original χρειαζόταν ξεχωριστή διεργασία ΕΙΔΙΚΑ για να μείνει το UI thread responsive - το
WPF async/await λύνει το ίδιο πρόβλημα εγγενώς, χωρίς να χρειάζεται καν το process spawning).

### Cross-cutting εύρημα: ενέργειες ΧΩΡΙΣ επιβεβαίωση που πιθανώς χρειάζονται μία στο port

Εντοπίστηκαν 4 σημεία όπου το ίδιο το ps1 ΔΕΝ έχει confirm dialog παρά την επικινδυνότητα -
ΣΗΜΕΙΩΣΗ για το port, όχι απαραίτητα bug προς "διόρθωση" (μπορεί να ήταν σκόπιμη επιλογή):
1. Επανεκκίνηση Wi-Fi adapters (Δίκτυο - top-level κουμπί)
2. Firewall rule enable/disable toggle (Δίκτυο)
3. Καθαρισμός Event Logs (Advanced)
4. **Αφαίρεση Ghost Device** (Advanced - `Remove-PnpDevice -Confirm:$false`, η πιο σαφής περίπτωση)

## 0.4ιδ WPF: ΟΛΕΣ οι υπόλοιπες 6 καρτέλες + Drivers ολοκληρώθηκαν (αυτόνομο πέρασμα, χρήστης ζήτησε "όσα περισσότερα μπορείς σε 5ωρο")

Commits `e93be01`..`c1fca37`. Κάθε καρτέλα: νέο Service.cs + View.xaml(.cs), wired στο MainWindow
switch. `dotnet build` 0/0 + εκκίνηση χωρίς crash επιβεβαιώθηκαν μετά από ΚΑΘΕ καρτέλα. ΚΑΜΙΑ
οπτική δοκιμή δεν έγινε (elevation μπλοκάρει interactive launch από μη-elevated context, ίδιος
περιορισμός με όλο το §0.4).

- **Drivers** (`DriverService.cs`): μόνο πηγή Windows Update (COM API μέσω temp .ps1, ίδιο μοτίβο
  winget) + backup/restore (DISM/pnputil) + driver store cleanup. AMD/NVIDIA/Dell πηγές ΔΕΝ
  μεταφέρθηκαν (web scraping/vendor tools, εκτός εμβέλειας).
- **Health** (`HealthCleanupService.cs`): Registry Cleaner ίδιο στενό scope + .reg backup, browser
  cache (Chrome/Edge/Brave/Opera/Firefox, μόνο ανιχνευμένοι), WinRE status/enable.
- **System** (`SystemService.cs`): Startup (reversible rename), Processes (top 25, critical list
  προστατευμένη), Storage (SHA256 duplicates/large files, μόνο προσωπικοί φάκελοι, recycle bin),
  Restore Points (Checkpoint/Restore-Computer μέσω PS - ΧΩΡΙΣ delete-by-sequence, ίδιος περιορισμός
  με ps1), Services (curated ~29, WMI ChangeStartMode + TweakBackupService), Benchmark (256MB
  write/read).
- **Network** (`NetworkService.cs`): firewall rules (Get/Set-NetFirewallRule μέσω PS), firewall
  policy (netsh export/import/reset), hosts editor (backup πριν save), Defender quick scan
  (fire-and-forget + windowsdefender:// URI).
- **Bloatware** (`BloatwareService.cs`): builtin (Appx+DISM+registry), 22 προτεινόμενες εφαρμογές
  (winget), UWP Manager (ξεχωριστό Window), Deep Uninstall (επίσημος uninstaller + residual scan).
- **Tweaks** (`TweakService.cs`, `CustomContextMenuService.cs`): 14+7+1(HAGS)+3(powercfg)=25 toggles
  μέσω ενιαίου `SimpleTweak` μοντέλου, Οπτικά Εφέ (3 confirm dialogs, ΠΟΤΕ UserPreferencesMask),
  Κρυπτογράφηση Συσκευής (ΜΟΝΟ άνοιγμα Ρυθμίσεων, ΠΟΤΕ registry - ίδια πολιτική με ps1), custom
  context menu CRUD (JSON store).
- **Advanced** (`AdvancedToolsService.cs`): 11 εργαλεία συντήρησης (incl. P/Invoke
  AdjustTokenPrivileges), 7 toolbox launchers (incl. GPU soft reset via keybd_event), curated+πλήρη
  Windows Features (PS), Ghost Devices.

**Διορθώσεις πέρα από 1:1 port** (βλ. εύρημα §0.4ιβ - σημεία χωρίς confirm dialog στο ps1 original):
προστέθηκε confirm dialog σε Ghost Device removal, Registry Editor άνοιγμα, Firewall reset/import,
Hosts save, Visual Effects, optional feature toggle, OneDrive free-up, process kill, restore-point
restore, deep-uninstall, recycle-bin operations - όλα τα destructive actions έχουν πλέον
επιβεβαίωση, ακόμα κι όπου το ps1 original δεν είχε.

**Απάντηση στο ερώτημα του χρήστη** ("το περιεχόμενο όπως είναι στις καρτέλες ενταγμένο είναι
σωστό;"): ΝΑΙ - κάθε λειτουργία τοποθετήθηκε ΑΚΡΙΒΩΣ στην ίδια καρτέλα/κάρτα που είχε στο ps1
original (καμία αναδιάταξη, βλ. ήδη το συμπέρασμα του §0.4η/§0.4ιγ IA review). Η κατηγοριοποίηση
του ps1 ήταν ήδη λογική· η μόνη πραγματική αλλαγή αυτού του περάσματος είναι η προσθήκη confirm
dialogs σε λίγα σημεία που δεν τα είχαν, ΟΧΙ αλλαγή τοποθεσίας περιεχομένου.

## 0. Σχέδιο Αρχιτεκτονικής: Driver Updater πολλαπλών πηγών (για v2.2.0 — ΔΕΝ έχει υλοποιηθεί ακόμα)

Ο χρήστης έδωσε ρητή, λεπτομερή κατεύθυνση αρχιτεκτονικής (βλ. πλήρη συζήτηση στο ίδιο session) για το πώς πρέπει να ξαναχτιστεί ο μηχανισμός Driver Updates (τρέχον v2.1.1: μόνο SDI/SDIO, βλ. καρτέλα Βελτιστοποίηση). Σύνοψη της κατεύθυνσης — **να ακολουθηθεί όταν ξεκινήσει η πραγματική υλοποίηση**, μην ξαναρωτήσεις τον χρήστη τα βασικά:

- **ΠΟΤΕ να μη φανεί το UI του SDIO στον χρήστη.** Το SDIO γίνεται ένας από πολλούς backend "providers", όχι το κέντρο της εφαρμογής. Headless/scripted εκτέλεση (SDIO υποστηρίζει ήδη command-line switches για αυτό) — ΟΧΙ SendKeys/window-hiding hacks (εύθραυστο).
- **Provider abstraction layer**: κάθε πηγή (SDIO, Windows Update/WUA, Microsoft Update Catalog, Intel, AMD, NVIDIA, OEM databases όπως Dell/HP/Lenovo, δική μας cached database) είναι ένα ξεχωριστό `*Provider.ps1` που επιστρέφει ΤΗΝ ΙΔΙΑ δομή δεδομένων (κοινό PSCustomObject σχήμα: DeviceId/HardwareID, DeviceName, DriverVersion, CurrentVersion, Provider/Source, IsCompatible, IsSigned, IsWHQL, OS, Architecture). Το υπόλοιπο πρόγραμμα (UI, ranking, εγκατάσταση) ΔΕΝ ξέρει από πού ήρθε ο driver.
- **Ταυτοποίηση με Hardware ID (VEN/DEV/SUBSYS/REV)**, όχι με όνομα συσκευής — ακριβέστερο matching, λιγότερα false positives.
- **Δικό μας Trust/Stability Score** αντί να εμπιστευόμαστε τυφλά την ετικέτα "stable" κάποιου provider: WHQL/υπογεγραμμένο +, επίσημος κατασκευαστής/OEM +, ταιριάζει Hardware ID +, νεότερος από τον εγκατεστημένο +, beta/unsigned/λάθος OS/λάθος αρχιτεκτονική — μεγάλο αρνητικό (πρακτικά απόρριψη). Μόνο ό,τι περνάει το κατώφλι εμφανίζεται στον χρήστη ως "Stable"/"Recommended".
- **Merge → Deduplicate → Compatibility filter → Stability filter → Ranking → ΔΙΚΟ ΜΑΣ UI.** Ο χρήστης βλέπει ΜΙΑ κάρτα ανά συσκευή με το καλύτερο candidate, ΠΟΤΕ 15 άσχετα αποτελέσματα.
- **Ένας κεντρικός Installation Manager/ουρά** — ποτέ δύο installers (π.χ. SDIO + WUA) να τρέχουν ταυτόχρονα για την ίδια συσκευή (τεκμηριωμένο conflict risk με τον Windows Update orchestrator).
- Στόχος αρχιτεκτονικής: να μπορεί να προστεθεί/αφαιρεθεί provider (π.χ. αύριο Realtek/ASUS) χωρίς να αλλάξει το UI ή ο πυρήνας του μηχανισμού.
- Προτεινόμενη δομή αρχείων μέσα στο project (αν αποφασιστεί διάσπαση σε πολλά αρχεία αντί για ένα ενιαίο .ps1): `Providers/SDIOProvider.ps1`, `Providers/WindowsUpdateProvider.ps1`, `Providers/VendorProviders.ps1`, `Detection/HardwareDetection.ps1`, `Ranking/DriverRanking.ps1`, `Installation/DriverInstaller.ps1` — ή, αν παραμείνει single-file (τρέχουσα σύμβαση όλης της εφαρμογής), ισοδύναμος διαχωρισμός σε ξεχωριστές functions/regions μέσα στο `Optimizer.ps1`.
- Ρητά ΕΚΤΟΣ του άμεσου πλάνου: MAS/activation-related οτιδήποτε (ήδη ΕΚΤΟΣ SCOPE γενικά, §Β παρακάτω).

---

## 1. Τι είναι η εφαρμογή

Εξειδικευμένη εφαρμογή συντήρησης/βελτιστοποίησης Windows 11, γραμμένη εξ ολοκλήρου σε PowerShell με WinForms GUI (όχι WPF, όχι .NET compiled app — τρέχει απευθείας ως .ps1 script). Ελληνικό UI με πλήρη μετάφραση σε Αγγλικά/Γαλλικά/Γερμανικά. Φιλοσοφία: "ελαφρύνει τα Windows χωρίς να χάνουν λειτουργικότητα" — κάθε επικίνδυνη ενέργεια είναι backup-able/reversible, καμία μόνιμη διαγραφή χωρίς Κάδο Ανακύκλωσης, καμία επιθετική σάρωση (π.χ. ο Registry Cleaner είναι σκόπιμα στενός, όχι επιθετικός).

### Καρτέλες (με τη σειρά)
0. **Αρχική** (νέα, v2.0.1) — μετρητές CPU/RAM/Δίσκου με glossy εικονίδια, dropdown επιλογής δίσκου, Βαθμολογία Υγείας Συστήματος, Ανάλυση Χώρου Δίσκου ανά Κατηγορία (v2.1.1, segmented bar + drilldown) — όλα πλέον μέσα σε ένα κεντραρισμένο `$global:scrollPanelHome` wrapper
1. **Βελτιστοποίηση** — Office/Gaming mode, Winget/MS Store/pip/npm/κ.ά. unified update manager, Driver updates (SDI), Driver backup/restore, Driver Store Cleanup (duplicate-version detection), Driver Report Export
2. **Υγεία & Συντήρηση** — SFC/DISM/CHKDSK, Registry Cleaner (στενός scope: μόνο missing uninstallers + obsolete MuiCache), Browser Cache Cleaner, WinRE health check
3. **Δίκτυο & Ασφάλεια** — DNS/Winsock, Firewall Rules Manager (search+toggle, ασύγχρονη φόρτωση), Firewall Policy Tools (export/import/.wfw/reset), Hosts File Editor, Windows Defender Γρήγορη Σάρωση (μετακινήθηκε εδώ από Υγεία & Συντήρηση στο v2.0.5, ρητό αίτημα χρήστη)
4. **Επιπλέον Ρυθμίσεις** — registry tweaks με backup/restore, AI & Copilot section, Performance section (HAGS/Battery/USB/PCIe), context-menu toggles (Ανάληψη Κυριότητας/PowerShell Εδώ), Visual Effects Presets
5. **Εφαρμογές & Bloat** — Recommended Apps κατάλογος (7 κατηγορίες, incl. Viber), bloatware removal/reinstall, Deep Uninstall
6. **Προηγμένα Εργαλεία** — 5 κάρτες: "Συντήρηση & Διάγνωση Συστήματος" + "Εργαλειοθήκη" (incl. GPU driver soft-reset) + "Πρόσθετες Λειτουργίες Windows" (6 δημοφιλή quick-toggle: NetFx3/Sandbox/Hyper-V/WSL/Telnet/SMB1) + "Όλες οι Πρόσθετες Λειτουργίες Windows" (v2.1.1, πλήρης αναζητήσιμη λίστα μέσω DISM) + "Διαχείριση Συσκευών-Φαντασμάτων"
7. **Σύστημα** — Startup Apps, Processes, Storage (duplicates/large files/OneDrive), Restore Points, Services-to-Manual

### Μενού (3 παράλληλα συστήματα, όλα λειτουργικά ταυτόχρονα)
- **Κλασικό** (`System.Windows.Forms.MenuItem`, παλιό MainMenu API): Εργαλεία/Προβολή/Ρυθμίσεις/Ιστορικό Ενεργειών/Βοήθεια — 5 top-level items, όλα με emoji πρόθεμα (🛠️👁️⚙️📋❓)
- **Σύγχρονο οριζόντιο** (`$global:horizModernStrip`): 5 custom-drawn items με πραγματικά εικονίδια (PictureBox + Get-AppIcon)
- **Πλευρικό** (`$global:sidebarForm`): ΞΕΧΩΡΙΣΤΟ Form (όχι embedded panel), 5 items, slide animation. Σε MenuMode="Sidebar" γίνεται "anchored" (`Test-SidebarShouldDock`) — τοποθετείται ΜΕΣΑ στα όρια του mainForm, κάτω από τη λωρίδα καρτελών (Y=118), με πραγματική δέσμευση χώρου στο περιεχόμενο κάθε καρτέλας (`Update-SidebarDockLayout`, βλ. v2.1.0/v2.1.1) αντί να το επικαλύπτει· σε άλλη περίπτωση παραμένει η αρχική συμπεριφορά (κολλάει έξω από το mainForm). Σκληρό clamp θέσης/ύψους (v2.1.1) εγγυάται ότι δεν βγαίνει ΠΟΤΕ εκτός των ορίων του mainForm.

Toggle μεταξύ σύγχρονου/πλευρικού μέσω ρύθμισης `$global:appSettings.MenuMode` ("HorizontalModern" / "Sidebar"). Ctrl+M εναλλάσσει ΚΑΙ το κλασικό.

---

## 2. Κρίσιμα Τεχνικά Ευρήματα (μην τα ξαναανακαλύψεις)

### Το reference code (WMT-GUI.ps1) είναι WPF, όχι WinForms
Επιβεβαιωμένο: χρησιμοποιεί `[System.Windows.WindowState]`, XAML-based UI. Η δική μας εφαρμογή είναι WinForms. **ΔΕΝ μπορείς να αντιγράψεις UI/layout κώδικα απευθείας** — τα δύο frameworks έχουν θεμελιωδώς διαφορετικό layout σύστημα (WPF: declarative Grid/DockPanel με αναλογικό sizing· WinForms: imperative, pixel-based με Anchor/Dock). **Μπορείς όμως να αντιγράψεις PowerShell/registry/CIM λογική** (π.χ. tweaks, εντοπισμός drivers, registry paths) — αυτό είναι framework-agnostic και έχει ήδη χρησιμοποιηθεί εκτενώς (MS Store update mechanism, AI/Copilot registry paths, Services-to-Manual λίστα, Registry Cleaner logic, Viber winget ID).

**Πάντα** ψάξε πρώτα στο `reference/WMT-GUI.ps1` για registry paths/CIM queries/PowerShell λογική πριν σχεδιάσεις κάτι από την αρχή — αλλά μην περιμένεις έτοιμο UI/layout κώδικα.

### Μεγιστοποίηση/Resize — ΕΓΚΑΤΑΛΕΙΦΘΗΚΕ ΣΚΟΠΙΜΑ
Έγιναν 2 πλήρεις προσπάθειες να γίνει η εφαρμογή resizable/maximizable (Anchor properties σε scrollPanels + content panels + tabStripPanel). Και οι δύο απέτυχαν να δώσουν σωστό αποτέλεσμα (tab scroll buttons παρέμεναν λάθος, περιεχόμενο δεν αναδιατασσόταν σωστά, animated background έσπαγε). **Η τρέχουσα κατάσταση (ισχύει ακόμα στο v2.0.5) έχει mainForm.FormBorderStyle="FixedSingle", MaximizeBox=$false** — σκόπιμη, τεκμηριωμένη απόφαση, όχι παράλειψη. Αν ξαναδοκιμαστεί, θα χρειαστεί είτε (α) πλήρη restructuring σε ένα κεντραρισμένο, fixed-size "container" panel μέσα σε resizable window (τεντωμένο φόντο, σταθερό περιεχόμενο - "letterboxing"), είτε (β) πραγματική μετάβαση σε framework με native layout engine.

### Winget/package manager updates — γνωστό, τεκμηριωμένο πρόβλημα elevation
Επιβεβαιωμένο από GitHub microsoft/winget-cli issues (#3418, discussion #3185): **το winget source δεν αρχικοποιείται σωστά σε elevated/Administrator context** (η εφαρμογή μας τρέχει πάντα elevated). Η εγκατάσταση μπορεί να "τερματίσει κανονικά" χωρίς να κάνει τίποτα. Διόρθωση που εφαρμόστηκε: `winget source reset --force` πριν το πρώτο install κάθε batch, `-WindowStyle Hidden` αντί για `-NoNewWindow` (WinForms process χωρίς δικό του console μπορεί να μπερδέψει ένα console tool σαν το winget.exe), και πραγματικός έλεγχος `.ExitCode` (πριν δεν γινόταν καθόλου).

### PowerShell string/escaping πειθαρχία
- Backtick `` ` `` για escape του `$` μέσα σε double-quoted strings, **ποτέ backslash** `\` (backslash δεν είναι escape character στην PowerShell — `\"` σε ένα string δεν κάνει escape, δημιουργεί ασυνεπή αριθμό backslashes πριν από quotes· έλεγχος: μονός αριθμός backslash πριν από `"` = πρόβλημα, ζυγός = ασφαλές/σκόπιμο π.χ. σε regex).
- Single-quoted heredocs (`@'...'@`) + `.Replace()` για embedded, πολυ-γραμμικά background scripts — ποτέ `-Command "..."` με nested quotes.
- **Μετά από ΚΑΘΕ edit**, τρέξε balance check: `python3 -c "print(open('Optimizer.ps1',encoding='utf-8').read().count('{'), ...count('}'))"`. Ένα σταθερό, ήδη γνωστό delta braces=3/brackets=5 είναι ψευδώς θετικό (StartsWith("{")/("[") literals, ANSI/regex patterns) — οτιδήποτε αλλάζει αυτό το delta σημαίνει πραγματικό πρόβλημα.
- Επίσης έλεγχος: κλήση-πριν-τον-ορισμό (function καλείται top-level πριν οριστεί), διπλότυπα function ορίσματα, αρνητικό cumulative brace depth. Τα scripts για αυτούς τους ελέγχους έχουν χρησιμοποιηθεί δεκάδες φορές μέσα στη συνομιλία — αναδημιούργησέ τα εύκολα με regex πάνω στις γραμμές του αρχείου.

### Emoji σε RichTextBox = "τετράγωνα" χωρίς per-character font switching
Ένα RichTextBox με μία γραμματοσειρά (π.χ. Segoe UI) δεν κάνει αυτόματο font fallback για emoji (🔧➕) — εμφανίζονται ως "tofu boxes". Λύση: `SelectionStart`/`SelectionLength`/`SelectionFont` για να δοθεί "Segoe UI Emoji" ΜΟΝΟ στο emoji character, το υπόλοιπο κείμενο κρατά το κανονικό font. Δες `Add-HistLineWithEmoji` στο Show-VersionHistoryWindow.

### mainForm.Top/.Height περιλαμβάνουν το Windows titlebar
Για οτιδήποτε πρέπει να ευθυγραμμιστεί με το **εσωτερικό, ορατό client area** (π.χ. το ξεχωριστό sidebar Form), χρησιμοποίησε `$mainForm.PointToScreen([System.Drawing.Point]::Empty).Y` και `$mainForm.ClientSize.Height`, όχι `.Top`/`.Height` (αυτά περιλαμβάνουν την εξωτερική titlebar/border).

### Animation timer performance
Το `$global:bgAnimTimer` (κινούμενο φόντο) δημιουργούσε ΝΕΟ bitmap σε ολόκληρο το μέγεθος της ενεργής καρτέλας κάθε 50ms (20x/δευτ) — σοβαρό performance hit, ειδικά αν το tab content μεγαλώσει. Μειώθηκε σε 150ms interval + παύση κατά τη διάρκεια active window resize (`$global:isResizing` flag, `ResizeBegin`/`ResizeEnd` events). Οποιοδήποτε νέο, συχνό Timer πρέπει να ελέγχεται για κόστος-ανά-tick πριν οριστεί το interval.

### Δύο παράλληλα translation συστήματα — ΜΗΝ τα μπερδεύεις
- **`TT("ελληνικό κείμενο")`**: το ίδιο το ελληνικό κείμενο είναι το dictionary key. Χρησιμοποιείται για ΟΛΟ το δυναμικό/μεγάλο κείμενο (tweaks, tooltips, μηνύματα, changelog). Αλλαγή του ελληνικού κειμένου = "σπάει" την αντιστοίχιση με τις EN/FR/DE μεταφράσεις μέχρι να ενημερωθούν και αυτές.
- **`T("KeyName")`**: σταθερά, ονομαστικά keys (μενού, tab τίτλοι, sidebar labels) σε ξεχωριστό `[ordered]@{}` dictionary ανά γλώσσα.
- Και τα δύο έχουν safe fallback (επιστρέφουν το ελληνικό/το key αν λείπει μετάφραση) — δεν κάνουν crash, αλλά αφήνουν ασυνεπές UI.
- Πολιτική που ακολουθήθηκε: γράφε πρώτα μόνο Ελληνικά κατά την ανάπτυξη, μαζικές μεταφράσεις σε EN/FR/DE στο τέλος κάθε "batch" δουλειάς, πριν το export.

### Backup/restore μηχανισμός (χρησιμοποιήσου παντού για tweaks)
Ενιαίο JSON backup file, μέσω `Get-TweakBackupData`/`Save-TweakBackupData`/`Backup-RegValueIfNeeded`/`Restore-BackedUpRegValue`/`Set-RegSafe`. Custom `New-ToggleSwitch` UI control (όχι CheckBox) καλεί OnScript/OffScript closures — **πάντα με `.GetNewClosure()`** όταν αναφέρονται σε loop variables (γνωστό, ξαναεμφανιζόμενο bug pattern).

### Ασύγχρονες, βαριές λειτουργίες — πάντα background process + Timer polling
Ποτέ `-Wait` σε βαριές εντολές (θα παγώνει το UI thread). Pattern: `Start-Process -WindowStyle Hidden -PassThru`, αποθήκευση του process object σε global state, `Timer.Add_Tick` κάθε ~500-800ms ελέγχει `.HasExited`. Χρησιμοποιείται για: winget scan/upgrade, driver scan, firewall rules load, registry clean scan, KLite install, storage scan.

### Κείμενο μέσα σε double-quoted strings που περιέχει κυριολεκτικό `$όνομα` (π.χ. περιγραφή bug που αναφέρει μεταβλητή) ΠΡΕΠΕΙ να γίνεται escape με backtick
Βρέθηκε πραγματικό, confirmed bug (v2.0.5): μια καταχώρηση στο `$global:versionHistory` περιέγραφε ένα παλιότερο bug αναφέροντας κυριολεκτικά το όνομα `$parent`, αλλά μέσα σε ένα κανονικό double-quoted string (`"...το $parent (η κάρτα-στόχος)..."`) — η PowerShell το interpolate-άρει ως μεταβλητή αναφορά, και αφού κανένα global `$parent` δεν υπάρχει σε εκείνο το σημείο, γινόταν silent αντικατάσταση με κενό string. Αποτέλεσμα: το Ιστορικό Εκδόσεων εμφάνιζε κενό αντί για "$parent" σε αυτή την καταχώρηση — ΚΑΝΕΝΑ parse error, το bug είναι αόρατο μέχρι να το δεις live ή να το ψάξεις ρητά. Διορθώθηκε με backtick escape (`` `$parent ``) στο ΙΔΙΟ σημείο ΚΑΙ στο αντίστοιχο TT() dictionary key (και τα δύο πρέπει να ταιριάζουν byte-for-byte μετά το interpolation, αφού και τα δύο strings evaluate στο ίδιο σημείο του script load). **Έλεγχος για μελλοντικές αλλαγές**: `grep -n '^\s\+"[^"]*\$[a-zA-Z]' Optimizer.ps1` μέσα στα line ranges του `$global:versionHistory` και `$global:stringTranslations` — οτιδήποτε ταιριάζει και ΔΕΝ έχει backtick πριν το `$` είναι πιθανό bug.

### Fake τυπογραφικά εισαγωγικά (π.χ. γερμανικά „..." ή γαλλικά «...») = ΠΟΤΕ με απλά ASCII `"` χαρακτήρα
Βρέθηκε πραγματικό, confirmed bug (v2.0.5) στην ίδια μαζική μετάφραση: γράφτηκαν γερμανικές μεταφράσεις με το σωστό ανοιγόμενο `„` (U+201E) αλλά με ΛΑΘΟΣ κλείσιμο — ένα απλό ASCII `"` (0x22) αντί για το σωστό Unicode `"` (U+201C). Το ASCII `"` έκλεινε πρόωρα το ΙΔΙΟ το PowerShell string literal, σπάζοντας το hash literal parsing με confusing, μακρινά error messages (π.χ. "Unexpected token 'Kästchen" σε άσχετη γραμμή). **Ασφαλέστερη πρακτική που υιοθετήθηκε**: απόφυγε εντελώς τα τυπογραφικά/curly quotes μέσα σε μεταφρασμένο κείμενο — χρησιμοποίησε πάντα απλά ASCII single quotes `'...'` για έμφαση λέξης/φράσης, ίδια σύμβαση με το υπόλοιπο ελληνικό κείμενο της εφαρμογής (π.χ. `'Εμφάνιση Μενού'`). Μετά από ΚΑΘΕ μαζική προσθήκη μεταφράσεων, τρέξε `[System.Management.Automation.Language.Parser]::ParseFile()` ΠΡΙΝ θεωρήσεις την αλλαγή ολοκληρωμένη — το delta braces/brackets ΔΕΝ πιάνει αυτού του είδους το σφάλμα (τα quotes δεν είναι braces/brackets), μόνο ο πραγματικός parser το πιάνει.

### Duplicate-key έλεγχος στα TT() dictionaries είναι CASE-INSENSITIVE — μην ψάχνεις μόνο case-sensitive
Βρέθηκε πραγματικό, confirmed bug (v2.1.1): προστέθηκε νέο κλειδί `"Ολοκληρώθηκε."` (κεφαλαίο Ο) ενώ ήδη υπήρχε `"ολοκληρώθηκε."` (πεζό ο) στο ΙΔΙΟ `en` dictionary — parse error "Duplicate keys". Το PowerShell hashtable literal duplicate-key check συγκρίνει keys ΧΩΡΙΣ διάκριση πεζών/κεφαλαίων, ενώ ένα απλό case-sensitive `grep`/regex ΔΕΝ θα το εντοπίσει (ακριβώς αυτό συνέβη - επιβεβαιώθηκε "0 αποτελέσματα" με case-sensitive αναζήτηση, ενώ το duplicate υπήρχε). **Έλεγχος για μελλοντικές αλλαγές**: όποτε ελέγχεις για collision πριν προσθέσεις νέο TT() key, χρησιμοποίησε case-INSENSITIVE αναζήτηση (π.χ. Grep `-i`), όχι μόνο exact-case match. Πρακτικός τρόπος εντοπισμού της ΑΚΡΙΒΟΥΣ γραμμής όταν συμβεί: το parse error message δείχνει το duplicate key string· μετά ψάξε ΧΩΡΙΣ `-i` decoy - συνήθως χρειάζεται προγραμματιστικός έλεγχος (line-by-line μέσα στο συγκεκριμένο dictionary's line range, HashSet με ordinal comparison) αφού το plain-text grep δεν αρκεί.

### Sidebar: ΔΥΟ ξεχωριστοί μηχανισμοί ανάλογα με anchored/όχι (v2.1.5, θεμελιώδης αλλαγή)
Μετά από ΤΕΣΣΕΡΙΣ διαδοχικές αποτυχημένες προσπάθειες να διορθωθεί με clamps/guards το πρόβλημα "το
πλευρικό μενού είναι εκτός των ορίων του παραθύρου" πάνω στο παλιό μοντέλο (πάντα `$global:sidebarForm`,
ξεχωριστό top-level Form, τοποθετημένο μέσω συντεταγμένων ΟΘΟΝΗΣ ακόμα και όταν "anchored"), η
αρχιτεκτονική άλλαξε θεμελιωδώς: **όταν anchored/docked (`Test-SidebarShouldDock` = true), χρησιμοποιείται
πλέον `$global:sidebarEmbedPanel` - ΠΡΑΓΜΑΤΙΚΟ child Panel του mainForm** (`$mainForm.Controls.Add(...)`),
με συντεταγμένες σχετικές ΜΟΝΟ με το `mainForm.ClientSize` (0 ή `ClientSize.Width-140` για X, πάντα 118
για Y) - ΚΑΝΕΝΑ σύστημα συντεταγμένων οθόνης, ΚΑΝΕΝΑ DPI/timing ρίσκο, δομικά αδύνατο να βγει εκτός ορίων.
Το `$global:sidebarForm` (ξεχωριστό Form) παραμένει ΜΟΝΟ για τη μη-anchored ("κολλημένο έξω από το
παράθυρο") περίπτωση - ένα child control δεν μπορεί δομικά να πετύχει αυτό το οπτικό αποτέλεσμα.
**Add-SidebarItem** τώρα δέχεται προαιρετικό `-Parent` (προεπιλογή: `$global:sidebarForm`) - τα 5
αντικείμενα του μενού χτίζονται ΔΙΠΛΑ, μία φορά σε κάθε container (μικρό κόστος διπλασιασμού έναντι
πολύπλοκου reparenting). **Show-Sidebar/Hide-Sidebar** διακλαδώνονται εσωτερικά ανάλογα με
`Test-SidebarShouldDock` - τα call sites (κουμπί ☰, startup timer, `Update-SidebarDockLayout`) ΔΕΝ
χρειάστηκε να αλλάξουν.
**ΕΝΗΜΕΡΩΣΗ v2.2.0**: το animation ολίσθησης (που αρχικά παραλείφθηκε σκόπιμα στην anchored περίπτωση,
βλ. προηγούμενη πρόταση σε παλιότερες εκδόσεις αυτού του αρχείου) ΠΡΟΣΤΕΘΗΚΕ τελικά και εκεί, μέσω νέου
`$global:sidebarEmbedSlideTimer` (15ms interval, easing `Left += diff*0.4`, στο ΙΔΙΟ `sidebarEmbedPanel`).
Αυτό είναι ΑΣΦΑΛΕΣ (σε αντίθεση με το παλιό μοντέλο) ακριβώς επειδή το WS_CHILD clipping guarantee σημαίνει
ότι ακόμα και οι ΕΝΔΙΑΜΕΣΕΣ θέσεις κατά το animation (πρόσκαιρα εκτός `mainForm.ClientSize.Width`) είναι
δομικά αδύνατο να αποδοθούν εκτός των ορίων του mainForm - clip, όχι overflow. `Show-Sidebar`/`Hide-Sidebar`
θέτουν `$global:sidebarEmbedTargetX` + `$global:sidebarEmbedClosing` και καλούν `.Start()` στον timer· το
`Add_Tick` του timer κάνει το animate και σταματάει μόνο του όταν φτάσει (tolerance 4px), κρύβοντας το panel
στο τέλος ΜΟΝΟ αν `$global:sidebarEmbedClosing = $true`. Ο παλιός περιοδικός "φρουρός" Timer (`sidebarBoundsGuardTimer`) παραμένει στον κώδικα αλλά είναι
πλέον ουσιαστικά αδρανής στην anchored περίπτωση (το sidebarForm μένει Visible=false εκεί) - δεν
αφαιρέθηκε, απλά δεν χρειάζεται πια, μηδενικό ρίσκο να τον αφήσεις.
**Αν το πρόβλημα "εκτός ορίων" αναφερθεί ΞΑΝΑ μετά από αυτό**: σημαίνει ότι ο χρήστης δοκιμάζει είτε (α)
την ΠΑΛΙΑ, μη-ενημερωμένη έκδοση (ζήτα restart), είτε (β) τη μη-anchored περίπτωση συγκεκριμένα (σπάνια
δοκιμασμένη - ελέγξτε `$global:appSettings.MenuMode`), είτε (γ) υπάρχει πραγματικά νέο, διαφορετικό bug -
ΜΗΝ ξαναδοκιμάσεις άλλο clamp πάνω στο ΠΑΛΙΟ μοντέλο, ελέγξτε πρώτα ότι το embedded panel μονοπάτι όντως
εκτελείται (`Test-SidebarShouldDock` πρέπει να είναι `$true`).

### AutoScrollMinSize είναι ΑΝΕΞΑΡΤΗΤΟ από το πραγματικό περιεχόμενο - συνηθισμένη ρίζα "ανεπιθύμητου scrollbar"
Βρέθηκε πραγματικό, confirmed bug (v2.2.0, καρτέλα Αρχική): ο χρήστης ανέφερε επίμονα "δεν θέλω scrollbar",
ενώ το ορατό περιεχόμενο ήταν ήδη αρκετά συμπαγές. Root cause: `AutoScrollMinSize` σε ένα AutoScroll Panel
είναι ένα ΑΝΕΞΑΡΤΗΤΟ, hardcoded "πάτωμα" στο εικονικό μέγεθος του scrollable καμβά - αν είναι μεγαλύτερο
από το πραγματικό περιεχόμενο, εμφανίζεται scrollbar ΑΝΕΞΑΡΤΗΤΑ από το πόσο συμπαγές γίνεται το περιεχόμενο
(`$global:scrollPanelHome.AutoScrollMinSize` ήταν `(1010, 1010)`, ενώ το πραγματικό περιεχόμενο τέλειωνε
γύρω στα 605-775px ανάλογα με την έκδοση). **Έλεγχος για μελλοντικές αλλαγές**: όποτε αναφέρεται
"ανεπιθύμητο scrollbar" ΠΑΡΑ το ότι το περιεχόμενο φαίνεται να χωράει, μην ψάχνεις μόνο τα Y-coordinates
των τελευταίων στοιχείων - έλεγξε ΠΡΩΤΑ το `AutoScrollMinSize` του συγκεκριμένου `scrollPanelX` έναντι του
πραγματικού τέλους περιεχομένου. Έγινε πλήρες audit ΟΛΩΝ των `scrollPanel*.AutoScrollMinSize` στο αρχείο
στο v2.2.0 (Optimization/Bloatware/System/Network/Advanced/Tweaks) - όλα επιβεβαιώθηκαν συνεπή· μόνο το
Home ήταν λάθος. Αν προστεθεί νέο περιεχόμενο σε οποιαδήποτε καρτέλα, ΠΑΝΤΑ ενημέρωσε το αντίστοιχο
`AutoScrollMinSize` ΜΑΖΙ με τη νέα κάρτα/γραμμή, ποτέ ξεχωριστά.

### Y-coordinate overlap σε κάρτες με πολλαπλές, διαδοχικές sections (Tweaks tab pattern)
Βρέθηκε πραγματικό, confirmed bug (v2.2.0, καρτέλα Επιπλέον Ρυθμίσεις): οι κάρτες "AI & Copilot" και
"Απόδοση" επικάλυπταν ορατά τις προηγούμενες γραμμές ρυθμίσεων - ΟΧΙ επειδή ήταν "κενές" (αρχική, λάθος
υπόθεση σε προγενέστερο πέρασμα, βλ. §4 σχόλιο overclaim), αλλά επειδή η Y θέση της επόμενης κάρτας ήταν
μικρότερη από το πραγματικό ύψος (Y + Height) της προηγούμενης. Σε αρχεία με δεκάδες διαδοχικές κάρτες σε
μία καρτέλα, ένα μεμονωμένο "μικρό" fix (π.χ. προσθήκη 1-2 γραμμών σε μια κάρτα) μπορεί να χρειαστεί να
μετατοπίσει ΟΛΕΣ τις επόμενες κάρτες μέχρι το τέλος της καρτέλας - ΟΧΙ μόνο την επόμενη. **Έλεγχος για
μελλοντικές αλλαγές**: μετά από οποιαδήποτε αλλαγή ύψους/θέσης κάρτας, υπολόγισε το νέο "κάτω άκρο"
(Y+Height) και επιβεβαίωσε ότι είναι ≤ στο Y της επόμενης κάρτας, ΓΙΑ ΟΛΗ την αλυσίδα μέχρι το τέλος της
καρτέλας, όχι μόνο το πρώτο ζευγάρι. Συνδέεται με το εύρημα §2 checklist item 7 ("Overlap check").

### Apply-MenuModeSettings πρέπει να κρύβει ΚΑΙ τα δύο sidebar containers, όχι μόνο το παλιό Form
Βρέθηκε πραγματικό, confirmed bug (v2.2.0): μετά την αρχιτεκτονική αλλαγή σε `$global:sidebarEmbedPanel`
(βλ. προηγούμενη ενότητα), το `Apply-MenuModeSettings` ΣΥΝΕΧΙΖΕ να κρύβει μόνο το παλιό
`$global:sidebarForm.Visible = $false` - ΠΟΤΕ το νέο embedded panel. Αποτέλεσμα: αλλαγή MenuMode εν ώρα
λειτουργίας (π.χ. από Sidebar σε Οριζόντιο) άφηνε το embedded panel "κολλημένο" ορατό. Διορθώθηκε με
ρητή προσθήκη `if ($global:sidebarEmbedPanel) { ...Stop() timer...; $global:sidebarEmbedPanel.Visible =
$false }`. **Μάθημα για μελλοντικές δυαδικές αρχιτεκτονικές** (όπου ένα UI στοιχείο έχει 2 διαφορετικές
υλοποιήσεις ανάλογα με συνθήκη, π.χ. anchored/floating): κάθε function που κάνει "global reset/hide" πρέπει
να ελέγχεται ρητά ότι καλύπτει ΚΑΙ τα δύο μονοπάτια, όχι μόνο αυτό που υπήρχε πρώτο ιστορικά.

### Driver engine: 3 νέες πηγές προστέθηκαν (AMD+NVIDIA ζωντανά επιβεβαιωμένα, Dell ΜΟΝΟ μέσω τεκμηρίωσης)
Μετά την οδηγία του χρήστη να ενσωματωθεί πραγματικός πολυ-πηγαίος driver engine (όχι μόνο Windows
Update), προστέθηκαν 3 νέες πηγές στο `Start-DriverScan`/`Complete-DriverScan` (ίδιο αρχείο, ~γραμμή
11080+): **AMD (amd.com)**, **NVIDIA (nvidia.com/gfwsl.geforce.com)** και **Dell (Dell Command Update
CLI)**. Σημαντική διαφορά μεθοδολογίας μεταξύ τους - κρατήστε τη διάκριση σε μελλοντικές αναφορές:
- **AMD**: ζωντανά επιβεβαιωμένο σε πραγματικά δεδομένα. Βρέθηκε (μέσω Browser tool navigation σε
  amd.com + network inspection) ότι η σελίδα οδηγών ανά μοντέλο GPU (π.χ.
  `.../graphics/radeon-rx/radeon-rx-7000-series/amd-radeon-rx-7800-xt.html`) είναι **server-rendered,
  ΚΑΜΙΑ κρυφή AJAX/JSON API** - το HTML περιέχει απευθείας το Release Date και το download link (η
  έκδοση Adrenalin είναι μέσα στο ίδιο το filename, π.χ. `amd-software-adrenalin-edition-26.8.1-...exe`).
  Το URL slug χτίζεται από το GPU name μέσω regex mapping (RX 9xxx/7xxx/6xxx/5xxx/Vega → αντίστοιχο
  series folder) + lowercase/hyphenate του ονόματος. Η σύγκριση "νεότερη έκδοση" γίνεται με ΗΜΕΡΟΜΗΝΙΕΣ
  (DriverDate εγκατεστημένου vs Release Date AMD), ΟΧΙ με αριθμό έκδοσης - επιβεβαιώθηκε ζωντανά ότι το
  Windows DriverVersion (π.χ. `32.0.31041.1004`) και το AMD Adrenalin marketing version (π.χ. `26.8.1`)
  είναι ΕΝΤΕΛΩΣ διαφορετικά συστήματα αρίθμησης - ΠΟΤΕ μην τα συγκρίνεις σαν strings/version numbers.
  Δοκιμάστηκε end-to-end σε πραγματικό hardware αυτού του dev sandbox (AMD Radeon RX 7900 GRE) - σωστά
  εντόπισε ότι υπήρχε νεότερος οδηγός διαθέσιμος.
- **NVIDIA**: ΕΠΙΣΗΣ ζωντανά επιβεβαιωμένο, ίδια αυστηρότητα με το AMD. Χρησιμοποιεί το ΔΙΚΟ ΤΗΣ NVIDIA
  δημόσιο μηχανισμό (όχι reverse-engineered guess - ίδιος μηχανισμός με το πολυετές, ευρέως
  χρησιμοποιούμενο ανοιχτού κώδικα εργαλείο TinyNvidiaUpdateChecker, ο κώδικάς του διαβάστηκε απευθείας
  από GitHub για επιβεβαίωση): 1) `https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=3`
  (επίσημη λίστα ΟΛΩΝ των NVIDIA προϊόντων → pfid, TypeID=4 για λίστα OS → osID, Windows 11=135), 2)
  `https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=
  DriverManualLookup&pfid=X&osID=Y&upCRD=0&dch=1` (0=Game Ready Driver, 1=DCH - το μοντέρνο πρότυπο).
  Το GPU name πρέπει να "καθαριστεί" πριν το ταίριασμα με τη λίστα (αφαίρεση "NVIDIA " prefix, "with
  Max-Q Design" suffix, "(OEM)", μεγέθους μνήμης, "Super"→"SUPER" - τεκμηριωμένοι κανόνες από το
  βοηθητικό project github.com/ZenitH-AT/nvidia-data, το οποίο απλώς κρατάει cache της ίδιας επίσημης
  NVIDIA λίστας). Δοκιμάστηκε ζωντανά με πραγματικό κλήση (GeForce RTX 3070, pfid=933) - επέστρεψε
  πραγματική τρέχουσα έκδοση/ημερομηνία/download URL. Ίδια λογική σύγκρισης με ημερομηνίες (όχι version
  strings) με το AMD, για τον ίδιο λόγο (NVIDIA marketing version π.χ. "616.56" ≠ Windows DriverVersion).
- **Πλήρες end-to-end test ολόκληρου του `Start-DriverScan` background script** (όχι μόνο το κομμάτι
  AMD/NVIDIA μεμονωμένα) έτρεξε ζωντανά σε αυτό το sandbox: εξήγαγε το heredoc σε αυτοτελές .ps1,
  έτρεξε το ΠΛΗΡΕΣ pipeline (WU + AMD + NVIDIA + Dell μαζί), και επιβεβαιώθηκε ότι η τελική JSON έξοδος
  έχει την ΑΚΡΙΒΩΣ αναμενόμενη δομή που διαβάζει το `Complete-DriverScan` - AmdResults σωστά γεμάτο,
  NvidiaResults/DellResult σωστά κενά (καμία εξαίρεση/σφάλμα) αφού το sandbox δεν έχει NVIDIA GPU ή Dell
  hardware. Αυτό είναι ο ασφαλέστερος τρόπος να δοκιμαστεί ένα τέτοιο script πριν παραδοθεί - τρέξε το
  ΠΡΑΓΜΑΤΙΚΟ background script (όχι απλά snippets) και επιβεβαίωσε τη μορφή της τελικής JSON.
- **Dell**: ΜΟΝΟ τεκμηρίωση + δημόσια production scripts (RMM/enterprise, π.χ.
  github.com/ajh0912/Useful-PowerShell/Get-DellUpdates.ps1) - **ΔΕΝ δοκιμάστηκε ζωντανά** (το sandbox
  δεν έχει Dell hardware/DCU εγκατεστημένο). Μηχανισμός: αν `Get-CimInstance Win32_ComputerSystem`
  δείξει Dell, ελέγχεται αν υπάρχει `dcu-cli.exe` (Program Files\Dell\CommandUpdate) - αν ναι, τρέχει
  αθόρυβα `/scan -report="<dir>"` (ΠΡΟΣΟΧΗ: τεκμηριωμένος περιορισμός - το `-report=` ΔΕΝ δέχεται
  διαδρομή μέσα σε `C:\Windows\Temp`, το κανονικό `$env:TEMP` χρήστη είναι ασφαλές), διαβάζει το
  `DCUApplicableUpdates.xml` που παράγεται μέσα στον φάκελο (`$xml.updates.update` με πεδία
  name/version/date/urgency/type/category). Αν το DCU ΔΕΝ είναι εγκατεστημένο, εμφανίζεται ΜΙΑ γραμμή
  πρότασης με κουμπί "Εγκατάσταση & Έλεγχος μέσω Dell" - ΚΑΜΙΑ αυτόματη εγκατάσταση χωρίς ρητό κλικ (ο
  χρήστης το ζήτησε ρητά ως "confirmation πριν εγκατασταθεί", μετά την εμπειρία με το SDI). **Αν ο
  χρήστης αναφέρει ότι η λίστα ενημερώσεων/report format δεν βγαίνει σωστά σε πραγματικό Dell PC**, αυτό
  ΔΕΝ είναι έκπληξη - χρειάζεται το πραγματικό output (`Get-Content DCUApplicableUpdates.xml -Raw`) από
  τον χρήστη για να διορθωθεί το parsing, ΟΧΙ ξανά-μάντεμα.
- **HP**: σκόπιμα ΔΕΝ υλοποιήθηκε ακόμα - τα CLI flags του HP Image Assistant
  (`/Operation:Analyze /Action:List /Silent /ReportFolder:`) βρέθηκαν μόνο σε φόρουμ/λιγότερο αυστηρά
  τεκμηριωμένες πηγές. Επιπλέον, η ίδια η support.hp.com δεν έχει το ίδιο απλό "search by model name"
  flow που έχει η AMD (δοκιμάστηκε ζωντανά - το HP flow είναι serial-number/account-driven, πιο
  πολύπλοκο). Πρόσθεσέ το ΜΟΝΟ μετά από την ίδια αυστηρότητα επιβεβαίωσης (επίσημο PDF/manual +
  τουλάχιστον ένα real-world production script που να συμφωνεί), ή αφού ο χρήστης επιβεβαιώσει ζωντανά.
- **Lenovo**: ΕΡΕΥΝΗΘΗΚΕ ζωντανά (καλύτερο εύρημα από το αρχικό HP/Lenovo forum-only σημείωμα, αλλά ΔΕΝ
  υλοποιήθηκε - πιο σύνθετο pattern από AMD/NVIDIA/Dell). Βρέθηκε το ανοιχτού κώδικα PowerShell module
  `jantari/LSUClient` (288 stars, MIT, ενεργά συντηρημένο, ρητά "does not require Lenovo System Update or
  any other external program") που αποκαλύπτει τον ΕΠΙΣΗΜΟ μηχανισμό: 1) το πρώτο 4-χαρακτήρων κομμάτι
  του `Win32_ComputerSystem.Model` (Lenovo Machine Type code, π.χ. "20Y4") χτίζει URL
  `https://download.lenovo.com/catalog/{MTM}_Win11.xml` (επιβεβαιώθηκε ζωντανά, 200 OK, πραγματικό XML,
  για 3 διαφορετικά πραγματικά MTM codes: 20Y4/21CB/20XW - "_Win10.xml" επίσης δουλεύει). 2) ΣΗΜΑΝΤΙΚΗ
  ΠΟΛΥΠΛΟΚΟΤΗΤΑ που δεν υπάρχει στο AMD/NVIDIA/Dell: αυτό το πρώτο XML είναι ΜΟΝΟ μια λίστα δεικτών
  (`<packages><package><location>URL_ΣΕ_ΑΛΛΟ_XML</location><category>...</category></package>...)` -
  ΚΑΘΕ πακέτο χρειάζεται ΔΕΥΤΕΡΗ, ξεχωριστή HTTP κλήση (π.χ.
  `https://download.lenovo.com/pccbbs/mobiles/n40oi02w_2_.xml`) για να πάρεις πραγματικό όνομα/έκδοση/
  ημερομηνία - ένα πραγματικό laptop είχε 39 πακέτα σε αυτή τη δοκιμή, άρα μια πλήρης σάρωση θα σήμαινε
  ΔΕΚΑΔΕΣ HTTP requests, πιο αργό/εύθραυστο από τις άλλες πηγές. ΕΠΙΣΗΣ: το `[xml]$xml = $content` cast
  ΑΠΟΤΥΓΧΑΝΕΙ αν δεν αφαιρεθεί πρώτα το UTF-8 BOM από το response string (το LSUClient το χειρίζεται
  ρητά: `$content -replace "^$UTF8ByteOrderMark"` όπου το BOM string φτιάχνεται από τα bytes 239,187,191
  - ΙΔΙΑ 3 bytes με το BOM του ίδιου του Optimizer.ps1, καθαρή σύμπτωση αλλά εύκολο να το θυμάσαι).
  **Απόφαση**: αναβλήθηκε για μελλοντικό, ξεχωριστό pass με περισσότερο χρόνο (χρειάζεται προσεκτικό
  σχεδιασμό για το πόσα/ποια από τα 39 πακέτα να ελεγχθούν πραγματικά ώστε να μη γίνει αργό, και ζωντανό
  τεστ σε πραγματικό Lenovo μηχάνημα) - ΜΗΝ ξαναρχίσεις την έρευνα από την αρχή, όλα τα παραπάνω είναι
  ήδη επιβεβαιωμένα.
- Ορθόδοξη μέθοδος όποτε χρειάζεται νέα πηγή δεδομένων από εξωτερικό site χωρίς επίσημο public API: άνοιξε
  το site με το Browser tool, δοκίμασε το πραγματικό UI flow (π.χ. product search/dropdown), και κοίταξε
  `read_network_requests` ΚΑΙ το raw HTML (`document.body.innerHTML`) - πολλά "modern" sites είναι στην
  πραγματικότητα server-rendered για το ΚΥΡΙΟ περιεχόμενο (χωρίς κρυφό API να σπάσει), ακόμα κι αν η
  πλοήγηση/φίλτρα είναι JS-based.
- Dead code που ΔΕΝ έχει καθαριστεί ακόμα (χαμηλή προτεραιότητα, δεν επηρεάζει συμπεριφορά): οι
  συναρτήσεις `Start-SdiDriverCheck`, `Find-SdioExecutable`, και τα globals `$global:sdiEnsureState`/
  `$global:sdiEnsureTimer` παραμένουν ορισμένα στο αρχείο από την εποχή του SDI, αλλά ΔΕΝ καλούνται
  πουθενά πλέον (το μοναδικό click handler του `$global:btnDriverScan` είναι `{ Start-DriverScan }`) -
  ασφαλές, ανενεργό, αλλά θα μπορούσε να αφαιρεθεί καθαρά σε κάποιο μελλοντικό pass.

### Icon συστήματα (2 ξεχωριστά, μη-αλληλοεπικαλυπτόμενα)
- `Get-AppIcon -Type X -Color C`: γενικό, vector (GDI+ Pen/Brush draws, μερικά Segoe Fluent Icons glyph-based) 24x24 σύστημα χρησιμοποιούμενο ΠΑΝΤΟΥ (tabs, menus, sidebar). Μην το αλλάξεις γενικά — επηρεάζει όλη την εφαρμογή.
- `Get-GlossyHardwareIcon -Type (CPU|RAM|Disk) -AccentColor C`: ΝΕΟ (v2.0.2), ξεχωριστό, 96x96, gradient+glossy-highlight σύστημα ΜΟΝΟ για τις 3 κάρτες της καρτέλας Αρχική.

---

## 3. Δομικός Έλεγχος — ΥΠΟΧΡΕΩΤΙΚΟΣ μετά από κάθε edit

Αυτή η πειθαρχία απέτρεψε δεκάδες πραγματικά bugs μέσα στη συνομιλία (π.χ. δομικό λάθος στο changelog array, κλήση-πριν-τον-ορισμό στο appToolTip, invalid `.Parent` reference). Ελάχιστο σετ ελέγχων μετά από ΚΑΘΕ str_replace/write:

1. **Ισορροπία**: `content.count('{')` vs `('}')`, ίδιο για `[`/`]`. Το delta πρέπει να παραμένει σταθερό (γνωστά ψευδώς θετικά: 3 braces, 5 brackets).
2. **Αρνητικό βάθος**: cumulative `{` minus `}` ανά γραμμή δεν πρέπει ποτέ να πέσει κάτω από 0.
3. **Κλήση-πριν-τον-ορισμό**: καμία top-level (μη-indented) κλήση συνάρτησης πριν τον `function X {` ορισμό της (deferred/scriptblock κλήσεις είναι ασφαλείς).
4. **Διπλότυπα function ορίσματα**.
5. **Backslash-quote**: μονός αριθμός `\` πριν από `"` σε μη-comment γραμμή = πιθανό πρόβλημα (έλεγξε αν είναι σκόπιμο regex/literal).
6. **Μεταφράσεις**: κάθε νέο `TT("...")` string πρέπει να υπάρχει ως key και στα 3 dictionaries (en/fr/de) πριν το export — αλλιώς γίνεται fallback σε Ελληνικά (ασφαλές αλλά ασυνεπές).
7. **Overlap check**: για νέα UI στοιχεία στην ίδια κάρτα/container, επιβεβαίωσε ότι τα rectangles (X,Y,W,H) δεν τέμνονται.

---

## 4. Πλήρης Λίστα Εκκρεμοτήτων (κατά προτεραιότητα σημασίας για τον χρήστη)

**Ενημερώθηκε στο v2.0.5.** Τα περισσότερα στοιχεία της παλιάς λίστας (§Β "PC Manager/WMT σύγκριση") έχουν πλέον υλοποιηθεί: Health Score, Toast notifications (`Show-ToastNotification` - **v2.3.0: πλέον συνδεδεμένο σε 3 σημεία ολοκλήρωσης** - δημιουργία σημείου επαναφοράς, ολοκλήρωση σάρωσης οδηγών, ολοκλήρωση ReTrim SSD· βλ. ενότητα 0.6 παρακάτω), Defender Quick Scan, στοχευμένος browser cache cleaner, WinRE check, Driver Store Cleanup, Ghost Device Manager, Windows Optional Features toggle, Visual Effects Presets, Firewall policy tools, Hosts file editor, context-menu toggles, GPU soft-reset. Retroactive cleanup του ΠΑΛΙΟΥ changelog (🔧/➕ tagging + έγχρωμες κουκκίδες) επίσης ολοκληρώθηκε σε ΟΛΕΣ τις 60+ εκδόσεις, ΚΑΙ οι μεταφράσεις EN/FR/DE του v2.0.4 batch (που είχαν αρχικά μείνει μόνο Ελληνικά) συμπληρώθηκαν.

### Α. Ρητά αιτήματα που ΔΕΝ έχουν ακόμα ενσωματωθεί
1. ~~Global styling για ΟΛΑ τα dropdown/ComboBox και radio buttons~~ — **ΕΠΙΒΕΒΑΙΩΘΗΚΕ ΠΛΗΡΗΣ ΚΑΛΥΨΗ v2.1.5**: audit βρήκε 5 ComboBox συνολικά στο αρχείο (comboTheme/comboLang/cboBenchDrive/cmbScope/cboHomeDrive) — και οι 5 έχουν `Set-ThemedComboBoxDrawing`. 15 RadioButton συνολικά, όλα μέσα στο Show-AppearanceSettingsWindow (θεματισμένα σωστά στη δημιουργία τους, αφού το dialog φτιάχνεται φρέσκο κάθε φορά με το τρέχον θέμα - καμία ζωντανή ενημέρωση μέσα σε ήδη ανοιχτό dialog, ελάσσων/αποδεκτός περιορισμός, όχι bug).
2. ~~Διάφανα πάνελ/κάρτες που δείχνουν το κινούμενο φόντο "από μέσα τους"~~ — **ΥΛΟΠΟΙΗΘΗΚΕ v2.4.0** μέσω `Set-PanelGlassBackground` (custom `Add_Paint` + χειροκίνητο crop, ΟΧΙ BackgroundImage σε AutoScroll panel - βλ. ενότητα 0.7) στα 5 κύρια scroll panels (Home/Health/Optimization/Network/System). **ΕΠΑΝΕΛΕΓΧΘΗΚΕ v2.6.0**: το crop math (`$srcX/$srcY/$srcW/$srcH`) δοκιμάστηκε ζωντανά (πραγματικό PowerShell + System.Drawing, ΟΧΙ θεωρητικά) σε 4 σενάρια - κανονική λειτουργία, Health tab χωρίς PC Manager rail, ΚΑΙ ένα ΤΕΧΝΗΤΟ edge-case όπου το bitmap του tab είναι μικρότερο από το panel (πιθανό race condition αν το Paint τρέξει πριν ολοκληρωθεί ένα resize) - σε ΟΛΑ τα σενάρια παράγεται έγκυρο (θετικών διαστάσεων) crop rectangle, ΚΑΙ το ήδη υπάρχον `if ($srcW -le 0 -or $srcH -le 0)` fallback καλύπτει την περίπτωση μηδενικού crop. Το `Graphics.DrawImage` με ασύμμετρα src/dest μεγέθη δεν πετάει exception, απλά κάνει stretch - ασφαλές ακόμα και στο degraded edge-case. ΣΗΜΕΙΩΣΗ ΕΙΛΙΚΡΙΝΕΙΑΣ: παραμένει ανεπιβεβαίωτο το ΤΕΛΙΚΟ οπτικό αποτέλεσμα (πραγματικό rendering) σε πραγματικά Windows - μόνο η ΛΟΓΙΚΗ/μαθηματικά έχουν πλέον επαληθευτεί ζωντανά, όχι το pixel-level αποτέλεσμα. ~~**ΑΝΟΙΧΤΟ ΘΕΜΑ (v2.5.0)**: "κόβεται από δεξιά" στο θέμα PC Manager~~ — **ΔΙΟΡΘΩΘΗΚΕ v2.8.0, με πραγματικό ζωντανό στιγμιότυπο ως απόδειξη** (v2.7.0, Αρχική, θέμα Microsoft PC Manager - ο χρήστης έστειλε screenshot με κόκκινο βέλος δείχνοντας μεγάλο κενό σκούρου φόντου ανάμεσα στις κάρτες και το δεξί άκρο του παραθύρου). Η ΠΡΑΓΜΑΤΙΚΗ ρίζα (διαφορετική από την υπόθεση DPI scaling του v2.5.0 σημειώματος): κάθε `scrollPanel*` διατηρούσε το ΙΔΙΟ σταθερό πλάτος (1070/1030px) ΑΝΕΞΑΡΤΗΤΑ από το θέμα - μόνο η θέση X του κεντραρίσματος άλλαζε δυναμικά με βάση το `$global:pcManagerRailOffset`. Στο θέμα PC Manager, το διαθέσιμο πλάτος περιεχομένου είναι στενότερο (μείον τα 100px της κάθετης μπάρας) αλλά το panel ΔΕΝ μίκραινε/ΔΕΝ μεγάλωνε ανάλογα - αφήνοντας ορατά μεγαλύτερο κενό δεξιά. Διορθώθηκε στο `Update-SidebarDockLayout` (~γραμμή 7311+): ΜΟΝΟ όταν `$isPCManagerSkin`, κάθε panel υπολογίζει `Size`/`Location` δυναμικά ώστε να γεμίζει `$pcMgrContentW - 2*20px` (20px συμμετρικό περιθώριο σε κάθε πλευρά, ίδιο μέγεθος με το X=20 που ήδη χρησιμοποιούν οι περισσότερες κάρτες) - στο κλασικό skin η ΠΑΛΙΑ, αμετάβλητη λογική (σταθερό πλάτος, κεντραρισμένο) παραμένει 100% ίδια. Επιβεβαιώθηκε ότι το `AutoScrollMinSize.Width` όλων των 8 panels παραμένει μικρότερο από το ΝΕΟ, φαρδύτερο πλάτος σε ΚΑΘΕ περίπτωση (καμία νέα οριζόντια μπάρα κύλισης). ΔΕΝ επιβεβαιώθηκε ΑΚΟΜΑ με νέο στιγμιότυπο μετά τη διόρθωση.
3. **SDI (Snappy Driver Installer) πλήρης απόκρυψη + πλήρης επανασχεδιασμός σε multi-source Driver Update Engine** — ρητά εγκεκριμένο από τον χρήστη να γίνει στην v2.2.0, μαζί με το πρώτο "PC Manager" skin (βλ. #4 παρακάτω). **Πλήρες σχέδιο αρχιτεκτονικής ήδη καταγεγραμμένο στην ενότητα 0 παραπάνω** (provider abstraction, SDIO+Windows Update+Intel/AMD/NVIDIA/OEM ως ισότιμοι providers, δικό μας trust/stability score, headless SDIO) — ακολούθησε το ΑΚΡΙΒΩΣ όταν συνεχιστεί η υλοποίηση, μην ξανασχεδιάσεις από την αρχή.
   **ΠΡΟΟΔΟΣ v2.2.0 (πρώτο βήμα)**: ανακαλύφθηκε ότι υπήρχε ΗΔΗ πλήρες, δοκιμασμένο Windows
   Update-based pipeline (`Start-DriverScan`/`Complete-DriverScan`/`Install-DriverUpdate`, καρτέλα
   Βελτιστοποίηση) που είχε αποσυνδεθεί από το κύριο κουμπί σε προγενέστερο πέρασμα υπέρ του SDI
   (`Start-SdiDriverCheck`, ανοίγει το ΔΙΚΟ ΤΟΥ ξεχωριστό παράθυρο - αντίθετο με την τεκμηριωμένη αρχή
   "ποτέ 3rd-party UI"). Το κύριο κυκλικό κουμπί "ΣΑΡΩΣΗ" ενεργοποιεί πλέον `Start-DriverScan` (Windows
   Update, αποτελέσματα στη ΔΙΚΗ ΜΑΣ λίστα, με ετικέτα πηγής "✓ Πηγή: Windows Update (επίσημη)" σε κάθε
   γραμμή - το πρώτο ρεαλιστικό `Provider` του τεκμηριωμένου σχεδίου).
   **ΔΙΟΡΘΩΣΗ v2.2.0 (ΥΠΕΡΣΕΙΕΙ το προηγούμενο σημείωμα)**: αρχικά προστέθηκε ΚΑΙ ένα δεύτερο, τίμια
   επισημασμένο κουμπί "Περισσότεροι Οδηγοί (SDI)" δίπλα στο κυκλικό - αυτό ΑΦΑΙΡΕΘΗΚΕ ΠΛΗΡΩΣ μετά από
   ρητή, επαναλαμβανόμενη απόρριψη του χρήστη ("ΓΙΑ ΤΕΛΕΥΤΑΙΑ ΦΟΡΑ ΔΕΝ ΘΕΛΩ ΝΑ ΕΜΦΑΝΙΖΕΤΑΙ ΤΟ ΠΑΡΑΘΥΡΟ
   SNAPPY" + ρητό "ΔΕΝ ΘΕΛΩ ΕΠΙΠΛΕΟΝ ΚΟΥΜΠΙΑ"). `Start-SdiDriverCheck`/SDIO ΔΕΝ καλείται πλέον από ΚΑΝΕΝΑ
   σημείο του κώδικα - **ΜΗΝ το ξαναπροτείνεις ως compromise, ούτε ως "τίμια επισημασμένο" δεύτερο κουμπί,
   ακόμα κι αν φαίνεται λογική μεσαία λύση.** Η εφαρμογή χρησιμοποιεί πλέον αποκλειστικά Windows Update ως
   ενιαία πηγή driver δεδομένων· το αίτημα του χρήστη "όλες οι βάσεις δεδομένων μία" ερμηνεύεται προς το
   παρόν ως ικανοποιημένο με αυτόν τον τρόπο (μία, ενιαία, ενσωματωμένη πηγή) παρά ως πολυ-πηγαία σύνθεση.
   **ΠΡΟΟΔΟΣ v2.2.0 (πραγματική πολυ-πηγαία σύνθεση)**: κατόπιν ρητής επιβεβαίωσης του χρήστη (ζητήθηκε
   "και τα δύο μαζί" GPU vendors + OEM, με AMD GPU ως test hardware), προστέθηκαν 3 ΝΕΕΣ αυτοματοποιημένες
   πηγές - **AMD (amd.com, ζωντανά επιβεβαιωμένο σε πραγματικό hardware)**, **NVIDIA
   (nvidia.com/gfwsl.geforce.com, ΕΠΙΣΗΣ ζωντανά επιβεβαιωμένο, ίδιο επίσημο μηχανισμό με το
   TinyNvidiaUpdateChecker)** και **Dell (Dell Command Update CLI, μόνο μέσω τεκμηρίωσης - ΔΕΝ
   δοκιμάστηκε ζωντανά, χρειάζεται επιβεβαίωση από τον χρήστη σε πραγματικό Dell PC)**. Πλήρεις τεχνικές
   λεπτομέρειες, URL patterns, XML/JSON report formats, και η μεθοδολογία ζωντανής επιβεβαίωσης μέσω
   Browser tool καταγράφονται στην ενότητα 2 ("Driver engine: 3 νέες πηγές προστέθηκαν"). Τώρα υπάρχουν 4
   αυτοματοποιημένες πηγές συνολικά (Windows Update + AMD + NVIDIA + Dell), άρα το trust/stability
   scoring σύστημα (βλ. παρακάτω) έχει πλέον νόημα να χτιστεί αν ζητηθεί.
   **ΕΝΗΜΕΡΩΣΗ (v2.3.0-v2.4.0)**: το trust/stability scoring σύστημα υλοποιήθηκε ΤΕΛΙΚΑ στην v2.3.0
   (`Test-DriverSourceOverlap`, βλ. ενότητα 0.6) - όταν το ΙΔΙΟ GPU επιβεβαιώνεται ανεξάρτητα ΚΑΙ από
   Windows Update ΚΑΙ από AMD/NVIDIA, η ενημέρωση επισημαίνεται "Επιβεβαιωμένο" αντί για απλή "πιθανή".
   Η headless ενσωμάτωση SDI ΕΠΙΣΗΣ υλοποιήθηκε τελικά (v2.2.5, μετά από νέο ρητό αίτημα χρήστη -
   "ΓΡΑΨΕ ΚΩΔΙΚΑ ΓΙΑ ΤΗΝ ΕΝΣΩΜΑΤΩΣΗ ΤΟΥ SNAPPY ORIGIN ΧΩΡΙΣ ΠΑΡΑΘΥΡΙΚΟ ΠΕΡΙΒΑΛΛΟΝ" - διαφορετικό από την
   ΠΑΛΙΟΤΕΡΗ, ρητά απορριφθείσα ιδέα ενός δεύτερου ΟΡΑΤΟΥ κουμπιού/παραθύρου SDI), με το "Sum: 0" bug
   (καμία λήψη index) διορθωμένο στην v2.4.0 μέσω του `-autoupdate` flag.
   **Τι ΠΡΑΓΜΑΤΙΚΑ δεν έχει γίνει ακόμα**: Intel GPU provider - **διερευνήθηκε ζωντανά (v2.5.0)** μέσω
   Browser tool (intel.com/download-center) και WebSearch· βρέθηκε ότι το intel.com download center
   χρησιμοποιεί ένα βαρύ, client-side rendered Coveo enterprise-search widget (`ws=idsa-suggested`,
   `entepriseSearch` hub) ΧΩΡΙΣ κανένα καθαρό, ανά-μοντέλο endpoint σύγκρισης έκδοσης (ΑΝΤΙΘΕΤΑ με το
   απλό per-model HTML page του AMD ή το τεκμηριωμένο AJAX endpoint της NVIDIA) - ο επίσημος μηχανισμός
   ενημέρωσης της Intel είναι το ξεχωριστό, proprietary "Intel Driver & Support Assistant" desktop client,
   χωρίς δημόσιο API. ΔΕΝ υλοποιήθηκε provider (θα παραβίαζε τον κανόνα "ίδια αυστηρότητα επιβεβαίωσης
   με AMD/NVIDIA πριν υλοποιηθεί, όχι μάντεμα ενός endpoint").
   **Lenovo - διερευνήθηκε ζωντανά (v2.5.0)** μέσω WebFetch στο επίσημο `docs.lenovocdrt.com` (Lenovo CDRT
   docs site). Ευρήματα: το **System Update** (`tvsu.exe`, winget id επιβεβαιωμένο: `Lenovo.SystemUpdate`
   - βλ. winstall.app/apps/Lenovo.SystemUpdate) **ΔΕΝ υποστηρίζει ενέργεια SCAN καθόλου** (μόνο DOWNLOAD/
   LIST/INSTALL) - το LIST ανοίγει ΔΙΚΟ ΤΟΥ παράθυρο επιλογής, αντίθετο με τον ήδη καθιερωμένο κανόνα
   "ΠΟΤΕ 3rd-party UI" αυτού του project (ο ίδιος λόγος που το SDI χρειάστηκε headless επανασχεδιασμό).
   Η ΜΟΝΗ ενέργεια SCAN βρίσκεται σε ΔΙΑΦΟΡΕΤΙΚΟ, ξεχωριστό εργαλείο - το **Thin Installer**
   (`ThinInstaller.exe /CM -search R -action SCAN -packagetypes 2,3 -repository <path>`, καθαρά exit
   codes: 10000=καμία ενημέρωση, 10001=βρέθηκαν - πολύ πιο απλό από το XML parsing του Dell) - ΑΛΛΑ αυτό
   είναι ένα φορητό (portable, "δεν χρειάζεται εγκατάσταση") εργαλείο enterprise IT deployment, ΟΧΙ κάτι
   προεγκατεστημένο σε τυπικό καταναλωτικό Lenovo PC (αντίθετα με το Dell Command Update, που ΕΙΝΑΙ συχνά
   ήδη εκεί ή εύκολα winget-εγκαταστάσιμο ως το ΙΔΙΟ εργαλείο που κάνει scan) - ΚΑΙ το `-repository`
   όρισμα χρειάζεται ρητό path (τοπικό/UNC/URL) που ΔΕΝ επιβεβαιώθηκε αν έχει ασφαλές, επίσημο δημόσιο
   default. Εναλλακτικά βρέθηκε το **LSUClient** PowerShell module (community/3rd-party, PowerShell
   Gallery, `Install-Module LSUClient` + `Get-LSUpdate`) - καθαρή, δημοφιλής λύση αλλά εισάγει ΝΕΑ
   εξωτερική εξάρτηση (εγκατάσταση 3rd-party module) που ΔΕΝ ταιριάζει στη μέχρι τώρα φιλοσοφία "μόνο ήδη-
   εγκατεστημένα, επίσημα εργαλεία κατασκευαστή, ΠΟΤΕ σιωπηλή προσθήκη νέας εξάρτησης χωρίς ρητή έγκριση".
   **ΣΥΜΠΕΡΑΣΜΑ**: ΔΕΝ υπάρχει (ακόμα) καθαρός, ασφαλής, headless μηχανισμός SCAN για Lenovo με το ΙΔΙΟ
   επίπεδο εμπιστοσύνης του Dell - ΔΕΝ υλοποιήθηκε provider. Αν ζητηθεί ξανά ρητά: η πιο ρεαλιστική
   επιλογή θα ήταν "εντοπίστηκε Lenovo PC - πρόταση εγκατάστασης Lenovo Vantage" (ίδιο UX μοτίβο με το
   Dell "not installed" branch, χωρίς κουμπί σάρωσης αφού καμία ασφαλής σιωπηλή μέθοδο δεν βρέθηκε).
   **HP** - ΔΕΝ διερευνήθηκε ακόμα καθόλου (χρειάζεται την ίδια έρευνα με WebFetch/Browser σε επίσημη
   τεκμηρίωση HP Image Assistant/HP Support Assistant CLI πριν οποιαδήποτε υλοποίηση).
4. **Πλήρες "PC Manager" skin** — **ΠΡΩΤΟ βήμα ΥΛΟΠΟΙΗΘΗΚΕ v2.2.0**: νέο θέμα χρωμάτων "Microsoft PC Manager"
   (dark+light) προστέθηκε στο `Get-ThemeColors` και στο `$themeNamesList` (ίδιο μηχανισμό με τα άλλα
   ~20 έτοιμα θέματα - καμία νέα υποδομή, μηδενικό ρίσκο layout). Ο χρήστης έστειλε 9 πραγματικά
   στιγμιότυπα του Microsoft PC Manager (dark mode, tabs: Home/Protection/Storage/Apps/AI Tools/
   Settings/About) ζητώντας ρητά ένα skin που να του μοιάζει, μετά από dismissed ερώτηση (δεν απάντησε
   τι εννοεί "skin" - έστειλε τα screenshots αντ' αυτού, ΚΑΙ ζήτησε "βρες κι άλλες φωτό από το διαδίκτυο").
   **Μεθοδολογία χρωμάτων (σημαντικό για μελλοντικές διορθώσεις)**:
   - LIGHT mode: βρέθηκε πραγματικό, επίσημο screenshot στο Wikimedia Commons
     (`Microsoft_PC_Manager_homepage_UI.png`) - τα χρώματα ΔΕΝ μαντεύτηκαν, δειγματίστηκαν με ΑΚΡΙΒΕΙΑ
     pixel μέσω canvas στο Browser tool (`ctx.getImageData`): Accent = ΑΚΡΙΒΩΣ rgb(0,95,184) (κουμπί
     "Boost"), MainBg = rgb(253,254,255), TabBg (πλευρική μπάρα) = rgb(230,243,253) (ελαφρύ γαλάζιο
     tint, ΟΧΙ καθαρό λευκό - εύκολο να το ξεχάσεις).
   - DARK mode: ΔΕΝ βρέθηκε αντίστοιχο ζωντανά-δειγματίσιμο (URL-fetchable) screenshot ίδιας ποιότητας -
     τα 9 screenshots του χρήστη ήρθαν ως pasted εικόνες στη συνομιλία, ΟΧΙ ως fetchable URLs, άρα ΔΕΝ
     ήταν δυνατό το ίδιο ακριβές canvas pixel sampling. Τα dark χρώματα βασίζονται σε προσεκτική οπτική
     επιθεώρηση + τη σύμβαση Fluent "brighten accent for dark backgrounds" (Accent dark =
     rgb(41,151,255), πιο ανοιχτό/ζωηρό από το light rgb(0,95,184), ίδια απόχρωση). **Αν ο χρήστης
     αναφέρει ότι το dark mode δεν ταιριάζει οπτικά**, διόρθωσε ΜΟΝΟ τα RGB values στο `Get-ThemeColors`
     case "Microsoft PC Manager" (dark branch, γραμμή ~4735) - μην ξανασχεδιάσεις τη λογική.
   - `CardRadius = 10` (στρογγυλεμένες κάρτες, ήδη υπάρχουσα υποδομή - `New-RoundedRectPath`,
     καταναλώνεται στη γραμμή ~9520), `BgStyle = "StandardDark"` και ΚΑΝΕΝΑ `ButtonStyle` (= λεπτό 1px
     περίγραμμα σε CardBorder χρώμα, το πιο "flat" διαθέσιμο ύφος - ταιριάζει με το πραγματικό PC
     Manager, το οποίο δεν έχει glossy/bevel/glow κουμπιά).
   - Δοκιμάστηκε standalone (εξαγωγή του `Get-ThemeColors` σε αυτοτελές .ps1, `Add-Type
     -AssemblyName System.Drawing`, κλήση με `$global:currentThemeName = "Microsoft PC Manager"` και
     ΚΑΙ τα δύο `$global:isDarkMode` = true/false) - επιστρέφει σωστά και τα δύο sets χρωμάτων, καμία
     typo/σφάλμα.
   **ΕΝΗΜΕΡΩΣΗ - Β΄ βήμα, ΠΛΗΡΗΣ ΑΛΛΑΓΗ ΔΙΑΤΑΞΗΣ, ΥΛΟΠΟΙΗΘΗΚΕ v2.2.0**: ο χρήστης ρητά ζήτησε "ΘΕΛΩ ΠΛΗΡΗ
   ΑΛΛΑΓΗ ΔΙΑΤΑΞΗΣ ΓΙΑ ΤΟ SKIN" μετά το πρώτο, χρωμάτων-μόνο βήμα. Προστέθηκε ΝΕΑ, πάντα-χτισμένη αλλά
   δυναμικά ορατή/κρυφή κάθετη μπάρα εικονιδίων (`$global:pcManagerNavRail`, 76px πλάτος, `Add-CustomTabPage
   :~7104+`) για τις 8 ΚΥΡΙΕΣ καρτέλες (Home/Optimization/Health/Network/Tweaks/Bloatware/AdvancedTools/
   System) - αντικαθιστά ΟΠΤΙΚΑ την οριζόντια λωρίδα pills (`$tabStripPanel`) ΜΟΝΟ όταν το θέμα
   "Microsoft PC Manager" είναι ενεργό.
   **ΚΡΙΣΙΜΗ ΔΙΑΚΡΙΣΗ (μην τη μπερδέψεις)**: αυτό είναι ΕΝΤΕΛΩΣ ΔΙΑΦΟΡΕΤΙΚΟ component από το ΑΛΛΟ,
   προϋπάρχον "sidebar" (`$global:sidebarForm`/`$global:sidebarEmbedPanel`, το ☰ μενού με
   Βοήθεια/Ιστορικό/Log/ViVeTool/Ρυθμίσεις, `Add-SidebarItem`) - ΕΚΕΙΝΟ ΔΕΝ πειράχτηκε καθόλου δομικά.
   Το ΝΕΟ rail είναι για τις 8 ΚΥΡΙΕΣ καρτέλες της εφαρμογής, ΟΧΙ για το δευτερεύον ☰ μενού.
   **Αρχιτεκτονική/functions**:
   - `Add-PCManagerRailItem` (`~7125`) - ίδιο βασικό πρότυπο με το `Add-SidebarItem` (icon πάνω/label
     κάτω, hover state) αλλά στενότερο (70x60 έναντι 110x100) και με ΝΕΟ selected-state: ημιδιάφανο
     accent-χρωματισμένο rounded pill (alpha 70/255) πίσω από το εικονίδιο - ΣΚΟΠΙΜΑ ΟΧΙ συμπαγές accent
     φόντο (θα χρειαζόταν εγγυημένα λευκό εικονίδιο πάνω του, ρίσκο κακής αντίθεσης σε light mode).
   - `Update-PCManagerRailVisualState` (`~7182`) - ενημερώνει χρώμα εικονιδίου/ετικέτας ανά αντικείμενο
     ανάλογα με το αν είναι το επιλεγμένο tab (Accent/Text αν επιλεγμένο, SubText αλλιώς) - ΣΚΟΠΙΜΑ ΔΕΝ
     καταχωρείται στα κοινόχρηστα `allIconBoxes`/`allTextLabels` arrays (εκείνα βάφουν ΕΝΙΑΙΟ χρώμα σε
     όλη την εφαρμογή, ασύμβατο με το per-item selected/unselected χρώμα που χρειάζεται εδώ).
   - `Select-CustomTab` (`~6871`) επεκτάθηκε με `if ($global:pcManagerRailItems) { Update-
     PCManagerRailVisualState }` στο τέλος - το null-check είναι ΑΠΑΡΑΙΤΗΤΟ (ΟΧΙ προαιρετικό defensive
     coding) αφού η ΠΡΩΤΗ κλήση `Select-CustomTab -Index 0` γίνεται ΠΡΙΝ οριστεί το rail (η σειρά
     δημιουργίας στο αρχείο είναι: tabs+pills πρώτα, μετά rail) - χωρίς το guard θα έσκαγε "command not
     found" στην εκκίνηση.
   - `Update-SidebarDockLayout` (`~7000`) επεκτάθηκε: υπολογίζει `$isPCManagerSkin`/`$global:
     pcManagerRailOffset` (0 ή 76), δείχνει/κρύβει rail vs `$tabStripPanel`, προσθέτει το rail offset
     ΣΤΟ ΙΔΙΟ σημείο που ήδη πρόσθετε το `$global:sidebarDockWidth` του ΑΛΛΟΥ sidebar (content
     Location/Size ΚΑΙ οι 8 γραμμές κεντραρίσματος `scrollPanel*`) - ίδιο μοτίβο, απλή πρόσθεση, ελάχιστο
     νέο ρίσκο. Καλείται ΤΩΡΑ ΚΑΙ από το `Update-UITheme` (`~9484`, μετά το `$script:theme = $currTheme`)
     ώστε η εναλλαγή θέματος ΖΩΝΤΑΝΑ (χωρίς restart) να ενημερώνει αμέσως την ορατότητα/διάταξη.
   - **Σύγκρουση με το ΑΛΛΟ sidebar όταν MenuMode="Sidebar" ΚΑΙ θέμα PC Manager ταυτόχρονα**: και τα δύο
     θα ήθελαν να "κατοικήσουν" στην αριστερή πλευρά - λύθηκε με αμοιβαίο αποκλεισμό (`Test-
     SidebarShouldDock`, `~7268`, ΚΑΙ το `$global:sidebarDockActive` στο `Update-SidebarDockLayout`
     ΠΡΕΠΕΙ να συμφωνούν ΠΑΝΤΑ - και τα δύο τώρα αποκλείουν το MenuMode-based docking όταν θέμα=PC
     Manager, ΔΙΑΤΗΡΩΝΤΑΣ το Maximized-based docking ανεπηρέαστο, άσχετος λόγος/μηχανισμός). Το ☰ μενού
     πέφτει πίσω στο ήδη αποδεδειγμένο floating (μη-anchored) μονοπάτι σε αυτή τη συνδυασμένη περίπτωση.
   **Icon types ανά καρτέλα (ΠΡΕΠΕΙ να ταιριάζουν με τη σειρά του `$global:customTabs`, Home=0...System=7
   - βλ. `~7043-7053`)**: Home=Monitor, Optimization=Optimization, Health=Health, Network=Network,
   Tweaks=Tweak, Bloatware=Apps, AdvancedTools=AdvancedTools, System=Chip.
   **ΕΠΑΝΕΛΕΓΧΘΗΚΕ με ακρίβεια (v2.6.0)**, πέρα από το αρχικό μολύβι/χαρτί: κάθε αντικείμενο rail είναι
   90x68px με βήμα 74px (8 αντικείμενα × 74 = 592, τελευταίο στο Y=522 => κάτω άκρο 590) - το ύψος του
   ίδιου του rail είναι πλέον `610 + pcManagerContentYReclaim` (βλ. ενότητα 0.8, v2.5.0 Y-reclaim) δηλ.
   666px στο ενεργό PC Manager skin - άφθονο περιθώριο (76px) μετά το τελευταίο αντικείμενο, ΠΕΡΙΣΣΟΤΕΡΟ
   απ' όσο πριν την v2.5.0. Η ετικέτα κειμένου (label, 90x34, Y=33 μέσα στο 68px αντικείμενο) ελέγχθηκε με
   πραγματικό `Graphics.MeasureString` (ΟΧΙ εκτίμηση) στο ΑΚΡΙΒΕΣ πλάτος (90px) και γραμματοσειρά
   (Segoe UI 7.5pt) για ΟΛΕΣ τις 32 μεταφράσεις ετικετών (8 καρτέλες × 4 γλώσσες, EL/EN/DE/FR) - η
   ΧΕΙΡΟΤΕΡΗ περίπτωση (π.χ. "Υγεία & Συντήρηση", "Systemzustand & Wartung", "Réglages Supplémentaires")
   τυλίγεται καθαρά σε 2 γραμμές με πραγματικό ύψος 27,9px - ΕΝΤΟΣ του διαθέσιμου 34px, ΚΑΜΙΑ γλώσσα/
   καρτέλα χρειάζεται 3η γραμμή. Το εικονίδιο (Y=8, ύψος 20, κάτω άκρο 28) έχει καθαρό κενό 5px πριν την
   ετικέτα (Y=33) - καμία επικάλυψη. **Παραμένει ανεπιβεβαίωτο ΜΟΝΟ το ΤΕΛΙΚΟ οπτικό αποτέλεσμα σε
   πραγματικά Windows** (π.χ. πραγματικό font rendering/ClearType, hover/selected χρώματα) - η ΓΕΩΜΕΤΡΙΑ
   πλέον είναι μαθηματικά αποκλεισμένη από το να είναι η αιτία τυχόν μελλοντικού προβλήματος κειμένου/
   επικάλυψης σε αυτό το rail. Αν αναφερθεί πρόβλημα ξανά, είναι πιθανότερο να είναι χρώματα/κοντράστ,
   όχι γεωμετρία.
5. ~~Πλήρης μεγιστοποίηση/fullscreen~~ — **ΥΛΟΠΟΙΗΘΗΚΕ v2.5.0** μέσω "letterbox" τεχνικής (ΟΧΙ πλήρες responsive reflow - βλ. αναλυτικά ενότητα 0.8). Πραγματικό resize/maximize/Snap Layouts λειτουργούν, το περιεχόμενο κεντράρεται στο διαθέσιμο χώρο στο σταθερό του μέγεθος. ΔΕΝ επιβεβαιώθηκε ζωντανά.

### Β. Λοιπά χαρακτηριστικά WMT που εντοπίστηκαν αλλά ΔΕΝ σχεδιάστηκαν ακόμα (χαμηλότερη προτεραιότητα)
- ~~Windows Optional Features UI ως πλήρης, αναζητήσιμη λίστα~~ — **ΥΛΟΠΟΙΗΘΗΚΕ v2.1.1** (νέα κάρτα "Όλες οι Πρόσθετες Λειτουργίες Windows", καρτέλα Προηγμένα Εργαλεία — τα 6 hardcoded quick-toggle παραμένουν επιπλέον, δεν αντικαταστάθηκαν).
- ~~Drive Benchmark~~ — **ΥΛΟΠΟΙΗΘΗΚΕ v2.1.1** (νέα κάρτα "Δοκιμή Ταχύτητας Δίσκου", καρτέλα Σύστημα — αυτοτελής, framework-agnostic υλοποίηση με προσωρινό αρχείο 256MB, ΟΧΙ αντιγραφή του βαρύ WPF UI του WMT).
- **ΡΗΤΑ ΕΚΤΟΣ SCOPE**: MAS activation button (νομικά/ηθικά προβληματικό, ήδη αφαιρέθηκε μια φορά ιστορικά), Winapp2/BleachBit aggressive cleaner rules (αντίθετο με τη συντηρητική φιλοσοφία της εφαρμογής), Provider Manager Steam/GOG/Epic (άσχετο θέμα).

### SDI headless: ΕΠΙΒΕΒΑΙΩΘΗΚΕ ζωντανά ότι χρειάζεται τοπική βάση driverpacks για να είναι χρήσιμο
Ο χρήστης έστειλε το ΠΡΑΓΜΑΤΙΚΟ `log.txt` που παράγει το SDI Origin (`-nogui -autoclose -output_dir -log_dir`)
από ζωντανό τρέξιμο. Ευρήματα (v2.2.5):
- Το log.txt είναι ένα εκτενές (300KB+), ΤΕΧΝΙΚΟ log - ΟΧΙ καθαρή λίστα "αυτοί οι οδηγοί χρειάζονται
  ενημέρωση". Ο ίδιος ο χρήστης το επιβεβαίωσε ως "μη χρηστική για τον χρήστη" αναφορά.
- Περιέχει μια ενότητα `Driverpacks\n  N  drivers\unpacked.7z\n  Sum: N` - αν `Sum: 0`, το SDI δεν έχει
  ΚΑΘΟΛΟΥ τοπική βάση δεδομένων οδηγών να συγκρίνει (η βάση κατεβαίνει ΞΕΧΩΡΙΣΤΑ, online, πιθανόν πολλά
  GB - το SDI Origin δεν την κατεβάζει αυτόματα με ένα απλό `-nogui` τρέξιμο). Χωρίς αυτήν, ΔΕΝ μπορεί να
  παραχθεί κανένα ουσιαστικό αποτέλεσμα.
- **ΚΡΙΣΙΜΟ, επιβεβαιωμένο εύρημα**: το αρχείο είναι γραμμένο στην **codepage προεπιλογής συστήματος**
  (π.χ. Windows-1253 για Ελληνικά), ΟΧΙ UTF-8 - διάβασμα με `Get-Content`/`[System.IO.File]::ReadAllText`
  χωρίς ρητό `[System.Text.Encoding]::Default` δίνει αλλοιωμένο κείμενο (π.χ. "�������" αντί για
  "Τυπικός ελεγκτής"). Επιβεβαιώθηκε ζωντανά η σωστή αποκωδικοποίηση.
- Ο κώδικας (`Start-DriverScan`, ενότητα SDI) τώρα διαβάζει το log.txt με τη σωστή codepage, εξάγει το
  `Sum: N` μέσω regex (`Driverpacks[\s\S]{0,200}?Sum:\s*(\d+)`), και εμφανίζει ΕΙΛΙΚΡΙΝΕΣ μήνυμα
  ("δεν υπάρχει τοπική βάση οδηγών") αντί για το παραπλανητικό "βρέθηκε αναφορά" όταν Sum=0 - ΧΩΡΙΣ
  κουμπί "Άνοιγμα Φακέλου" σε αυτή την περίπτωση (το log.txt δεν έχει πρακτική αξία να το δει ο χρήστης).
- **Τι ΔΕΝ έχει λυθεί ακόμα**: πώς να αποκτηθεί μια χρήσιμη, πραγματική λίστα ενημερώσεων από το SDI
  όταν ΥΠΑΡΧΕΙ τοπική βάση (Sum>0) - δεν έχει δοκιμαστεί ζωντανά ΑΚΟΜΑ αυτή η περίπτωση. Το χρονικό όριο
  αυξήθηκε ήδη σε 10 λεπτά (v2.7.0) ώστε το `-autoupdate` (ήδη ενεργό από το v2.4.0) να προλάβει να
  κατεβάσει ό,τι χρειάζεται - επόμενο βήμα: ο χρήστης να τρέξει σάρωση και να στείλει το ΠΡΑΓΜΑΤΙΚΟ log
  αν προκύψει Sum>0, ώστε να χτιστεί αξιόπιστο parsing πάνω σε ΠΡΑΓΜΑΤΙΚΑ δεδομένα (όχι εικασία).
  **ΑΠΟΡΡΙΦΘΗΚΕ ρητά (v2.8.0)**: πρόταση για custom `CheckedListBox` UI που να διαβάζει/παρουσιάζει
  μεμονωμένους οδηγούς από το SDI log και να επιτρέπει επιλεκτική εγκατάσταση - το παράδειγμα κώδικα που
  δόθηκε χρησιμοποιούσε ΑΝΕΠΙΒΕΒΑΙΩΤΟ flag (`-showall`, δεν υπάρχει στην επίσημη τεκμηρίωση sdi-tool.org)
  και ΕΙΚΑΣΤΙΚΗ λογική ανάλυσης (`$line -match "Driver|Update|Missing"`, placeholder `HardwareID =
  "PCI\VEN_..."`) - το ΠΡΑΓΜΑΤΙΚΟ log.txt από ζωντανό τρέξιμο χρήστη (v2.2.5) έχει ήδη επιβεβαιωθεί ΩΣ
  τεχνικό log ΕΚΤΕΛΕΣΗΣ, όχι δομημένη λίστα οδηγών - χτίσιμο UI πάνω σε εικαστική ανάλυση ρισκάρει να
  δείξει λάθος όνομα/ID οδηγού σε απόφαση που αλλάζει το σύστημα του χρήστη, μη αποδεκτό ρίσκο χωρίς
  πραγματικά δεδομένα Sum>0 για επαλήθευση πρώτα.

### PC Manager skin (v2.2.0-v2.2.5): γνωστά, ΔΙΟΡΘΩΜΕΝΑ bugs από πρώτο γύρο ζωντανών στιγμιότυπων
Μετά την πρώτη ζωντανή δοκιμή (στιγμιότυπα χρήστη), βρέθηκαν και διορθώθηκαν:
- Η κάθετη μπάρα εικονιδίων άφηνε ορατή ΚΑΙ την οριζόντια λωρίδα καρτελών/κουμπιά κύλισης (‹ ›) μετά από
  αλλαγή μεγέθους παραθύρου - το `Update-SidebarDockLayout` (που ελέγχει την ορατότητα) καλούνταν ΜΟΝΟ
  στην εκκίνηση/αλλαγή θέματος, ΟΧΙ σε κάθε `$mainForm.Add_Resize` - διορθώθηκε.
- Τα κουμπιά κύλισης καρτελών (`$btnTabScrollLeft`/`$btnTabScrollRight`) είναι ΞΕΧΩΡΙΣΤΑ controls (παιδιά
  του `$mainForm`, ΟΧΙ του `$tabStripPanel`) - το `.Visible=false` του tabStripPanel δεν τα κάλυπτε ΚΑΘΟΛΟΥ.
- Οι ετικέτες της μπάρας έσπαγαν ΜΕΣΑ σε λέξη (καμία απόσταση) για μακριές ελληνικές λέξεις (π.χ.
  "Βελτιστοποίηση") - το πλάτος (76px, βασισμένο στα ΣΥΝΤΟΜΑ αγγλικά του πραγματικού PC Manager) ήταν
  ανεπαρκές. Αυξήθηκε σε 100px + αφιερωμένη μικρότερη γραμματοσειρά (`$fontRailLabel`, 7.5pt Regular).
- **ΔΕΝ επιβεβαιώθηκε ακόμα ζωντανά μετά τη διόρθωση** (χρειάζεται νέο στιγμιότυπο) - αν τα ονόματα ΑΚΟΜΑ
  δεν χωράνε καθαρά, το επόμενο βήμα είναι είτε ΠΕΡΑΙΤΕΡΩ μείωση γραμματοσειράς είτε συντομευμένα
  ονόματα ΕΙΔΙΚΑ για τη μπάρα (π.χ. "Βελτιστ." αντί για πλήρες "Βελτιστοποίηση") - ΜΗΝ ξαναδοκιμάσεις
  απλή αύξηση πλάτους ξανά χωρίς νέο στιγμιότυπο, το όριο πλάτους έχει ήδη κόστος στο διαθέσιμο
  περιεχόμενο (μεγαλύτερο `pcManagerRailOffset` = στενότερο content area στις υπόλοιπες καρτέλες).
- ~~**ΓΝΩΣΤΟ, ΜΗ διορθωμένο ακόμα**: η κάρτα Δίσκου (3η) στην καρτέλα Αρχική εμφανίστηκε κομμένη από κάθετη
  μπάρα κύλισης~~ — **ΠΙΘΑΝΟΤΑΤΑ ΔΙΟΡΘΩΘΗΚΕ ΕΜΜΕΣΑ v2.4.0**: η ρίζα εντοπίστηκε τελικά (v2.4.0, "διόρθωση
  περιθωρίων Αρχικής" - βλ. ενότητα 0.7) να ΜΗΝ είναι κάποια κάθετη μπάρα κύλισης, αλλά ότι το
  `$global:scrollPanelHome.Size.Width` ήταν 1030px - ΑΚΡΙΒΩΣ ίσο με το πλάτος των ίδιων των καρτών του
  (`$cardHealthScore`/`$cardDiskAnalysis` στο X=20, πλάτος 1030 => δεξί άκρο X=1050, 20px ΠΕΡΑ από το ίδιο
  το panel) - ενώ ΟΛΕΣ οι υπόλοιπες καρτέλες ήδη χρησιμοποιούν panel πλάτους 1070px για το ΙΔΙΟ πλάτος
  καρτών 1030px. Διορθώθηκε σε 1070px, ταιριάζοντας με το ήδη αποδεδειγμένο μοτίβο - οι κάρτες τώρα
  χωράνε με συμμετρικό 20px περιθώριο και στις δύο πλευρές, ΧΩΡΙΣ ανάγκη για οποιαδήποτε μπάρα κύλισης.
  Αυτό είναι πολύ πιθανό η ΙΔΙΑ ρίζα με το ακόμα ανοιχτό "content κόβεται από δεξιά στο PC Manager skin"
  (§Α.2 παραπάνω) - ΔΕΝ επιβεβαιώθηκε ζωντανά ότι έχει λυθεί πλήρως, αλλά η μαθηματική υπερχείλιση των
  20px δεν υπάρχει πια στον κώδικα. **ΕΛΕΓΧΘΗΚΑΝ ρητά, ένα-προς-ένα, ΟΛΑ τα υπόλοιπα 7 scrollPanel* (v2.5.0,
  grep όλων των `Helper-CreateCard` calls ανά panel)** για το ΙΔΙΟ ακριβώς μοτίβο ασυμφωνίας - ΚΑΝΕΝΑ άλλο
  instance δεν βρέθηκε: Optimization/Health/Network/System (panel=1070, κάρτες X=20 πλάτος=1030 => δεξί
  άκρο 1050, συμμετρικό 20px περιθώριο και στις δύο πλευρές), Tweaks (panel=1070, κάρτες X=0-5 πλάτος=1030
  => δεξί άκρο 1030-1035, ασύμμετρο αλλά ΧΩΡΙΣ υπερχείλιση), Bloatware/Advanced (panel=1030 - ΣΚΟΠΙΜΑ
  διαφορετικό μοτίβο, κάρτες 995-1010 πλάτος, στενότερες ΓΙΑ να αφήνουν χώρο στη δική τους απαραίτητη
  κάθετη μπάρα κύλισης λόγω πολύ ψηλότερου περιεχομένου - ήδη σωστό, όχι bug). Η Αρχική ήταν η ΜΟΝΑΔΙΚΗ
  εξαίρεση σε όλη την εφαρμογή.

### Γ. Πολιτική που έχει καθιερωθεί
- Ιστορικό Εκδόσεων: αν ένα icon/symbol δεν είναι εγγυημένα ασφαλές να αποδοθεί σωστά, να μη χρησιμοποιείται (μόνο 🔧/➕ έχουν επαληθευτεί, ως έγχρωμες κουκκίδες πλέον, όχι font glyphs).
- **ΚΑΝΟΝΑΣ** (v2.0.5, ρητό αίτημα χρήστη): όποτε γίνεται fix σε text-consistency πρόβλημα (π.χ. changelog tagging/μεταφράσεις), να σαρώνεται ΠΑΝΤΑ το ΠΛΗΡΕΣ κείμενο (π.χ. όλο το `$global:versionHistory`), ΟΧΙ μόνο το πρόσφατα-προστεθέν κομμάτι.
- Κάθε νέο UI control πρέπει να ακολουθεί σωστά το ενεργό theme (BackColor/ForeColor από `$theme`/`$currTheme`) εξαρχής, ΚΑΙ να προστίθεται στο `Update-UITheme` αν ζει στην κύρια (μη-modal) φόρμα.
- Κάθε νέο, συχνό (sub-few-second) Timer πρέπει να τρέχει τη βαριά δουλειά ασύγχρονα (persistent Runspace ή background process + polling) — ΠΟΤΕ συγχρονισμένες κλήσεις μέσα σε `Add_Tick`.
- **ΚΑΝΟΝΑΣ** (v2.2.5, ρητό αίτημα χρήστη): οι καταχωρήσεις του Ιστορικού Εκδόσεων (`$global:versionHistory`, ΚΑΙ οι EN/FR/DE μεταφράσεις τους) πρέπει να αναφέρουν ΜΟΝΟ την προσθήκη/διόρθωση - ΠΟΤΕ φράσεις όπως "ο χρήστης ζήτησε/ανέφερε/εντόπισε", "ρητό αίτημα χρήστη". Αυτό αφορά ΑΠΟΚΛΕΙΣΤΙΚΑ το user-facing changelog - οι εσωτερικές `#` παρατηρήσεις κώδικα (που ΕΠΙΤΗΔΕΣ χρησιμοποιούν αυτή τη φρασεολογία για μελλοντική αναφορά/context) ΔΕΝ αγγίζονται. Εφαρμόστηκε αναδρομικά σε ΟΛΟ το υπάρχον `$global:versionHistory` (όχι μόνο νέες καταχωρήσεις) στο v2.2.5, σύμφωνα με τον κανόνα "πλήρους σάρωσης κειμένου" παραπάνω.

---

## 5. Πώς να δουλέψεις πάνω σε αυτό

1. Διάβασε πρώτα ολόκληρο αυτό το αρχείο.
2. Άνοιξε `Optimizer.ps1` και εντόπισε τα σχετικά σημεία με βάση τα functions/global vars που αναφέρονται εδώ (π.χ. `grep -n "function Show-Sidebar"`).
3. Πριν αλλάξεις οτιδήποτε, τρέξε τους δομικούς ελέγχους της ενότητας 3 σε baseline για να ξέρεις τι είναι ήδη "γνωστό ψευδώς θετικό".
4. Κάνε μικρές, ελεγμένες αλλαγές (str_replace-style, όχι ολικές επανεγγραφές αρχείου).
5. Μετά από ΚΑΘΕ αλλαγή: ξανατρέξε τους ελέγχους της ενότητας 3.
6. Πριν οποιαδήποτε παράδοση: πλήρες μαζικό pass μεταφράσεων (αν προστέθηκε νέο ελληνικό κείμενο) + version bump + changelog entry (με 🔧/➕ πρόθεμα, χωρίς meta-αναφορές τεχνικού debugging στον χρήστη) + export.
7. Δώσε προτεραιότητα στα αιτήματα της ενότητας 4.Α πριν προχωρήσεις σε τη 4.Β (νέα, μη-ρητά ζητημένα features).
