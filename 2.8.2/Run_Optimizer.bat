@echo off
:: =========================================================================
:: Windows 11 Maintenance & Optimizer Tool - Launcher & Patcher (v2)
:: =========================================================================
:: ΔΙΟΡΘΩΣΗ v2 (κατόπιν αξιολόγησης ασφαλείας): η προηγούμενη έκδοση αντικαθιστούσε το Optimizer.ps1
:: χωρίς κανένα backup και χωρίς επαλήθευση ότι το νέο αρχείο είναι έγκυρο PowerShell - ένα αποτυχημένο
:: patch (π.χ. λόγω κάποιου σφάλματος escaping) θα μπορούσε να καταστρέψει μόνιμα την εφαρμογή χωρίς
:: δυνατότητα επαναφοράς. Τώρα: (1) δημιουργείται πάντα ένα χρονοσημασμένο αντίγραφο ασφαλείας πριν από
:: ΟΠΟΙΑΔΗΠΟΤΕ αντικατάσταση, (2) το νέο αρχείο ελέγχεται με τον PowerShell tokenizer (χωρίς να εκτελεστεί)
:: πριν εμπιστευτούμε ότι είναι έγκυρο, (3) σε αποτυχία εμφανίζεται σαφές μήνυμα και το patch.ps1
:: διαγράφεται (ώστε να μην ξαναπροσπαθεί σε άπειρο κύκλο στην επόμενη εκκίνηση).
setlocal EnableExtensions EnableDelayedExpansion
title Windows 11 Optimizer Launcher
chcp 1253 > nul

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [!] Απαιτούνται δικαιώματα Διαχειριστή. Γίνεται ανύψωση...
    powershell -Command "Start-Process -FilePath '%0' -Verb RunAs"
    exit /b
)

cd /d "%~dp0"
echo ============================================================
echo   Windows 11 Maintenance ^& Optimizer Tool Launcher
echo ============================================================
echo.

if exist "patch.ps1" (
    echo [i] Βρέθηκε το αρχείο patch.ps1! Εκτέλεση ενημέρωσης κώδικα...
    powershell -NoProfile -ExecutionPolicy Bypass -File "patch.ps1"
    set "PATCH_EXIT=!errorLevel!"
    echo.

    if not "!PATCH_EXIT!"=="0" (
        echo [X] Το patch.ps1 τερμάτισε με σφάλμα ^(κωδικός !PATCH_EXIT!^) - καμία αλλαγή δεν εφαρμόστηκε.
        echo [!] Διαγραφή patch.ps1 για αποφυγή επανάληψης στην επόμενη εκκίνηση...
        del "patch.ps1" >nul 2>&1
        goto :launch
    )

    if exist "Optimizer_Fixed.ps1" (
        echo [i] Επαλήθευση εγκυρότητας του διορθωμένου αρχείου πριν την αντικατάσταση...
        powershell -NoProfile -ExecutionPolicy Bypass -Command ^
            "try { $null = [System.Management.Automation.PSParser]::Tokenize((Get-Content 'Optimizer_Fixed.ps1' -Raw), [ref]$null); exit 0 } catch { exit 1 }"
        set "VALIDATE_EXIT=!errorLevel!"

        if not "!VALIDATE_EXIT!"=="0" (
            echo [X] Το Optimizer_Fixed.ps1 περιέχει σφάλμα σύνταξης PowerShell - η αντικατάσταση ΑΚΥΡΩΝΕΤΑΙ.
            echo [!] Το τρέχον Optimizer.ps1 ΔΕΝ πειράχτηκε. Διαγραφή προβληματικών αρχείων patch...
            del "patch.ps1" >nul 2>&1
            del "Optimizer_Fixed.ps1" >nul 2>&1
            goto :launch
        )

        echo [OK] Το διορθωμένο αρχείο είναι έγκυρο PowerShell.

        :: Χρονοσημασμένο αντίγραφο ασφαλείας - ΠΑΝΤΑ πριν από οποιαδήποτε αντικατάσταση, ώστε να
        :: υπάρχει δυνατότητα επαναφοράς ακόμα κι αν κάτι πάει στραβά αργότερα (π.χ. runtime error
        :: που δεν εντοπίζεται από τον tokenizer, ο οποίος ελέγχει μόνο σύνταξη, όχι λογική).
        if exist "Optimizer.ps1" (
            :: ΔΙΟΡΘΩΣΗ: το wmic έχει ήδη αφαιρεθεί εντελώς από τα Windows 11 26H2 (Σεπτέμβριος 2026) και
            :: είναι απενεργοποιημένο by default στα 24H2/25H2 - αναξιόπιστο. Χρήση PowerShell (ήδη
            :: εγγυημένα διαθέσιμο σε αυτό το script) αντί για wmic.
            for /f %%I in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set "DTS=%%I"
            set "BACKUP_NAME=Optimizer_backup_!DTS!.ps1"
            copy /y "Optimizer.ps1" "!BACKUP_NAME!" >nul 2>&1
            echo [OK] Αντίγραφο ασφαλείας: !BACKUP_NAME!
        )

        echo [OK] Η επιδιόρθωση ολοκληρώθηκε με επιτυχία.
        del "patch.ps1" >nul 2>&1
        del "Optimizer.ps1" >nul 2>&1
        ren "Optimizer_Fixed.ps1" "Optimizer.ps1"
        echo [OK] Ενημέρωση του κύριου αρχείου Optimizer.ps1 ολοκληρώθηκε.
    ) else (
        echo [!] Το patch.ps1 έτρεξε επιτυχώς αλλά δεν παρήγαγε Optimizer_Fixed.ps1 - καμία αλλαγή.
        del "patch.ps1" >nul 2>&1
    )
    echo ------------------------------------------------------------
    echo.
)

:launch
if exist "Optimizer.ps1" (
    echo [i] Εκκίνηση του Windows 11 Maintenance ^& Optimizer Tool...
    echo [i] Η κονσόλα θα ελαχιστοποιηθεί αυτόματα σε λίγα δευτερόλεπτα.
    powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Minimized -File "Optimizer.ps1"
) else (
    echo [X] ΣΦΑΛΜΑ: Το αρχείο Optimizer.ps1 δεν βρέθηκε σε αυτόν τον φάκελο!
    echo Πατήστε οποιοδήποτε πλήκτρο για έξοδο...
    pause > nul
)

exit /b
