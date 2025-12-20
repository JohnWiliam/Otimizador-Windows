# System Optimizer Build Script
# Compiles the icon generator, creates the icon, and publishes the main app.

$ErrorActionPreference = "Stop"

Write-Host "Checking for .NET SDK..."
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "The .NET SDK is not found. Please install .NET 8 SDK or later."
    exit 1
}

$dotnetVersion = dotnet --version
Write-Host "Using .NET SDK version: $dotnetVersion"

# --- paths ---
$root = $PSScriptRoot
$iconResizerProj = Join-Path $root "src\IconResizer\IconResizer.csproj"
$mainProj = Join-Path $root "src\SystemOptimizer\SystemOptimizer.csproj"
$outputDir = Join-Path $root "Build"
$assetsDir = Join-Path $root "src\SystemOptimizer\Assets"
$sourceLogo = Join-Path $assetsDir "logo.png"
$targetIcon = Join-Path $assetsDir "icon.ico"

# --- Step 1: Build Icon Resizer Tool ---
Write-Host "`n[1/3] Building Internal Tools (IconResizer)..."
dotnet build $iconResizerProj -c Release -v q

# Locate the compiled tool
$resizerExe = Join-Path $root "src\IconResizer\bin\Release\net8.0-windows\IconResizer.exe"

if (-not (Test-Path $resizerExe)) {
    Write-Error "Failed to build IconResizer tool."
    exit 1
}

# --- Step 2: Generate High-Res Icon ---
Write-Host "`n[2/3] Generating High-Resolution Icon..."
if (Test-Path $sourceLogo) {
    # Run the tool: IconResizer.exe <input> <output>
    & $resizerExe $sourceLogo $targetIcon
} else {
    Write-Warning "logo.png not found in Assets. Skipping icon generation."
}

# --- Step 3: Build Main Application ---
Write-Host "`n[3/3] Building and Publishing SystemOptimizer..."

if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}

# We restore specifically for win-x64
Write-Host "Restoring dependencies..."
dotnet restore $mainProj -r win-x64

Write-Host "Publishing Single File Executable..."
# -p:PublishSingleFile=true: Merges everything into one EXE
# --self-contained: Includes .NET runtime
dotnet publish $mainProj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --no-restore -o $outputDir

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild Successful!" -ForegroundColor Green
    Write-Host "Executable is located at: $outputDir\SystemOptimizer.exe" -ForegroundColor Green
    Write-Host "Note: If the icon looks blurry in Explorer, try moving the .exe to a new folder (Explorer caches thumbnails)."
} else {
    Write-Error "Build failed."
}
