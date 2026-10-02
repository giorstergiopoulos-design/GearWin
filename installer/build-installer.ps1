# Builds the GearWin installer: publish (self-contained win-x64) -> tests (optional) -> Inno Setup.
# Run from anywhere:  powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
# Options:  -SkipTests   skip "dotnet test"
param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'wpf\OptimizerWpf\OptimizerWpf.csproj'
$tests = Join-Path $root 'wpf\OptimizerWpf.Tests\OptimizerWpf.Tests.csproj'
$publish = Join-Path $root 'wpf\OptimizerWpf\publish\win-x64'
$iss = Join-Path $root 'installer\OptimizerWpf.iss'

# GearWin.exe may be running (single instance) and lock files in publish\
# GearWin runs elevated (requireAdministrator), so a normal PowerShell cannot kill it ("access denied").
Get-Process GearWin -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
if (Get-Process GearWin -ErrorAction SilentlyContinue) {
    throw 'GearWin is still running. Close it first (tray icon > Exit) or run this script from an elevated (Run as administrator) PowerShell.'
}

if (-not $SkipTests) {
    Write-Host '== dotnet test ==' -ForegroundColor Cyan
    dotnet test $tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed - installer not built.' }
}

Write-Host '== dotnet publish (self-contained win-x64) ==' -ForegroundColor Cyan
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish $proj -c Release -r win-x64 --self-contained true -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$candidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
$iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'ISCC.exe (Inno Setup 6) not found.' }

Write-Host '== Inno Setup ==' -ForegroundColor Cyan
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw 'ISCC failed.' }

Get-ChildItem (Join-Path $root 'installer\Output') -Filter 'GearWin-Setup-*.exe' | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | ForEach-Object {
    Write-Host ("OK: " + $_.FullName) -ForegroundColor Green
}
