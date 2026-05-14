# System Optimizer WinUI 3 Build Script
# Compila a aplicação nativa WinUI 3 e empacota o resultado final como MSIX.

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
    Write-Host "ERRO CRÍTICO: O SDK do .NET não foi encontrado." -ForegroundColor Red
    Write-Host "Certifique-se de que o .NET 10 SDK e as cargas de trabalho Windows App SDK/WinUI estejam instaladas."
    exit 1
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

Write-Host "`n[1/3] Building Internal Tools (IconResizer)..."
dotnet build $iconResizerProj -c Release -v q
$resizerExe = Join-Path $root "src\IconResizer\bin\Release\net10.0-windows\IconResizer.exe"

if (-not (Test-Path $resizerExe)) {
    Write-Host "Failed to build IconResizer tool at: $resizerExe" -ForegroundColor Red
    exit 1
}

Write-Host "`n[2/3] Generating High-Resolution Icon..."
if (Test-Path $sourceLogo) {
    & $resizerExe $sourceLogo $targetIcon
} else {
    Write-Warning "logo.png not found in Assets. Skipping icon generation."
}

Write-Host "`n[3/3] Building WinUI 3 MSIX package..."
if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

Write-Host "Restoring dependencies for win-x64..."
dotnet restore $mainProj -r win-x64

Write-Host "Creating unsigned MSIX package..."
dotnet msbuild $mainProj `
    "-t:Restore;Publish" `
    -p:Configuration=Release `
    -p:RuntimeIdentifier=win-x64 `
    -p:Platform=x64 `
    -p:WindowsPackageType=MSIX `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageSigningEnabled=false `
    -p:AppxBundle=Never `
    -p:UapAppxPackageBuildMode=SideloadOnly `
    -p:AppxPackageDir="$outputDir\" `
    -p:WindowsAppSDKSelfContained=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

$msix = Get-ChildItem -Path $outputDir -Recurse -Filter "*.msix" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $msix) {
    Write-Host "Build finished but MSIX package was not found in $outputDir." -ForegroundColor Red
    exit 1
}

$size = $msix.Length / 1MB
Write-Host "`nBuild Successful!" -ForegroundColor Green
Write-Host "MSIX created at: $($msix.FullName)" -ForegroundColor Green
Write-Host ("Final Size: {0:N2} MB" -f $size) -ForegroundColor Cyan
Write-Host "Note: package is unsigned for sideload/development workflows." -ForegroundColor Yellow
