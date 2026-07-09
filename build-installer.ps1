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
    Write-Host 'Inno Setup compiler (ISCC.exe) not found on PATH.'
    Write-Host 'Install Inno Setup and ensure ISCC.exe is on PATH, then re-run this script.'
    Write-Host 'Alternatively, open installer.iss in Inno Setup IDE and compile it manually.'
    exit 1
}

$issPath = Join-Path $scriptDir 'installer.iss'
Write-Host "Building installer using ISCC: $issPath"
& "$($iscc.Path)" $issPath