<#
SoundFX Studio - guided installer.

Usage:
  irm https://raw.githubusercontent.com/MakaVeli2202/SoundFX-Studio/main/install.ps1 | iex
  powershell -ExecutionPolicy Bypass -File install.ps1

Command line (non-interactive):
  powershell -ExecutionPolicy Bypass -File install.ps1 -Install
  powershell -ExecutionPolicy Bypass -File install.ps1 -FreshInstall
  powershell -ExecutionPolicy Bypass -File install.ps1 -Uninstall
  powershell -ExecutionPolicy Bypass -File install.ps1 -UninstallAll
  powershell -ExecutionPolicy Bypass -File install.ps1 -ArtTuneRollback
  powershell -ExecutionPolicy Bypass -File install.ps1 -CheckUpdate
  Combine with -Silent (no prompts) and -NoLaunch.

Downloads the latest stable release from GitHub and installs it silently
(Inno Setup /VERYSILENT). Existing installations are upgraded in place unless
you choose the fresh install / uninstall options.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Theme: SoundFX Studio accent colors, mapped to console-safe values.
# AccentBlue #00D4FF -> Cyan | AccentGreen #22C55E -> Green | AccentRose #F43F5E -> Red
# AccentPurple #7B3FFF -> Magenta | AccentWarning #F59E0B -> Yellow | Border #34506E -> DarkCyan
$C = @{
    Blue    = 'Cyan'
    Green   = 'Green'
    Rose    = 'Red'
    Purple  = 'Magenta'
    Warning = 'Yellow'
    Border  = 'DarkCyan'
    Muted   = 'DarkGray'
}

$script:repo       = 'MakaVeli2202/SoundFX-Studio'
$script:AppName    = 'SoundFX Studio'
$script:AppExe     = Join-Path $env:ProgramFiles "SoundFX Studio\SoundFXStudio.exe"
$script:AppDataDir = Join-Path $env:APPDATA 'SoundFXStudio'
$script:AppId      = '{A2B3C4D5-E6F7-4812-9ABC-DEF012345678}'
$script:SfxVersion = '1.0.1'
$script:BoxWidth   = 76
$script:ScreenWidth = 120
$script:BoxMargin  = ' ' * [Math]::Floor(($script:ScreenWidth - $script:BoxWidth - 2) / 2)
$script:ArtTunePs1 = Join-Path $env:ProgramFiles "SoundFX Studio\Assets\ArtTune\ArtTune-OneClick.ps1"

# ---------------------------------------------------------------- helpers ---

function Center-Text {
    param([string]$Text, [int]$Width)
    $spaces = [Math]::Max(0, $Width - $Text.Length)
    $leftPad = [Math]::Floor($spaces / 2)
    return (' ' * $leftPad) + $Text + (' ' * ($spaces - $leftPad))
}

function Write-CenteredBlock {
    param([hashtable[]]$Lines, [int]$ScreenWidth = $script:ScreenWidth)
    $maxLen = ($Lines | ForEach-Object { $_.Text.Length } | Measure-Object -Maximum).Maximum
    $margin = ' ' * [Math]::Max(0, [Math]::Floor(($ScreenWidth - $maxLen) / 2))
    foreach ($l in $Lines) {
        Write-Host "$margin$($l.Text)" -ForegroundColor $l.Color
    }
    return $margin
}

function Write-BoxLine {
    param([string]$Text = '', [string]$Color = 'DarkGray')
    Write-Host "$($script:BoxMargin)$([char]0x2551)  $Text$(' ' * [Math]::Max(0, $script:BoxWidth - $Text.Length - 2))$([char]0x2551)" -ForegroundColor $Color
}

function Write-BoxTop { Write-Host "$($script:BoxMargin)$([char]0x2554)$([string]::new([char]0x2550, $script:BoxWidth))$([char]0x2557)" -ForegroundColor $C.Blue }
function Write-BoxMid  { Write-Host "$($script:BoxMargin)$([char]0x2560)$([string]::new([char]0x2550, $script:BoxWidth))$([char]0x2563)" -ForegroundColor $C.Blue }
function Write-BoxBottom { Write-Host "$($script:BoxMargin)$([char]0x255A)$([string]::new([char]0x2550, $script:BoxWidth))$([char]0x255D)" -ForegroundColor $C.Blue }

function Write-Banner {
    Write-Host ''
    Write-BoxTop
    Write-Host "$($script:BoxMargin)$([char]0x2551)$(Center-Text $script:AppName $script:BoxWidth)$([char]0x2551)" -ForegroundColor $C.Blue
    Write-Host "$($script:BoxMargin)$([char]0x2551)$(Center-Text 'A lightweight SFX / voice effects suite' $script:BoxWidth)$([char]0x2551)" -ForegroundColor White
    Write-Host "$($script:BoxMargin)$([char]0x2551)$(Center-Text 'github.com/MakaVeli2202/SoundFX-Studio' $script:BoxWidth)$([char]0x2551)" -ForegroundColor $C.Muted
    Write-Host "$($script:BoxMargin)$([char]0x2551)$(' ' * $script:BoxWidth)$([char]0x2551)" -ForegroundColor $C.Blue
    Write-Host "$($script:BoxMargin)$([char]0x2551)$(Center-Text 'Guided install: app + Voicemeeter driver stack.' $script:BoxWidth)$([char]0x2551)" -ForegroundColor $C.Muted
    Write-Host "$($script:BoxMargin)$([char]0x2551)$(Center-Text "v$($script:SfxVersion)  $([char]0x2022)  $([char]0x2713) upgrade keeps your sounds & settings" $script:BoxWidth)$([char]0x2551)" -ForegroundColor $C.Border
    Write-BoxBottom
    Write-Host ''
}

function Write-Warning {
    Write-BoxTop
    Write-Host "$($script:BoxMargin)$([char]0x2551)$(Center-Text "$([char]0x26A0) WARNING" $script:BoxWidth)$([char]0x2551)" -ForegroundColor $C.Warning
    Write-BoxMid
    Write-BoxLine 'This installer installs and manages audio components on this PC.' $C.Warning
    Write-BoxLine 'Close all apps and save your work before continuing.' $C.Warning
    Write-BoxLine 'SoundFX Studio uses Voicemeeter to route your sound effects.' $C.Warning
    Write-BoxLine 'Audio may be interrupted briefly during setup.' $C.Warning
    Write-BoxBottom
    Write-Host ''
}

function Write-Step {
    param([int]$n, [string]$text)
    $pct = [Math]::Floor($n / $script:TotalSteps * 100)
    $bar = ''
    $width = 20
    $filled = [Math]::Floor($width * $n / $script:TotalSteps)
    for ($i = 0; $i -lt $width; $i++) { $bar += if ($i -lt $filled) { $([char]0x2588) } else { $([char]0x2591) } }
    Write-Host "$($script:BoxMargin)[$bar] $pct%  $text" -ForegroundColor $C.Muted
}

function Write-Ok {
    param([string]$text)
    Write-Host "$($script:BoxMargin)$([char]0x2713) $text" -ForegroundColor $C.Green
}

function Write-Info {
    param([string]$text)
    Write-Host "$($script:BoxMargin)  $text" -ForegroundColor $C.Muted
}

function Write-ErrorLine {
    param([string]$text)
    Write-Host "$($script:BoxMargin)[!] $text" -ForegroundColor $C.Rose
}

function Invoke-WaitSpinner {
    param(
        [string]$Message,
        [scriptblock]$Until,
        [int]$TimeoutSeconds = 120
    )
    $frames = @('|', '/', '-', '\')
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $i = 0
    $pad = 100
    while ($sw.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $frame = $frames[$i % $frames.Count]
        $line = "$($script:BoxMargin)$frame $Message"
        Write-Host "`r$($line.PadRight($pad))" -NoNewline -ForegroundColor $C.Muted
        if (& $Until) {
            Write-Host "`r$($script:BoxMargin)$([char]0x2713) $Message$(' ' * [Math]::Max(0, $pad - $Message.Length - 4))" -ForegroundColor $C.Green
            return $true
        }
        Start-Sleep -Milliseconds 200
        $i++
    }
    Write-Host "`r$($script:BoxMargin)! $Message (timed out)$(' ' * [Math]::Max(0, $pad - $Message.Length - 4))" -ForegroundColor $C.Warning
    return $false
}

function Write-ProcessBar {
    # Real-time animated fill bar while a process runs. '$Message'.
    param(
        [System.Diagnostics.Process]$Process,
        [string]$Message
    )
    $width = 24
    $pad = 100
    $i = 0
    while (-not $Process.HasExited) {
        $i = ($i + 1) % ($width + 1)
        $bar = ''
        for ($j = 0; $j -lt $width; $j++) { $bar += if ($j -lt $i) { $([char]0x2588) } else { $([char]0x2591) } }
        $pct = [Math]::Floor($i / $width * 100)
        Write-Host "`r$($script:BoxMargin)[$bar] $pct%  $Message$(' ' * [Math]::Max(0, $pad - $Message.Length - 30))" -NoNewline -ForegroundColor $C.Blue
        Start-Sleep -Milliseconds 150
    }
    $full = ([string][char]0x2588) * $width
    Write-Host "`r$($script:BoxMargin)[$full] 100%  $Message$(' ' * [Math]::Max(0, $pad - $Message.Length - 30))" -ForegroundColor $C.Green
}

function Test-IsAdmin {
    return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Resolve-VersionTag {
    # Strips a leading 'v' from a version tag so 'v1.2.3' and '1.2.3' compare fine.
    param([string]$Tag)
    if (-not $Tag) { return '' }
    return $Tag.Trim().TrimStart('v', 'V').Trim()
}

function Get-InstalledVersion {
    $info = Get-UninstallInfo
    if ($null -ne $info -and $info.Version -ne 'unknown') { return $info.Version }
    return $null
}

function Test-LatestRelease {
    # Returns @{ Available; Latest; Current } or $null if the check fails.
    try {
        $release = Invoke-RestMethod "https://api.github.com/repos/$($script:repo)/releases/latest"
        $latest = Resolve-VersionTag $release.tag_name
        $current = Resolve-VersionTag (Get-InstalledVersion)
        if (-not $latest) { return $null }
        if (-not $current) { return @{ Available = $false; Latest = $latest; Current = $null } }
        $vNew = [version]$latest
        $vCur = [version]$current
        return @{ Available = ($vNew -gt $vCur); Latest = $latest; Current = $current }
    } catch {
        return $null
    }
}

function Get-LaunchChoice {
    $exe = $script:AppExe
    if (-not (Test-Path $exe)) { return }
    while ($true) {
        Write-Host ''
        $null = Write-CenteredBlock @(
            @{ Text = '[l] Launch SoundFX Studio now'; Color = 'White' }
            @{ Text = '[m] Back to main menu'; Color = $C.Muted }
            @{ Text = '[q] Quit'; Color = $C.Muted }
        )
        Write-Host ''
        Write-Host "$($script:BoxMargin)Choice: " -ForegroundColor $C.Warning -NoNewline
        $key = Read-Host
        switch ($key.ToLower()) {
            'l' {
                $null = Start-Process $exe
                Write-Host "$($script:BoxMargin)Launched $($script:AppName)." -ForegroundColor $C.Green
                return 'quit'
            }
            'm' { return 'mainMenu' }
            'q' { return 'quit' }
            default { Write-Host "$($script:BoxMargin)Invalid choice." -ForegroundColor $C.Rose }
        }
    }
}

# --------------------------------------------------------- uninstall helpers ---

# StrictMode-safe registry value read: returns $null when the key or value is
# missing instead of throwing "property cannot be found".
function Get-RegValue {
    param([string]$KeyPath, [string]$ValueName)
    try {
        $key = Get-Item -LiteralPath $KeyPath -ErrorAction Stop
        return $key.GetValue($ValueName)
    } catch {
        return $null
    }
}

function Get-UninstallInfo {
    <#
    Finds the installed copy's uninstall info (uninstall exe + version).
    Returns $null when SoundFX Studio is not installed.
    #>
    $keys = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$($script:AppId)_is1",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\$($script:AppId)_is1",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$($script:AppId)_is1"
    )
    foreach ($key in $keys) {
        if (Test-Path $key) {
            $version = Get-RegValue $key 'DisplayVersion'
            if ([string]::IsNullOrWhiteSpace($version)) { $version = 'unknown' }
            return [pscustomobject]@{
                Key     = $key
                Version = $version
                UninstallString = [string](Get-RegValue $key 'UninstallString')
                QuietUninstallString = [string](Get-RegValue $key 'QuietUninstallString')
            }
        }
    }
    # Fallback: locate unins000.exe next to the app any way possible
    if (Test-Path "$env:ProgramFiles\SoundFX Studio\unins000.exe") {
        return [pscustomobject]@{
            Key = $null
            Version = '(program files)'
            UninstallString = "`"$env:ProgramFiles\SoundFX Studio\unins000.exe`""
            QuietUninstallString = "`"$env:ProgramFiles\SoundFX Studio\unins000.exe`" /VERYSILENT"
        }
    }
    return $null
}

function Get-InstalledVmEdition {
    $keys = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:VoicemeeterBanana {17359A74-1236-5467}",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:VoicemeeterPotato {17359A74-1236-5467}"
    )
    foreach ($key in $keys) {
        if (Test-Path $key) {
            $name = [string](Get-RegValue $key 'DisplayName')
            if ($name -match 'Lookback|Banana|Potato') {
                return [pscustomobject]@{ Present = $true; Paid = $true; Name = $name }
            }
            return [pscustomobject]@{ Present = $true; Paid = $false; Name = $name }
        }
    }
    if (Test-Path 'C:\Program Files (x86)\VB\Voicemeeter\voicemeeter.exe') {
        return [pscustomobject]@{ Present = $true; Paid = $false; Name = 'Voicemeeter (Standard)' }
    }
    return [pscustomobject]@{ Present = $false; Paid = $false; Name = '' }
}

# ------------------------------------------------------------ Art Tune rollback ---

function Invoke-ArtTuneRollback {
    # Runs the app's own full ArtTune rollback (VB-CABLE, Voicemeeter, E-APO,
    # ReaPlugs, HeSuVi, LEQ, library, endpoint names, config, driver packages).
    if (-not (Test-Path -LiteralPath $script:ArtTunePs1)) {
        Write-Host "$($script:BoxMargin)Art Tune rollback script not found at:" -ForegroundColor $C.Warning
        Write-Host "$($script:BoxMargin)  $($script:ArtTunePs1)" -ForegroundColor $C.Muted
        Write-Host "$($script:BoxMargin)Install the app first (menu [1]), or run the app's own 'Reset / Art Tune rollout'." -ForegroundColor $C.Muted
        return $false
    }
    Write-Host ''
    Write-Host "$($script:BoxMargin)Rolling back Art Tune stack (VB-CABLE, Voicemeeter, E-APO, ReaPlugs, HeSuVi, LEQ, library, endpoints)..." -ForegroundColor $C.Warning
    try {
        $proc = Start-Process powershell.exe -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$script:ArtTunePs1`"", '-UninstallEverything' -PassThru -Wait -WindowStyle Hidden
        if ($proc.ExitCode -eq 0) {
            Write-Ok 'Art Tune stack rolled back cleanly (audio returned to stock).'
            return $true
        }
        Write-Host "$($script:BoxMargin)Art Tune rollback returned exit code $($proc.ExitCode)." -ForegroundColor $C.Warning
        return $false
    } catch {
        Write-Host "$($script:BoxMargin)Art Tune rollback failed: $($_.Exception.Message)" -ForegroundColor $C.Rose
        return $false
    }
}

# ------------------------------------------------------------- install path ---

function Invoke-Install {
    Write-Host ''
    Write-Host "$($script:BoxMargin)Fetching latest $($script:AppName) release..." -ForegroundColor $C.Muted
    $release = Invoke-RestMethod "https://api.github.com/repos/$($script:repo)/releases/latest"

    $asset = $release.assets |
        Where-Object { $_.name -like '*Setup*.exe' } |
        Select-Object -First 1

    if ($null -eq $asset) {
        throw "No setup executable found in release $($release.tag_name)."
    }

    $installer = Join-Path $env:TEMP $asset.name
    $installerSize = [math]::Round($asset.size / 1MB, 1)

    Write-Host ''
    Write-Ok ('Latest release  ' + $release.tag_name)
    Write-Info ('Size            ' + $installerSize + ' MB')

    $script:TotalSteps = 2

    Write-Step 1 'Downloading package'
    $totalBytes = $asset.size
    $progressId = 100
    $dl = Start-Job -ArgumentList $asset.browser_download_url, $installer -ScriptBlock {
        param($uri, $out)
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $uri -OutFile $out -UseBasicParsing
    }
    while ($dl.State -eq 'Running') {
        $have = if (Test-Path $installer) { (Get-Item $installer).Length } else { 0 }
        $pct = if ($totalBytes -gt 0) { [math]::Floor($have / $totalBytes * 100) } else { 0 }
        Write-Progress -Activity "Downloading $($script:AppName)" -Status ("{0:N1} MB / {1:N1} MB" -f ($have / 1MB), ($totalBytes / 1MB)) -PercentComplete $pct -Id $progressId
        Start-Sleep -Milliseconds 200
    }
    Receive-Job $dl | Out-Null
    Remove-Job $dl
    Write-Progress -Activity "Downloading $($script:AppName)" -Completed -Id $progressId
    Write-Ok "$($asset.name) downloaded"

    Write-Step 2 "Installing $($script:AppName)"
    $p = Start-Process -FilePath $installer -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -PassThru
    Write-ProcessBar -Process $p -Message "Installing $($script:AppName)…"
    if ($p.ExitCode -ne 0) { Write-ErrorLine "Installer returned exit code $($p.ExitCode)." }
    Write-Ok "$($script:AppName) $($release.tag_name) installed"
    Remove-Item $installer -ErrorAction SilentlyContinue

    Write-Host ''
    Write-BoxTop
    Write-Host "$($script:BoxMargin)$([char]0x2551)$(Center-Text "$([char]0x2713)  SETUP COMPLETE" $script:BoxWidth)$([char]0x2551)" -ForegroundColor $C.Green
    Write-BoxMid
    Write-BoxLine "$([char]0x2713) $($script:AppName) installed" $C.Green
    Write-BoxLine "$([char]0x2713) Voicemeeter handled by the installer" $C.Green
    Write-BoxLine '' $C.Muted
    Write-BoxLine 'Press the Windows key and type "SoundFX Studio" to launch.' $C.Muted
    Write-BoxBottom
    Write-Host ''
}

# ----------------------------------------------------------- uninstall paths ---

function Stop-VmAudioService {
    # The VB virtual cable kernel driver is the main BSOD risk during removal.
    try {
        $svc = Get-CimInstance Win32_Service -Filter "Name='VBAudioVACMME'" -ErrorAction SilentlyContinue
        if ($null -ne $svc) {
            Stop-Service 'VBAudioVACMME' -Force -ErrorAction SilentlyContinue
            sc.exe delete VBAudioVACMME | Out-Null
        }
    } catch { /* best effort */ }
}

function Get-VbDriverPackages {
    # oemN.inf package names belonging to VB-Audio / Voicemeeter audio drivers.
    try {
        $out = pnputil /enum-drivers 2>$null
        $blocks = @()
        $cur = [System.Collections.Generic.List[string]]::new()
        foreach ($line in $out) {
            if ([string]::IsNullOrWhiteSpace($line)) {
                if ($cur.Count) { $blocks += ,($cur -join "`n"); $cur = [System.Collections.Generic.List[string]]::new() }
                continue
            }
            $cur.Add([string]$line)
        }
        if ($cur.Count) { $blocks += ,($cur -join "`n") }
        foreach ($b in $blocks) {
            if ($b -match 'VB-Audio|Voicemeeter') {
                $m = [regex]::Match($b, '(?m)^Published Name:\s*(.+)$')
                if ($m.Success) { $m.Groups[1].Value.Trim() }
            }
        }
    } catch { }
}

function Invoke-VmManualRemoval {
    # Fully silent removal of VB-Audio Voicemeeter WITHOUT its GUI uninstaller
    # (VB uninstallers ignore /S and pop a "Remove" dialog). Mirrors what the
    # app's ArtTune rollback does, all hands-free.
    Write-Host "$($script:BoxMargin)Removing $($script:VmEdition) files, driver + registry silently..." -ForegroundColor $C.Muted
    $ok = $true

    Stop-VmAudioService

    foreach ($inf in Get-VbDriverPackages) {
        try { pnputil /delete-driver "$inf" /uninstall /force 2>$null | Out-Null } catch { }
    }

    $vmFolders = @(
        @{ Path = 'C:\Program Files (x86)\VB\Voicemeeter'; Guard = 'Voicemeeter' },
        @{ Path = 'C:\Program Files\VB\Voicemeeter'; Guard = 'Voicemeeter' }
    )
    foreach ($folder in $vmFolders) {
        if (Test-Path -LiteralPath $folder.Path) {
            try {
                if ($folder.Path -match "[$([regex]::Escape('VB\Voicemeeter'))]") {
                    Remove-Item -LiteralPath $folder.Path -Recurse -Force -ErrorAction Stop
                    Write-Ok "Removed $($folder.Path)"
                }
            } catch { $ok = $false }
        }
    }

    $vmKeys = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:VoicemeeterBanana {17359A74-1236-5467}",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:VoicemeeterPotato {17359A74-1236-5467}"
    )
    foreach ($vk in $vmKeys) {
        try { Remove-Item -Path $vk -Recurse -Force -ErrorAction Stop } catch { }
    }

    $startMenu = @(
        "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Voicemeeter",
        "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\VB-Audio"
    )
    foreach ($dir in $startMenu) {
        if (Test-Path -LiteralPath $dir) {
            try { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction Stop } catch { }
        }
    }

    return $ok
}

function Invoke-VmSilentUninstall {
    # Tries the recorded uninstaller (hidden, 90s cap, force-kill on stall);
    # falls back to a fully silent manual removal when it's a VB-style GUI
    # uninstaller or wasn't recorded.
    $vmKeys = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}"
    )
    $vmUn = $null
    foreach ($vk in $vmKeys) {
        if (Test-Path $vk) {
            $q = [string](Get-RegValue $vk 'QuietUninstallString')
            if (-not [string]::IsNullOrWhiteSpace($q)) { $vmUn = $q; break }
        }
    }
    if (-not $vmUn) {
        foreach ($vk in $vmKeys) {
            if (Test-Path $vk) {
                $u = [string](Get-RegValue $vk 'UninstallString')
                if (-not [string]::IsNullOrWhiteSpace($u)) { $vmUn = $u; break }
            }
        }
    }

    if ($vmUn) {
        try {
            $vmUnPath = ($vmUn -replace '"', '').Trim()
            if (Test-Path -LiteralPath $vmUnPath) {
                Write-Host "$($script:BoxMargin)" -NoNewline
                Write-Host "Uninstalling $($script:VmEdition) (silent)..." -ForegroundColor $C.Muted
                $silentArg = if ($vmUnPath -match 'unins000\.exe$') { '/VERYSILENT' } else { '/S' }
                $pp = Start-Process -FilePath $vmUnPath -ArgumentList $silentArg, '/NORESTART' -PassThru -WindowStyle Hidden
                $waited = $pp.WaitForExit(90000)
                if (-not $waited) {
                    Write-WarnMessage "$($script:VmEdition) uninstaller stalled - force-killing and removing silently."
                    try { $pp.Kill() } catch { }
                }
                if ($pp.ExitCode -eq 0) { return $true }
                Write-Host "$($script:BoxMargin)$($script:VmEdition) uninstaller exited $($pp.ExitCode) - removing silently." -ForegroundColor $C.Warning
            }
        } catch {
            Write-Host "$($script:BoxMargin)$($script:VmEdition) uninstaller failed - removing silently." -ForegroundColor $C.Warning
        }
    }

    return Invoke-VmManualRemoval
}

function Remove-App {
    param(
        [bool]$RemoveData,
        [bool]$RemoveVoicemeeter,
        [switch]$SkipRestartPrompt
    )
    $removed = @()
    $kept = @()

    $info = Get-UninstallInfo
    if ($null -eq $info) {
        Write-Host "$($script:BoxMargin)$($script:AppName) is not installed; nothing to remove." -ForegroundColor $C.Warning
    } else {
        Write-Host ""
        Write-Host "$($script:BoxMargin)Uninstalling $($script:AppName) $($info.Version)..." -ForegroundColor $C.Muted
        $un = if ($info.QuietUninstallString) { $info.QuietUninstallString } elseif ($info.UninstallString) { $info.UninstallString } else { $null }
        if ($un) {
            try {
                $unPath = ($info.UninstallString -replace '"', '')
                if ($unPath -match '\.exe$') {
                    $p = Start-Process -FilePath $unPath -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -PassThru -Wait
                    if ($p.ExitCode -eq 0) { $removed += "$($script:AppName) $($info.Version)" }
                    else { $kept += "$($script:AppName) (uninstaller returned $($p.ExitCode))" }
                } else {
                    $kept += "$($script:AppName) (uninstaller not a .exe)"
                }
            } catch {
                $kept += "$($script:AppName) (uninstall failed: $($_.Exception.Message))"
            }
        } else {
            $kept += "$($script:AppName) (no uninstaller found)"
        }
    }

    if ($RemoveData -and (Test-Path $script:AppDataDir)) {
        Remove-Item $script:AppDataDir -Recurse -Force -ErrorAction SilentlyContinue
        $removed += 'App data (sounds, settings, bindings)'
    } elseif (-not $RemoveData -and (Test-Path $script:AppDataDir)) {
        $kept += 'App data (sounds, settings)'
    }

    if ($RemoveVoicemeeter) {
        $vm = Get-InstalledVmEdition
        if ($vm.Present) {
            $script:VmEdition = $vm.Name
            if ($vm.Paid) {
                $kept += "$($vm.Name) (licensed edition - not touched)"
            } elseif (Invoke-VmSilentUninstall) {
                $removed += $vm.Name
            } else {
                $kept += "$($vm.Name) (removal incomplete)"
            }
        }
    } elseif ((Test-Path $script:AppExe) -or ($null -ne (Get-UninstallInfo))) {
        $vm = Get-InstalledVmEdition
        if ($vm.Present) { $kept += $vm.Name }
    }

    Write-UninstallCompletion -Removed $removed -Kept $kept

    if ($removed | Where-Object { $_ -match 'Voicemeeter' }) {
        if ($SkipRestartPrompt) {
            Write-Host "$($script:BoxMargin)Restart at your convenience to fully clear the removed audio driver." -ForegroundColor $C.Warning
        } else {
            Write-Host "$($script:BoxMargin)A restart is recommended to fully clear the removed audio driver." -ForegroundColor $C.Warning
            Write-Host "$($script:BoxMargin)Restart now? [Y/n]: " -ForegroundColor $C.Warning -NoNewline
            $restart = Read-Host
            if ($restart -ne 'n' -and $restart -ne 'N') { Restart-Computer -Force }
        }
    }
}

function Write-WarnMessage {
    param([string]$Text)
    Write-Host "$($script:BoxMargin)$Text" -ForegroundColor $C.Warning
}

function Write-UninstallCompletion {
    param([string[]]$Removed, [string[]]$Kept)
    Write-Host ''
    Write-BoxTop
    Write-Host "$($script:BoxMargin)$([char]0x2551)$(Center-Text "$([char]0x2713)  UNINSTALL COMPLETE" $script:BoxWidth)$([char]0x2551)" -ForegroundColor $C.Green
    Write-BoxMid
    Write-BoxLine '' $C.Muted
    if ($Removed.Count -gt 0) {
        Write-BoxLine 'Removed:' $C.Green
        foreach ($item in $Removed) { Write-BoxLine "  $([char]0x2713) $item" $C.Green }
    } else {
        Write-BoxLine 'Nothing was removed.' $C.Warning
    }
    if ($Kept.Count -gt 0) {
        Write-BoxLine '' $C.Muted
        Write-BoxLine 'Kept:' $C.Muted
        foreach ($item in $Kept) { Write-BoxLine "  - $item" $C.Muted }
        Write-BoxLine '' $C.Muted
        Write-BoxLine 'You can change these in Apps & Features or the app.' $C.Muted
    }
    Write-BoxLine '' $C.Muted
    Write-BoxLine 'Set your Windows default audio device back to your' $C.Muted
    Write-BoxLine 'headphones/DAC from the Sound settings if needed.' $C.Muted
    Write-BoxBottom
    Write-Host ''
}

# ------------------------------------------------------------- thank you path ---

function Show-ThankYou {
    Write-Host ''
    Write-BoxTop
    Write-Host "$($script:BoxMargin)$([char]0x2551)$(Center-Text 'Thank You' $script:BoxWidth)$([char]0x2551)" -ForegroundColor $C.Blue
    Write-BoxMid
    Write-BoxLine 'This project relies on tools built by talented' $C.Muted
    Write-BoxLine 'developers. Show them some love:' $C.Muted
    Write-BoxLine '' $C.Muted
    $credits = @(
        @{ Num = '1'; Tool = 'Voicemeeter'; Dev = 'Vincent Burel (VB-Audio)'; Url = 'https://vb-audio.com/Voicemeeter/' }
        @{ Num = '2'; Tool = 'Inno Setup'; Dev = 'jrsoftware'; Url = 'https://jrsoftware.org/isinfo.php' }
        @{ Num = '3'; Tool = '.NET / WPF'; Dev = 'Microsoft'; Url = 'https://dotnet.microsoft.com' }
        @{ Num = '4'; Tool = 'SoundFX Studio'; Dev = 'MakaVeli2202'; Url = 'https://github.com/MakaVeli2202/SoundFX-Studio' }
    )
    foreach ($credit in $credits) {
        Write-BoxLine "[$($credit.Num)] $($credit.Tool) - $($credit.Dev)" $C.Muted
    }
    Write-BoxLine '' $C.Muted
    Write-BoxBottom
    Write-Host ''

    while ($true) {
        $null = Write-CenteredBlock @(
            @{ Text = '[1-4] Open developer page'; Color = 'White' }
            @{ Text = '[m] Back to main menu'; Color = $C.Purple }
            @{ Text = '[q] Quit'; Color = $C.Muted }
        )
        Write-Host ''
        Write-Host "$($script:BoxMargin)Choice: " -ForegroundColor $C.Warning -NoNewline
        $key = Read-Host
        $num = 0
        if ([int]::TryParse($key, [ref]$num) -and $num -ge 1 -and $num -le $credits.Count) {
            $null = Start-Process $credits[$num - 1].Url
            Write-Host "$($script:BoxMargin)Opened $($credits[$num - 1].Dev) in browser." -ForegroundColor $C.Green
        }
        elseif ($key -eq 'm' -or $key -eq 'M') { return 'mainMenu' }
        elseif ($key -eq 'q' -or $key -eq 'Q') { return 'quit' }
        else { Write-Host "$($script:BoxMargin)Invalid choice." -ForegroundColor $C.Rose }
    }
}

# --------------------------------------------------------- update check path ---

function Show-UpdateStatus {
    # Prints a one-line update status (used by the menu + startup).
    $check = Test-LatestRelease
    if ($null -eq $check) {
        Write-Host "$($script:BoxMargin)Update check failed (offline or GitHub unreachable)." -ForegroundColor $C.Muted
        return
    }
    if (-not $check.Current) {
        Write-Host "$($script:BoxMargin)Latest release: $($check.Latest)" -ForegroundColor $C.Muted
        return
    }
    if ($check.Available) {
        Write-Host "$($script:BoxMargin)Update available: $($check.Current) -> $($check.Latest)" -ForegroundColor $C.Warning
    } else {
        Write-Host "$($script:BoxMargin)You are up to date ($($check.Current))." -ForegroundColor $C.Green
    }
}

# ---------------------------------------------------------------- dispatch ---

# --- parse command line ---
$wantInstall        = $args -contains '-Install' -or $args -contains '-install'
$wantFresh          = $args -contains '-FreshInstall' -or $args -contains '-freshinstall'
$wantUninstall      = $args -contains '-Uninstall' -or $args -contains '-uninstall'
$wantUninstallAll   = $args -contains '-UninstallAll' -or $args -contains '-uninstallall'
$wantArtTuneRoll    = $args -contains '-ArtTuneRollback' -or $args -contains '-arttunerollback'
$wantCheckUpdate    = $args -contains '-CheckUpdate' -or $args -contains '-checkupdate'
$silent             = $args -contains '-Silent' -or $args -contains '-silent'
$noLaunch           = $args -contains '-NoLaunch' -or $args -contains '-nolaunch'

function Confirm-Prompt {
    # Silent mode answers NO (or YES for Fresh flow where everything is expected).
    param([string]$Question, [bool]$DefaultYes = $false)
    if ($silent) { return $DefaultYes }
    Write-Host "$($script:BoxMargin)$Question" -ForegroundColor $C.Warning -NoNewline
    $a = Read-Host
    if ($DefaultYes) { return ($a -ne 'n' -and $a -ne 'N') }
    return ($a -eq 'y' -or $a -eq 'Y')
}

# --- non-interactive CLI flows ---
if ($wantCheckUpdate) {
    $r = Test-LatestRelease
    if ($null -eq $r) { Write-Host '0'; exit 2 }
    Write-Host $r.Latest
    exit 0
}

if ($wantArtTuneRoll) {
    $ok = Invoke-ArtTuneRollback
    exit (-not $ok)
}

if ($wantFresh) {
    if (-not (Test-IsAdmin)) { Write-ErrorLine 'Elevated PowerShell is required for a fresh install.'; exit 1 }
    Write-Host "$($script:BoxMargin)FRESH INSTALL: removing everything, then installing the latest version." -ForegroundColor $C.Warning
    $okRoll = Invoke-ArtTuneRollback
    Remove-App -RemoveData $true -RemoveVoicemeeter $true -SkipRestartPrompt
    if ($silent) { exit 0 }
    Invoke-Install
    if (-not $noLaunch) { $null = Start-Process $script:AppExe }
    exit 0
}

if ($wantInstall) {
    if (-not (Test-IsAdmin)) { Write-ErrorLine 'Elevated PowerShell is required.'; exit 1 }
    Invoke-Install
    if (-not $noLaunch) { $null = Start-Process $script:AppExe -ErrorAction SilentlyContinue }
    exit 0
}

if ($wantUninstallAll) {
    if (-not (Test-IsAdmin)) { Write-ErrorLine 'Elevated PowerShell is required.'; exit 1 }
    Remove-App -RemoveData $true -RemoveVoicemeeter $true
    exit 0
}

if ($wantUninstall) {
    if (-not (Test-IsAdmin)) { Write-ErrorLine 'Elevated PowerShell is required.'; exit 1 }
    Remove-App -RemoveData $false -RemoveVoicemeeter $false
    exit 0
}

# --- interactive main ---

Write-Banner
Write-Warning

if (-not (Test-IsAdmin)) {
    Write-ErrorLine 'Elevated (Run as Administrator) PowerShell is required.'
    Write-ErrorLine 'Right-click PowerShell -> "Run as administrator", then re-run.'
    Write-Host ''
    exit 1
}

Show-UpdateStatus

:mainMenu while ($true) {
    Write-Host ''
    $menuItems = @(
        @{ Text = 'What would you like to do?'; Color = $C.Blue }
        @{ Text = ''; Color = 'White' }
        @{ Text = '[1] Install SoundFX Studio - download and set up (start here)'; Color = 'White' }
        @{ Text = '    Installs the app and Voicemeeter. Upgrades keep your sounds and settings.'; Color = $C.Muted }
        @{ Text = '[2] Upgrade / Repair - re-run the installer, keep everything'; Color = 'White' }
        @{ Text = '[3] Uninstall app - remove the program, keep your sounds and settings'; Color = 'White' }
        @{ Text = '[4] Uninstall everything - app, data, and Voicemeeter'; Color = 'White' }
        @{ Text = '[5] Fresh install - remove everything, then install the latest version'; Color = 'White' }
        @{ Text = '[a] Reset / roll back Art Tune stack - clean uninstall of the audio tuning'; Color = $C.Purple }
        @{ Text = '[u] Check for updates'; Color = $C.Purple }
        @{ Text = '[t] Thank you - credits & developer links'; Color = $C.Purple }
        @{ Text = '[Q] Quit'; Color = $C.Muted }
    )
    $menuMargin = Write-CenteredBlock $menuItems
    Write-Host ''

    while ($true) {
        Write-Host "$menuMargin" -NoNewline
        Write-Host 'Choice: ' -ForegroundColor $C.Warning -NoNewline
        $selection = Read-Host

        if ($selection -eq 't' -or $selection -eq 'T') {
            $result = Show-ThankYou
            if ($result -eq 'quit') { break mainMenu }
            continue mainMenu
        }
        if ($selection -eq 'u' -or $selection -eq 'U') { Show-UpdateStatus; continue mainMenu }
        if ($selection -eq 'a' -or $selection -eq 'A') { $null = Invoke-ArtTuneRollback; continue mainMenu }
        if ($selection -eq 'q' -or $selection -eq 'Q') { break mainMenu }
        $num = 0
        if ([int]::TryParse($selection, [ref]$num) -and $num -ge 1 -and $num -le 5) {
            $menuChoice = $num
            break
        }
        Write-Host "$($menuMargin)Invalid choice. Enter 1-5, a, u, t, or q." -ForegroundColor $C.Rose
    }

    switch ($menuChoice) {
        3 {
            Remove-App -RemoveData $false -RemoveVoicemeeter $false
            continue mainMenu
        }
        4 {
            $vm = Get-InstalledVmEdition
            $wantVm = $false
            if ($vm.Present) {
                Write-Host ''
                if ($vm.Paid) {
                    Write-Host "$($script:BoxMargin)$($vm.Name) detected - a licensed edition SoundFX did not install." -ForegroundColor $C.Warning
                    Write-Host "$($script:BoxMargin)Leaving it untouched." -ForegroundColor $C.Warning
                } else {
                    Write-Host "$($script:BoxMargin)$($vm.Name) detected (installed by SoundFX Studio)." -ForegroundColor $C.Warning
                    Write-Host "$($script:BoxMargin)Remove it too? [Y/n]: " -ForegroundColor $C.Warning -NoNewline
                    $vmAns = Read-Host
                    $wantVm = ($vmAns -ne 'n' -and $vmAns -ne 'N')
                }
            }
            Remove-App -RemoveData $true -RemoveVoicemeeter $wantVm
            continue mainMenu
        }
        5 {
            Write-Host ''
            Write-Host "$($script:BoxMargin)FRESH INSTALL: removing everything, then installing the latest version." -ForegroundColor $C.Warning
            $null = Invoke-ArtTuneRollback
            Remove-App -RemoveData $true -RemoveVoicemeeter $true -SkipRestartPrompt
            Invoke-Install
            $r = Get-LaunchChoice
            if ($r -eq 'quit') { break mainMenu }
            continue mainMenu
        }
        default {
            Invoke-Install
            $r = Get-LaunchChoice
            if ($r -eq 'quit') { break mainMenu }
            continue mainMenu
        }
    }
}

Write-Host "$($script:BoxMargin)Quit. Nothing changed." -ForegroundColor $C.Muted
Write-Host ''