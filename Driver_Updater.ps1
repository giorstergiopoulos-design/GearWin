# ==============================================================================
# DriverUpdater.ps1 - Automated CLI Driver Update Engine
# ==============================================================================
#Requires -RunAsAdministrator

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- Models & Data Structures ---
class DeviceInfo {
    [string]$DeviceName
    [string]$HardwareId
    [version]$CurrentVersion
    [datetime]$CurrentDate
}

class DriverCandidate {
    [string]$Title
    [string]$HardwareId
    [version]$Version
    [datetime]$ReleaseDate
    [string]$DownloadUrl
    [bool]$IsWhql
    [bool]$IsBeta
    [int]$Score
}

# --- Module 1: Hardware Scanner ---
function Get-SystemDevices {
    Write-Host "[1/4] Scanning system hardware..." -ForegroundColor Cyan
    $devices = [System.Collections.Generic.List[DeviceInfo]]::new()
    
    $pnpEntities = Get-CimInstance -ClassName Win32_PnPEntity | Where-Object { $_.ConfigManagerErrorCode -eq 0 }

    foreach ($entity in $pnpEntities) {
        if ([string]::IsNullOrEmpty($entity.Name) -or -not $entity.HardwareID) { continue }

        # Λήψη του primary (πιο συγκεκριμένου) Hardware ID
        $primaryHwId = $entity.HardwareID[0]
        
        # Λήψη στοιχείων του τρέχοντος driver
        $driverVer = [version]"0.0.0.0"
        $driverDate = [datetime]::MinValue

        $signedDriver = Get-CimInstance -Query "ASSOCIATORS OF {Win32_PnPEntity.DeviceID='$($entity.DeviceID.Replace('\','\\'))'} WHERE ResultClass = Win32_PnPSignedDriver" -ErrorAction SilentlyContinue
        if ($signedDriver) {
            if ($signedDriver.DriverVersion) {
                [version]::TryParse($signedDriver.DriverVersion, [ref]$driverVer) | Out-Null
            }
            if ($signedDriver.DriverDate) {
                $driverDate = $signedDriver.DriverDate
            }
        }

        $device = [DeviceInfo]::new()
        $device.DeviceName = $entity.Name
        $device.HardwareId = $primaryHwId
        $device.CurrentVersion = $driverVer
        $device.CurrentDate = $driverDate
        
        $devices.Add($device)
    }
    
    Write-Host "Found $($devices.Count) active devices." -ForegroundColor Green
    return $devices
}

# --- Module 2: Microsoft Update Catalog Query Engine ---
function Find-CatalogCandidates {
    param ([string]$HardwareId)
    
    $candidates = [System.Collections.Generic.List[DriverCandidate]]::new()
    $encodedHwId = [System.Uri]::EscapeDataString($HardwareId)
    $url = "https://www.catalog.update.microsoft.com/Search.aspx?q=$encodedHwId"

    try {
        $webClient = New-Object System.Net.WebClient
        $webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)")
        $html = $webClient.DownloadString($url)

        # Parsing των αποτελεσμάτων μέσω Regex
        $pattern = '(?s)<tr id="(?<UpdateId>[a-f0-9\-]+)_row".*?class="tableRow">.*?<a.*?>(?<Title>.*?)</a>.*?<td>(?<Class>.*?)</td>.*?<td>(?<Version>.*?)</td>.*?<td>(?<Date>.*?)</td>'
        $matches = [regex]::Matches($html, $pattern)

        foreach ($match in $matches) {
            $title = $match.Groups['Title'].Value.Trim()
            $verStr = $match.Groups['Version'].Value.Trim()
            $dateStr = $match.Groups['Date'].Value.Trim()

            $version = [version]"0.0.0.0"
            [version]::TryParse($verStr, [ref]$version) | Out-Null
            
            $date = [datetime]::MinValue
            [datetime]::TryParse($dateStr, [ref]$date) | Out-Null

            $isBeta = $title -match "Beta" -or $title -match "Preview"

            $candidate = [DriverCandidate]::new()
            $candidate.Title = $title
            $candidate.HardwareId = $HardwareId
            $candidate.Version = $version
            $candidate.ReleaseDate = $date
            $candidate.IsWhql = $true # Τα drivers του Catalog είναι πιστοποιημένα
            $candidate.IsBeta = $isBeta
            $candidate.DownloadUrl = "https://www.catalog.update.microsoft.com/DownloadDialog.aspx?updateIDs=$($match.Groups['UpdateId'].Value)"

            $candidates.Add($candidate)
        }
    }
    catch {
        # Σφάλμα δικτύου ή μηδενικά αποτελέσματα
    }

    return $candidates
}

# --- Module 3: Candidate Ranker & Filtering Engine ---
function Get-BestCandidate {
    param (
        [DeviceInfo]$Device,
        [System.Collections.Generic.List[DriverCandidate]]$Candidates
    )

    $bestCandidate = $null
    $maxScore = -1

    foreach ($candidate in $Candidates) {
        # Αποκλεισμός Beta και ίσων/παλαιότερων εκδόσεων
        if ($candidate.IsBeta) { continue }
        if ($candidate.Version -le $Device.CurrentVersion) { continue }

        # Scoring Engine
        $score = 0
        if ($candidate.IsWhql) { $score += 40 }
        if ($candidate.Version -gt $Device.CurrentVersion) { $score += 30 }
        if ($candidate.ReleaseDate -gt $Device.CurrentDate) { $score += 20 }

        $candidate.Score = $score

        if ($score -gt $maxScore) {
            $maxScore = $score
            $bestCandidate = $candidate
        }
    }

    return $bestCandidate
}

# --- Module 4: Installer Engine & Verification ---
function Install-DriverPackage {
    param (
        [string]$DownloadUrl,
        [string]$TargetDirectory
    )

    New-Item -ItemType Directory -Path $TargetDirectory -Force | Out-Null
    $cabPath = Join-Path $TargetDirectory "driver_pack.cab"

    # 1. Download
    try {
        $webClient = New-Object System.Net.WebClient
        $webClient.DownloadFile($DownloadUrl, $cabPath)
    }
    catch {
        Write-Host "    [ERROR] Download failed." -ForegroundColor Red
        return $false
    }

    # 2. Authenticode Signature Verification
    $sig = Get-AuthenticodeSignature -FilePath $cabPath
    if ($sig.Status -ne "Valid") {
        Write-Host "    [ERROR] Signature Verification Failed. File is not signed or untrusted." -ForegroundColor Red
        return $false
    }

    # 3. Extract CAB
    $extractPath = Join-Path $TargetDirectory "extracted"
    New-Item -ItemType Directory -Path $extractPath -Force | Out-Null
    
    $expandProc = Start-Process -FilePath "expand.exe" -ArgumentList "`"$cabPath`" -F:* `"$extractPath`"" -NoNewWindow -Wait -PassThru
    if ($expandProc.ExitCode -ne 0) {
        Write-Host "    [ERROR] Failed to extract CAB file." -ForegroundColor Red
        return $false
    }

    # 4. Silent Installation via PnPUtil
    $pnpProc = Start-Process -FilePath "pnputil.exe" -ArgumentList "/add-driver `"$extractPath\*.inf`" /subdirs /install" -NoNewWindow -Wait -PassThru
    return ($pnpProc.ExitCode -eq 0)
}

# --- Main Pipeline Coordinator ---
function Main {
    Clear-Host
    Write-Host "====================================================" -ForegroundColor Yellow
    Write-Host "   Automated CLI Driver Update Engine (PowerShell)  " -ForegroundColor Yellow
    Write-Host "====================================================" -ForegroundColor Yellow
    Write-Host ""

    $devices = Get-SystemDevices

    foreach ($device in $devices) {
        Write-Host "`nChecking: $($device.DeviceName)" -ForegroundColor White
        Write-Host "  Hardware ID : $($device.HardwareId)" -ForegroundColor Gray
        Write-Host "  Current Ver : v$($device.CurrentVersion) ($($device.CurrentDate.ToString('yyyy-MM-dd')))" -ForegroundColor Gray

        $candidates = Find-CatalogCandidates -HardwareId $device.HardwareId
        if ($candidates.Count -eq 0) {
            Write-Host "  -> Up to date (No matches in catalog)." -ForegroundColor DarkGray
            continue
        }

        $bestMatch = Get-BestCandidate -Device $device -Candidates $candidates

        if ($null -ne $bestMatch) {
            Write-Host "  -> UPDATE FOUND: $($bestMatch.Title)" -ForegroundColor Green
            Write-Host "     New Version : v$($bestMatch.Version) ($($bestMatch.ReleaseDate.ToString('yyyy-MM-dd')))" -ForegroundColor Green
            Write-Host "  -> Downloading & Verifying Package..." -ForegroundColor Cyan

            $tempPath = Join-Path $env:TEMP ("DriverUpdate_" + [guid]::NewGuid().ToString("N"))
            $success = Install-DriverPackage -DownloadUrl $bestMatch.DownloadUrl -TargetDirectory $tempPath

            if ($success) {
                Write-Host "  -> [SUCCESS] Driver installed successfully!" -ForegroundColor Green
            } else {
                Write-Host "  -> [FAILED] Installation or verification failed." -ForegroundColor Red
            }

            # Cleanup
            if (Test-Path $tempPath) {
                Remove-Item -Path $tempPath -Recurse -Force
            }
        } else {
            Write-Host "  -> Up to date (No newer stable version found)." -ForegroundColor DarkGray
        }
    }

    Write-Host "`n=== Process Completed ===" -ForegroundColor Yellow
}

# Run execution pipeline
Main