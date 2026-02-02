# ================================================================
# Script de Verificação de Requisitos
# DeliveryPrintClient - Windows Print Client
# ================================================================
# Versão: 1.0
# Data: 19/11/2025
# ================================================================

param(
    [switch]$Fix = $false  # Se true, tenta corrigir problemas automaticamente
)

# Cores para output
$ESC = [char]27
$RED = "$ESC[91m"
$GREEN = "$ESC[92m"
$YELLOW = "$ESC[93m"
$BLUE = "$ESC[94m"
$RESET = "$ESC[0m"

# Contadores
$checksPassed = 0
$checksFailed = 0
$warnings = 0

# ================================================================
# Funções Auxiliares
# ================================================================

function Write-Header {
    Write-Host ""
    Write-Host "==========================================" -ForegroundColor Cyan
    Write-Host " Verificação de Requisitos" -ForegroundColor Cyan
    Write-Host " DeliveryPrintClient v1.4.2" -ForegroundColor Cyan
    Write-Host "==========================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Check {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Details = ""
    )

    if ($Passed) {
        Write-Host "✅ $Name" -ForegroundColor Green -NoNewline
        if ($Details) {
            Write-Host ": $Details" -ForegroundColor Gray
        } else {
            Write-Host ""
        }
        $script:checksPassed++
    } else {
        Write-Host "❌ $Name" -ForegroundColor Red -NoNewline
        if ($Details) {
            Write-Host ": $Details" -ForegroundColor Gray
        } else {
            Write-Host ""
        }
        $script:checksFailed++
    }
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠️  $Message" -ForegroundColor Yellow
    $script:warnings++
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ️  $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

# ================================================================
# Verificações
# ================================================================

Write-Header

# ================================================================
# 1. Verificar Windows 10/11 (64-bit)
# ================================================================

Write-Host "1. Verificando Sistema Operacional..." -ForegroundColor Yellow
Write-Host ""

$os = Get-CimInstance Win32_OperatingSystem
$osName = $os.Caption
$osVersion = $os.Version
$osArchitecture = $os.OSArchitecture

Write-Info "Sistema: $osName"
Write-Info "Versão: $osVersion"
Write-Info "Arquitetura: $osArchitecture"
Write-Host ""

# Verificar se é Windows 10/11 64-bit
$isWindows10or11 = $osName -match "Windows 10|Windows 11"
$is64bit = $osArchitecture -eq "64-bit"

if ($isWindows10or11 -and $is64bit) {
    Write-Check "Windows 10/11 (64-bit)" $true "OK"
} else {
    if (-not $isWindows10or11) {
        Write-Check "Windows 10/11 (64-bit)" $false "Sistema não suportado: $osName"
        Write-Warning "DeliveryPrintClient requer Windows 10 ou 11"
    }
    if (-not $is64bit) {
        Write-Check "Windows 10/11 (64-bit)" $false "Arquitetura incorreta: $osArchitecture"
        Write-Warning "DeliveryPrintClient requer sistema 64-bit"
    }
}

Write-Host ""

# ================================================================
# 2. Verificar Visual C++ Redistributable 2015-2022 (x64)
# ================================================================

Write-Host "2. Verificando Visual C++ Redistributable..." -ForegroundColor Yellow
Write-Host ""

$vcRedistInstalled = $false
$vcRedistPaths = @(
    "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
)

foreach ($path in $vcRedistPaths) {
    if (Test-Path $path) {
        $vcRedist = Get-ItemProperty $path -ErrorAction SilentlyContinue
        if ($vcRedist) {
            $vcRedistInstalled = $true
            Write-Info "Versão instalada: $($vcRedist.Version)"
            break
        }
    }
}

if ($vcRedistInstalled) {
    Write-Check "Visual C++ Redistributable 2015-2022 (x64)" $true "INSTALADO"
} else {
    Write-Check "Visual C++ Redistributable 2015-2022 (x64)" $false "NÃO INSTALADO"
    Write-Host ""
    Write-Warning "Visual C++ Redistributable é OBRIGATÓRIO!"
    Write-Host ""
    Write-Host "   Download: " -NoNewline -ForegroundColor Cyan
    Write-Host "https://aka.ms/vs/17/release/vc_redist.x64.exe" -ForegroundColor White
    Write-Host ""

    if ($Fix) {
        Write-Host "   Tentando baixar e instalar..." -ForegroundColor Yellow
        try {
            $downloadUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe"
            $downloadPath = "$env:TEMP\vc_redist.x64.exe"
            Invoke-WebRequest -Uri $downloadUrl -OutFile $downloadPath
            Start-Process -FilePath $downloadPath -ArgumentList "/install", "/quiet", "/norestart" -Wait
            Write-Success "Visual C++ Redistributable instalado com sucesso!"
            Write-Warning "Reinicie o computador para aplicar as mudanças."
        } catch {
            Write-Error-Custom "Falha ao baixar/instalar: $_"
        }
    }
}

Write-Host ""

# ================================================================
# 3. Verificar .NET Desktop Runtime 6.0
# ================================================================

Write-Host "3. Verificando .NET Desktop Runtime..." -ForegroundColor Yellow
Write-Host ""

$dotnetInstalled = $false
$dotnetVersion = ""

try {
    $runtimes = & dotnet --list-runtimes 2>&1
    foreach ($runtime in $runtimes) {
        if ($runtime -match "Microsoft\.WindowsDesktop\.App\s+(6\.\d+\.\d+)") {
            $dotnetInstalled = $true
            $dotnetVersion = $matches[1]
            Write-Info "Versão instalada: $dotnetVersion"
            break
        }
    }
} catch {
    # dotnet não encontrado
}

if ($dotnetInstalled) {
    Write-Check ".NET Desktop Runtime 6.0" $true "INSTALADO"
} else {
    Write-Check ".NET Desktop Runtime 6.0" $false "NÃO INSTALADO"
    Write-Host ""
    Write-Warning ".NET Desktop Runtime é OBRIGATÓRIO!"
    Write-Host ""
    Write-Host "   Download: " -NoNewline -ForegroundColor Cyan
    Write-Host "https://dotnet.microsoft.com/download/dotnet/6.0/runtime" -ForegroundColor White
    Write-Host "   Selecione: 'Windows Desktop Runtime 6.0.x (x64)'" -ForegroundColor Gray
    Write-Host ""
}

Write-Host ""

# ================================================================
# 4. Verificar Impressoras Instaladas
# ================================================================

Write-Host "4. Verificando Impressoras..." -ForegroundColor Yellow
Write-Host ""

$printers = Get-Printer -ErrorAction SilentlyContinue

if ($printers) {
    $printerCount = ($printers | Measure-Object).Count
    Write-Check "Impressoras encontradas" $true "$printerCount impressora(s)"
    Write-Host ""

    Write-Info "Impressoras disponíveis:"
    foreach ($printer in $printers) {
        $isDefault = if ($printer.Name -eq (Get-CimInstance Win32_Printer | Where-Object { $_.Default -eq $true }).Name) { " (Padrão)" } else { "" }
        Write-Host "   - $($printer.Name)$isDefault" -ForegroundColor Gray
        Write-Host "     Driver: $($printer.DriverName)" -ForegroundColor DarkGray
        Write-Host "     Porta: $($printer.PortName)" -ForegroundColor DarkGray
        Write-Host ""
    }
} else {
    Write-Check "Impressoras encontradas" $false "NENHUMA IMPRESSORA INSTALADA"
    Write-Host ""
    Write-Warning "Você precisa instalar uma impressora térmica!"
    Write-Host ""
    Write-Info "Como instalar:"
    Write-Host "   1. Conecte a impressora térmica USB" -ForegroundColor Gray
    Write-Host "   2. Instale o driver fornecido pelo fabricante" -ForegroundColor Gray
    Write-Host "   3. Configure a impressora no Windows" -ForegroundColor Gray
    Write-Host ""
}

Write-Host ""

# ================================================================
# 5. Verificar Conectividade com API
# ================================================================

Write-Host "5. Verificando Conectividade com API..." -ForegroundColor Yellow
Write-Host ""

$apiUrl = "delivery2.agenciaexpresso.com.br"
$apiPort = 443

try {
    $connection = Test-NetConnection -ComputerName $apiUrl -Port $apiPort -WarningAction SilentlyContinue

    if ($connection.TcpTestSucceeded) {
        Write-Check "Conectividade API (HTTPS)" $true "OK"
        Write-Info "IP: $($connection.RemoteAddress)"
    } else {
        Write-Check "Conectividade API (HTTPS)" $false "FALHA"
        Write-Host ""
        Write-Warning "Não foi possível conectar à API!"
        Write-Host ""
        Write-Info "Verifique:"
        Write-Host "   - Conexão com a internet" -ForegroundColor Gray
        Write-Host "   - Firewall do Windows" -ForegroundColor Gray
        Write-Host "   - Antivírus/Firewall de terceiros" -ForegroundColor Gray
        Write-Host ""
    }
} catch {
    Write-Check "Conectividade API (HTTPS)" $false "ERRO: $_"
}

Write-Host ""

# ================================================================
# 6. Verificar Arquivo Desbloqueado
# ================================================================

Write-Host "6. Verificando DeliveryPrintClient.exe..." -ForegroundColor Yellow
Write-Host ""

$exePath = Join-Path $PSScriptRoot "DeliveryPrintClient.exe"

if (Test-Path $exePath) {
    Write-Info "Arquivo encontrado: $exePath"

    # Verificar se está bloqueado
    $fileInfo = Get-Item $exePath -Stream * -ErrorAction SilentlyContinue | Where-Object { $_.Stream -eq "Zone.Identifier" }

    if ($fileInfo) {
        Write-Check "Arquivo desbloqueado" $false "BLOQUEADO"
        Write-Host ""
        Write-Warning "O arquivo está bloqueado pelo Windows!"
        Write-Host ""
        Write-Info "Como desbloquear:"
        Write-Host "   1. Clique com botão direito em DeliveryPrintClient.exe" -ForegroundColor Gray
        Write-Host "   2. Selecione 'Propriedades'" -ForegroundColor Gray
        Write-Host "   3. Marque 'Desbloquear' na aba 'Geral'" -ForegroundColor Gray
        Write-Host "   4. Clique em 'OK'" -ForegroundColor Gray
        Write-Host ""

        if ($Fix) {
            Write-Host "   Tentando desbloquear automaticamente..." -ForegroundColor Yellow
            try {
                Unblock-File -Path $exePath
                Write-Success "Arquivo desbloqueado com sucesso!"
            } catch {
                Write-Error-Custom "Falha ao desbloquear: $_"
            }
        }
    } else {
        Write-Check "Arquivo desbloqueado" $true "OK"
    }

    # Verificar tamanho
    $fileSize = (Get-Item $exePath).Length / 1MB
    Write-Info "Tamanho: $([math]::Round($fileSize, 2)) MB"

} else {
    Write-Check "DeliveryPrintClient.exe encontrado" $false "ARQUIVO NÃO ENCONTRADO"
    Write-Host ""
    Write-Warning "O arquivo DeliveryPrintClient.exe não foi encontrado nesta pasta!"
    Write-Host ""
    Write-Info "Certifique-se de que:"
    Write-Host "   - O arquivo está na mesma pasta deste script" -ForegroundColor Gray
    Write-Host "   - Você baixou o executável corretamente" -ForegroundColor Gray
    Write-Host ""
}

Write-Host ""

# ================================================================
# 7. Verificar Permissões de Administrador (Opcional)
# ================================================================

Write-Host "7. Verificando Permissões..." -ForegroundColor Yellow
Write-Host ""

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if ($isAdmin) {
    Write-Info "Executando como Administrador: SIM"
    Write-Host "   Todas as permissões disponíveis" -ForegroundColor Gray
} else {
    Write-Info "Executando como Administrador: NÃO"
    Write-Host "   ⚠️  Para primeira execução, recomenda-se executar como Administrador" -ForegroundColor Yellow
}

Write-Host ""

# ================================================================
# Resumo Final
# ================================================================

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " RESUMO DA VERIFICAÇÃO" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Verificações passaram: " -NoNewline -ForegroundColor White
Write-Host "$checksPassed" -ForegroundColor Green

Write-Host "Verificações falharam: " -NoNewline -ForegroundColor White
Write-Host "$checksFailed" -ForegroundColor Red

Write-Host "Avisos: " -NoNewline -ForegroundColor White
Write-Host "$warnings" -ForegroundColor Yellow

Write-Host ""

if ($checksFailed -eq 0) {
    Write-Host "==========================================" -ForegroundColor Green
    Write-Host " ✅ TODOS OS REQUISITOS ATENDIDOS!" -ForegroundColor Green
    Write-Host " Você pode executar DeliveryPrintClient.exe" -ForegroundColor Green
    Write-Host "==========================================" -ForegroundColor Green
} else {
    Write-Host "==========================================" -ForegroundColor Red
    Write-Host " ❌ REQUISITOS NÃO ATENDIDOS" -ForegroundColor Red
    Write-Host " Corrija os problemas acima antes de executar" -ForegroundColor Red
    Write-Host "==========================================" -ForegroundColor Red
    Write-Host ""

    if (-not $Fix) {
        Write-Info "Dica: Execute com -Fix para tentar corrigir automaticamente:"
        Write-Host "   .\verificar-requisitos.ps1 -Fix" -ForegroundColor Cyan
    }
}

Write-Host ""

# ================================================================
# Instruções de Instalação
# ================================================================

if ($checksFailed -gt 0) {
    Write-Host "=========================================="  -ForegroundColor Yellow
    Write-Host " INSTRUÇÕES DE INSTALAÇÃO" -ForegroundColor Yellow
    Write-Host "==========================================" -ForegroundColor Yellow
    Write-Host ""

    if (-not $vcRedistInstalled) {
        Write-Host "📥 Visual C++ Redistributable:" -ForegroundColor Cyan
        Write-Host "   1. Baixe: https://aka.ms/vs/17/release/vc_redist.x64.exe" -ForegroundColor Gray
        Write-Host "   2. Execute como Administrador" -ForegroundColor Gray
        Write-Host "   3. Instale e reinicie o PC" -ForegroundColor Gray
        Write-Host ""
    }

    if (-not $dotnetInstalled) {
        Write-Host "📥 .NET Desktop Runtime 6.0:" -ForegroundColor Cyan
        Write-Host "   1. Acesse: https://dotnet.microsoft.com/download/dotnet/6.0/runtime" -ForegroundColor Gray
        Write-Host "   2. Baixe 'Windows Desktop Runtime 6.0.x (x64)'" -ForegroundColor Gray
        Write-Host "   3. Execute como Administrador" -ForegroundColor Gray
        Write-Host "   4. Instale e reinicie o PC" -ForegroundColor Gray
        Write-Host ""
    }

    Write-Host "📚 Documentação Completa:" -ForegroundColor Cyan
    Write-Host "   Leia: REQUISITOS_WINDOWS.md" -ForegroundColor Gray
    Write-Host ""
}

Write-Host ""
Write-Host "Pressione qualquer tecla para sair..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
