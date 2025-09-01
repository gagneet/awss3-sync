# Build script for Strata S3 Manager
# Run this script to build the application

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = ".\publish",
    [switch]$SelfContained = $false,
    [switch]$SingleFile = $false
)

Write-Host "Building Strata S3 Manager..." -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow

# Clean previous builds
if (Test-Path $OutputPath) {
    Write-Host "Cleaning previous build..." -ForegroundColor Yellow
    Remove-Item -Path $OutputPath -Recurse -Force
}

# Restore NuGet packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to restore packages!" -ForegroundColor Red
    exit 1
}

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build -c $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Publish the application
Write-Host "Publishing application..." -ForegroundColor Yellow

$publishArgs = @(
    "publish",
    "-c", $Configuration,
    "-o", $OutputPath,
    "--no-build"
)

if ($SelfContained) {
    $publishArgs += "--self-contained"
    $publishArgs += "-r"
    $publishArgs += "win-x64"
}

if ($SingleFile) {
    $publishArgs += "-p:PublishSingleFile=true"
    $publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
}

dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# Copy configuration file
Write-Host "Copying configuration files..." -ForegroundColor Yellow
Copy-Item -Path "appsettings.json" -Destination $OutputPath -Force

# Create sample configuration if it doesn't exist
$configPath = Join-Path $OutputPath "appsettings.json"
if (-not (Test-Path $configPath)) {
    Write-Host "Creating sample configuration..." -ForegroundColor Yellow
    @"
{
  "AWS": {
    "AccessKey": "YOUR_AWS_ACCESS_KEY",
    "SecretKey": "YOUR_AWS_SECRET_KEY",
    "Region": "ap-southeast-2",
    "BucketName": "your-strata-bucket"
  },
  "Cognito": {
    "UserPoolId": "YOUR_USER_POOL_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "",
    "Region": "ap-southeast-2",
    "IdentityPoolId": "YOUR_IDENTITY_POOL_ID",
    "EnableOfflineMode": false,
    "OfflineCacheDurationDays": 7
  },
  "Performance": {
    "MaxConcurrentUploads": 5,
    "MaxConcurrentDownloads": 5,
    "ChunkSizeBytes": 5242880,
    "EnableMetadataCache": true,
    "MetadataCacheDurationMinutes": 5,
    "EnableDeltaSync": true,
    "SyncBatchSize": 100
  }
}
"@ | Out-File -FilePath $configPath -Encoding UTF8
}

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Output location: $OutputPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Update appsettings.json with your AWS configuration"
Write-Host "2. Follow AWS_IAM_SETUP_GUIDE.md to configure Cognito"
Write-Host "3. Run AWSS3Sync.exe to start the application"