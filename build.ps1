# System Optimizer WinUI 3 Build Script
# Compila o gerador de ícones, atualiza os assets e empacota o app principal como MSIX.

$ErrorActionPreference = "Stop"

Write-Host "Checking for .NET SDK..."
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $possiblePaths = @(
        "C:\Program Files\dotnet",
        "C:\Program Files (x86)\dotnet"
    )

    foreach ($path in $possiblePaths) {
        if (Test-Path "$path\dotnet.exe") {
            Write-Host "O comando 'dotnet' não estava no PATH, mas foi encontrado em: $path" -ForegroundColor Yellow
            $env:PATH = "$env:PATH;$path"
            break
        }
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "ERRO CRÍTICO: o SDK do .NET não foi encontrado. Instale o .NET 10 SDK e execute novamente."
}

$dotnetVersion = dotnet --version
Write-Host "Using .NET SDK version: $dotnetVersion"

$root = $PSScriptRoot
$iconResizerProj = Join-Path $root "src\IconResizer\IconResizer.csproj"
$mainProj = Join-Path $root "src\SystemOptimizer\SystemOptimizer.csproj"
$outputDir = Join-Path $root "Build"
$assetsDir = Join-Path $root "src\SystemOptimizer\Assets"
$sourceLogo = Join-Path $assetsDir "logo.png"
$targetIcon = Join-Path $assetsDir "icon.ico"
$configuration = "Release"
$runtime = "win-x64"

Write-Host "`n[1/3] Building internal tools (IconResizer)..."
dotnet build $iconResizerProj -c $configuration -v minimal

$resizerExe = Join-Path $root "src\IconResizer\bin\$configuration\net10.0-windows\IconResizer.exe"
if (-not (Test-Path $resizerExe)) {
    throw "Failed to build IconResizer tool at: $resizerExe"
}

Write-Host "`n[2/3] Generating high-resolution icon..."
if (Test-Path $sourceLogo) {
    & $resizerExe $sourceLogo $targetIcon
} else {
    Write-Warning "logo.png not found in Assets. Skipping icon generation."
}

Write-Host "`n[3/3] Restoring and packaging SystemOptimizer as MSIX..."
if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $outputDir | Out-Null

dotnet restore $mainProj -r $runtime

dotnet build $mainProj `
    -c $configuration `
    -r $runtime `
    --no-restore `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageSigningEnabled=false `
    -p:AppxBundle=Never `
    -p:AppxPackageDir="$outputDir\"

$msix = Get-ChildItem -Path $outputDir -Filter "*.msix" -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $msix) {
    throw "Build finished, but no .msix package was found in $outputDir."
}

$sizeFormatted = "{0:N2} MB" -f (($msix.Length) / 1MB)
Write-Host "`nBuild Successful!" -ForegroundColor Green
Write-Host "MSIX package created at: $($msix.FullName)" -ForegroundColor Green
Write-Host "Final Size: $sizeFormatted" -ForegroundColor Cyan
Write-Host "Note: The app is packaged as MSIX and uses native WinUI 3/Windows App SDK." -ForegroundColor Cyan
