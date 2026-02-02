# ========================================
# Delivery Print Client v1.4.2
# Script de Setup Completo Automático
# ========================================

$ErrorActionPreference = "Stop"

# Cores
function Write-Success { param($msg) Write-Host "✅ $msg" -ForegroundColor Green }
function Write-Error { param($msg) Write-Host "❌ $msg" -ForegroundColor Red }
function Write-Info { param($msg) Write-Host "ℹ️  $msg" -ForegroundColor Cyan }
function Write-Warning { param($msg) Write-Host "⚠️  $msg" -ForegroundColor Yellow }

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SETUP COMPLETO - DELIVERY PRINT CLIENT" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ========================================
# ETAPA 1: Verificar/Instalar .NET SDK
# ========================================

Write-Info "ETAPA 1/4: Verificando .NET SDK 6.0..."

$dotnetInstalled = $false
try {
    $dotnetVersion = & dotnet --version 2>$null
    if ($dotnetVersion) {
        $versionNumber = [version]$dotnetVersion
        if ($versionNumber.Major -ge 6) {
            Write-Success ".NET SDK $dotnetVersion já instalado"
            $dotnetInstalled = $true
        }
    }
} catch {
    # dotnet não encontrado
}

if (-not $dotnetInstalled) {
    Write-Warning ".NET SDK 6.0 não encontrado. Iniciando download e instalação..."
    Write-Info "Tamanho: ~200 MB | Tempo estimado: 5 minutos"

    $dotnetUrl = "https://download.visualstudio.microsoft.com/download/pr/b395fa18-c53b-4f7f-bf91-6b2d3c43fedb/d83a318111da9e15f5ecebfd2d190e89/dotnet-sdk-6.0.428-win-x64.exe"
    $dotnetInstaller = "$env:TEMP\dotnet-sdk-6.0-installer.exe"

    Write-Info "Baixando .NET SDK 6.0..."
    Invoke-WebRequest -Uri $dotnetUrl -OutFile $dotnetInstaller -UseBasicParsing

    Write-Info "Instalando .NET SDK 6.0 (aguarde, pode demorar 5 minutos)..."
    Start-Process -FilePath $dotnetInstaller -ArgumentList "/install", "/quiet", "/norestart" -Wait -NoNewWindow

    Remove-Item $dotnetInstaller -Force

    Write-Success ".NET SDK 6.0 instalado com sucesso!"
    Write-Warning "Reinicie o PowerShell se houver problemas"
}

Write-Host ""

# ========================================
# ETAPA 2: Verificar/Instalar Inno Setup
# ========================================

Write-Info "ETAPA 2/4: Verificando Inno Setup 6..."

$innoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$innoInstalled = Test-Path $innoSetupPath

if ($innoInstalled) {
    Write-Success "Inno Setup 6 já instalado"
} else {
    Write-Warning "Inno Setup 6 não encontrado. Iniciando download e instalação..."
    Write-Info "Tamanho: ~3 MB | Tempo estimado: 2 minutos"

    $innoUrl = "https://jrsoftware.org/download.php/is.exe"
    $innoInstaller = "$env:TEMP\innosetup-installer.exe"

    Write-Info "Baixando Inno Setup 6..."
    Invoke-WebRequest -Uri $innoUrl -OutFile $innoInstaller -UseBasicParsing

    Write-Info "Instalando Inno Setup 6 (aguarde)..."
    # Instalação silenciosa do Inno Setup
    Start-Process -FilePath $innoInstaller -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-" -Wait -NoNewWindow

    Remove-Item $innoInstaller -Force

    # Verificar se instalou
    if (Test-Path $innoSetupPath) {
        Write-Success "Inno Setup 6 instalado com sucesso!"
    } else {
        Write-Error "Falha ao instalar Inno Setup 6. Instale manualmente de https://jrsoftware.org/isdl.php"
        exit 1
    }
}

Write-Host ""

# ========================================
# ETAPA 3: Compilar Aplicação
# ========================================

Write-Info "ETAPA 3/4: Compilando aplicação .NET..."

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectDir

Write-Info "Restaurando pacotes NuGet..."
& dotnet restore

Write-Info "Compilando projeto (modo Release, self-contained)..."
& dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "Erro ao compilar aplicação!"
    exit 1
}

Write-Success "Aplicação compilada com sucesso!"

# Verificar se executável foi gerado
$exePath = Join-Path $projectDir "bin\Release\net6.0-windows\win-x64\publish\DeliveryPrintClient.exe"
if (Test-Path $exePath) {
    $exeSize = (Get-Item $exePath).Length / 1MB
    Write-Success "Executável gerado: $([math]::Round($exeSize, 2)) MB"
} else {
    Write-Error "Executável não encontrado em: $exePath"
    exit 1
}

Write-Host ""

# ========================================
# ETAPA 4: Criar Instalador
# ========================================

Write-Info "ETAPA 4/4: Criando instalador com Inno Setup..."

$installerDir = Join-Path $projectDir "installer"
$setupScript = Join-Path $installerDir "setup.iss"
$outputDir = Join-Path $installerDir "output"

# Criar pasta output se não existir
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

Write-Info "Executando Inno Setup Compiler..."
& $innoSetupPath $setupScript

if ($LASTEXITCODE -ne 0) {
    Write-Error "Erro ao criar instalador!"
    exit 1
}

Write-Success "Instalador criado com sucesso!"

# Verificar instalador
$installerPath = Join-Path $outputDir "DeliveryPrintClient-Setup-v1.4.2.exe"
if (Test-Path $installerPath) {
    $installerSize = (Get-Item $installerPath).Length / 1MB
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  INSTALADOR CRIADO COM SUCESSO!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "📦 Instalador: DeliveryPrintClient-Setup-v1.4.2.exe" -ForegroundColor Cyan
    Write-Host "   Localização: $installerPath" -ForegroundColor Gray
    Write-Host "   Tamanho: $([math]::Round($installerSize, 2)) MB" -ForegroundColor Gray
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " O QUE O INSTALADOR FAZ:" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Success "Verifica Windows 10/11 64-bit"
    Write-Success "Instala Visual C++ Redistributable (se necessário)"
    Write-Success "Instala .NET Desktop Runtime 6.0 (se necessário)"
    Write-Success "Copia aplicação para C:\Program Files\Delivery Print Client"
    Write-Success "Cria atalhos (Desktop + Menu Iniciar)"
    Write-Success "Configura auto-start no Windows (opcional)"
    Write-Success "Cria desinstalador completo"
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " DISTRIBUIÇÃO:" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Envie este arquivo para os usuários finais." -ForegroundColor Gray
    Write-Host "Eles devem executar como Administrador." -ForegroundColor Gray
    Write-Host "Instalação levará 10-15 minutos (automática)." -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Error "Instalador não encontrado em: $installerPath"
    exit 1
}

Write-Host ""
Write-Success "SETUP COMPLETO FINALIZADO!"
Write-Host ""
