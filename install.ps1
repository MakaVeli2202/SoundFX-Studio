<#
    SoundFX Studio - installer.

    USAGE
      One-liner (self-elevates through UAC):
        irm https://raw.githubusercontent.com/MakaVeli2202/SoundFX-Studio/main/install.ps1 | iex

      From a file / any terminal:
        powershell -ExecutionPolicy Bypass -File install.ps1

    NON-INTERACTIVE
      powershell -ExecutionPolicy Bypass -File install.ps1 -Install
      powershell -ExecutionPolicy Bypass -File install.ps1 -Upgrade
      powershell -ExecutionPolicy Bypass -File install.ps1 -Uninstall
      powershell -ExecutionPolicy Bypass -File install.ps1 -Status
      powershell -ExecutionPolicy Bypass -File install.ps1 -CheckUpdate
      ... plus -Silent (no prompts), -NoLaunch, -RemoveData, -RemoveVoicemeeter.

    WHAT IT DOES
      Nothing has to be installed first: the setup carries the app
      (self-contained, so no .NET runtime) plus the Voicemeeter installer, and
      handles the rest. Upgrade is install-over-the-top, so sounds, settings and
      key bindings are kept.
#>

[CmdletBinding()]
param(
    [switch]$Install,
    [switch]$Upgrade,
    [switch]$Uninstall,
    [switch]$CheckUpdate,
    [switch]$Status,
    [switch]$RemoveData,
    [switch]$RemoveVoicemeeter,
    [switch]$Silent,
    [switch]$NoLaunch,
    [switch]$Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# ------------------------------------------------------------------ config ---

$script:Repo        = 'MakaVeli2202/SoundFX-Studio'
$script:AssetName   = 'SoundFXStudio-Setup.exe'
$script:ApiBase     = "https://api.github.com/repos/$script:Repo"
$script:DownloadUrl = "https://github.com/$script:Repo/releases/latest/download/$script:AssetName"
$script:ScriptUrl   = "https://raw.githubusercontent.com/$script:Repo/main/install.ps1"
$script:AppName     = 'SoundFX Studio'
$script:AppFolder   = Join-Path $env:ProgramFiles $script:AppName
$script:AppExe      = Join-Path $script:AppFolder 'SoundFXStudio.exe'
$script:AppDataDir  = Join-Path $env:APPDATA 'SoundFXStudio'
$script:ScriptVer   = '1.2.0'

# Console-safe accents (UI hexes -> console colors).
$script:Accent = 'Cyan'
$script:Good   = 'Green'
$script:Warn   = 'Yellow'
$script:Bad    = 'Red'
$script:Alt    = 'Magenta'
$script:Dim    = 'DarkGray'
$script:Line   = 'DarkCyan'

# `irm | iex` runs from memory, so there is no file to re-launch. Captured at
# script scope because $MyInvocation inside a function would describe the
# function, not this script. Read through PSObject: an IEX'd scriptblock has no
# Path property at all, and touching it under StrictMode is a fatal error.
$script:SelfPath = $null
if ($MyInvocation.MyCommand) {
    $pathProperty = $MyInvocation.MyCommand.PSObject.Properties['Path']
    if ($pathProperty) { $script:SelfPath = [string]$pathProperty.Value }
}
$script:Width    = 72
$script:Margin   = '  '
$script:ExitCode = 0

# --------------------------------------------------------------- elevation ---

function Test-Elevated {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($id)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-CurrentTokens {
    # Rebuilds the command line from the bound switches so the elevated re-run
    # repeats the exact same job ($args is empty once a param block has bound).
    $tokens = @()
    if ($Install)             { $tokens += '-Install' }
    if ($Upgrade)             { $tokens += '-Upgrade' }
    if ($Uninstall)           { $tokens += '-Uninstall' }
    if ($CheckUpdate)         { $tokens += '-CheckUpdate' }
    if ($Status)              { $tokens += '-Status' }
    if ($RemoveData)          { $tokens += '-RemoveData' }
    if ($RemoveVoicemeeter)   { $tokens += '-RemoveVoicemeeter' }
    if ($Silent)              { $tokens += '-Silent' }
    if ($NoLaunch)            { $tokens += '-NoLaunch' }
    $tokens += @($args)
    return ,$tokens
}

function Start-ElevatedRerun {
    param([string[]]$Tokens)

    $self = $script:SelfPath
    if ([string]::IsNullOrWhiteSpace($self) -or -not (Test-Path -LiteralPath $self)) {
        $self = Join-Path $env:TEMP 'SoundFXStudio-install.ps1'
        Write-Host ''
        Write-Info 'Saving a copy of this script so it can restart as admin...'
        Invoke-WebRequest -Uri $script:ScriptUrl -OutFile $self -UseBasicParsing
    }

    $argumentList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$self`"") + $Tokens
    $style = if ($Silent) { 'Hidden' } else { 'Normal' }
    $null = Start-Process -FilePath 'powershell.exe' -Verb RunAs -WindowStyle $style -ArgumentList $argumentList
    Write-Host ''
    Write-Ok  'Reopened as administrator in a new window.'
    Write-Dim 'This window can be closed.'
}

# A read-only run must not drag the user through a UAC prompt. The gate itself
# sits at the bottom of the file, once every helper it needs exists.
$script:ReadOnly = $CheckUpdate -or $Status -or $Help

# -------------------------------------------------------------------- UI ---

function Write-Rule {
    param([string]$Text = '', [string]$Color = $script:Line)
    if ([string]::IsNullOrEmpty($Text)) {
        Write-Host "$($script:Margin)$([string][char]0x2500 * $script:Width)" -ForegroundColor $Color
        return
    }
    Write-Host "$($script:Margin)$Text" -ForegroundColor $Color
}

function Center-Text {
    param([string]$Text, [int]$Width)
    $pad = [Math]::Max(0, $Width - $Text.Length)
    $left = [Math]::Floor($pad / 2)
    return (' ' * $left) + $Text + (' ' * ($pad - $left))
}

function Center-Margin {
    try {
        $w = $Host.UI.RawUI.WindowSize.Width
        if ($w -gt ($script:Width + 6)) { return ' ' * [Math]::Floor(($w - $script:Width) / 2) }
    }
    catch { /* not a real console */ }
    return $script:Margin
}

function Write-Banner {
    param([string]$Tagline = 'A lightweight SFX / voice effects suite')
    $m    = Center-Margin
    $fill = ([string][char]0x2500) * $script:Width
    $tl   = [string][char]0x250C; $tr = [string][char]0x2510
    $ml   = [string][char]0x251C; $mr = [string][char]0x2524
    $bl   = [string][char]0x2514; $br = [string][char]0x2518
    $v    = [string][char]0x2502
    $name = $script:AppName.ToUpper()
    $ver  = "installer v$($script:ScriptVer)"

    Write-Host ''
    Write-Host "$m$tl$fill$tr" -ForegroundColor $script:Accent
    Write-Host "$m$v$(Center-Text $name $script:Width)$v" -ForegroundColor $script:Accent
    Write-Host "$m$v$(Center-Text $Tagline $script:Width)$v" -ForegroundColor White
    Write-Host "$m$ml$fill$mr" -ForegroundColor $script:Line
    Write-Host "$m$v$(Center-Text $ver $script:Width)$v" -ForegroundColor $script:Dim
    Write-Host "$m$bl$fill$br" -ForegroundColor $script:Accent
    Write-Host ''
}

function Write-Section {
    param([string]$Text)
    Write-Host ''
    Write-Host "$($script:Margin)$Text" -ForegroundColor $script:Accent
    Write-Rule
}

function Write-Ok       { param([string]$Text) Write-Host "$($script:Margin)$([char]0x2714) $Text" -ForegroundColor $script:Good }
function Write-Info     { param([string]$Text) Write-Host "$($script:Margin)  $Text" -ForegroundColor White }
function Write-Dim      { param([string]$Text) Write-Host "$($script:Margin)  $Text" -ForegroundColor $script:Dim }
function Write-WarnLine { param([string]$Text) Write-Host "$($script:Margin)$([char]0x26A0) $Text" -ForegroundColor $script:Warn }
function Write-ErrorLine{ param([string]$Text) Write-Host "$($script:Margin)$([char]0x2716) $Text" -ForegroundColor $script:Bad }

function Write-Pair {
    param([string]$Label, [string]$Value, [string]$Color = 'White')
    $label = ($Label.PadRight(14))
    Write-Host "$($script:Margin)$label $Value" -ForegroundColor $Color
}

function Write-ProgressBar {
    param([int]$Percent, [string]$Message)
    if ([Console]::IsOutputRedirected) {
        if ($Percent -ge 100) { Write-Host "$($script:Margin)  $Message" -ForegroundColor $script:Dim }
        return
    }
    $barWidth = 22
    $filled = [Math]::Min($barWidth, [Math]::Floor($barWidth * $Percent / 100))
    $bar = ([string][char]0x2588) * $filled + ([string][char]0x2501) * ($barWidth - $filled)
    Write-Host "`r$($script:Margin)  [$bar] $($Percent.ToString().PadLeft(3))%  $Message " -NoNewline -ForegroundColor $script:Accent
}

function Write-ActivityBar {
    # Indeterminate sweep for work that reports no progress (setup, winget).
    param([System.Diagnostics.Process]$Process, [string]$Message)
    if ([Console]::IsOutputRedirected) {
        $Process.WaitForExit()
        return
    }
    $barWidth = 22
    $sweep = 0
    while (-not $Process.HasExited) {
        $bar = ([string][char]0x2501) * $barWidth
        for ($k = $sweep; $k -lt [Math]::Min($sweep + 4, $barWidth); $k++) {
            $bar = $bar.Substring(0, $k) + [char]0x2588 + $bar.Substring($k + 1)
        }
        Write-ProgressBar -Percent 100 -Message $Message
        $sweep = ($sweep + 1) % ($barWidth - 3)
        Start-Sleep -Milliseconds 120
    }
    Write-ProgressBar -Percent 100 -Message $Message
    Write-Host ''
}

function Read-Choice {
    param([string]$Prompt = 'Select', [string[]]$Valid)
    Write-Host "$($script:Margin)" -NoNewline
    Write-Host "$Prompt  " -ForegroundColor $script:Warn -NoNewline
    Write-Host '> ' -ForegroundColor $script:Alt -NoNewline
    $answer = (Read-Host).Trim().ToLowerInvariant()
    if ($Valid -contains $answer -or $Valid -contains '*') { return $answer }
    Write-ErrorLine "Invalid choice. Enter: $($Valid -join ', ')"
    return ''
}

function Confirm {
    param([string]$Question, [bool]$DefaultYes = $false)
    if ($Silent) { return $DefaultYes }
    $hint = if ($DefaultYes) { '[Y/n]' } else { '[y/N]' }
    Write-Host "$($script:Margin)$Question $hint " -ForegroundColor $script:Warn -NoNewline
    $a = (Read-Host).Trim().ToLowerInvariant()
    if ($DefaultYes) { return ($a -ne 'n') }
    return ($a -eq 'y')
}

# ------------------------------------------------------------------ state ---

function Get-RegValue {
    # StrictMode-safe: $null instead of "property cannot be found" when absent.
    param([string]$KeyPath, [string]$ValueName)
    try { return (Get-Item -LiteralPath $KeyPath -ErrorAction Stop).GetValue($ValueName) }
    catch { return $null }
}

function Get-AppInstall {
    # Registered uninstall entry for SoundFX Studio, or $null when not installed.
    $id = '{A2B3C4D5-E6F7-4812-9ABC-DEF012345678}_is1'
    $keys = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$id",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\$id",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$id"
    )
    foreach ($key in $keys) {
        if (-not (Test-Path -LiteralPath $key)) { continue }
        $version = [string](Get-RegValue $key 'DisplayVersion')
        $uninstall = [string](Get-RegValue $key 'UninstallString')
        $quiet = [string](Get-RegValue $key 'QuietUninstallString')
        $exe = ($uninstall -replace '"', '') -replace '\s+/VERYSILENT.*$', ''
        if (-not $exe -or -not (Test-Path -LiteralPath $exe)) {
            $candidate = Join-Path $script:AppFolder 'unins000.exe'
            if (Test-Path -LiteralPath $candidate) { $exe = $candidate }
        }
        return [pscustomobject]@{
            Key             = $key
            Version         = if ($version) { $version } else { 'unknown' }
            UninstallExe    = $exe
            QuietUninstall  = if ($quiet) { $quiet } else { $uninstall }
        }
    }

    # No registry entry (e.g. a manual copy): still offer to clean it up.
    if (Test-Path -LiteralPath (Join-Path $script:AppFolder 'unins000.exe')) {
        $candidate = Join-Path $script:AppFolder 'unins000.exe'
        return [pscustomobject]@{
            Key          = $null
            Version      = 'unknown'
            UninstallExe = $candidate
            QuietUninstall = $null
        }
    }
    return $null
}

function Get-LatestRelease {
    try {
        $release = Invoke-RestMethod -Uri "$script:ApiBase/releases/latest" -Headers @{ 'User-Agent' = 'SoundFXStudio-Installer' } -UseBasicParsing
        $tag = [string]$release.tag_name
        if (-not $tag) { return $null }
        return [pscustomobject]@{
            Tag     = $tag.Trim().TrimStart('v', 'V')
            Url     = if ($release.assets | Where-Object { $_.name -like '*Setup*.exe' }) {
                          $script:DownloadUrl
                      } else { '' }
            Size    = 0
        }
    }
    catch { return $null }
}

function Get-VoicemeeterState {
    $keys = @(
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:VoicemeeterBanana {17359A74-1236-5467}',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:VoicemeeterPotato {17359A74-1236-5467}'
    )
    foreach ($key in $keys) {
        if (-not (Test-Path -LiteralPath $key)) { continue }
        $name = [string](Get-RegValue $key 'DisplayName')
        $paid = $name -match 'Banana|Potato|Lookback'
        return [pscustomobject]@{ Present = $true; Paid = $paid; Name = if ($name) { $name } else { 'Voicemeeter' } }
    }
    if (Test-Path -LiteralPath 'C:\Program Files (x86)\VB\Voicemeeter\voicemeeter.exe') {
        return [pscustomobject]@{ Present = $true; Paid = $false; Name = 'Voicemeeter' }
    }
    return [pscustomobject]@{ Present = $false; Paid = $false; Name = '' }
}

function Stop-AppIfRunning {
    $running = @(Get-Process -Name 'SoundFXStudio' -ErrorAction SilentlyContinue)
    if ($running.Count -eq 0) { return }
    Write-Info "Closing $($script:AppName)..."
    foreach ($p in $running) {
        try { Stop-Process -Id $p.Id -Force -ErrorAction Stop } catch { }
    }
    Start-Sleep -Milliseconds 800
}

# ---------------------------------------------------------------- install ---

function Save-Release {
    # Streams the asset by hand: Invoke-WebRequest crawls at ~100 KB/s here,
    # while a plain HttpWebRequest pull runs at ~8 MB/s.
    param([pscustomobject]$Release)

    $url = $Release.Url
    if (-not $url) { $url = $script:DownloadUrl }
    $target = Join-Path $env:TEMP $script:AssetName
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue }

    [System.Net.ServicePointManager]::SecurityProtocol =
        [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12

    Write-Info "Downloading $script:AssetName (v$($Release.Tag))..."
    $request = [System.Net.HttpWebRequest]::Create($url)
    $request.UserAgent = 'SoundFXStudio-Installer'
    $request.Timeout = 60000
    $request.ReadWriteTimeout = 120000
    $response = $request.GetResponse()
    try {
        $expected = [int64]$response.ContentLength
        $stream = $response.GetResponseStream()
        $file = [System.IO.File]::Create($target)
        try {
            $buffer = New-Object byte[] 262144
            $have = 0L
            while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $file.Write($buffer, 0, $read)
                $have += $read
                $percent = if ($expected -gt 0) { [int][Math]::Min(99, [Math]::Floor(100 * $have / $expected)) } else { 0 }
                $mb = [Math]::Round($have / 1MB, 1)
                Write-ProgressBar -Percent $percent -Message "$mb MB"
            }
        }
        finally { $file.Dispose() }
    }
    finally { $response.Dispose() }

    Write-ProgressBar -Percent 100 -Message 'download complete'
    Write-Host ''
    return $target
}

function Install-Release {
    <#
      Install and Upgrade share this path: the Inno Setup package installs over
      an existing copy, so sounds, settings and key bindings survive. The only
      difference between the two is the wording around it.
    #>
    param([switch]$IsUpgrade)

    $installed = Get-AppInstall

    if ($IsUpgrade -and $null -eq $installed) {
        Write-WarnLine 'Nothing is installed yet - continuing with a first-time install.'
        $IsUpgrade = $false
    }

    $release = Get-LatestRelease
    if ($null -eq $release) {
        Write-WarnLine 'Could not reach GitHub to look up the latest release.'
        if (-not (Confirm 'Download the latest release anyway?' $true)) { return $false }
        $release = [pscustomobject]@{ Tag = 'latest'; Url = $script:DownloadUrl; Size = 0 }
    }
    if (-not $release.Url) {
        throw "Release v$($release.Tag) has no *$script:AssetName asset - build and upload the setup first."
    }

    if ($IsUpgrade) {
        Write-Ok "Upgrade: keeping your sounds, settings and bindings (installed $($installed.Version) -> v$($release.Tag))"
    }

    Stop-AppIfRunning

    $setup = Save-Release -Release $release
    if (-not (Test-Path -LiteralPath $setup)) { throw 'The setup file could not be downloaded.' }

    $process = Start-Process -FilePath $setup -PassThru -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/CLOSEAPPLICATIONS'
    )
    Write-ActivityBar -Process $process -Message 'Installing'
    $exit = $process.ExitCode
    Remove-Item -LiteralPath $setup -Force -ErrorAction SilentlyContinue

    if ($exit -ne 0 -and $exit -ne 5) {
        throw "Setup failed with exit code $exit. Close $($script:AppName) and any Voicemeeter window, then try again."
    }

    if (-not (Test-Path -LiteralPath $script:AppExe)) {
        throw "Setup finished but $($script:AppExe) is missing - the install did not complete."
    }

    Write-Ok "$($script:AppName) v$($release.Tag) installed to $($script:AppFolder)"
    return $true
}

function Get-TagObject {
    # Version stamp for the completion panel, tolerant of a failed API call.
    $release = Get-LatestRelease
    return [pscustomobject]@{ Tag = $(if ($null -ne $release) { $release.Tag } else { 'latest' }) }
}

function Write-InstallComplete {
    param([pscustomobject]$Release)
    $vm = Get-VoicemeeterState
    Write-Host ''
    Write-Rule "  $($script:AppName) is ready"
    Write-Rule
    Write-Ok "  App        v$($Release.Tag)"
    Write-Info ("  Voicemeeter " + $(if ($vm.Present) { $vm.Name } else { 'not detected - run the setup again if audio routing fails' }))
    Write-Info  "  Launch     Start menu -> $script:AppName"
    Write-Dim   "  Settings   $script:AppDataDir"
    if (-not $vm.Present) {
        Write-WarnLine 'Restart Windows once so the Voicemeeter audio driver finishes loading.'
    }
    Write-Rule
    Write-Host ''
}

# -------------------------------------------------------------- uninstall ---

function Remove-VoicemeeterDriver {
    <#
      Voicemeeter's own uninstaller ignores /S and pops a "Remove" dialog, so
      the driver is torn down natively instead. The VB virtual cable kernel
      driver is the main BSOD risk here - stop the service first.
    #>
    Write-Info 'Removing Voicemeeter driver, service and registry entries...'

    foreach ($name in @('VBAudioVACMME', 'VBAudioVACMME64', 'VBAudioVACMME32', 'VBAudioVMME')) {
        if (Get-Service -Name $name -ErrorAction SilentlyContinue) {
            try {
                Stop-Service -Name $name -Force -ErrorAction SilentlyContinue
                $null = & sc.exe delete $name
            }
            catch { }
        }
    }

    try {
        $blocks = (((& pnputil.exe /enum-drivers 2>$null) | Out-String) -split "(?:\r?\n){2,}")
        foreach ($block in $blocks) {
            if ($block -notmatch 'VB-Audio|Voicemeeter') { continue }
            $match = [regex]::Match($block, '(?m)^Published Name:\s*(.+)$')
            if ($match.Success) {
                $null = & pnputil.exe /delete-driver $match.Groups[1].Value.Trim() /uninstall /force 2>$null
            }
        }
    }
    catch { }

    foreach ($folder in @('C:\Program Files (x86)\VB\Voicemeeter', 'C:\Program Files\VB\Voicemeeter')) {
        if (Test-Path -LiteralPath $folder) {
            try { Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction Stop }
            catch { Write-WarnLine "Could not fully remove $folder" }
        }
    }

    foreach ($key in @(
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:VoicemeeterBanana {17359A74-1236-5467}',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:VoicemeeterPotato {17359A74-1236-5467}'
    )) {
        if (Test-Path -LiteralPath $key) { Remove-Item -LiteralPath $key -Recurse -Force -ErrorAction SilentlyContinue }
    }

    foreach ($dir in @(
        "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Voicemeeter",
        "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\VB-Audio"
    )) {
        if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
    }

    Write-Ok 'Voicemeeter removed'
}

function Uninstall-App {
    $removed = @()
    $kept    = @()

    Stop-AppIfRunning

    $installed = Get-AppInstall
    if ($null -eq $installed) {
        Write-WarnLine "$($script:AppName) is not installed - nothing to remove."
    }
    elseif (-not $installed.UninstallExe) {
        $kept += "$($script:AppName) (no uninstaller found - remove $script:AppFolder by hand)"
    }
    else {
        Write-Info "Uninstalling $($script:AppName) $($installed.Version)..."
        try {
            $process = Start-Process -FilePath $installed.UninstallExe -PassThru -ArgumentList @(
                '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART'
            )
            Write-ActivityBar -Process $process -Message 'Uninstalling'
            if ($process.ExitCode -eq 0) { $removed += "$($script:AppName) $($installed.Version)" }
            else { $kept += "$($script:AppName) (uninstaller returned $($process.ExitCode))" }
        }
        catch {
            $kept += "$($script:AppName) (uninstall failed: $($_.Exception.Message))"
        }
    }

    $dropData = $RemoveData
    if ((Test-Path -LiteralPath $script:AppDataDir) -and -not $dropData) {
        $dropData = Confirm 'Also delete your sounds, settings and key bindings?' $false
    }
    if ($dropData) {
        if (Test-Path -LiteralPath $script:AppDataDir) {
            Remove-Item -LiteralPath $script:AppDataDir -Recurse -Force -ErrorAction SilentlyContinue
            $removed += 'App data (sounds, settings, bindings)'
        }
    }
    elseif (Test-Path -LiteralPath $script:AppDataDir) {
        $kept += 'App data (sounds, settings, bindings)'
    }

    $vm = Get-VoicemeeterState
    if ($vm.Present) {
        $dropVm = $RemoveVoicemeeter
        if ($vm.Paid) {
            $kept += "$($vm.Name) (licensed edition - left untouched)"
        }
        elseif (-not $dropVm) {
            $dropVm = Confirm 'Also remove Voicemeeter and its audio driver? (needed only if nothing else uses it)' $false
        }
        if ($vm.Paid) { $dropVm = $false }
        if ($dropVm) {
            Remove-VoicemeeterDriver
            $removed += $vm.Name
        }
        else {
            $kept += $vm.Name
        }
    }

    Write-Host ''
    Write-Rule '  Uninstall complete'
    Write-Rule
    if ($removed.Count -gt 0) {
        Write-Info 'Removed'
        foreach ($item in $removed) { Write-Host "$($script:Margin)    $([char]0x2714) $item" -ForegroundColor $script:Good }
    }
    if ($kept.Count -gt 0) {
        Write-Info 'Kept'
        foreach ($item in $kept) { Write-Host "$($script:Margin)    - $item" -ForegroundColor $script:Dim }
    }
    if ($removed.Count -eq 0 -and $kept.Count -eq 0) { Write-Dim 'Nothing to remove.' }
    if ($removed | Where-Object { $_ -match 'Voicemeeter' }) {
        Write-WarnLine 'Restart Windows to fully unload the removed audio driver.'
    }
    Write-Dim 'If audio sounds wrong, set your headphones/DAC as default in Windows Sound settings.'
    Write-Rule
    Write-Host ''
}

# ----------------------------------------------------------------- status ---

function Write-Status {
    $installed = Get-AppInstall
    $release = Get-LatestRelease

    Write-Pair 'Installed' $(if ($null -eq $installed) { 'no' } else { "$($script:AppName) $($installed.Version)" }) `
        $(if ($null -eq $installed) { $script:Dim } else { $script:Good })
    if ($null -eq $installed) {
        Write-Dim '           nothing in Program Files yet'
    }

    if ($null -eq $release) {
        Write-Pair 'Latest' 'unknown (GitHub unreachable)' $script:Warn
    }
    else {
        $upToDate = ($null -ne $installed) -and ($installed.Version -eq $release.Tag)
        Write-Pair 'Latest' "v$($release.Tag)  $([char]0x00B7)  github.com/$script:Repo/releases" `
            $(if ($upToDate) { $script:Good } else { $script:Alt })
        if (-not $upToDate) {
            Write-Dim $(if ($null -eq $installed) { '           start with option 1 below' } else { '           option 2 below updates in place' })
        }
    }

    $vm = Get-VoicemeeterState
    Write-Pair 'Voicemeeter' $(if ($vm.Present) { $vm.Name } else { 'not installed' }) `
        $(if ($vm.Present) { $script:Good } else { $script:Warn })

    Write-Pair 'Prerequisites' 'none - the app and Voicemeeter ship in the setup' $script:Good

    Write-Pair 'Folder' $script:AppFolder $script:Dim
    Write-Pair 'Data' $script:AppDataDir $script:Dim
    Write-Host ''
}

function Write-Help {
    Write-Banner -Tagline 'A lightweight SFX / voice effects suite'
    Write-Section '  Usage'
    Write-Info "irm $script:ScriptUrl | iex"
    Write-Info 'powershell -ExecutionPolicy Bypass -File install.ps1 [-Install|-Upgrade|-Uninstall]'
    Write-Host ''
    Write-Pair '-Install'  'Download and set up the latest release' $script:Accent
    Write-Pair '-Upgrade'  'Install over the current copy, keeping your data' $script:Accent
    Write-Pair '-Uninstall' 'Remove the app (asks about your sounds and Voicemeeter)' $script:Accent
    Write-Pair '-CheckUpdate' 'Print the latest release tag and exit (no admin needed)' $script:Accent
    Write-Pair '-Status'       'Show what is installed right now (no admin needed)' $script:Accent
    Write-Pair '-RemoveData' 'With -Uninstall: also delete sounds/settings/bindings' $script:Dim
    Write-Pair '-RemoveVoicemeeter' 'With -Uninstall: also remove Voicemeeter + driver' $script:Dim
    Write-Pair '-Silent' 'No prompts (for scripts and CI)' $script:Dim
    Write-Pair '-NoLaunch' 'Do not start the app when the install finishes' $script:Dim
    Write-Host ''
    Write-Info "  Installer v$($script:ScriptVer)  $([char]0x00B7)  github.com/$script:Repo"
    Write-Host ''
}

# ------------------------------------------------------------------- main ---

function Invoke-Main {
    # Returns nothing on purpose: the menu prints through the host, and the
    # process exit code travels in $script:ExitCode so nothing leaks into the
    # pipeline (which would break -CheckUpdate's machine-readable output).

    # --- read-only jobs -------------------------------------------------
    if ($args.Count -gt 0) {
        Write-WarnLine "Ignoring unknown argument(s): $($args -join ' ')"
        Write-Dim 'Run with -Help to see the supported switches.'
        Write-Host ''
    }

    if ($Help) { Write-Help; $script:ExitCode = 0; return }

    if ($CheckUpdate) {
        $release = Get-LatestRelease
        if ($null -eq $release) { Write-ErrorLine 'Update check failed (offline or GitHub unreachable).'; $script:ExitCode = 2; return }
        Write-Output $release.Tag
        $script:ExitCode = 0
        return
    }

    Write-Banner

    # --- status (read-only, so it also works before elevating) -----------
    if ($Status) { Write-Status; $script:ExitCode = 0; return }

    # --- non-interactive jobs -------------------------------------------
    if ($Uninstall) { Uninstall-App; $script:ExitCode = 0; return }

    if ($Install -or $Upgrade) {
        $release = Get-LatestRelease
        $installed = Get-AppInstall
        if ($Upgrade -and $null -ne $installed -and $null -ne $release -and $installed.Version -eq $release.Tag) {
            Write-Ok "Already on v$($release.Tag) - running the installer again to repair files."
        }
        if (-not (Install-Release -IsUpgrade:$Upgrade)) { $script:ExitCode = 1; return }
        Write-InstallComplete -Release (Get-TagObject)
        if (-not $NoLaunch -and -not $Silent) {
            if (Confirm 'Launch it now?' $true) { $null = Start-Process -FilePath $script:AppExe }
        }
        $script:ExitCode = 0
        return
    }

    # --- interactive menu ----------------------------------------------
    if (-not (Test-Elevated)) {
        Write-ErrorLine 'Run this from an elevated PowerShell (Run as administrator).'
        $script:ExitCode = 1
        return
    }

    while ($true) {
        Write-Section '  SoundFX Studio'
        Write-Status

        $items = @(
            @{ Key = '1'; Text = 'Install';   Hint = 'first-time setup: app + Voicemeeter' }
            @{ Key = '2'; Text = 'Upgrade';   Hint = 'latest release, your sounds and settings stay' }
            @{ Key = '3'; Text = 'Uninstall'; Hint = 'remove the app (asks what else to keep)' }
            @{ Key = '0'; Text = 'Exit';      Hint = 'close this window' }
        )
        Write-Host ''
        foreach ($item in $items) {
            $label = "[$($item.Key)]  $($item.Text)".PadRight(17)
            Write-Host "$($script:Margin)$label" -ForegroundColor $script:Accent -NoNewline
            Write-Host "$($item.Hint)" -ForegroundColor $script:Dim
        }
        Write-Host ''

        $choice = Read-Choice -Prompt 'Choice' -Valid @('1', '2', '3', '0')

        if ($choice -eq '0') { $script:ExitCode = 0; return }

        if ($choice -eq '1' -or $choice -eq '2') {
            $isUpgrade = ($choice -eq '2')
            if (-not $isUpgrade) {
                $installed = Get-AppInstall
                if ($null -ne $installed) {
                    Write-WarnLine "v$($installed.Version) is already installed - continuing upgrades it in place."
                    $isUpgrade = $true
                }
            }
            if (Install-Release -IsUpgrade:$isUpgrade) {
                Write-InstallComplete -Release (Get-TagObject)
                if (Confirm 'Launch it now?' $true) { $null = Start-Process -FilePath $script:AppExe }
            }
        }
        elseif ($choice -eq '3') {
            Uninstall-App
        }
        else { continue }

        if ($Silent) { $script:ExitCode = 0; return }
        Write-Host ''
        $null = Read-Host '  Press Enter for the menu'
    }
}

# ------------------------------------------------------------- elevation ---

# Runs last, so the UAC prompt only appears once the job is known. Voicemeeter
# installs an audio driver, so anything that touches it needs a real admin
# token. Instead of printing an error and dying, hand the whole job to a new
# elevated window and step aside.
if (-not $script:ReadOnly -and -not (Test-Elevated)) {
    Write-Host ''
    Write-WarnLine 'Administrator rights are required - Voicemeeter installs an audio driver.'
    Write-Dim 'Windows will ask you to allow it in a moment.'
    Write-Host ''
    try {
        Start-ElevatedRerun -Tokens (Get-CurrentTokens)
        exit 0
    }
    catch {
        Write-Host ''
        Write-ErrorLine 'Could not restart as administrator automatically.'
        Write-WarnLine 'Right-click PowerShell -> "Run as administrator", then run this again:'
        Write-Host ''
        Write-Dim "    irm $script:ScriptUrl | iex"
        Write-Host ''
        if (-not $Silent) { $null = Read-Host '  Press Enter to close' }
        exit 1
    }
}

try {
    Invoke-Main
    exit $script:ExitCode
}
catch {
    Write-Host ''
    Write-ErrorLine $_.Exception.Message
    if ($_.InvocationInfo -and $_.InvocationInfo.ScriptLineNumber -gt 0) {
        $line = [string]$_.InvocationInfo.Line
        if ($line) { Write-Dim "line $($_.InvocationInfo.ScriptLineNumber): $($line.Trim())" }
    }
    Write-Host ''
    Write-Dim "  Need help? https://github.com/$script:Repo/issues"
    if (-not $Silent) { $null = Read-Host '  Press Enter to close' }
    exit 1
}
