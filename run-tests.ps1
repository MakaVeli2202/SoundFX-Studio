# SoundFX Studio - Test Runner
# Usage:
#   .\run-tests.ps1              # Run all tests (unit + UI)
#   .\run-tests.ps1 -Unit        # Unit tests only
#   .\run-tests.ps1 -UI          # UI tests only
#   .\run-tests.ps1 -Filter "MainWindow"  # Filter by name

param(
    [switch]$Unit,
    [switch]$UI,
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "=== SoundFX Studio Test Runner ===" -ForegroundColor Cyan

# Build first
Write-Host "`n[1/3] Building..." -ForegroundColor Yellow
dotnet build "$root\SoundFXStudio.slnx" 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Trying individual projects..." -ForegroundColor DarkYellow
    dotnet build "$root\SoundFXStudio\SoundFXStudio.csproj" --verbosity quiet
    if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED" -ForegroundColor Red; exit 1 }
}

# Kill leftover app instances
Write-Host "`n[2/3] Cleaning up..." -ForegroundColor Yellow
Get-Process -Name "SoundFXStudio" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Run tests
Write-Host "`n[3/3] Running tests..." -ForegroundColor Yellow

$testFilter = ""
if ($Filter) { $testFilter = "--filter" }

if ($Unit -or (-not $UI)) {
    Write-Host "`n--- Unit Tests ---" -ForegroundColor Green
    if ($Filter) {
        dotnet test "$root\SoundFXStudio.Tests\SoundFXStudio.Tests.csproj" --verbosity normal --filter "FullyQualifiedName~$Filter"
    } else {
        dotnet test "$root\SoundFXStudio.Tests\SoundFXStudio.Tests.csproj" --verbosity normal
    }
    $unitResult = $LASTEXITCODE
}

if ($UI -or (-not $Unit)) {
    Write-Host "`n--- UI Tests (FlaUI) ---" -ForegroundColor Green
    if ($Filter) {
        dotnet test "$root\SoundFXStudio.UI.Tests\SoundFXStudio.UI.Tests.csproj" --verbosity normal --filter "FullyQualifiedName~$Filter"
    } else {
        dotnet test "$root\SoundFXStudio.UI.Tests\SoundFXStudio.UI.Tests.csproj" --verbosity normal
    }
    $uiResult = $LASTEXITCODE
}

# Cleanup
Get-Process -Name "SoundFXStudio" -ErrorAction SilentlyContinue | Stop-Process -Force

# Summary
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
if ($unitResult -eq 0) { Write-Host "  Unit: PASS" -ForegroundColor Green }
elseif ($unitResult) { Write-Host "  Unit: FAIL" -ForegroundColor Red }
if ($uiResult -eq 0) { Write-Host "  UI:   PASS" -ForegroundColor Green }
elseif ($uiResult) { Write-Host "  UI:   FAIL" -ForegroundColor Red }

exit ($unitResult ?? 0) -bor ($uiResult ?? 0)
