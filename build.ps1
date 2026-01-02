# System Optimizer Build Script
# Compila o gerador de ícones, cria o ícone e publica o app principal com otimização segura.

$ErrorActionPreference = "Stop"

# --- Verificação e Correção do PATH do .NET ---
Write-Host "Checking for .NET SDK..."

# 1. Verifica se o comando já existe no PATH
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    # 2. Se não encontrar, procura nos locais padrão (x64 e x86)
    $possiblePaths = @(
        "C:\Program Files\dotnet",
        "C:\Program Files (x86)\dotnet"
    )

    foreach ($path in $possiblePaths) {
        if (Test-Path "$path\dotnet.exe") {
            Write-Host "O comando 'dotnet' não estava no PATH, mas foi encontrado em: $path" -ForegroundColor Yellow
            Write-Host "Adicionando ao PATH temporariamente..." -ForegroundColor Yellow
            $env:PATH = "$env:PATH;$path"
            break
        }
    }
}

# 3. Verificação Final (Se falhar aqui, realmente não está acessível)
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "ERRO CRÍTICO: O SDK do .NET não foi encontrado." -ForegroundColor Red
    Write-Host "Certifique-se de que o .NET 10 SDK está instalado corretamente."
    Write-Host "Dica: Tente fechar e abrir novamente este terminal ou reiniciar o PC."
    Write-Host "Pressione Enter para sair..."
    Read-Host
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
# Caminho ajustado para .NET 10 conforme sua estrutura
$resizerExe = Join-Path $root "src\IconResizer\bin\Release\net10.0-windows\IconResizer.exe"

if (-not (Test-Path $resizerExe)) {
    Write-Host "Failed to build IconResizer tool." -ForegroundColor Red
    Write-Host "Verifique se o caminho de saída está correto em: $resizerExe"
    Write-Host "Pressione Enter para sair..."
    Read-Host
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
Write-Host "`n[3/3] Building and Publishing SystemOptimizer (SingleFile Compressed)..."

if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}

# Restaura especificamente para win-x64 antes do publish
Write-Host "Restoring dependencies..."
dotnet restore $mainProj -r win-x64

Write-Host "Publishing..."
# Flags explicadas:
# -p:PublishSingleFile=true            : Gera um único EXE.
# -p:EnableCompressionInSingleFile=true : Comprime o conteúdo dentro do EXE (Reduz tamanho).
# -p:PublishReadyToRun=false           : Desativa pré-compilação nativa (Reduz tamanho significativamente).
# -p:IncludeNativeLibrariesForSelfExtract=true : Inclui libs nativas necessárias.

dotnet publish $mainProj -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishReadyToRun=false `
    --no-restore `
    -o $outputDir

# --- Optional Step: UPX Compression ---
# Se o upx.exe estiver disponível no PATH, aplica compressão extra.
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
        Write-Host "Build finished but executable not found." -ForegroundColor Red
    }
} else {
    Write-Host "Build failed." -ForegroundColor Red
}

# --- PAUSA FINAL ---
Write-Host "`nProcesso finalizado. Pressione Enter para fechar..." -ForegroundColor Yellow
Read-Host
