@echo off
:: Optimizer.ps1 τρεχει ΠΑΝΤΑ ως Administrator (self-elevation) - το WPF exe εχει πλεον
:: requireAdministrator manifest (βλ. wpf/OptimizerWpf/app.manifest) που θα εμφανισει το UAC
:: prompt μονο του, αλλα αυτο το .bat ελεγχει ρητα και εξαναγκαζει elevation πριν καν
:: προσπαθησει να ξεκινησει το exe, ωστε να μη βασιζεται μονο στο manifest.
net session >nul 2>&1
if %errorLevel% == 0 (
    start "" "%~dp0wpf\OptimizerWpf\bin\Debug\net8.0-windows\OptimizerWpf.exe"
) else (
    powershell -NoProfile -Command "Start-Process -FilePath '%~dp0wpf\OptimizerWpf\bin\Debug\net8.0-windows\OptimizerWpf.exe' -Verb RunAs"
)
