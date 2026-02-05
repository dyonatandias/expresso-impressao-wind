# ================================================================
# Script para Compilar o Instalador
# ================================================================
# Expresso Delivery Print Client v2.3.0
# Data: 05/02/2026
# ================================================================
# O executavel e self-contained (.NET 6) - nao requer runtime
# ================================================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Build do Instalador" -ForegroundColor Cyan
Write-Host " Expresso Delivery Print Client v2.3.0" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ================================================================
# 1. VERIFICAR PRE-REQUISITOS
# ================================================================

Write-Host "Verificando pre-requisitos..." -ForegroundColor Yellow
Write-Host ""

# Verificar se Inno Setup esta instalado
$innoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $innoSetupPath)) {
    Write-Host "Inno Setup 6 nao encontrado!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Por favor, instale o Inno Setup 6:" -ForegroundColor Yellow
    Write-Host "   1. Acesse: https://jrsoftware.org/isdl.php" -ForegroundColor Cyan
    Write-Host "   2. Baixe: innosetup-6.x.x.exe" -ForegroundColor Cyan
    Write-Host "   3. Instale (marque 'Install Inno Setup Preprocessor')" -ForegroundColor Cyan
    Write-Host "   4. Execute este script novamente" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Pressione qualquer tecla para sair..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host "  Inno Setup 6 encontrado" -ForegroundColor Green

# Verificar se .NET SDK esta instalado
$dotnetVersion = dotnet --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "  .NET SDK nao encontrado!" -ForegroundColor Red
    Write-Host "   Por favor, instale .NET 6.0 SDK de: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

Write-Host "  .NET SDK $dotnetVersion encontrado" -ForegroundColor Green

# Verificar se LICENSE.txt existe
$licensePath = "$PSScriptRoot\..\LICENSE.txt"
if (-not (Test-Path $licensePath)) {
    Write-Host "  LICENSE.txt nao encontrado. Criando arquivo padrao..." -ForegroundColor Yellow

    $licenseContent = @"
MIT License

Copyright (c) 2025-2026 Agencia Expresso

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
"@

    Set-Content -Path $licensePath -Value $licenseContent -Encoding UTF8
    Write-Host "   LICENSE.txt criado" -ForegroundColor Green
}

Write-Host ""

# ================================================================
# 2. COMPILAR APLICACAO .NET
# ================================================================

Write-Host "Compilando aplicacao .NET (self-contained)..." -ForegroundColor Yellow
Write-Host ""

$projectDir = "$PSScriptRoot\.."
$publishDir = "$projectDir\bin\Release\net6.0-windows\win-x64\publish"

# Limpar build anterior
if (Test-Path $publishDir) {
    Write-Host "   Limpando build anterior..." -ForegroundColor Gray
    Remove-Item $publishDir -Recurse -Force
}

# Publicar aplicacao (self-contained, single-file)
Write-Host "   Publicando aplicacao..." -ForegroundColor Cyan

Push-Location $projectDir

$publishCommand = "dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false"

Write-Host "   Executando: $publishCommand" -ForegroundColor DarkGray
Write-Host ""

Invoke-Expression $publishCommand

if ($LASTEXITCODE -ne 0) {
    Pop-Location
    Write-Host ""
    Write-Host "Erro ao compilar aplicacao!" -ForegroundColor Red
    exit 1
}

Pop-Location

Write-Host ""
Write-Host "  Aplicacao compilada com sucesso" -ForegroundColor Green

# Verificar se executavel foi gerado
$exePath = "$publishDir\ExpressoDeliveryPrintClient.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "Executavel nao encontrado em: $publishDir" -ForegroundColor Red
    exit 1
}

$exeSize = (Get-Item $exePath).Length / 1MB
Write-Host "   Tamanho: $([math]::Round($exeSize, 2)) MB" -ForegroundColor Gray

# Copiar para publish/ (referenciado pelo setup.iss)
$publishTargetDir = "$projectDir\publish"
if (-not (Test-Path $publishTargetDir)) {
    New-Item -ItemType Directory -Path $publishTargetDir | Out-Null
}
Copy-Item $exePath "$publishTargetDir\ExpressoDeliveryPrintClient.exe" -Force
Write-Host "   Copiado para publish/" -ForegroundColor Gray

# Copiar para raiz (portatil)
Copy-Item $exePath "$projectDir\ExpressoDeliveryPrintClient.exe" -Force
Write-Host "   Copiado para raiz (portatil)" -ForegroundColor Gray

Write-Host ""

# ================================================================
# 3. COMPILAR INSTALADOR COM INNO SETUP
# ================================================================

Write-Host "Compilando instalador com Inno Setup..." -ForegroundColor Yellow
Write-Host ""

$setupScript = "$PSScriptRoot\setup.iss"

if (-not (Test-Path $setupScript)) {
    Write-Host "Arquivo setup.iss nao encontrado!" -ForegroundColor Red
    exit 1
}

# Criar pasta de output se nao existir
$outputDir = "$PSScriptRoot\output"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

# Executar Inno Setup Compiler
Write-Host "   Compilando setup.iss..." -ForegroundColor Cyan

$isccArgs = @("/Q", $setupScript)  # /Q = Quiet mode

& $innoSetupPath $isccArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Erro ao compilar instalador!" -ForegroundColor Red
    Write-Host "   Verifique o arquivo setup.iss" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "  Instalador compilado com sucesso!" -ForegroundColor Green

# ================================================================
# 4. RESUMO
# ================================================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " COMPILACAO CONCLUIDA!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Mostrar portatil
$portableExe = "$projectDir\ExpressoDeliveryPrintClient.exe"
if (Test-Path $portableExe) {
    $portableSize = (Get-Item $portableExe).Length / 1MB
    Write-Host "  Portatil: ExpressoDeliveryPrintClient.exe" -ForegroundColor Cyan
    Write-Host "   Localizacao: $portableExe" -ForegroundColor Gray
    Write-Host "   Tamanho: $([math]::Round($portableSize, 2)) MB" -ForegroundColor Gray
    Write-Host ""
}

# Mostrar instalador
$installerFiles = Get-ChildItem $outputDir -Filter "*.exe" -ErrorAction SilentlyContinue

if ($installerFiles -and $installerFiles.Count -gt 0) {
    foreach ($installer in $installerFiles) {
        $installerSize = $installer.Length / 1MB
        Write-Host "  Instalador: $($installer.Name)" -ForegroundColor Cyan
        Write-Host "   Localizacao: $($installer.FullName)" -ForegroundColor Gray
        Write-Host "   Tamanho: $([math]::Round($installerSize, 2)) MB" -ForegroundColor Gray
        Write-Host ""
    }

    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " O QUE O INSTALADOR FAZ:" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Verifica Windows 10/11 64-bit" -ForegroundColor Green
    Write-Host "  Copia aplicacao para pasta do usuario" -ForegroundColor Green
    Write-Host "  Cria atalhos (Desktop + Menu Iniciar)" -ForegroundColor Green
    Write-Host "  Configura auto-start no Windows (opcional)" -ForegroundColor Green
    Write-Host "  Cria desinstalador completo" -ForegroundColor Green
    Write-Host ""
    Write-Host "  NOTA: O exe e self-contained (.NET 6)." -ForegroundColor Yellow
    Write-Host "  Nao requer instalacao de runtime separado." -ForegroundColor Yellow
    Write-Host ""

    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host " PROXIMO PASSO:" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "Distribua o instalador ou o portatil para os usuarios!" -ForegroundColor Cyan
}
else {
    Write-Host "  Nenhum instalador foi gerado." -ForegroundColor Yellow
    Write-Host "   Verifique os logs acima para erros." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Pressione qualquer tecla para sair..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
