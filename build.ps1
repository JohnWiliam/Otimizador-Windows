# System Optimizer WinUI 3 Build Script
# Gera o ícone do aplicativo, restaura dependências e empacota a versão final como MSIX.

$ErrorActionPreference = "Stop"

function Add-DotNetToPathIfNeeded {
    if (Get-Command dotnet -ErrorAction SilentlyContinue) { return }

    $possiblePaths = @(
        "C:\Program Files\dotnet",
        "C:\Program Files (x86)\dotnet"
    )

    foreach ($path in $possiblePaths) {
        if (Test-Path (Join-Path $path "dotnet.exe")) {
            Write-Host "dotnet encontrado em $path; adicionando ao PATH desta sessão." -ForegroundColor Yellow
            $env:PATH = "$env:PATH;$path"
            return
        }
    }
}

Add-DotNetToPathIfNeeded

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "O SDK do .NET não foi encontrado. Instale o .NET SDK com suporte a Windows/WinUI 3."
}

$root = $PSScriptRoot
$iconResizerProj = Join-Path $root "src\IconResizer\IconResizer.csproj"
$mainProj = Join-Path $root "src\SystemOptimizer\SystemOptimizer.csproj"
$outputDir = Join-Path $root "Build"
$msixDir = Join-Path $outputDir "MSIX"
$assetsDir = Join-Path $root "src\SystemOptimizer\Assets"
$sourceLogo = Join-Path $assetsDir "logo.png"
$targetIcon = Join-Path $assetsDir "icon.ico"
$certPath = if ($env:SYSTEMOPTIMIZER_MSIX_CERTIFICATE) { $env:SYSTEMOPTIMIZER_MSIX_CERTIFICATE } else { Join-Path $outputDir "SystemOptimizer_TemporaryKey.pfx" }
$certPassword = if ($env:SYSTEMOPTIMIZER_MSIX_CERTIFICATE_PASSWORD) { $env:SYSTEMOPTIMIZER_MSIX_CERTIFICATE_PASSWORD } else { "SystemOptimizer-LocalBuild-ChangeMe!" }

Write-Host "Using .NET SDK version: $(dotnet --version)"

if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $msixDir -Force | Out-Null

Write-Host "`n[1/4] Building internal icon tool..."
dotnet build $iconResizerProj -c Release -v minimal
$resizerExe = Join-Path $root "src\IconResizer\bin\Release\net10.0-windows\IconResizer.exe"
if (-not (Test-Path $resizerExe)) {
    throw "IconResizer não foi encontrado em: $resizerExe"
}

Write-Host "`n[2/4] Generating high-resolution icon..."
if (Test-Path $sourceLogo) {
    & $resizerExe $sourceLogo $targetIcon
} else {
    Write-Warning "logo.png não encontrado em Assets. Mantendo o ícone atual."
}

Write-Host "`n[3/4] Restoring WinUI 3 dependencies..."
dotnet restore $mainProj -r win-x64

if (-not (Test-Path $certPath)) {
    if (-not (Get-Command New-SelfSignedCertificate -ErrorAction SilentlyContinue)) {
        throw "Nenhum certificado MSIX foi informado e New-SelfSignedCertificate não está disponível. Defina SYSTEMOPTIMIZER_MSIX_CERTIFICATE."
    }

    Write-Host "Gerando certificado local de assinatura MSIX em: $certPath" -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject "CN=SystemOptimizer" `
        -KeyUsage DigitalSignature `
        -FriendlyName "System Optimizer Local MSIX Signing" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

    $securePassword = ConvertTo-SecureString $certPassword -AsPlainText -Force
    Export-PfxCertificate -Cert $cert -FilePath $certPath -Password $securePassword | Out-Null
    Write-Warning "Certificado temporário criado. Para instalar o MSIX em outra máquina, confie este certificado ou informe um certificado oficial via SYSTEMOPTIMIZER_MSIX_CERTIFICATE."
}

Write-Host "`n[4/4] Publishing packaged WinUI 3 app as signed MSIX..."
dotnet publish $mainProj -c Release -r win-x64 --self-contained --no-restore `
    -p:WindowsPackageType=MSIX `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageDir="$msixDir\" `
    -p:AppxBundle=Never `
    -p:AppxPackageSigningEnabled=true `
    -p:PackageCertificateKeyFile="$certPath" `
    -p:PackageCertificatePassword="$certPassword"

$packages = Get-ChildItem -Path $msixDir -Recurse -Include *.msix,*.msixbundle | Sort-Object LastWriteTime -Descending
if (-not $packages) {
    throw "A publicação terminou sem gerar um pacote .MSIX em $msixDir."
}

$package = $packages | Select-Object -First 1
$sizeFormatted = "{0:N2} MB" -f ($package.Length / 1MB)
Write-Host "`nBuild Successful!" -ForegroundColor Green
Write-Host "MSIX package created at: $($package.FullName)" -ForegroundColor Green
Write-Host "Final Size: $sizeFormatted" -ForegroundColor Cyan
Write-Host "Note: o artefato distribuível agora é MSIX; não há empacotamento single-file .exe." -ForegroundColor Cyan
