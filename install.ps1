<#
SoundFX Studio - one-line installer.

Usage:
  irm https://raw.githubusercontent.com/MakaVeli2202/SoundFX-Studio/main/install.ps1 | iex

Downloads the latest stable release from GitHub and installs it silently
(Inno Setup /VERYSILENT). Existing installations are upgraded in place;
your sounds and settings are preserved.
#>

$ErrorActionPreference = 'Stop'

$repo = 'MakaVeli2202/SoundFX-Studio'

function Write-Banner {
    $w = 76
    Write-Host ''
    Write-Host ('=' * $w) -ForegroundColor Cyan
    Write-Host ('  ' + 'SoundFX Studio Installer') -ForegroundColor Cyan
    Write-Host ('  ' + 'A lightweight SFX / voice effects suite') -ForegroundColor Gray
    Write-Host ('  ' + 'github.com/MakaVeli2202/SoundFX-Studio') -ForegroundColor DarkGray
    Write-Host ('  ' + 'Guided install: driver + effects stack.') -ForegroundColor DarkGray
    Write-Host ('=' * $w) -ForegroundColor Cyan
    Write-Host ''
}

function Write-Warning {
    Write-Host '                           WARNING' -ForegroundColor Yellow
    Write-Host '  This installer will install audio components on this PC.' -ForegroundColor Yellow
    Write-Host '  Close all apps and save your work before continuing.' -ForegroundColor Yellow
    Write-Host '  SoundFX Studio stays on top so your sound effects keep' -ForegroundColor Yellow
    Write-Host '  working while you play or stream.' -ForegroundColor Yellow
    Write-Host ''
}

function Write-Step {
    param([int]$n, [string]$text)
    Write-Host ("  Installing [{0}/{1}]...  {2}" -f $n, $totalSteps, $text) -ForegroundColor Gray
}

function Write-Ok {
    param([string]$text)
    Write-Host ('  ' + [char]0x2713 + ' ' + $text) -ForegroundColor Green
}

function Write-Info {
    param([string]$text)
    Write-Host ('  ' + $text) -ForegroundColor DarkGray
}

function Get-MenuChoice {
    Write-Host '  What would you like to do?' -ForegroundColor White
    Write-Host ''
    Write-Host '  [1] Install SoundFX Studio (start here)' -ForegroundColor Gray
    Write-Host '      Upgrades existing installs; keeps your sounds and settings.' -ForegroundColor DarkGray
    Write-Host '  [Q] Quit' -ForegroundColor Gray
    Write-Host ''
}

# ---------------------------------------------------------------- main ---
Write-Banner
Write-Warning

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host '  [!] You need an elevated (Run as Administrator) PowerShell for this.' -ForegroundColor Red
    Write-Host '  Right-click PowerShell -> "Run as administrator", then re-run the command.' -ForegroundColor Red
    Write-Host ''
    exit 1
}

# Show menu, default to install
Get-MenuChoice
$choice = Read-Host '  Choice'
if ($choice -eq 'q') { Write-Host '  Quit. Nothing changed.' -ForegroundColor Gray; exit 0 }
if ($choice -ne '1') { Write-Host '  Invalid choice, using default: Install (1)' -ForegroundColor DarkGray; $choice = '1' }

Write-Host ''
Write-Host '  Fetching latest SoundFX Studio release...' -ForegroundColor Gray

$release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest"

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

$totalSteps = 2

# 1) Download with a real progress bar
Write-Step 1 'Downloading package'
$totalBytes = $asset.size
$progressId = 100
$dl = Start-Job -ArgumentList $asset.browser_download_url, $installer -ScriptBlock {
    param($ui, $out)
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $ui -OutFile $out -UseBasicParsing
}
while ($dl.State -eq 'Running') {
    $have = if (Test-Path $installer) { (Get-Item $installer).Length } else { 0 }
    $pct = if ($totalBytes -gt 0) { [math]::Floor($have / $totalBytes * 100) } else { 0 }
    Write-Progress -Activity 'Downloading SoundFX Studio' -Status ("{0:N1} MB / {1:N1} MB" -f ($have / 1MB), ($totalBytes / 1MB)) -PercentComplete $pct -Id $progressId
    Start-Sleep -Milliseconds 200
}
Receive-Job $dl | Out-Null
Remove-Job $dl
Write-Progress -Activity 'Downloading SoundFX Studio' -Completed -Id $progressId
Write-Ok "$($asset.name) downloaded"

# 2) Install silently
Write-Step 2 'Installing SoundFX Studio'
Start-Process -FilePath $installer -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait
Write-Ok "SoundFX Studio $($release.tag_name) installed"
Remove-Item $installer -ErrorAction SilentlyContinue

Write-Host ''
Write-Host ('=' * 76) -ForegroundColor Cyan
Write-Host '  Done! SoundFX Studio is installed.' -ForegroundColor Green
Write-Host '  Press the Windows key and type "SoundFX Studio" to launch.' -ForegroundColor Gray
Write-Host ('=' * 76) -ForegroundColor Cyan
Write-Host ''