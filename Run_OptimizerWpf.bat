@echo off
:: Optimizer.ps1 τρεχει ΠΑΝΤΑ ως Administrator (self-elevation) - το WPF exe εχει πλεον
:: requireAdministrator manifest (βλ. wpf/OptimizerWpf/app.manifest) που θα εμφανισει το UAC
:: prompt μονο του, αλλα αυτο το .bat ελεγχει ρητα και εξαναγκαζει elevation πριν καν
:: προσπαθησει να ξεκινησει το exe, ωστε να μη βασιζεται μονο στο manifest.
:: ΔΙΟΡΘΩΣΗ - ρητο αιτημα χρηστη: "το ονομα του exe να προσαρμοστει στο ονομα της εφαρμογης" -
:: το AssemblyName στο OptimizerWpf.csproj αλλαξε (τελευταια φορα: μετονομασια σε GearWin,
:: βλ. σχολιο εκει) - το .bat πρεπει να δειχνει παντα στο ΤΡΕΧΟΝ ονομα exe, αλλιως δεν εκκινει τιποτα.
::
:: ΔΙΟΡΘΩΣΗ - ρητο αιτημα χρηστη: "οταν κανεις release το Run_OptimizerWpf.bat να συνδεεται με
:: αυτο" - πριν εδειχνε ΜΟΝΟ στο bin\Debug (μενει παλιωμενο μολις γινει build σε Release για
:: κυκλοφορια/installer) - τωρα προτιμα το πιο προσφατο πραγματικο build: πρωτα το self-contained
:: publish (dotnet publish, το ιδιο exe που μπαινει στον installer), μετα bin\Release, και μονο ως
:: τελευταια λυση bin\Debug - ετσι το .bat "ακολουθει" αυτοματα οποιο build εκανες τελευταιο,
:: χωρις να χρειαζεται χειροκινητη αλλαγη διαδρομης σε καθε release.
setlocal
set "BASE=%~dp0wpf\OptimizerWpf"
set "EXE=%BASE%\publish\win-x64\GearWin.exe"
if not exist "%EXE%" set "EXE=%BASE%\bin\Release\net8.0-windows\GearWin.exe"
if not exist "%EXE%" set "EXE=%BASE%\bin\Debug\net8.0-windows\GearWin.exe"

net session >nul 2>&1
if %errorLevel% == 0 (
    start "" "%EXE%"
) else (
    powershell -NoProfile -Command "Start-Process -FilePath '%EXE%' -Verb RunAs"
)
