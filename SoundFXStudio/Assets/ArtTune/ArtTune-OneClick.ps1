# ArtTune-OneClick.ps1
#
# One-click, non-interactive port of ArtIsWar/ArtTuneDB Install-ArtTune.ps1
# (https://github.com/ArtIsWar/ArtTuneDB). Installs the exact same ArtTune
# stack: VB-CABLE + Voicemeeter, ReaPlugs, Equalizer APO, HeSuVi, LEQ Control
# Panel, the ArtTuneDB library (HRIR/JSFX/VST), endpoint rename + icons, and
# config.txt. Paths, URLs, registry keys, and silent-install arguments are
# copied from the reference installer so the result matches ArtTTuneDB exactly.
#
# Elevation: run via powershell.exe -Verb RunAs from the app. Components that
# still require a human are left interactive on purpose (E-APO Device Selector,
# HeSuVi 7z SFX dialog) and this script waits for them.

param(
    [switch]$InstallStack,
    [switch]$InstallLibrary,
    [switch]$SetupEndpoints,
    [switch]$RenameVoicemeeter,
    [switch]$SkipExisting,
    [switch]$UninstallEverything,
    [switch]$UninstallStack,
    [switch]$UninstallLibrary,
    [switch]$ResetEndpoints,
    [switch]$SkipVoicemeeter,
    [string]$Game = '',
    [string]$Version = '',
    [string]$SixteenChFile = '',
    [string]$EqFile = '',
    [int]$LeqReleaseTime = 0,
    [string]$TempDir = ''
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

if (-not $TempDir) { $TempDir = Join-Path $env:TEMP "ArtTune-OneClick" }
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

# Paths / URLs copied verbatim from Install-ArtTune.ps1 ----------------------
$script:BoxMargin = ''
$script:ArtTuneDBRoot = Join-Path $env:ProgramFiles "EqualizerAPO\config\ArtTuneDB"
$script:TempPath = $TempDir
$script:AssetBase = 'https://cdn.artiswar.io'
$script:LibraryMirrorBase = "$($script:AssetBase)/ArtTuneDB"
$script:LibraryReleaseAsset = 'ArtTuneDB-library.zip'

$script:MMDEVICES_RENDER  = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render'
$script:MMDEVICES_CAPTURE = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture'
$script:PKEY_FRIENDLY   = '{a45c254e-df1c-4efd-8020-67d146a850e0},2'
$script:PKEY_DESC       = '{b3f8fa53-0004-438e-9003-51a46e139bfc},6'
$script:PKEY_FORMFACTOR = '{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},0'
$script:PKEY_ICON       = '{259abffc-50a7-47ce-af08-68c9a7d73366},12'

$script:LeqReleaseTimeKey3    = '{9c00eeed-edce-4cd8-ae08-cb05e8ef57a0},3'
$script:LeqReleaseTimeKey1599 = '{9c00eeed-edce-4cd8-ae08-cb05e8ef57a0},1599'

function Write-Step  { param([string]$msg) Write-Host "[ARTTUNE] $msg" }
function Write-Ok    { param([string]$msg) Write-Host "[ARTTUNE] OK $msg" }
function Write-Warn  { param([string]$msg) Write-Host "[ARTTUNE] WARN $msg" }
function Write-Err   { param([string]$msg) Write-Host "[ARTTUNE] ERROR $msg" }

function Get-UrlToFile {
    param([string]$Url, [string]$OutFile, [int]$TimeoutSeconds = 120, [string]$FallbackUrl = '')
    # Cache: reuse a previous good download (real archive/executable, not a partial).
    if (Test-Path $OutFile) {
        $cached = Get-Item $OutFile
        if ($cached.Length -ge 1024 -and (Test-BinaryHeader $OutFile)) {
            Write-Ok "Using cached $([IO.Path]::GetFileName($OutFile)) ($($cached.Length) bytes)"
            return $true
        }
        Remove-Item $OutFile -Force -ErrorAction SilentlyContinue   # stale/partial cache
    }
    foreach ($try in @($Url, $FallbackUrl)) {
        if (-not $try) { continue }
        $what = if ($try -eq $Url) { 'download' } else { 'mirror' }
        Write-Host "[ARTTUNE] Downloading ($what)..."
        $job = Start-Job -ScriptBlock {
            param($u, $o)
            Invoke-WebRequest -Uri $u -OutFile $o -UseBasicParsing -ErrorAction Stop
        } -ArgumentList $try, $OutFile
        $done = Wait-Job $job -Timeout $TimeoutSeconds
        if ($done) {
            Receive-Job $job -ErrorVariable jerr -ErrorAction SilentlyContinue | Out-Null
            if (-not $jerr -and (Test-Path $OutFile) -and (Get-Item $OutFile).Length -ge 1024 -and (Test-BinaryHeader $OutFile)) {
                Remove-Job $job -Force -ErrorAction SilentlyContinue
                return $true
            }
            Write-Warn "Download failed for $try : $jerr"
        } else {
            Write-Warn "Download timed out after ${TimeoutSeconds}s: $try"
            Stop-Job $job -ErrorAction SilentlyContinue
        }
        Remove-Job $job -Force -ErrorAction SilentlyContinue
    }
    return $false
}

function Test-BinaryHeader {
    param([string]$FilePath)
    if (-not (Test-Path $FilePath)) { return $false }
    $bytes = [System.IO.File]::ReadAllBytes($FilePath)
    if ($bytes.Length -lt 2) { return $false }
    $prefix = [System.Text.Encoding]::ASCII.GetString($bytes, 0, 2)
    return ($prefix -eq 'MZ' -or $prefix -eq '7z' -or $prefix -eq 'PK')
}

function Get-VoicemeeterFolder {
    $vmRegKey = 'VB:Voicemeeter {17359A74-1236-5467}'
    $regPaths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$vmRegKey",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\$vmRegKey"
    )
    foreach ($rp in $regPaths) {
        $props = Get-ItemProperty -LiteralPath $rp -ErrorAction SilentlyContinue
        if (-not $props) { continue }
        if ($props.PSObject.Properties['InstallLocation']) {
            $loc = "$($props.InstallLocation)".Trim().Trim('"')
            if ($loc -and (Test-Path -LiteralPath $loc -PathType Container)) { return $loc }
        }
        if ($props.PSObject.Properties['UninstallString']) {
            $us = "$($props.UninstallString)"
            $m = [regex]::Match($us, '^\s*"?(.+?\.exe)')
            if ($m.Success) {
                $parent = Split-Path -Parent $m.Groups[1].Value
                if ($parent -and (Test-Path -LiteralPath $parent -PathType Container)) { return $parent }
            }
        }
    }
    return "$env:ProgramFiles\Voicemeeter"
}

function Test-VoicemeeterEdition {
    $vmFolder = Get-VoicemeeterFolder
    [pscustomobject]@{
        Standard  = Test-Path -LiteralPath (Join-Path $vmFolder 'voicemeeter.exe')
        Banana    = Test-Path -LiteralPath (Join-Path $vmFolder 'voicemeeterpro.exe')
        Potato    = Test-Path -LiteralPath (Join-Path $vmFolder 'voicemeeter8.exe')
        Folder    = $vmFolder
    }
}

function Install-VBAudioCertificate {
    param([string]$ExePath = '')
    $thumbprint = 'A77952D93229D0EC36E2543081EEA7D125732B9C'
    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new('TrustedPublisher', 'LocalMachine')
    try {
        $store.Open('ReadOnly')
        $existing = $store.Certificates.Find(
            [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
            $thumbprint, $false)
        if ($existing.Count -gt 0) { $store.Close(); return $true }
    } catch { } finally { try { $store.Close() } catch { } }

    $cert = $null
    if ($ExePath -and (Test-Path -LiteralPath $ExePath)) {
        try { $cert = (Get-AuthenticodeSignature -LiteralPath $ExePath -ErrorAction Stop).SignerCertificate } catch { }
    }
    if (-not $cert) {
        Write-Warn 'Could not extract the VB-Audio publisher certificate.'
        return $false
    }
    try {
        $store2 = [System.Security.Cryptography.X509Certificates.X509Store]::new('TrustedPublisher', 'LocalMachine')
        $store2.Open('ReadWrite')
        $store2.Add($cert)
        $store2.Close()
        return $true
    } catch {
        Write-Warn "Could not pre-trust VB-Audio certificate: $($_.Exception.Message)"
    }
    return $false
}

function Get-EapoInstallPath {
    $path = (Get-ItemProperty -Path 'HKLM:\SOFTWARE\EqualizerAPO' -Name 'InstallPath' -ErrorAction SilentlyContinue).InstallPath
    if (-not $path) { $path = Join-Path $env:ProgramFiles 'EqualizerAPO' }
    return "$path"
}

# ── VB-CABLE ────────────────────────────────────────────────────────────────
function Test-VBCableInstalled {
    $setupPresent = Get-ChildItem "C:\Program Files\VB\CABLE", "C:\Program Files (x86)\VB\CABLE" -Filter 'VBCABLE_Setup*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $setupPresent) { return $false }
    $dev = Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.FriendlyName -eq 'VB-Audio Virtual Cable' -and $_.Status -eq 'OK' }
    return [bool]$dev
}

function Install-VBCable {
    param([string]$ZipPath)
    if (Test-VBCableInstalled) { Write-Ok 'VB-CABLE already installed'; return $true }

    Write-Step 'Installing VB-CABLE...'
    $extractPath = Join-Path $script:TempPath 'VBCable_Extract'
    if (Test-Path $extractPath) { Remove-Item $extractPath -Recurse -Force }
    Expand-Archive -LiteralPath $ZipPath -DestinationPath $extractPath -Force -ErrorAction Stop

    $setupExe = Get-ChildItem -LiteralPath $extractPath -Filter 'VBCABLE_Setup*.exe' -Recurse -ErrorAction Stop | Select-Object -First 1
    if (-not $setupExe) { Write-Err 'VB-CABLE setup exe not found in archive'; return $false }

    $null = Install-VBAudioCertificate -ExePath $setupExe.FullName
    $proc = Start-Process -FilePath $setupExe.FullName -ArgumentList '-i -h' -PassThru -ErrorAction Stop
    if (-not $proc.WaitForExit(240000)) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        Write-Err 'VB-CABLE installer timed out -- try again.'
        return $false
    }
    Start-Sleep -Seconds 3
    $cableDev = Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.FriendlyName -eq 'VB-Audio Virtual Cable' } | Select-Object -First 1
    if ($cableDev) {
        if ($cableDev.Status -eq 'OK') { Write-Ok 'VB-CABLE installed' }
        else { Write-Warn "VB-CABLE driver installed; its device is pending restart ($($cableDev.Status)) -- a reboot is required before endpoint setup." }
    } else {
        Write-Warn 'VB-CABLE driver installed, but its virtual device will only appear after a reboot. A restart is required before endpoint setup.'
    }
    Remove-Item $extractPath -Recurse -Force -ErrorAction SilentlyContinue
    return $true
}

# ── Voicemeeter ─────────────────────────────────────────────────────────────
function Install-Voicemeeter {
    param([string]$ZipPath)
    if ((Test-VoicemeeterEdition).Standard) { Write-Ok 'Voicemeeter already installed'; return $true }

    Write-Step 'Installing Voicemeeter...'
    $extractPath = Join-Path $script:TempPath 'Voicemeeter_Extract'
    if (Test-Path $extractPath) { Remove-Item $extractPath -Recurse -Force }
    Expand-Archive -LiteralPath $ZipPath -DestinationPath $extractPath -Force -ErrorAction Stop

    $setupExe = Get-ChildItem -LiteralPath $extractPath -Filter '*Setup*.exe' -Recurse -ErrorAction Stop | Select-Object -First 1
    if (-not $setupExe) { Write-Err 'Voicemeeter setup exe not found in archive'; return $false }

    $null = Install-VBAudioCertificate -ExePath $setupExe.FullName
    $proc = Start-Process -FilePath $setupExe.FullName -ArgumentList '-i -h' -PassThru -ErrorAction Stop
    if (-not $proc.WaitForExit(240000)) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        Write-Err 'Voicemeeter installer timed out -- try again.'
        return $false
    }
    Start-Sleep -Seconds 2

    if (-not (Test-VoicemeeterEdition).Standard) { Write-Err 'Voicemeeter verification failed'; return $false }
    Remove-Item $extractPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Ok 'Voicemeeter installed'
    return $true
}

# ── ReaPlugs ────────────────────────────────────────────────────────────────
function Confirm-ReaPlugsInstaller {
    # ReaPlugs' NSIS pages expose their confirm buttons as Panes, not Buttons.
    # Click the CENTRE of any element whose name matches a confirm word (any
    # control type) and stop. The old Button-only / bottom-right-click logic
    # stalled or cancelled the installer.
    param([int]$ProcId, [int]$TimeoutSeconds = 90)
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    try { Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class M {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, int dwData, UIntPtr dwExtraInfo);
}
"@ -ErrorAction Stop } catch { }
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $namePat = 'Agree|^Install|&Install|Next|Finish|^OK|Accept'
    while ((Get-Date) -lt $deadline) {
        $p = Get-Process -Id $ProcId -ErrorAction SilentlyContinue
        if (-not $p -or $p.HasExited) { return }
        $pidCond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcId)
        $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $pidCond)
        if (-not $win) { Start-Sleep -Milliseconds 500; continue }
        $all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($el in $all) {
            $nm = "$($el.Current.Name)"
            if ($nm -match $namePat) {
                $rect = $el.Current.BoundingRectangle
                if ($rect.Width -gt 4 -and $rect.Height -gt 4) {
                    $x = [int]($rect.X + $rect.Width / 2)
                    $y = [int]($rect.Y + $rect.Height / 2)
                    [M]::SetCursorPos($x, $y)
                    Start-Sleep -Milliseconds 150
                    [M]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
                    [M]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
                    Write-Host "[ARTTUNE] ReaPlugs dialog auto-confirmed (click-center '$nm' @ $x,$y)."
                    Start-Sleep -Seconds 2
                    return
                }
            }
        }
        Start-Sleep -Milliseconds 700
    }
}

function Install-ReaPlugs {
    param([string]$InstallerPath)
    $verifyDir = "${env:ProgramFiles}\VSTPlugins\ReaPlugs"
    $dlls = @(Get-ChildItem "$verifyDir\*.dll" -ErrorAction SilentlyContinue)
    if ($dlls.Count -ge 5) { Write-Ok "ReaPlugs already installed ($($dlls.Count) DLLs)"; return $true }

    Write-Step 'Installing ReaPlugs...'
    Unblock-File -LiteralPath $InstallerPath -ErrorAction SilentlyContinue
    $proc = Start-Process -FilePath $InstallerPath -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -PassThru -ErrorAction Stop
    if (-not $proc.WaitForExit(90000)) {
        Write-Warn 'ReaPlugs installer still open -- auto-confirming its dialog...'
        Confirm-ReaPlugsInstaller -ProcId $proc.Id
        if (-not $proc.WaitForExit(15000)) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            Write-Err 'ReaPlugs installer would not complete.'
            return $false
        }
    }
    Start-Sleep -Seconds 2
    Write-Ok 'ReaPlugs installed'
    return $true
}

# ── Equalizer APO ───────────────────────────────────────────────────────────
function Start-EapoSelectorWatcher {
    # The E-APO installer runs the Device Selector configurator at the end.
    # Watch in the background (>= setup duration) and close it so installs are
    # fully hands-free. Runs elevated, so it may close the elevated dialog.
    $job = Start-Job -ScriptBlock {
        Add-Type -Name U -Namespace W -MemberDefinition @'
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowW(string cn, string tn);
[DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr h, uint m, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
'@
        $deadline = (Get-Date).AddMinutes(10)
        while ((Get-Date) -lt $deadline) {
            Get-Process -Name 'Configurator' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
            $h = [W.U]::FindWindowW($null, 'Equalizer APO Device Selector')
            if ($h -ne [IntPtr]::Zero -and [W.U]::IsWindow($h)) {
                [W.U]::PostMessageW($h, 0x0012, [IntPtr]::Zero, [IntPtr]::Zero) # WM_CLOSE
            }
            Start-Sleep -Milliseconds 700
        }
    }
    return $job
}

function Install-Eapo {
    param([string]$InstallerPath)
    $eapoRoot = (Get-EapoInstallPath).TrimEnd('\')
    # /S is semi-silent: the Device Selector still spawns afterwards. The
    # watcher below closes it for us. /D sets the install location.
    Write-Step 'Installing Equalizer APO (Device Selector auto-dismissed)...'
    $watcher = Start-EapoSelectorWatcher
    try {
        $proc = Start-Process -FilePath $InstallerPath -ArgumentList "/S /D=$eapoRoot" -PassThru -ErrorAction Stop
        $proc.WaitForExit()
        Start-Sleep -Seconds 2
    } finally {
        Stop-Job $watcher -ErrorAction SilentlyContinue
        Remove-Job $watcher -Force -ErrorAction SilentlyContinue
    }
    # Verify. /D= is a REQUEST, not a guarantee -- an existing install's recorded
    # path wins.
    $path = (Get-ItemProperty -Path 'HKLM:\SOFTWARE\EqualizerAPO' -Name 'InstallPath' -ErrorAction SilentlyContinue).InstallPath
    if (-not $path -or -not (Test-Path (Join-Path "$path" 'config'))) {
        $path = $eapoRoot
    }
    if (Test-Path (Join-Path "$path" 'config')) {
        Write-Ok "Equalizer APO installed to $path"
        return $true
    }
    Write-Err 'Equalizer APO verification failed'
    return $false
}

# ── HeSuVi (silent 7z SFX extraction; interactive fallback if needed) ───────
function Install-HeSuVi {
    param([string]$InstallerPath)
    $heSuViDir = Join-Path $env:ProgramFiles 'EqualizerAPO\config\HeSuVi'
    if (Test-Path (Join-Path $heSuViDir 'hesuvi.txt')) { Write-Ok 'HeSuVi already installed'; return $true }

    $detect = {
        param($dirs)
        foreach ($d in $dirs) {
            if (Test-Path (Join-Path $d 'hesuvi.txt')) { return $d }
        }
        return $null
    }

    # Attempt silent SFX extraction (-o"dir" -y). Timeout in case the package
    # turns out not to be a 7z SFX after all.
    Write-Step 'Installing HeSuVi...'
    $proc = Start-Process -FilePath $InstallerPath -ArgumentList "-o`"$heSuViDir`" -y" -PassThru -ErrorAction Stop
    if (-not $proc.WaitForExit(60000)) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }

    $deadline = (Get-Date).AddSeconds(120)
    $found = $null
    while ((Get-Date) -lt $deadline -and -not $found) {
        $found = & $detect @(
            $heSuViDir,
            (Join-Path $env:APPDATA 'HeSuVi'),
            (Join-Path $env:USERPROFILE 'HeSuVi')
        )
        if (-not $found) { Start-Sleep -Seconds 2 }
    }

    if ($found) { Write-Ok "HeSuVi installed to $found"; return $true }

    # Fallback: interactive extraction (inform the user, poll 5 minutes).
    Write-Warn 'Silent HeSuVi extraction failed -- launching visible extractor, complete the dialog...'
    Start-Process -FilePath $InstallerPath -ErrorAction SilentlyContinue | Out-Null
    $deadline = (Get-Date).AddMinutes(5)
    while ((Get-Date) -lt $deadline -and -not $found) {
        $found = & $detect @(
            $heSuViDir,
            (Join-Path $env:APPDATA 'HeSuVi'),
            (Join-Path $env:USERPROFILE 'HeSuVi')
        )
        if (-not $found) { Start-Sleep -Seconds 2 }
    }

    if ($found) { Write-Ok "HeSuVi installed to $found"; return $true }
    Write-Err 'HeSuVi not detected. Complete the HeSuVi dialog, then re-run.'
    return $false
}

# ── LEQ Control Panel ───────────────────────────────────────────────────────
function Install-SoundControl {
    param([string]$SourcePath)
    Write-Step 'Installing LEQ Control Panel...'
    $scFolder = Join-Path $env:LOCALAPPDATA 'Programs\LEQControlPanel'
    $scExe = Join-Path $scFolder 'LEQControlPanel.exe'

    $running = Get-Process -Name 'LEQControlPanel' -ErrorAction SilentlyContinue
    if ($running) {
        Stop-Process -Name 'LEQControlPanel' -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 1
    }

    New-Item -ItemType Directory -Path $scFolder -Force | Out-Null
    Copy-Item -LiteralPath $SourcePath -Destination $scExe -Force
    Write-Ok 'LEQ Control Panel installed'
    return $true
}

# ── ArtTuneDB library ───────────────────────────────────────────────────────
function Resolve-LibraryPayloadRoot {
    param([string]$StagingDir)
    $looksLikeLibrary = {
        param($dir)
        if (-not $dir -or -not (Test-Path $dir)) { return $false }
        if (Test-Path (Join-Path $dir 'vst'))  { return $true }
        if (Test-Path (Join-Path $dir 'jsfx')) { return $true }
        foreach ($g in @('BF6', 'BO6', 'BO7', 'PS5-BO6', 'STD')) {
            if (Test-Path (Join-Path $dir $g)) { return $true }
        }
        return $false
    }
    if (& $looksLikeLibrary $StagingDir) { return $StagingDir }
    $lib = Join-Path $StagingDir 'library'
    if (& $looksLikeLibrary $lib) { return $lib }
    $subs = @(Get-ChildItem -Path $StagingDir -Directory -ErrorAction SilentlyContinue)
    if ($subs.Count -eq 1) {
        $w = $subs[0].FullName
        if (& $looksLikeLibrary $w) { return $w }
        $wlib = Join-Path $w 'library'
        if (& $looksLikeLibrary $wlib) { return $wlib }
    }
    $found = Get-ChildItem -Path $StagingDir -Recurse -Directory -Filter 'library' -ErrorAction SilentlyContinue |
        Where-Object { & $looksLikeLibrary $_.FullName } | Select-Object -First 1
    if ($found) { return $found.FullName }
    return $null
}

function Install-ArtTuneLibrary {
    param([switch]$SkipBackup)
    $libRoot = Join-Path $script:ArtTuneDBRoot 'library'
    if ((-not $SkipBackup) -and (Test-Path (Join-Path $libRoot 'version.txt'))) {
        Write-Ok 'ArtTuneDB library already installed'
        return $true
    }
    if ($SkipBackup -and (Get-ChildItem $libRoot -ErrorAction SilentlyContinue)) {
        Write-Ok 'ArtTuneDB library already present'
        return $true
    }

    Write-Step 'Fetching ArtTuneDB library release...'
    $assetName = $script:LibraryReleaseAsset
    try {
        $verResp = Invoke-WebRequest -Uri "$($script:LibraryMirrorBase.TrimEnd('/'))/latest-version.txt" -UseBasicParsing -ErrorAction Stop
        $cdnVersion = ((("$($verResp.Content)") -split "`r?`n")[0]).Trim()
        if ($cdnVersion -and $cdnVersion -notmatch '[\\/\s]') {
            $assetName = "ArtTuneDB-$cdnVersion.zip"
            Write-Host "[ARTTUNE] Library version (CDN): $cdnVersion"
        }
    } catch {
        Write-Host "[ARTTUNE] CDN version lookup failed; using default asset name."
    }

    $zipPath = Join-Path $script:TempPath 'ArtTuneDB-library.zip'
    $mirrorUrl = "$($script:LibraryMirrorBase.TrimEnd('/'))/$assetName"
    $downloaded = Get-UrlToFile -Url $mirrorUrl -OutFile $zipPath -TimeoutSeconds 300
    if (-not $downloaded) { Write-Err 'Library download failed.'; return $false }

    $staging = Join-Path $script:TempPath 'ArtTuneDB-library-extract'
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    $srcRoot = $null
    try {
        Expand-Archive -LiteralPath $zipPath -DestinationPath $staging -Force
        $srcRoot = Resolve-LibraryPayloadRoot -StagingDir $staging
    } catch {
        Write-Err "Could not extract the library zip: $($_.Exception.Message)"
    }

    if (-not $srcRoot) {
        Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
        Write-Err 'Library payload root not found.'
        return $false
    }
    $srcRoot = (Get-Item -LiteralPath $srcRoot).FullName

    # Backup existing library (dated) unless skipping (fresh install).
    if ((-not $SkipBackup) -and (Test-Path $libRoot)) {
        try {
            $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
            $backupDest = "$script:ArtTuneDBRoot\library-backup-$stamp"
            Copy-Item -LiteralPath $libRoot -Destination $backupDest -Recurse -Force
            Write-Host "[ARTTUNE] Previous library backed up to $backupDest"
        } catch {
            Write-Warn "Backup failed; existing library left untouched. $($_.Exception.Message)"
            Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
            return $false
        }
    }

    New-Item -ItemType Directory -Path $libRoot -Force | Out-Null
    Get-ChildItem -Path $libRoot -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item -Path (Join-Path $srcRoot '*') -Destination $libRoot -Recurse -Force
    $fileCount = @(Get-ChildItem -LiteralPath $libRoot -Recurse -File -ErrorAction SilentlyContinue).Count
    Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    Write-Ok "ArtTuneDB library installed ($fileCount files)."
    return $true
}

function Get-BundledLibraryPath {
    $candidates = @(Join-Path $env:ProgramFiles 'EqualizerAPO\config\ArtTuneDB\library')
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) { return $c }
    }
    return $null
}

function Install-ArtTuneHRIR {
    $hrirDir   = Join-Path $env:ProgramFiles 'EqualizerAPO\config\HeSuVi\hrir'
    $hrir44Dir = Join-Path $hrirDir '44'
    $targets = @(
        @{ Name = 'EAC_Default'; Rate = '48 kHz';   Url = 'https://cdn.artiswar.io/HeSuVi/hrir/EAC_Default.wav';    Dest = (Join-Path $hrirDir   'EAC_Default.wav') }
        @{ Name = 'EAC_Default'; Rate = '44.1 kHz'; Url = 'https://cdn.artiswar.io/HeSuVi/hrir/44/EAC_Default.wav'; Dest = (Join-Path $hrir44Dir 'EAC_Default.wav') }
        @{ Name = 'EAC_Refined'; Rate = '48 kHz';   Url = 'https://cdn.artiswar.io/HeSuVi/hrir/EAC_Refined.wav';    Dest = (Join-Path $hrirDir   'EAC_Refined.wav') }
        @{ Name = 'EAC_Refined'; Rate = '44.1 kHz'; Url = 'https://cdn.artiswar.io/HeSuVi/hrir/44/EAC_Refined.wav'; Dest = (Join-Path $hrir44Dir 'EAC_Refined.wav') }
    )
    if (-not ($targets | Where-Object { -not (Test-Path $_.Dest) })) {
        Write-Ok 'ArtTune HRIR already installed.'
        return $true
    }
    Write-Step 'Installing ArtTune HRIR...'
    New-Item -ItemType Directory -Path $hrirDir -Force | Out-Null
    New-Item -ItemType Directory -Path $hrir44Dir -Force | Out-Null
    $ok = $true
    foreach ($t in $targets) {
        if (Test-Path $t.Dest) { continue }
        if (-not (Get-UrlToFile -Url $t.Url -OutFile $t.Dest -TimeoutSeconds 120)) { $ok = $false }
    }
    if ($ok) { Write-Ok 'ArtTune HRIR installed (EAC_Default + EAC_Refined, 48 kHz + 44.1 kHz).' }
    else { Write-Warn 'Some ArtTune HRIR files could not be downloaded.' }
    return $ok
}

function Install-JsfxPlugins {
    $jsfxDir = Join-Path $env:ProgramFiles 'VSTPlugins\ReaPlugs\JS\Effects\ArtTuneKit'
    $file1 = Join-Path $jsfxDir 'atk_spatial_engine.jsfx'
    $file2 = Join-Path $jsfxDir 'atk_stereo_spatial_enhancer.jsfx'
    if ((Test-Path $file1) -and (Test-Path $file2)) { Write-Ok 'JSFX plugins already installed.'; return $true }

    $reaPlugsDir = Join-Path $env:ProgramFiles 'VSTPlugins\ReaPlugs'
    if (-not (Test-Path $reaPlugsDir)) { Write-Warn 'ReaPlugs not found. Install ReaPlugs first.'; return $false }

    $bundled = Get-BundledLibraryPath
    $srcDir = if ($bundled) { Join-Path $bundled 'jsfx' } else { $null }
    if (-not $srcDir -or -not (Test-Path $srcDir)) { Write-Warn 'bundled jsfx\ not found; skipping JSFX install.'; return $false }

    $srcFiles = @(Get-ChildItem -Path $srcDir -Filter '*.jsfx' -File -ErrorAction SilentlyContinue)
    if ($srcFiles.Count -eq 0) { Write-Warn 'no .jsfx files in the bundle.'; return $false }

    Write-Step 'Installing JSFX plugins...'
    New-Item -ItemType Directory -Path $jsfxDir -Force | Out-Null
    foreach ($f in $srcFiles) { Copy-Item -Path $f.FullName -Destination (Join-Path $jsfxDir $f.Name) -Force }
    if ((Test-Path $file1) -and (Test-Path $file2)) { Write-Ok "JSFX plugins installed to $jsfxDir"; return $true }
    Write-Warn 'JSFX plugin verification failed.'
    return $false
}

function Install-VstPlugins {
    $vstDir = Join-Path $env:ProgramFiles 'VSTPlugins\ArtTuneKit'
    $bundled = Get-BundledLibraryPath
    $srcDir = if ($bundled) { Join-Path $bundled 'vst' } else { $null }
    if (-not $srcDir -or -not (Test-Path $srcDir)) { Write-Warn 'bundled vst\ not found; skipping VST install.'; return $false }

    $srcDlls = @(Get-ChildItem -Path $srcDir -Filter '*.dll' -File -ErrorAction SilentlyContinue)
    if ($srcDlls.Count -eq 0) { Write-Warn 'no VST DLLs in the bundle.'; return $false }

    $allPresent = $true
    foreach ($dll in $srcDlls) {
        if (-not (Test-Path (Join-Path $vstDir $dll.Name))) { $allPresent = $false; break }
    }
    if (-not $allPresent) {
        Write-Step 'Installing ArtTuneKit VST...'
        New-Item -ItemType Directory -Path $vstDir -Force | Out-Null
        foreach ($dll in $srcDlls) { Copy-Item -Path $dll.FullName -Destination (Join-Path $vstDir $dll.Name) -Force }
    }
    New-Item -ItemType Directory -Path $vstDir -Force | Out-Null
    $ok = $true
    foreach ($dll in $srcDlls) { if (-not (Test-Path (Join-Path $vstDir $dll.Name))) { $ok = $false } }
    if ($ok) { Write-Ok "ArtTuneKit VST installed to $vstDir"; return $true }
    Write-Warn 'VST verification failed.'
    return $false
}

function Backup-EAPOConfigFile {
    $configFile = Join-Path $env:ProgramFiles 'EqualizerAPO\config\config.txt'
    try {
        if (-not (Test-Path $configFile)) { return $true }
        $firstLine = (Get-Content -LiteralPath $configFile -TotalCount 1 -ErrorAction SilentlyContinue)
        if ($firstLine -ne '# ArtTuneDB config.txt') { return $true }   # user's own config -- do not touch when replacing
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $backup = Join-Path $env:ProgramFiles "EqualizerAPO\config\config.txt.bak-$stamp"
        Copy-Item -LiteralPath $configFile -Destination $backup -Force
        return $true
    } catch { return $false }
}

function Get-ArtTuneEndpoints {
    $render8 = $null; $render16 = $null; $capture = $null
    if (Test-Path $script:MMDEVICES_RENDER) {
        foreach ($key in Get-ChildItem -Path $script:MMDEVICES_RENDER -ErrorAction SilentlyContinue) {
            try {
                $state = (Get-ItemProperty -Path $key.PSPath -Name 'DeviceState' -ErrorAction SilentlyContinue).DeviceState
                if ($state -ne 1) { continue }
                $propsPath = Join-Path $key.PSPath 'Properties'
                if (-not (Test-Path $propsPath)) { continue }
                $props = Get-ItemProperty -Path $propsPath -ErrorAction SilentlyContinue
                if ($props.$script:PKEY_DESC -ne 'VB-Audio Virtual Cable') { continue }
                $ff = [int]($props.$script:PKEY_FORMFACTOR)
                if ($ff -eq 2) { if (-not $render16) { $render16 = $key.PSChildName } }
                else { if (-not $render8) { $render8 = $key.PSChildName } }
            } catch { continue }
        }
    }
    if (Test-Path $script:MMDEVICES_CAPTURE) {
        foreach ($key in Get-ChildItem -Path $script:MMDEVICES_CAPTURE -ErrorAction SilentlyContinue) {
            try {
                $state = (Get-ItemProperty -Path $key.PSPath -Name 'DeviceState' -ErrorAction SilentlyContinue).DeviceState
                if ($state -ne 1) { continue }
                $propsPath = Join-Path $key.PSPath 'Properties'
                if (-not (Test-Path $propsPath)) { continue }
                $props = Get-ItemProperty -Path $propsPath -ErrorAction SilentlyContinue
                if ($props.$script:PKEY_DESC -ne 'VB-Audio Virtual Cable') { continue }
                if (-not $capture) { $capture = $key.PSChildName }
            } catch { continue }
        }
    }
    return [pscustomobject]@{ Render8 = $render8; Render16 = $render16; Capture = $capture }
}

function Get-VoicemeeterEndpoints {
    $render = $null; $capture = $null
    if (Test-Path $script:MMDEVICES_RENDER) {
        foreach ($key in Get-ChildItem -Path $script:MMDEVICES_RENDER -ErrorAction SilentlyContinue) {
            try {
                $state = (Get-ItemProperty -Path $key.PSPath -Name 'DeviceState' -ErrorAction SilentlyContinue).DeviceState
                if ($state -ne 1) { continue }
                $propsPath = Join-Path $key.PSPath 'Properties'
                if (-not (Test-Path $propsPath)) { continue }
                $props = Get-ItemProperty -Path $propsPath -ErrorAction SilentlyContinue
                $name = "$($props.$script:PKEY_FRIENDLY)"
                if ($name -eq 'Voicemeeter Input' -or $name -eq 'Normal Audio') { if (-not $render) { $render = $key.PSChildName } }
            } catch { continue }
        }
    }
    if (Test-Path $script:MMDEVICES_CAPTURE) {
        foreach ($key in Get-ChildItem -Path $script:MMDEVICES_CAPTURE -ErrorAction SilentlyContinue) {
            try {
                $state = (Get-ItemProperty -Path $key.PSPath -Name 'DeviceState' -ErrorAction SilentlyContinue).DeviceState
                if ($state -ne 1) { continue }
                $propsPath = Join-Path $key.PSPath 'Properties'
                if (-not (Test-Path $propsPath)) { continue }
                $props = Get-ItemProperty -Path $propsPath -ErrorAction SilentlyContinue
                $name = "$($props.$script:PKEY_FRIENDLY)"
                if ($name -eq 'Voicemeeter Out B1' -or $name -eq 'Virtual Mix' -or
                    ($name -and $name.Contains('Voicemeeter Output'))) { if (-not $capture) { $capture = $key.PSChildName } }
            } catch { continue }
        }
    }
    return [pscustomobject]@{ Render = $render; Capture = $capture }
}

function Restart-AudioServices {
    Write-Step 'Restarting audio services...'
    try { Restart-Service -Name 'Audiosrv' -Force -ErrorAction Stop }
    catch {
        Start-Sleep -Seconds 2
        Restart-Service -Name 'Audiosrv' -Force -ErrorAction Stop
    }
    Start-Sleep -Seconds 3
    Write-Ok 'Audio services restarted.'
}

function Set-ArtTuneEndpoints {
    param([bool]$IncludeVoicemeeter = $false)
    Write-Step 'Detecting VB-CABLE endpoints...'
    $eps = Get-ArtTuneEndpoints
    $r8 = $eps.Render8; $r16 = $eps.Render16; $cap = $eps.Capture
    if (-not $r8 -and -not $r16 -and -not $cap) {
        Write-Warn 'No active VB-CABLE endpoints detected; skipping endpoint configuration.'
        return [pscustomobject]@{ Render8 = $null; Render16 = $null; Capture = $null; Verified = $false }
    }
    $vmRender = $null; $vmCapture = $null
    if ($IncludeVoicemeeter) {
        Write-Step 'Detecting Voicemeeter endpoints...'
        $vmEps = Get-VoicemeeterEndpoints
        $vmRender = $vmEps.Render; $vmCapture = $vmEps.Capture
    }

    $iconDir = 'C:\ProgramData\ArtTune\icons'
    New-Item -ItemType Directory -Path $iconDir -Force -ErrorAction SilentlyContinue | Out-Null
    $getIcon = {
        param($FileName)
        $dest = Join-Path $iconDir $FileName
        try {
            Invoke-WebRequest -Uri "https://cdn.artiswar.io/$FileName" -OutFile $dest -UseBasicParsing -ErrorAction Stop
            if (Test-Path -LiteralPath $dest) { return $dest }
        } catch { Write-Warn "Icon download failed for $FileName (non-fatal)." }
        return $null
    }
    $r8Icon  = if ($r8)  { & $getIcon 'ArtTuneCable.ico' }         else { $null }
    $r16Icon = if ($r16) { & $getIcon 'ArtTunePlusCable.ico' }     else { $null }
    $capIcon = if ($cap) { & $getIcon 'ArtTuneUnifiedOutput.ico' } else { $null }
    $r8IconReg  = if ($r8Icon)  { ($r8Icon + ',0').Replace('\', '\\') }  else { $null }
    $r16IconReg = if ($r16Icon) { ($r16Icon + ',0').Replace('\', '\\') } else { $null }
    $capIconReg = if ($capIcon) { ($capIcon + ',0').Replace('\', '\\') } else { $null }

    $renderKey  = 'HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render'
    $captureKey = 'HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture'
    $reg = "Windows Registry Editor Version 5.00`r`n"
    if ($r8) {
        $reg += "`r`n[$renderKey\$r8\Properties]`r`n"
        $reg += "`"$script:PKEY_FRIENDLY`"=`"Art Tune`"`r`n"
        if ($r8IconReg) { $reg += "`"$script:PKEY_ICON`"=`"$r8IconReg`"`r`n" }
    }
    if ($r16) {
        $reg += "`r`n[$renderKey\$r16\Properties]`r`n"
        $reg += "`"$script:PKEY_FRIENDLY`"=`"Art Tune +`"`r`n"
        if ($r16IconReg) { $reg += "`"$script:PKEY_ICON`"=`"$r16IconReg`"`r`n" }
    }
    if ($cap) {
        $reg += "`r`n[$captureKey\$cap\Properties]`r`n"
        $reg += "`"$script:PKEY_FRIENDLY`"=`"Art Tune Unified Output`"`r`n"
        if ($capIconReg) { $reg += "`"$script:PKEY_ICON`"=`"$capIconReg`"`r`n" }
    }
    if ($vmRender) {
        $reg += "`r`n[$renderKey\$vmRender\Properties]`r`n"
        $reg += "`"$script:PKEY_FRIENDLY`"=`"Normal Audio`"`r`n"
    }
    if ($vmCapture) {
        $reg += "`r`n[$captureKey\$vmCapture\Properties]`r`n"
        $reg += "`"$script:PKEY_FRIENDLY`"=`"Virtual Mix`"`r`n"
    }

    Write-Step 'Applying endpoint names and icons...'
    $regFile = Join-Path $script:TempPath 'arttune_endpoints.reg'
    $reg | Out-File -FilePath $regFile -Encoding ASCII -Force
    try {
        $proc = Start-Process -FilePath 'regedit.exe' -ArgumentList "/s `"$regFile`"" -Wait -PassThru -WindowStyle Hidden
        if ($proc.ExitCode -ne 0) { Write-Warn "registry import returned exit code $($proc.ExitCode) (non-fatal)." }
    } catch { Write-Warn "registry import failed (non-fatal): $($_.Exception.Message)" }
    Remove-Item $regFile -Force -ErrorAction SilentlyContinue

    Restart-AudioServices

    Start-Sleep -Milliseconds 200
    $expected = @()
    if ($r8)  { $expected += @{ Type = 'Render';  GUID = $r8;  Name = 'Art Tune';                Icon = $(if ($r8Icon)  { "$r8Icon,0" }  else { $null }) } }
    if ($r16) { $expected += @{ Type = 'Render';  GUID = $r16; Name = 'Art Tune +';              Icon = $(if ($r16Icon) { "$r16Icon,0" } else { $null }) } }
    if ($cap) { $expected += @{ Type = 'Capture'; GUID = $cap; Name = 'Art Tune Unified Output'; Icon = $(if ($capIcon) { "$capIcon,0" } else { $null }) } }
    $cableCount = $expected.Count
    if ($vmRender)  { $expected += @{ Type = 'Render';  GUID = $vmRender;  Name = 'Normal Audio'; Icon = $null } }
    if ($vmCapture) { $expected += @{ Type = 'Capture'; GUID = $vmCapture; Name = 'Virtual Mix';  Icon = $null } }

    $allOk = $true
    $iconsAttempted = 0
    foreach ($e in $expected) {
        $propsPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\$($e.Type)\$($e.GUID)\Properties"
        try {
            $actual = (Get-ItemProperty -LiteralPath $propsPath -Name $script:PKEY_FRIENDLY -ErrorAction Stop).$script:PKEY_FRIENDLY
            if ($actual -ne $e.Name) { $allOk = $false }
        } catch { $allOk = $false }
        if ($e.Icon) {
            $iconsAttempted++
            try {
                $actualIcon = (Get-ItemProperty -LiteralPath $propsPath -Name $script:PKEY_ICON -ErrorAction Stop).$script:PKEY_ICON
                if ("$actualIcon" -ne $e.Icon) { $allOk = $false }
            } catch { $allOk = $false }
        }
    }
    if ($allOk) {
        if ($iconsAttempted -eq 0) { Write-Ok 'Endpoints renamed and verified (names); no icons applied.' }
        elseif ($iconsAttempted -eq $cableCount) { Write-Ok 'Endpoints renamed and verified (names + icons).' }
        else { Write-Ok 'Endpoints renamed and verified (names + icons where available).' }
    } else {
        Write-Warn 'endpoint name or icon verification incomplete (non-fatal).'
    }
    return [pscustomobject]@{ Render8 = $r8; Render16 = $r16; Capture = $cap; Verified = $allOk }
}

# ── config.txt writer (ApplyTune) ───────────────────────────────────────────
function Write-ArtTuneConfig {
    param(
        [string]$RenderGuid8,
        [string]$RenderGuid16,
        [string]$Game,
        [string]$Version,
        [string]$SixteenChFile,
        [string]$EqFile
    )
    $configFile = Join-Path $env:ProgramFiles 'EqualizerAPO\config\config.txt'
    $boostFile  = Join-Path $script:ArtTuneDBRoot 'boost.txt'
    $null = Backup-EAPOConfigFile

    if (-not (Test-Path -LiteralPath $boostFile)) {
        New-Item -ItemType Directory -Path $script:ArtTuneDBRoot -Force -ErrorAction SilentlyContinue | Out-Null
        $boostLines = @(
            '# ArtTuneDB Output Boost -- set your dB below and save. 0 dB = off.'
            '# config.txt includes this last on every chain; E-APO reloads it live.'
            'Preamp: 0 dB'
        )
        Set-Content -Path $boostFile -Value ($boostLines -join "`r`n") -Force -ErrorAction SilentlyContinue
    }

    $normGuid = { param($g)
        if ([string]::IsNullOrWhiteSpace($g)) { return $null }
        $g = $g.Trim().ToLowerInvariant()
        if ($g -notmatch '^\{') { $g = '{' + $g + '}' }
        return $g
    }
    $g8  = & $normGuid $RenderGuid8
    $g16 = & $normGuid $RenderGuid16
    $device8Line  = if ($g8)  { "Device: Art Tune VB-Audio Virtual Cable $g8" }   else { $null }
    $device16Line = if ($g16) { "Device: Art Tune + VB-Audio Virtual Cable $g16" } else { $null }

    $lib = 'ArtTuneDB\library'
    $configRoot = Join-Path $env:ProgramFiles 'EqualizerAPO\config'
    $resolvesTo = { param($Rel)
        if (-not $Rel) { return $false }
        return Test-Path -LiteralPath (Join-Path $configRoot ($Rel -replace '\\', '\'))
    }
    $resolveIncludes = {
        param($Pieces)
        $out = @()
        foreach ($p in $Pieces) {
            $label = $p.Label
            $rel = $p.Path
            if ($rel -and (& $resolvesTo $rel)) {
                if ($label) { $out += $label }
                $out += "Include: $rel"
            } else {
                if ($label) { $out += $label }
                $out += 'Include: ArtTuneDB\library\'
                Write-Warn "Tune file missing (using placeholder): $rel"
            }
        }
        return $out
    }

    $has16 = [bool]($SixteenChFile)
    $verDir = if ($Game -and $Version) { "$Game\$Version" } else { $null }
    $prePath   = if ($verDir) { "$lib\$verDir\${Game}_${Version}_pre.txt" } else { $null }
    $targetEq  = if ($verDir) { "$lib\$verDir\${Game}_Target_${Version}.txt" } else { $null }
    $postPath  = if ($verDir) { "$lib\$verDir\${Game}_${Version}_post.txt" } else { $null }
    $sixteenPath = if ($has16 -and $verDir) { "$lib\$verDir\$($SixteenChFile -replace '/','\')" } else { $null }
    $eq16Path    = if ($EqFile -and $verDir) { "$lib\$verDir\eq\$($EqFile -replace '/','\')" } else { $null }

    $lines = @()
    $lines += '# ArtTuneDB config.txt'
    $lines += '# Complete each "ArtTuneDB\library\" path below, e.g. ...\BO7\V4\BO7_V4_pre.txt'
    $lines += ''
    $lines += '# ---- 8ch profile (Art Tune) - uses HeSuVi ----'
    if ($device8Line) { $lines += $device8Line }
    $lines += (& $resolveIncludes @(
        @{ Label = '# PRE HESUVI'; Path = $prePath },
        @{ Label = '# DO NOT REMOVE HESUVI - LOAD HESUVI PRESET FOR VERSION #'; Path = 'HeSuVi\hesuvi.txt' },
        @{ Label = '# EQ -- swap for your squig.link EQ (target file is in your version folder)'; Path = $targetEq },
        @{ Label = '# POST HESUVI'; Path = $postPath }
    ))
    $lines += '# OUTPUT BOOST -- set your dB in ArtTuneDB\boost.txt (0 dB = off)'
    $lines += 'Include: ArtTuneDB\boost.txt'

    if ($has16 -and $device16Line) {
        $lines += ''
        $lines += '# ---- 16ch profile (Art Tune +) - no HeSuVi, needs the bundled VST ----'
        $lines += '# ONLY use files with _16ch_ in the name (see "Choose a 16ch Tune.txt").'
        $lines += $device16Line
        $lines += (& $resolveIncludes @(
            @{ Label = '# 16ch TUNE'; Path = $sixteenPath },
            @{ Label = '# EQ -- swap for your squig.link EQ'; Path = $eq16Path }
        ))
        $lines += '# OUTPUT BOOST'
        $lines += 'Include: ArtTuneDB\boost.txt'
    }

    try {
        Set-Content -Path $configFile -Value ($lines -join "`r`n") -Force
        Write-Ok 'config.txt written.'
        return $true
    } catch {
        Write-Err "Could not write config.txt: $($_.Exception.Message)"
        return $false
    }
}

# ── LEQ release time ────────────────────────────────────────────────────────
function Set-ReqReleaseTime {
    param([string]$DeviceGuid, [int]$ReleaseTime)
    if ($ReleaseTime -lt 2 -or $ReleaseTime -gt 7) { return $true }
    $fxKeyPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\$DeviceGuid\FxProperties"
    if (-not (Test-Path -LiteralPath $fxKeyPath)) { Write-Warn "FxProperties not found for $DeviceGuid"; return $false }
    $hex = "03,00,00,00,01,00,00,00,$($ReleaseTime.ToString('x2')),00,00,00"
    $regKeyPath = $fxKeyPath -replace '^HKLM:\\', 'HKEY_LOCAL_MACHINE\'
    $regContent = "Windows Registry Editor Version 5.00`r`n`r`n[$regKeyPath]`r`n"
    $regContent += "`"$script:LeqReleaseTimeKey3`"=hex:$hex`r`n"
    $regContent += "`"$script:LeqReleaseTimeKey1599`"=hex:$hex`r`n"
    $regFile = Join-Path $script:TempPath 'arttune_leq.reg'
    $regContent | Out-File -FilePath $regFile -Encoding ASCII -Force
    try {
        $proc = Start-Process -FilePath "$env:SystemRoot\regedit.exe" -ArgumentList '/s', "`"$regFile`"" -Wait -PassThru -WindowStyle Hidden
        if ($proc.ExitCode -ne 0) { Write-Warn 'regedit LEQ import failed'; return $false }
    } catch { Write-Warn "regedit LEQ import failed: $($_.Exception.Message)"; return $false }
    Remove-Item $regFile -Force -ErrorAction SilentlyContinue
    Write-Ok "LEQ release time set to $ReleaseTime for $DeviceGuid"
    return $true
}

# ── Desktop shortcut + folder README (library install tail) ────────────────
function Write-FolderReadme {
    $artTuneDBDir = $script:ArtTuneDBRoot
    if (-not (Test-Path $artTuneDBDir)) { return }
    try {
        $iconPath = Join-Path $artTuneDBDir 'ArtTuneDB.ico'
        if (-not (Test-Path $iconPath)) {
            Invoke-WebRequest -Uri 'https://cdn.artiswar.io/ArtTuneDBLogo.ico' -OutFile $iconPath -UseBasicParsing -ErrorAction SilentlyContinue
        }
        $card = @(
            'ArtTuneDB - Art of War game audio tuning'
            ''
            '1. Pick your game + version from this library folder.'
            '2. Edit C:\Program Files\EqualizerAPO\config\config.txt Include paths (or use SoundFX Studio).'
            '3. Load the matching HeSuVi preset for your version.'
            '4. LEQ: follow the "LEQ - Release Time" file inside your version folder.'
            ''
            'boost.txt at the ArtTuneDB ROOT sets the final output boost (0 dB = off).'
        )
        Set-Content -Path (Join-Path $artTuneDBDir 'README.txt') -Value ($card -join "`r`n") -Force -ErrorAction SilentlyContinue

        $lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'ArtTuneDB.lnk'
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($lnk)
        $shortcut.TargetPath = $artTuneDBDir
        $shortcut.WorkingDirectory = $artTuneDBDir
        if (Test-Path $iconPath) { $shortcut.IconLocation = "$iconPath,0" }
        $shortcut.Save()
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null
    } catch { Write-Warn "Could not write folder shortcut/readme: $($_.Exception.Message)" }
}

# ══════════════════════════ FULL ROLLBACK ════════════════════════════════════
function Remove-RegValues {
    param([string]$KeyPath, [string[]]$Names)
    foreach ($n in $Names) {
        try { Remove-ItemProperty -LiteralPath $KeyPath -Name $n -ErrorAction Stop } catch { }
    }
}

function Reset-ArtTuneEndpointNames {
    # Capture cable + Voicemeeter guids FIRST (detection needs the renames), then
    # delete the friendly-name/icon overrides and LEQ release-time values so
    # Windows falls back to the stock vendor names like it was never installed.
    $eps = Get-ArtTuneEndpoints
    $vmEps = Get-VoicemeeterEndpoints
    $targets = @()
    if ($eps.Render8)  { $targets += @{ View = 'Render';  Guid = $eps.Render8 } }
    if ($eps.Render16) { $targets += @{ View = 'Render';  Guid = $eps.Render16 } }
    if ($eps.Capture)  { $targets += @{ View = 'Capture'; Guid = $eps.Capture } }
    if ($vmEps.Render)  { $targets += @{ View = 'Render';  Guid = $vmEps.Render } }
    if ($vmEps.Capture) { $targets += @{ View = 'Capture'; Guid = $vmEps.Capture } }

    foreach ($t in $targets) {
        $propsPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\$($t.View)\$($t.Guid)\Properties"
        if (Test-Path -LiteralPath $propsPath) {
            Remove-RegValues -KeyPath $propsPath -Names @($script:PKEY_FRIENDLY, $script:PKEY_ICON)
        }
    }
    # LEQ release time on every render endpoint (we only ever wrote these two)
    if (Test-Path $script:MMDEVICES_RENDER) {
        foreach ($key in Get-ChildItem $script:MMDEVICES_RENDER -ErrorAction SilentlyContinue) {
            $fx = Join-Path $key.PSPath 'FxProperties'
            if (Test-Path $fx) { Remove-RegValues -KeyPath $fx -Names @($script:LeqReleaseTimeKey3, $script:LeqReleaseTimeKey1599) }
        }
    }
    Write-Ok "Endpoint renames/icons and LEQ release-time removed ($($targets.Count) endpoints)."
}

function Set-EndpointDeviceState {
    param([string]$View, [string]$Guid, [int]$State)
    $key = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\$View\$Guid"
    try {
        Set-ItemProperty -LiteralPath $key -Name 'DeviceState' -Value $State -Type DWord -ErrorAction Stop
        return $true
    } catch { return $false }
}

function Hide-UnusedVbEndpoints {
    # Keep only the endpoints the Art Tune stack actually uses and hide the rest
    # (Voicemeeter B2/B3/Aux/VAIO and other VB-CABLE variants) so they stop
    # cluttering Windows app device pickers. Windows hides DeviceState=Disabled
    # endpoints from most applications; we operate on the MMDevices registry so
    # no physical device is ever touched.
    function Test-RetainEndpoint {
        param([string]$name)
        if (-not $name) { return $false }
        $n = $name.ToLowerInvariant()
        if ($n -match 'art tune' -or $n -eq 'normal audio' -or $n -eq 'virtual mix') { return $true }
        if ($n -match 'voicemeeter input' -and $n -notmatch 'aux') { return $true }
        if ($n -match 'voicemeeter out b1' -and $n -notmatch 'aux|virtual') { return $true }
        if ($n -match 'virtual cable' -or $n -match '^cable ' -or $n -match 'cable input|output') { return $true }
        return $false
    }
    $hidden = @()
    foreach ($view in @(@{ N = 'Render'; Flow = $script:MMDEVICES_RENDER }, @{ N = 'Capture'; Flow = $script:MMDEVICES_CAPTURE })) {
        if (-not (Test-Path $view.Flow)) { continue }
        foreach ($key in Get-ChildItem $view.Flow -ErrorAction SilentlyContinue) {
            $propsPath = Join-Path $key.PSPath 'Properties'
            $name = ''
            if (Test-Path $propsPath) {
                $name = "$((Get-ItemProperty -LiteralPath $propsPath -Name $script:PKEY_FRIENDLY -ErrorAction SilentlyContinue).$script:PKEY_FRIENDLY)"
            }
            if (-not $name) {
                $name = "$((Get-ItemProperty -LiteralPath $key.PSPath -Name 'DeviceDesc' -ErrorAction SilentlyContinue).DeviceDesc)"
            }
            if (-not $name) { continue }
            if ($name -notmatch 'voicemeeter|vb-audio|cable|virtual cable') { continue }
            if (Test-RetainEndpoint $name) { continue }
            if (Set-EndpointDeviceState -View $view.N -Guid $key.PSChildName -State 2) { $hidden += "$($view.N):$name" }
        }
    }
    if ($hidden.Count -gt 0) {
        Write-Ok "Hidden $(@($hidden).Count) unused VB-Audio/Voicemeeter endpoints: $($hidden -join '; ')"
    } else {
        Write-Warn 'No unused VB-Audio/Voicemeeter endpoints to hide.'
    }
}

function Show-VbEndpoints {
    # Re-enable every HM Software VB-Audio/Voicemeeter/CABLE endpoint (rollback).
    $shown = @()
    foreach ($view in @(@{ N = 'Render'; Flow = $script:MMDEVICES_RENDER }, @{ N = 'Capture'; Flow = $script:MMDEVICES_CAPTURE })) {
        if (-not (Test-Path $view.Flow)) { continue }
        foreach ($key in Get-ChildItem $view.Flow -ErrorAction SilentlyContinue) {
            $propsPath = Join-Path $key.PSPath 'Properties'
            $name = "$((Get-ItemProperty -LiteralPath $propsPath -Name $script:PKEY_FRIENDLY -ErrorAction SilentlyContinue).$script:PKEY_FRIENDLY)"
            if (-not $name) { $name = "$((Get-ItemProperty -LiteralPath $key.PSPath -Name 'DeviceDesc' -ErrorAction SilentlyContinue).DeviceDesc)" }
            if ($name -notmatch 'voicemeeter|vb-audio|cable|virtual cable|art tune|normal audio|virtual mix') { continue }
            $state = (Get-ItemProperty -LiteralPath $key.PSPath -Name 'DeviceState' -ErrorAction SilentlyContinue).DeviceState
            if ($null -ne $state -and [int]$state -eq 2) {
                if (Set-EndpointDeviceState -View $view.N -Guid $key.PSChildName -State 1) { $shown += "$($view.N):$name" }
            }
        }
    }
    if ($shown.Count -gt 0) { Write-Ok "Re-enabled @($shown.Count) endpoints: $($shown -join '; ')" }
}

function Restore-ArtTuneConfig {
    $configDir = Join-Path $env:ProgramFiles 'EqualizerAPO\config'
    $configFile = Join-Path $configDir 'config.txt'
    $backup = Get-ChildItem -Path $configDir -Filter 'config.txt.bak-*' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($backup) {
        Copy-Item -LiteralPath $backup.FullName -Destination $configFile -Force
        Write-Ok 'config.txt restored from backup.'
    }
    elseif (Test-Path -LiteralPath $configFile) {
        Set-Content -LiteralPath $configFile -Value '# Equalizer APO config.txt -- restored by SoundFX Studio rollback.' -Force -ErrorAction SilentlyContinue
        Write-Ok 'config.txt reset to default.'
    }
    else { Write-Warn 'No config.txt to restore.' }
}

function Invoke-SilentUninstall {
    param([string]$Name, [string]$Exe, [string]$Switch = '/S')
    if (-not (Test-Path -LiteralPath $Exe)) { Write-Warn "$Name not found, skipping: $Exe"; return }
    try {
        Write-Step "Uninstalling $Name..."
        $proc = Start-Process -FilePath $Exe -ArgumentList $Switch -PassThru -WindowStyle Hidden
        if (-not $proc.WaitForExit(90000)) {
            Write-Warn "$Name uninstaller did not exit in 90s -- force-killing."
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            return
        }
        Start-Sleep -Seconds 2
        if ($proc.ExitCode -eq 0) { Write-Ok "$Name uninstalled." }
        else { Write-Warn "$Name uninstaller returned exit code $($proc.ExitCode)." }
    } catch { Write-Warn "$Name uninstall failed: $($_.Exception.Message)" }
}

function Remove-VbAudioDevices {
    # VB setup uninstallers can stall on hidden dialogs; remove the software
    # devices natively so the endpoints are gone regardless.
    $targets = @(Get-PnpDevice -ErrorAction SilentlyContinue |
        Where-Object { $_.FriendlyName -match 'VB-Audio' -and $_.InstanceId -match '^ROOT\\MEDIA\\' })
    if (-not $targets) { Write-Ok 'No VB-Audio software devices to remove.'; return }
    foreach ($t in $targets) {
        pnputil /remove-device "$($t.InstanceId)" 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Warn "Could not remove $($t.InstanceId)" }
        else { Write-Ok "Removed $($t.InstanceId)" }
    }
}

function Invoke-VbAudioDriverCleanup {
    # Remove leftover VB-Audio driver packages from the DriverStore (best effort).
    try {
        $raw = pnputil /enum-drivers 2>$null
        $block = @(); $published = @()
        foreach ($line in $raw) {
            if ($line -match '^\s*$') { $block = @(); continue }
            $block += $line
            if ($line -match 'Published Name:\s*(oem\d+\.inf)') {
                if (($block -join "`n") -match 'Provider Name:\s*VB-Audio') { $published += $matches[1] }
            }
        }
        foreach ($inf in ($published | Select-Object -Unique)) {
            Write-Step "Removing leftover VB-Audio driver package $inf..."
            $out = pnputil /delete-driver "$inf" /uninstall /force 2>&1
            if ($LASTEXITCODE -ne 0) { Write-Warn "Could not remove driver package $inf : $out" }
            else { Write-Ok "Driver package $inf removed." }
        }
        if (-not $published) { Write-Ok 'No leftover VB-Audio driver packages found.' }
    } catch { Write-Warn "Driver cleanup skipped: $($_.Exception.Message)" }
}

function Remove-VoicemeeterNative {
    # VB-Audio's Uninstall.exe ignores /S and pops a "Remove" dialog needing a
    # click (and can stall / BSOD). Remove Voicemeeter natively instead: stop
    # the kernel services, delete driver packages, remove software devices,
    # folders and registry - fully silent.
    Write-Step 'Removing Voicemeeter natively (silent, no interactive uninstaller)...'
    foreach ($svc in 'VBAudioVACMME', 'VBAudioVACMME64', 'VBAudioVACMME32', 'VBAudioVMME') {
        Stop-Service -Name $svc -Force -ErrorAction SilentlyContinue
        sc.exe delete $svc 2>$null | Out-Null
    }
    Start-Sleep -Milliseconds 500
    Remove-VbAudioDevices
    Invoke-VbAudioDriverCleanup
    foreach ($dir in @("C:\Program Files\VB\Voicemeeter", "C:\Program Files (x86)\VB\Voicemeeter",
                       "C:\Program Files\VB\Voicemeeter Banana", "C:\Program Files (x86)\VB\Voicemeeter Banana",
                       "C:\Program Files\VB\Voicemeeter Potato", "C:\Program Files (x86)\VB\Voicemeeter Potato")) {
        if (Test-Path -LiteralPath $dir) {
            Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            if (-not (Test-Path -LiteralPath $dir)) { Write-Ok "Removed $dir" }
        }
    }
    foreach ($rk in @("HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}",
                      "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}",
                      "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:VoicemeeterBanana {17359A74-1236-5467}",
                      "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:VoicemeeterPotato {17359A74-1236-5467}",
                      'HKLM:\SOFTWARE\VB-Audio', 'HKLM:\SOFTWARE\WOW6432Node\VB-Audio')) {
        if (Test-Path -LiteralPath $rk) {
            Remove-Item -LiteralPath $rk -Recurse -Force -ErrorAction SilentlyContinue
            if (-not (Test-Path -LiteralPath $rk)) { Write-Ok "Removed registry key $rk" }
        }
    }
    foreach ($menu in @((Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Voicemeeter'),
                        (Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Voicemeeter Banana'),
                        (Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Voicemeeter Potato'))) {
        if (Test-Path -LiteralPath $menu) {
            Remove-Item -LiteralPath $menu -Recurse -Force -ErrorAction SilentlyContinue
            if (-not (Test-Path -LiteralPath $menu)) { Write-Ok "Removed $menu" }
        }
    }
}

function Invoke-ArtTuneRollback {
    Write-Step 'FULL ROLLBACK: restoring machine to pre-ArtTune state.'

    # 1. config.txt
    Restore-ArtTuneConfig

    # 2. remove ArtTuneDB library / HeSuVi / JSFX / VST folders first (before E-APO goes)
    $folders = @(
        "$($script:ArtTuneDBRoot)",
        (Join-Path $env:ProgramFiles 'EqualizerAPO\config\HeSuVi'),
        (Join-Path $env:ProgramFiles 'VSTPlugins\ReaPlugs\JS\Effects\ArtTuneKit'),
        (Join-Path $env:ProgramFiles 'VSTPlugins\ArtTuneKit'),
        (Join-Path $env:ProgramData 'ArtTune')
    )
    foreach ($f in $folders) {
        if (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Recurse -Force -ErrorAction SilentlyContinue; Write-Ok "Removed $f" }
    }
    $desktopLnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'ArtTuneDB.lnk'
    if (Test-Path $desktopLnk) { Remove-Item $desktopLnk -Force -ErrorAction SilentlyContinue; Write-Ok 'Removed desktop ArtTuneDB shortcut.' }

    # 3. endpoint names/icons + LEQ values (deleting overrides restores stock names)
    Write-Step 'Resetting endpoint names, icons and LEQ release time...'
    Reset-ArtTuneEndpointNames
    Show-VbEndpoints

    # 4. LEQ Control Panel (has no uninstaller -- remove manually)
    Stop-Process -Name 'LEQControlPanel' -Force -ErrorAction SilentlyContinue
    $leqDest = Join-Path $env:LOCALAPPDATA 'Programs\LEQControlPanel'
    if (Test-Path -LiteralPath $leqDest) {
        Remove-Item -LiteralPath $leqDest -Recurse -Force -ErrorAction SilentlyContinue
        Write-Ok "Removed LEQ Control Panel ($leqDest)"
    }
    foreach ($menu in @(
        (Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\LEQ Control Panel.lnk'),
        (Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\LEQ Control Panel'),
        (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\LEQ Control Panel.lnk')
    )) {
        if (Test-Path -LiteralPath $menu) { Remove-Item -LiteralPath $menu -Recurse -Force -ErrorAction SilentlyContinue }
    }
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    try { Remove-ItemProperty -LiteralPath $runKey -Name 'LEQControlPanel' -ErrorAction SilentlyContinue } catch { }

    # 5. run the NSIS uninstallers
    Stop-Process -Name 'voicemeeter' -Force -ErrorAction SilentlyContinue
    Stop-Process -Name 'Voicemeeter' -Force -ErrorAction SilentlyContinue
    Invoke-SilentUninstall -Name 'ReaPlugs' -Exe (Join-Path $env:ProgramFiles 'VSTPlugins\ReaPlugs\ReaPlugs-Uninst.exe')
    Invoke-SilentUninstall -Name 'Equalizer APO' -Exe (Join-Path $env:ProgramFiles 'EqualizerAPO\Uninstall.exe')

    # VB-CABLE / Voicemeeter: their NSIS setups are interactive and can stall
    # silently, so always follow up with a native device/driver removal.
    $vbcSetup = Get-ChildItem 'C:\Program Files\VB\CABLE', 'C:\Program Files (x86)\VB\CABLE' -Filter 'VBCABLE_Setup*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($vbcSetup) { Invoke-SilentUninstall -Name 'VB-CABLE' -Exe $vbcSetup.FullName }

    if ($SkipVoicemeeter) {
        Write-Ok 'Voicemeeter removal handed to the master installer (native silent).'
    } else {
        Remove-VoicemeeterNative
    }

    # 6. force-remove any remaining VB-Audio software devices, folders and
    # registry keys so stock state holds even if the NSIS uninstallers stalled.
    Remove-VbAudioDevices
    foreach ($dir in @("C:\Program Files\VB\CABLE", "C:\Program Files (x86)\VB\CABLE",
                       "C:\Program Files\VB\Voicemeeter", "C:\Program Files (x86)\VB\Voicemeeter")) {
        if (Test-Path -LiteralPath $dir) {
            Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            if (-not (Test-Path -LiteralPath $dir)) { Write-Ok "Removed $dir" }
        }
    }
    foreach ($rk in @("HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$vmRegKey",
                      "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\$vmRegKey",
                      'HKLM:\SOFTWARE\VB-Audio',
                      'HKLM:\SOFTWARE\WOW6432Node\VB-Audio')) {
        if (Test-Path -LiteralPath $rk) {
            Remove-Item -LiteralPath $rk -Recurse -Force -ErrorAction SilentlyContinue
            if (-not (Test-Path -LiteralPath $rk)) { Write-Ok "Removed registry key $rk" }
        }
    }
    Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run\LEQControlPanel' -ErrorAction SilentlyContinue

    # 7. leftover driver packages
    Invoke-VbAudioDriverCleanup

    # 8. restart audio so Windows rebuilds the endpoint graph from scratch
    Restart-AudioServices
    Write-Ok 'Rollback complete. System restored to stock audio.'
}

# ══════════════════════════ MAIN ════════════════════════════════════════════
$failed = $false

if ($UninstallEverything -or $UninstallStack -or $UninstallLibrary -or $ResetEndpoints) {
    if ($ResetEndpoints -and -not $UninstallEverything -and -not $UninstallStack -and -not $UninstallLibrary) {
        Write-Step 'Reset endpoint names, icons and LEQ release time...'
        Reset-ArtTuneEndpointNames
        Show-VbEndpoints
        Restart-AudioServices
        Write-Host '[ARTTUNE] RESULT:OK'
        exit 0
    }
    if ($UninstallLibrary -and -not $UninstallEverything -and -not $UninstallStack) {
        Write-Step 'Remove ArtTuneDB library, HeSuVi, JSFX and VST plugins...'
        foreach ($f in @(
            "$($script:ArtTuneDBRoot)",
            (Join-Path $env:ProgramFiles 'EqualizerAPO\config\HeSuVi'),
            (Join-Path $env:ProgramFiles 'VSTPlugins\ReaPlugs\JS\Effects\ArtTuneKit'),
            (Join-Path $env:ProgramFiles 'VSTPlugins\ArtTuneKit'),
            (Join-Path $env:ProgramData 'ArtTune'))) {
            if (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Recurse -Force -ErrorAction SilentlyContinue; Write-Ok "Removed $f" }
        }
        Write-Host '[ARTTUNE] RESULT:OK'
        exit 0
    }
    Invoke-ArtTuneRollback
    if ($failed) {
        Write-Host '[ARTTUNE] RESULT:FAILED'
        exit 1
    }
    Write-Host '[ARTTUNE] RESULT:OK'
    exit 0
}

if ($InstallStack) {
    # VB-CABLE
    if (-not (Test-VBCableInstalled)) {
        $zip = Join-Path $script:TempPath 'VBCableSetup.zip'
        Write-Step 'Downloading VB-CABLE...'
        if (Get-UrlToFile -Url 'https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip' -OutFile $zip -TimeoutSeconds 120) {
            if (-not (Install-VBCable -ZipPath $zip)) { $failed = $true }
        } else { $failed = $true }
    } else { Write-Ok 'VB-CABLE already installed' }

    # Voicemeeter
    $vm = Test-VoicemeeterEdition
    if (-not $vm.Standard) {
        $zip = Join-Path $script:TempPath 'VoicemeeterSetup.zip'
        Write-Step 'Downloading Voicemeeter...'
        $vmUrl = 'https://download.vb-audio.com/Download_CABLE/VoicemeeterSetup_v1122.zip'
        if (Get-UrlToFile -Url $vmUrl -OutFile $zip -TimeoutSeconds 135) {
            if (-not (Install-Voicemeeter -ZipPath $zip)) { $failed = $true }
        } else { $failed = $true }
    } else { Write-Ok 'Voicemeeter already installed' }

    # ReaPlugs
    if (-not (@(Get-ChildItem "${env:ProgramFiles}\VSTPlugins\ReaPlugs\*.dll" -ErrorAction SilentlyContinue).Count -ge 5)) {
        $exe = Join-Path $script:TempPath 'reaplugs_x64.exe'
        Write-Step 'Downloading ReaPlugs...'
        if (Get-UrlToFile -Url 'https://www.reaper.fm/reaplugs/reaplugs236_x64-install.exe' -OutFile $exe -TimeoutSeconds 120) {
            if (-not (Install-ReaPlugs -InstallerPath $exe)) { $failed = $true }
        } else { $failed = $true }
    } else { Write-Ok 'ReaPlugs already installed' }

    # Equalizer APO (always reruns so the Device Selector reopens)
    $exe = Join-Path $script:TempPath 'EqualizerAPO64.exe'
    Write-Step 'Downloading Equalizer APO...'
    if (Get-UrlToFile -Url 'https://cdn.artiswar.io/other-installers/EqualizerAPO-x64-1.4.2.exe' -OutFile $exe -TimeoutSeconds 120 -FallbackUrl 'https://sourceforge.net/projects/equalizerapo/files/1.4/EqualizerAPO64-1.4.exe/download') {
        if (-not (Install-Eapo -InstallerPath $exe)) { $failed = $true }
    } else { $failed = $true }

    # HeSuVi
    $heSuViDir = Join-Path $env:ProgramFiles 'EqualizerAPO\config\HeSuVi'
    if (-not (Test-Path (Join-Path $heSuViDir 'hesuvi.txt'))) {
        $exe = Join-Path $script:TempPath 'HeSuVi.exe'
        Write-Step 'Downloading HeSuVi...'
        if (Get-UrlToFile -Url 'https://cdn.artiswar.io/other-installers/HeSuVi_2.0.0.1.exe' -OutFile $exe -TimeoutSeconds 120 -FallbackUrl 'https://sourceforge.net/projects/hesuvi/files/HeSuVi_2.0.0.1.exe/download') {
            if (-not (Install-HeSuVi -InstallerPath $exe)) { $failed = $true }
        } else { $failed = $true }
    } else { Write-Ok 'HeSuVi already installed' }

    # LEQ Control Panel
    $scExe = Join-Path $env:LOCALAPPDATA 'Programs\LEQControlPanel\LEQControlPanel.exe'
    if (-not (Test-Path -LiteralPath $scExe)) {
        $src = Join-Path $script:TempPath 'LEQControlPanel.exe'
        Write-Step 'Downloading LEQ Control Panel...'
        $leqUrl = $null
        try {
            $headers = @{ 'Accept' = 'application/vnd.github+json' }
            $release = Invoke-RestMethod 'https://api.github.com/repos/ArtIsWar/LEQControlPanel/releases/latest' -Headers $headers -ErrorAction Stop
            $asset = $release.assets | Where-Object { $_.name -eq 'LEQControlPanel.exe' } | Select-Object -First 1
            if ($asset) { $leqUrl = $asset.browser_download_url }
        } catch { }
        if (-not (Get-UrlToFile -Url $leqUrl -OutFile $src -TimeoutSeconds 600 -FallbackUrl 'https://cdn.artiswar.io/LEQControlPanel.exe')) {
            # Primary GitHub URL empty (offline resolver); the IWR above already
            # used the CDN fallback path.
            if (-not (Test-Path $src)) { $failed = $true }
        }
        if (-not $failed) { if (-not (Install-SoundControl -SourcePath $src)) { $failed = $true } }
    } else { Write-Ok 'LEQ Control Panel already installed' }

    # SkipExisting passed: remove downloaded installers we no longer need.
    Remove-Item (Join-Path $script:TempPath 'VBCableSetup.zip') -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $script:TempPath 'VoicemeeterSetup.zip') -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $script:TempPath 'reaplugs_x64.exe') -Force -ErrorAction SilentlyContinue
}

if ($InstallLibrary) {
    $libRoot = Join-Path $script:ArtTuneDBRoot 'library'
    $fresh = -not (Test-Path (Join-Path $libRoot 'version.txt'))
    if (-not (Install-ArtTuneLibrary -SkipBackup:$fresh)) { $failed = $true }
    else {
        if (-not (Install-ArtTuneHRIR)) {}
        if (-not (Install-JsfxPlugins)) {}
        if (-not (Install-VstPlugins)) {}
        Write-FolderReadme
    }
}

if ($SetupEndpoints) {
    $null = Set-ArtTuneEndpoints -IncludeVoicemeeter $RenameVoicemeeter
    Hide-UnusedVbEndpoints
}

if ($Game -and $Version) {
    $eps = Get-ArtTuneEndpoints
    Write-ArtTuneConfig -RenderGuid8 $eps.Render8 -RenderGuid16 $eps.Render16 -Game $Game -Version $Version -SixteenChFile $SixteenChFile -EqFile $EqFile | Out-Null
    if ($LeqReleaseTime -ge 2) {
        $target = if ($SixteenChFile -and $eps.Render16) { $eps.Render16 } else { $eps.Render8 }
        if ($target) { $null = Set-ReqReleaseTime -DeviceGuid $target -ReleaseTime $LeqReleaseTime }
    }
}

if ($failed) {
    Write-Host '[ARTTUNE] RESULT:FAILED'
    exit 1
}
Write-Host '[ARTTUNE] RESULT:OK'
exit 0