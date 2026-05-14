# System Optimizer Build Script
# Compila ferramentas internas, gera ícones e empacota o aplicativo WinUI 3 como MSIX.

$ErrorActionPreference = "Stop"

Write-Host "Checking for .NET SDK..."
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $possiblePaths = @("C:\Program Files\dotnet", "C:\Program Files (x86)\dotnet")
    foreach ($path in $possiblePaths) {
        if (Test-Path "$path\dotnet.exe") {
            Write-Host "O comando 'dotnet' não estava no PATH, mas foi encontrado em: $path" -ForegroundColor Yellow
            $env:PATH = "$env:PATH;$path"
            break
        }
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "ERRO CRÍTICO: O SDK do .NET não foi encontrado." -ForegroundColor Red
    exit 1
}

$dotnetVersion = dotnet --version
Write-Host "Using .NET SDK version: $dotnetVersion"

$root = $PSScriptRoot
$iconResizerProj = Join-Path $root "src\IconResizer\IconResizer.csproj"
$mainProj = Join-Path $root "src\SystemOptimizer\SystemOptimizer.csproj"
$outputDir = Join-Path $root "Build"
$msixDir = Join-Path $outputDir "MSIX"
$assetsDir = Join-Path $root "src\SystemOptimizer\Assets"
$sourceLogo = Join-Path $assetsDir "logo.png"
$targetIcon = Join-Path $assetsDir "icon.ico"

Write-Host "`n[1/3] Building Internal Tools (IconResizer)..."
dotnet build $iconResizerProj -c Release -v q
$resizerExe = Join-Path $root "src\IconResizer\bin\Release\net10.0-windows\IconResizer.exe"
if (-not (Test-Path $resizerExe)) {
    throw "Failed to build IconResizer tool at $resizerExe"
}

Write-Host "`n[2/3] Generating High-Resolution Icon..."
if (Test-Path $sourceLogo) {
    & $resizerExe $sourceLogo $targetIcon
} else {
    Write-Warning "logo.png not found in Assets. Skipping icon generation."
}

Write-Host "`n[3/3] Building and Packaging SystemOptimizer (WinUI 3 MSIX)..."
if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $msixDir | Out-Null

Write-Host "Restoring dependencies..."
dotnet restore $mainProj -r win-x64

Write-Host "Creating unsigned MSIX package..."
dotnet build $mainProj -c Release -r win-x64 --self-contained `
    -p:WindowsPackageType=MSIX `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageSigningEnabled=false `
    -p:AppxBundle=Never `
    -p:AppxPackageDir="$msixDir\" `
    --no-restore

$msix = Get-ChildItem -Path $msixDir -Filter *.msix -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msix) {
    throw "Build finished but MSIX package was not found in $msixDir"
}

$size = (Get-Item $msix.FullName).Length / 1MB
Write-Host "`nBuild Successful!" -ForegroundColor Green
Write-Host "MSIX package created at: $($msix.FullName)" -ForegroundColor Green
Write-Host ("Final Size: {0:N2} MB" -f $size) -ForegroundColor Cyan
Write-Host "Note: package signing remains disabled for local/internal builds." -ForegroundColor Yellow
