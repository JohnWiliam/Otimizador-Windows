# System Optimizer Build Script
# Compila o gerador de ícones, cria o ícone e publica o app principal com otimização máxima.

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

# --- Step 3: Build Main Application (Optimized) ---
Write-Host "`n[3/3] Building and Publishing SystemOptimizer (Trimmed & SingleFile)..."

if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}

# Restaura especificamente para win-x64 antes do publish
Write-Host "Restoring dependencies..."
dotnet restore $mainProj -r win-x64

Write-Host "Publishing..."
# Flags explicadas:
# -p:PublishSingleFile=true            : Gera um único EXE.
# -p:PublishTrimmed=true               : Remove código .NET não utilizado (reduz tamanho).
# -p:TrimMode=partial                  : Modo seguro para WPF (evita quebrar a UI).
# -p:PublishReadyToRun=false           : Desativa pré-compilação nativa (reduz tamanho, boot levemente mais lento na 1ª vez).
# -p:EnableCompressionInSingleFile=true : Comprime o conteúdo dentro do EXE.
# -p:IncludeNativeLibrariesForSelfExtract=true : Inclui libs nativas necessárias.

dotnet publish $mainProj -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=true `
    -p:TrimMode=partial `
    -p:PublishReadyToRun=false `
    --no-restore `
    -o $outputDir

# --- Optional Step: UPX Compression ---
# Se o upx.exe estiver disponível no PATH ou na pasta, aplica compressão extra.
if ($LASTEXITCODE -eq 0) {
    $exePath = Join-Path $outputDir "SystemOptimizer.exe"
    
    # Verifica se o comando 'upx' existe no sistema
    if (Get-Command upx -ErrorAction SilentlyContinue) {
        Write-Host "`n[Bonus] UPX detected! Applying ultra compression..." -ForegroundColor Cyan
        # --best: melhor compressão
        # --lzma: algoritmo mais eficiente
        upx --best --lzma "$exePath"
    } else {
        Write-Host "`n[Info] UPX not found. Skipping extra compression (Optional)." -ForegroundColor Gray
    }

    # Relatório Final
    if (Test-Path $exePath) {
        $size = (Get-Item $exePath).Length / 1MB
        $sizeFormatted = "{0:N2} MB" -f $size

        Write-Host "`nBuild Successful!" -ForegroundColor Green
        Write-Host "Executable created at: $exePath" -ForegroundColor Green
        Write-Host "Final Size: $sizeFormatted" -ForegroundColor Cyan
        Write-Host "Note: This is a portable, self-contained executable."
    } else {
        Write-Error "Build finished but executable not found."
    }
} else {
    Write-Error "Build failed."
}
