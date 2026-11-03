# Builds GearWin-Setup-<version>.exe on a CI runner: self-contained publish + Inno Setup (see ci-install-inno.ps1). Output: installer\Output\.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet publish wpf/OptimizerWpf/OptimizerWpf.csproj -c Release -r win-x64 --self-contained true -o wpf/OptimizerWpf/publish/win-x64
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
    $iscc = @("$env:LOCALAPPDATA\Programs", $env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { $_ -and (Test-Path $_) } |
        ForEach-Object { Get-ChildItem $_ -Directory -Filter 'Inno Setup*' -ErrorAction SilentlyContinue } | Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'ISCC.exe' } | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw 'ISCC.exe not found' }
    Push-Location installer
    try { & $iscc OptimizerWpf.iss; if ($LASTEXITCODE -ne 0) { throw 'ISCC failed' } } finally { Pop-Location }
    $setup = Get-ChildItem installer/Output -Filter 'GearWin-Setup-*.exe' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    Copy-Item $setup.FullName (Join-Path $root 'installer/Output/GearWin-Setup.exe') -Force
    (Get-FileHash $setup.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  GearWin-Setup.exe' | Set-Content installer/Output/GearWin-Setup.exe.sha256
    "Built $($setup.Name)"
} finally { Pop-Location }
