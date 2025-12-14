# System Optimizer Build Script
# This script publishes the application as a single self-contained executable.

$ErrorActionPreference = "Stop"

Write-Host "Checking for .NET SDK..."
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "The .NET SDK is not found. Please install .NET 8 SDK or later."
    exit 1
}

$dotnetVersion = dotnet --version
Write-Host "Using .NET SDK version: $dotnetVersion"

$projectPath = Join-Path $PSScriptRoot "src\SystemOptimizer\SystemOptimizer.csproj"
$outputDir = Join-Path $PSScriptRoot "Build"

if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}

Write-Host "Restoring dependencies for win-x64..."
# We must restore for the specific runtime because we use --no-restore in the publish step (or implied by some environments).
dotnet restore $projectPath -r win-x64

Write-Host "Building and Publishing Single File Executable..."
# -r win-x64: Target Windows 64-bit
# --self-contained: Bundle .NET runtime (Portable)
# -p:PublishSingleFile=true: Merge into one .exe
# -p:IncludeNativeLibrariesForSelfExtract=true: Ensure native libs are included
# -c Release: Release configuration
# --no-restore: Use the assets we just restored

dotnet publish $projectPath -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --no-restore -o $outputDir

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild Successful!" -ForegroundColor Green
    Write-Host "Executable is located at: $outputDir\SystemOptimizer.exe" -ForegroundColor Green
    Write-Host "You can zip this file or the entire Build folder."
} else {
    Write-Error "Build failed."
}
