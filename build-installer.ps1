<#
Publish SoundFX Studio and compile Inno Setup installer (if Inno is installed).

Usage:
  .\build-installer.ps1           # publish and try to build installer
  .\build-installer.ps1 -PublishOnly
#>

param(
    [switch]$PublishOnly
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $scriptDir 'SoundFXStudio\SoundFXStudio.csproj'
$publishDir = Join-Path $scriptDir 'publish'

Write-Host "Publishing project: $project -> $publishDir"
dotnet publish $project -c Release -r win-x64 --self-contained false -o $publishDir

if ($PublishOnly) { Write-Host 'Publish complete (PublishOnly specified).'; exit 0 }

$iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue
if ($null -eq $iscc) {
    # A per-user Inno Setup install is not on PATH, so check the usual spots.
    $candidates = @(@(
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) })
    if ($candidates) { $iscc = [pscustomobject]@{ Path = $candidates[0] } }
}
if ($null -eq $iscc) {
    Write-Host 'Inno Setup compiler (ISCC.exe) not found on PATH or in the standard install folders.'
    Write-Host 'Install Inno Setup (winget install JRSoftware.InnoSetup), then re-run this script.'
    Write-Host 'Alternatively, open installer.iss in the Inno Setup IDE and compile it manually.'
    exit 1
}

$issPath = Join-Path $scriptDir 'installer.iss'
Write-Host "Building installer using ISCC: $issPath"
& "$($iscc.Path)" $issPath