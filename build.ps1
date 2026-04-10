# Build script for AWS S3 File Sync
# Usage:
#   .\build.ps1                          # Self-contained single-file release (default)
#   .\build.ps1 -Configuration Debug     # Debug build
#   .\build.ps1 -Version 1.2.3           # Set version
#   .\build.ps1 -Runtime win-arm64       # Arm64 target
#   .\build.ps1 -NoSingleFile            # Framework-dependent (requires .NET 8 on target)

param(
    [string]$Configuration = "Release",
    [string]$OutputPath    = ".\publish",
    [string]$Runtime       = "win-x64",
    [string]$Version       = "1.0.0",
    [switch]$NoSingleFile,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$Project = "FileSyncApp.WinForms\FileSyncApp.WinForms.csproj"

Write-Host "=== AWS S3 File Sync - Build Script ===" -ForegroundColor Cyan
Write-Host "Configuration : $Configuration"  -ForegroundColor Yellow
Write-Host "Runtime       : $Runtime"        -ForegroundColor Yellow
Write-Host "Version       : $Version"        -ForegroundColor Yellow
Write-Host "Single-file   : $(-not $NoSingleFile)" -ForegroundColor Yellow
Write-Host ""

# Clean output
if (Test-Path $OutputPath) {
    Write-Host "Cleaning previous output..." -ForegroundColor Yellow
    Remove-Item -Path $OutputPath -Recurse -Force
}

# Restore
Write-Host "Restoring packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) { Write-Host "Restore failed." -ForegroundColor Red; exit 1 }

# Test (unless skipped)
if (-not $SkipTests) {
    Write-Host "Running tests..." -ForegroundColor Yellow
    dotnet test --no-restore -c $Configuration --logger "console;verbosity=minimal"
    if ($LASTEXITCODE -ne 0) { Write-Host "Tests failed." -ForegroundColor Red; exit 1 }
}

# Publish
Write-Host "Publishing application..." -ForegroundColor Yellow

$publishArgs = @(
    "publish", $Project,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-o", $OutputPath,
    "-p:Version=$Version",
    "-p:AssemblyVersion=$Version.0",
    "-p:FileVersion=$Version.0",
    "-p:PublishReadyToRun=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true"
)

if (-not $NoSingleFile) {
    $publishArgs += "-p:PublishSingleFile=true"
}

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed." -ForegroundColor Red; exit 1 }

# Ensure appsettings template is present for first-time users
$templateSrc  = "FileSyncApp.WinForms\appsettings.template.json"
$templateDest = Join-Path $OutputPath "appsettings.template.json"
$configDest   = Join-Path $OutputPath "appsettings.json"

if (Test-Path $templateSrc) {
    Copy-Item $templateSrc $templateDest -Force
}

# If no appsettings.json exists in output, create one from the template
if (-not (Test-Path $configDest)) {
    if (Test-Path $templateSrc) {
        Copy-Item $templateSrc $configDest -Force
        Write-Host "Created default appsettings.json - update with your AWS credentials." -ForegroundColor Cyan
    }
}

# Zip the output for easy distribution
$zipName = "FileSyncApp-$Version-$Runtime.zip"
$zipPath = Join-Path (Split-Path $OutputPath -Parent) $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$OutputPath\*" -DestinationPath $zipPath
Write-Host "Distribution zip: $zipPath" -ForegroundColor Green

Write-Host ""
Write-Host "=== Build complete ===" -ForegroundColor Green
Write-Host "Executable  : $OutputPath\FileSyncApp.exe" -ForegroundColor Cyan
Write-Host "Distribution: $zipPath"                    -ForegroundColor Cyan
Write-Host ""
Write-Host "First-time setup:" -ForegroundColor Yellow
Write-Host "  1. Edit appsettings.json with your AWS credentials"
Write-Host "  2. Run FileSyncApp.exe"
