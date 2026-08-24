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

Write-Host 'Fetching latest SoundFX Studio release...'

$release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest"

$asset = $release.assets |
    Where-Object { $_.name -like '*Setup*.exe' } |
    Select-Object -First 1

if ($null -eq $asset)
{
    throw "No setup executable found in release $($release.tag_name)."
}

$installer = Join-Path $env:TEMP $asset.name

Write-Host "Downloading $($asset.name) ($([math]::Round($asset.size / 1MB, 1)) MB)..."
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $installer -UseBasicParsing

Write-Host "Installing SoundFX Studio $($release.tag_name)..."
Start-Process -FilePath $installer -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait

Remove-Item $installer -ErrorAction SilentlyContinue

Write-Host "Done. SoundFX Studio $($release.tag_name) is installed."
