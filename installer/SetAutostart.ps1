# Ρητό αίτημα χρήστη: επιλογή "Εκκίνηση με τα Windows" στον installer (εκτός από τις Ρυθμίσεις της
# ίδιας της εφαρμογής). Δεν δημιουργεί απευθείας την εργασία Task Scheduler εδώ - γράφει μόνο το ίδιο
# flag (LaunchWithWindowsToTray) που ήδη διαβάζει App.xaml.cs's EnsureLaunchTaskAsync() σε ΚΑΘΕ
# εκκίνηση της εφαρμογής (βλ. Services/SystemService.cs) - έτσι ξαναχρησιμοποιείται η ΙΔΙΑ,
# ήδη δοκιμασμένη λογική δημιουργίας της εργασίας αντί να διπλασιάζεται εδώ σε PowerShell.
# Windows PowerShell 5.1-συμβατό σκόπιμα (ConvertFrom-Json -AsHashtable δεν υπάρχει στο 5.1) -
# merge πάνω σε PSCustomObject ώστε να διατηρούνται όσες ρυθμίσεις υπάρχουν ήδη σε αναβάθμιση.
$path = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'OptimizerWpf\AppSettings.json'
$dir = Split-Path $path
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }

$obj = $null
if (Test-Path $path) {
    try { $obj = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json } catch { $obj = $null }
}
if ($null -eq $obj) { $obj = New-Object PSObject }

if ($obj.PSObject.Properties.Match('LaunchWithWindowsToTray').Count -gt 0) {
    $obj.LaunchWithWindowsToTray = $true
} else {
    $obj | Add-Member -NotePropertyName 'LaunchWithWindowsToTray' -NotePropertyValue $true -Force
}

$obj | ConvertTo-Json -Depth 10 | Set-Content -Path $path -Encoding UTF8
