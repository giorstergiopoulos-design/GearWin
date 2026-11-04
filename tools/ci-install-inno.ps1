# Makes sure a free Inno Setup that has every language file of installer\OptimizerWpf.iss is on the (GitHub-hosted) runner.
# 1. the one of the runner image; 2. when it lacks a language file: Chocolatey upgrades it in place (free); 3. when Arabic.isl (a 6.5+ translation) is still
# missing: the copy kept in tools\ci\ is put into its Languages folder. (A direct download from jrsoftware.org was tried first and returned a file that is not
# an installer on the runner, so it is no longer used.)
$ErrorActionPreference = 'Stop'
function Find-Iscc {
    # The real install folders first: the ISCC.exe on the PATH of a runner is a Chocolatey shim (C:\ProgramData\Chocolatey\bin) whose folder has no Languages.
    $real = @("$env:LOCALAPPDATA\Programs", $env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { $_ -and (Test-Path $_) } |
        ForEach-Object { Get-ChildItem $_ -Directory -Filter 'Inno Setup*' -ErrorAction SilentlyContinue } | Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'ISCC.exe' } | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($real) { return $real }
    $found = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
    if ($found -and $found -notlike '*\Chocolatey\bin\*') { return $found }
    return $null
}
function Missing-Languages($iscc) {
    $dir = Join-Path (Split-Path $iscc) 'Languages'
    # every "compiler:Languages\X.isl" the script names
    $wanted = Select-String -Path "$PSScriptRoot\..\installer\OptimizerWpf.iss" -Pattern 'compiler:Languages\\(\w+\.isl)' -AllMatches |
        ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
    $wanted | Where-Object { -not (Test-Path (Join-Path $dir $_)) }
}

# 6.9.7: Inno Setup 7 is REQUIRED (installer\OptimizerWpf.iss refuses older compilers: an older uninstaller would be kept on upgrades).
# Free source: the official release of github.com/jrsoftware/issrc (the same file the winget manifest JRSoftware.InnoSetup.7 7.1.1 points to), SHA-256 pinned.
$InnoVersion = '7.1.1'
$InnoUrl = 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_1/innosetup-7.1.1-x64.exe'
$InnoSha256 = '8a5a6711c3766a6e5fb74f280ba0e3551e719ce94538d901fdbd1f8f9920b3f0'
function Is-Inno7($path) { $path -and ((Get-Item $path).VersionInfo.ProductVersion -match '^7\.' -or (Split-Path (Split-Path $path) -Leaf) -like '*Setup 7*') }
$iscc = Find-Iscc
if (-not (Is-Inno7 $iscc)) {
    $setup = Join-Path ([IO.Path]::GetTempPath()) 'innosetup-7-x64.exe'
    Invoke-WebRequest -Uri $InnoUrl -OutFile $setup -UseBasicParsing
    $actual = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $InnoSha256) { Remove-Item $setup -Force; throw "Inno Setup $InnoVersion download has the wrong SHA-256 ($actual)" }
    $p = Start-Process -FilePath $setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/ALLUSERS' -Wait -PassThru
    if ($p.ExitCode -ne 0) { throw "Inno Setup installer failed with exit code $($p.ExitCode)" }
    $iscc = Find-Iscc
}
if (-not (Is-Inno7 $iscc)) { throw "Inno Setup 7 is not available (found: $iscc)" }
"Inno Setup found: $iscc"
$missing = @(Missing-Languages $iscc)
foreach ($file in @($missing)) {
    $kept = Join-Path $PSScriptRoot "ci\$file"
    if (Test-Path $kept) { Copy-Item $kept (Join-Path (Split-Path $iscc) 'Languages') -Force; "copied $file from tools\ci" }
}
$missing = @(Missing-Languages $iscc)
if ($missing.Count -gt 0) {
    Get-ChildItem (Join-Path (Split-Path $iscc) 'Languages') | Select-Object -ExpandProperty Name | Out-Host
    throw "Inno Setup at $iscc still lacks: $($missing -join ', ')"
}
"Inno Setup: $iscc"
