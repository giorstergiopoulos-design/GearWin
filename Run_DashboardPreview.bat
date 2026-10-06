@echo off
:: Ρητό αίτημα χρήστη: "ολοκλήρωσέ το και παράδωσέ το" - διπλό κλικ αντί για να χρειάζεται να
:: πληκτρολογείται η εντολή "--dashboard-preview" κάθε φορά σε τερματικό. Ίδιο ακριβώς μοτίβο
:: εντοπισμού exe/elevation με το Run_OptimizerWpf.bat (βλ. εκεί για την πλήρη αιτιολόγηση) - μόνο
:: διαφορά το πρόσθετο όρισμα. Ανοίγει το πιλοτικό "Πίνακα Ελέγχου" (Views/DashboardPreviewWindow),
:: ΟΧΙ την κανονική εφαρμογή - βλ. App.xaml.cs.
setlocal
set "BASE=%~dp0wpf\OptimizerWpf"
set "EXE=%BASE%\publish\win-x64\GearWin.exe"
if not exist "%EXE%" set "EXE=%BASE%\bin\Release\net10.0-windows\GearWin.exe"
if not exist "%EXE%" set "EXE=%BASE%\bin\Debug\net10.0-windows\GearWin.exe"

net session >nul 2>&1
if %errorLevel% == 0 (
    start "" "%EXE%" --dashboard-preview
) else (
    powershell -NoProfile -Command "Start-Process -FilePath '%EXE%' -ArgumentList '--dashboard-preview' -Verb RunAs"
)
