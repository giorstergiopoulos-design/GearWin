# End-to-end test of GearWin-Setup.exe on a CLEAN, DISPOSABLE Windows machine (a GitHub-hosted runner). It installs, upgrades, starts and uninstalls for
# real, so it REFUSES to run anywhere else (it would change the registry and Program Files of the machine it runs on).
#
#   -Scenario fresh        the new setup on a clean machine
#   -Scenario from-latest  the newest published release first (and started, so the upgrade runs while it is open), then the new setup
#
# Results: <Out>\report.md (also the job summary), the Inno logs, performance.log, a screenshot. Exit code 1 on any failed check.
param(
    [Parameter(Mandatory)][ValidateSet('fresh', 'from-latest')][string]$Scenario,
    [Parameter(Mandatory)][string]$NewSetup,
    [string]$Repository = 'giorstergiopoulos-design/GearWin',
    [string]$Out = (Join-Path ([IO.Path]::GetTempPath()) 'gearwin-e2e'),
    [switch]$IAmADisposableMachine
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -and -not $IAmADisposableMachine) {
    throw 'This script installs and uninstalls the program for real. Run it only on a GitHub-hosted runner or a disposable VM (-IAmADisposableMachine). Never on a PC that is in use.'
}
New-Item -ItemType Directory -Force $Out | Out-Null
$AppId = '{B7B4D3B0-7B2A-4B0C-9D5A-2F6C9A1E4D2A}_is1'
$UninstallKey = "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$AppId"
$MachineDir = Join-Path $env:ProgramFiles 'GearWin - Complete PC Care'
$DataDir = Join-Path $env:LOCALAPPDATA 'OptimizerWpf'
$Menu = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\GearWin - Complete PC Care'
$report = New-Object System.Collections.Generic.List[string]
$failures = 0

function Check([bool]$ok, [string]$what) {
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    if (-not $ok) { $script:failures++ }
    $script:report.Add("- **$mark** $what")
    Write-Host "$mark  $what"
}
function Run-Setup([string]$exe, [string[]]$arguments, [string]$logName) {
    $log = Join-Path $Out $logName
    # NOT Start-Process -Wait (it also waits for the uninstaller's helper processes) and never forever: 6.9.5 timeout per step
    $p = Start-Process -FilePath $exe -ArgumentList ($arguments + "/LOG=`"$log`"") -PassThru
    if (-not $p.WaitForExit(240000)) {
        $script:report.Add("- **FAIL** ${logName}: $exe did not finish within 4 minutes (killed); see the log in the artifact")
        $script:failures++
        Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like 'unins*' -or $_.ProcessName -like '_unins*' } | Stop-Process -Force -ErrorAction SilentlyContinue
        try { $p.Kill() } catch { }
        return -1
    }
    return $p.ExitCode
}
function Reg-Key([string]$path) { Test-Path "HKLM:\$path" }
function Exe-Copies { @($MachineDir) | Where-Object { Test-Path $_ } | ForEach-Object { Get-ChildItem $_ -Filter 'GearWin.exe' -Recurse -ErrorAction SilentlyContinue } }
function Screenshot([string]$name) {
    try {
        Add-Type -AssemblyName System.Windows.Forms, System.Drawing
        $b = [Windows.Forms.SystemInformation]::VirtualScreen
        $bmp = New-Object Drawing.Bitmap $b.Width, $b.Height
        [Drawing.Graphics]::FromImage($bmp).CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
        $bmp.Save((Join-Path $Out $name)); $bmp.Dispose()
    } catch { Write-Host "screenshot not possible: $($_.Exception.Message)" }
}
function Stop-GearWin { Get-Process GearWin -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2 }

$report.Add("# GearWin installer end-to-end: $Scenario on $([Environment]::OSVersion.VersionString), $([Environment]::ProcessorCount) logical processors")
$report.Add('')

# ---- 1. the old version ---------------------------------------------------------------------------------------------------------------
if ($Scenario -eq 'from-latest') {
    $tag = (& gh release view --repo $Repository --json tagName --jq .tagName)
    $oldDir = Join-Path $Out 'old'
    New-Item -ItemType Directory -Force $oldDir | Out-Null
    & gh release download $tag --repo $Repository --pattern 'GearWin-Setup-*.exe' --dir $oldDir --clobber 2>&1 | Out-Host
    $oldSetup = Get-ChildItem $oldDir -Filter 'GearWin-Setup-*.exe' | Select-Object -First 1
    if (-not $oldSetup) {
        $report.Add("- skipped: release $tag has no GearWin-Setup asset (nothing to upgrade from)")
        $report | Set-Content (Join-Path $Out 'report.md'); if ($env:GITHUB_STEP_SUMMARY) { $report | Add-Content $env:GITHUB_STEP_SUMMARY }
        exit 0
    }
    $code = Run-Setup $oldSetup.FullName @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') 'old-setup.log'
    Check ($code -eq 0) "the old setup ($tag) installed (exit code $code)"
    Check ((Exe-Copies).Count -ge 1) 'the old version is installed'
    $old = Start-Process -FilePath (Join-Path $MachineDir 'GearWin.exe') -ArgumentList '--tray' -PassThru
    Start-Sleep -Seconds 10
    Check (-not $old.HasExited) 'the old program is running before the upgrade'
}

# ---- 2. the new setup -----------------------------------------------------------------------------------------------------------------
$code = Run-Setup $NewSetup @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CLOSEAPPLICATIONS', '/FORCECLOSEAPPLICATIONS') 'new-setup.log'
Check ($code -eq 0) "the new setup finished (exit code $code)"
Start-Sleep -Seconds 2
$exe = Join-Path $MachineDir 'GearWin.exe'
Check (Test-Path $exe) 'GearWin.exe is in Program Files'
$expected = (Get-Item $NewSetup).VersionInfo.ProductVersion
if ($expected) { $expected = $expected.Split('+')[0].Trim() }
if ($expected -and (Test-Path $exe)) { Check (((Get-Item $exe).VersionInfo.ProductVersion) -like "$expected*") "the installed program is version $expected" }
Check (Reg-Key $UninstallKey) 'the uninstall entry exists (HKLM)'
Check (@(Exe-Copies).Count -eq 1) "exactly one GearWin.exe is installed (found $(@(Exe-Copies).Count))"
Check ((Test-Path $Menu) -and @(Get-ChildItem $Menu -Filter '*.lnk' -ErrorAction SilentlyContinue).Count -ge 1) 'the all-users Start menu shortcut exists'
Check (Test-Path (Join-Path $MachineDir 'unins000.exe')) 'the uninstaller is there'
if ($Scenario -eq 'from-latest') {
    $stray = @(Get-Process GearWin -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path -like "$MachineDir*" -and $_.StartTime -lt (Get-Date).AddSeconds(-2) })
    Write-Host "still running after the upgrade: $($stray.Count)"
}

# ---- 3. start it, once per tier ---------------------------------------------------------------------------------------------------------
Stop-GearWin
$log = Join-Path $DataDir 'performance.log'
Remove-Item $log -ErrorAction SilentlyContinue
$smoke = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 20
Check (-not $smoke.HasExited) 'the program starts and keeps running'
Screenshot 'running.png'
Stop-GearWin
$line = if (Test-Path $log) { (Select-String -Path $log -Pattern 'Performance tier:' | Select-Object -Last 1).Line } else { $null }
$hasTiers = [bool]$line
if ($hasTiers) { Check ($line -match 'Performance tier: (Low|Balanced) \(detected\)') "the runner is detected as a Low or Balanced PC (hosted runners have 4 vCPU / 16 GB): $line" }
else { $report.Add('- _NOT VERIFIED_ this build writes no performance.log (versions before 6.9.4 have no performance levels)') }
foreach ($tier in 'Low', 'Balanced', 'High') {
    $env:GEARWIN_FORCE_TIER = $tier
    $p = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Seconds 12
    Check (-not $p.HasExited) "the program runs with the tier forced to $tier"
    Stop-GearWin
}
$env:GEARWIN_FORCE_TIER = ''
if ($hasTiers -and (Test-Path $log)) {
    foreach ($tier in 'Low', 'Balanced', 'High') { Check (@(Select-String -Path $log -Pattern "Performance tier: $tier \(forced\)").Count -ge 1) "performance.log shows the forced $tier tier" }
    Copy-Item $log (Join-Path $Out 'performance.log') -ErrorAction SilentlyContinue
}

# ---- 4. uninstall ---------------------------------------------------------------------------------------------------------------------
$uninstaller = Join-Path $MachineDir 'unins000.exe'
$code = Run-Setup $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') 'uninstall.log'
for ($i = 0; $i -lt 90 -and (Test-Path (Join-Path $MachineDir 'GearWin.exe')); $i++) { Start-Sleep -Seconds 1 }
Check (-not (Test-Path (Join-Path $MachineDir 'GearWin.exe'))) 'the program file is gone after the uninstall'
Check (-not (Test-Path $MachineDir) -or @(Get-ChildItem $MachineDir -Recurse -File -ErrorAction SilentlyContinue).Count -eq 0) 'no files are left in the Program Files folder'
Check (-not (Reg-Key $UninstallKey)) 'the uninstall entry is gone (HKLM)'
Check (-not (Test-Path $Menu)) 'the Start menu folder is gone'
Check (@(Exe-Copies).Count -eq 0) 'no GearWin.exe is left in the program folder'

$report.Add('')
$report.Add('- _NOT VERIFIED_ the UAC prompt (the runner is an administrator without prompts), SmartScreen, a real graphics card, several monitors and per-monitor DPI cannot be tested on a runner.')
$report.Add('')
$report.Add("**Result: $(if ($failures -eq 0) { 'all checks passed' } else { "$failures check(s) FAILED" })**")
$report | Set-Content (Join-Path $Out 'report.md')
if ($env:GITHUB_STEP_SUMMARY) { $report | Add-Content $env:GITHUB_STEP_SUMMARY }
if ($failures -gt 0) { exit 1 }
