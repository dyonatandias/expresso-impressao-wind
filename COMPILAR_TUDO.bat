@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
cls

echo.
echo ========================================
echo   EXPRESSO DELIVERY PRINT CLIENT v2.3.0
echo   Compilacao Automatica
echo ========================================
echo.
echo IMPORTANTE: Se o executavel nao abrir depois de compilar:
echo   1. Verifique o log em: Logs\app-YYYY-MM-DD.log (mesma pasta)
echo   2. Execute como administrador (clique direito -^> Executar como admin)
echo   3. Verifique se .NET Desktop Runtime 6.0 esta instalado
echo.

:: Parar processo se estiver rodando
tasklist /FI "IMAGENAME eq ExpressoDeliveryPrintClient.exe" 2>NUL | find /I "ExpressoDeliveryPrintClient.exe" >NUL
if %errorLevel%==0 (
    echo [INFO] Parando processo existente...
    taskkill /F /IM ExpressoDeliveryPrintClient.exe >nul 2>&1
    timeout /t 2 /nobreak >nul
)

:: Limpar builds anteriores
echo [1/4] Limpando builds anteriores...
if exist "bin\Release" rmdir /S /Q bin\Release >nul 2>&1
if exist "obj" rmdir /S /Q obj >nul 2>&1
echo [OK] Limpeza concluida
echo.

:: Compilar
echo [2/4] Compilando executavel standalone...
echo       (Aguarde 2-3 minutos)
echo.

dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true /p:DebugType=None /p:DebugSymbols=false

if %errorLevel% neq 0 (
    echo.
    echo [ERRO] Falha na compilacao!
    echo.
    echo SOLUCAO: Instale o .NET SDK 6.0 ou superior
    echo Download: https://dotnet.microsoft.com/download/dotnet/6.0
    echo.
    pause
    exit /b 1
)

:: Copiar executavel para a raiz
echo.
echo [3/4] Copiando executavel para a raiz...

if exist "bin\Release\net6.0-windows\win-x64\publish\ExpressoDeliveryPrintClient.exe" (
    copy "bin\Release\net6.0-windows\win-x64\publish\ExpressoDeliveryPrintClient.exe" "ExpressoDeliveryPrintClient.exe" >nul 2>&1
    if !errorLevel!==0 (
        echo [OK] Executavel portatil copiado!

        :: Mostrar tamanho
        for %%A in ("ExpressoDeliveryPrintClient.exe") do (
            set SIZE=%%~zA
        )
        set /a SIZE_MB=!SIZE! / 1048576
        echo Tamanho: !SIZE_MB! MB
    ) else (
        echo [ERRO] Falha ao copiar executavel
    )
) else (
    echo [ERRO] Executavel nao encontrado
    echo.
    pause
    exit /b 1
)

:: Compilar instalador (se InnoSetup disponivel)
echo.
echo [4/4] Verificando InnoSetup para gerar instalador...

if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    echo [INFO] InnoSetup 6 encontrado! Gerando instalador...
    echo.

    :: Copiar exe para publish (referenciado pelo setup.iss)
    if not exist "publish" mkdir publish
    copy "ExpressoDeliveryPrintClient.exe" "publish\ExpressoDeliveryPrintClient.exe" >nul 2>&1

    :: Compilar instalador
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /Q "installer\setup.iss"

    if !errorLevel!==0 (
        echo [OK] Instalador gerado em: installer\output\
        for %%A in ("installer\output\ExpressoDeliveryPrintClient-Setup-v2.3.0.exe") do (
            set SETUP_SIZE=%%~zA
        )
        if defined SETUP_SIZE (
            set /a SETUP_MB=!SETUP_SIZE! / 1048576
            echo Tamanho: !SETUP_MB! MB
        )
    ) else (
        echo [AVISO] Falha ao gerar instalador. O executavel portatil esta OK.
        echo         Verifique se installer\setup.iss esta correto.
    )
) else (
    echo [INFO] InnoSetup nao encontrado. Apenas executavel portatil gerado.
    echo.
    echo        Para gerar tambem o instalador (Setup):
    echo        1. Instale InnoSetup 6: https://jrsoftware.org/isdl.php
    echo        2. Execute este script novamente
    echo        Ou use: installer\build-installer.ps1 (PowerShell)
)

echo.
echo ========================================
echo   COMPILACAO CONCLUIDA!
echo ========================================
echo.
echo Arquivos gerados:
echo   Portatil: ExpressoDeliveryPrintClient.exe (NESTA PASTA)
if exist "installer\output\ExpressoDeliveryPrintClient-Setup-v2.3.0.exe" (
    echo   Setup:    installer\output\ExpressoDeliveryPrintClient-Setup-v2.3.0.exe
)
echo.
echo Proximo passo:
echo   1. Executar: ExpressoDeliveryPrintClient.exe
echo      (Se nao abrir: clique direito -^> Executar como administrador)
echo.
echo   2. Configurar:
echo      - URL da API: https://delivery2.agenciaexpresso.com.br
echo      - API Key: (pegar no admin)
echo      - Secret Key: (pegar no admin)
echo      - Impressora: Selecionar da lista
echo.
echo   3. Salvar e Testar
echo.
echo Logs salvos em: Logs\app-YYYY-MM-DD.log (nesta pasta)
echo Config salvo em: config.json (nesta pasta)
echo.
echo Pronto! Sistema funcionando!
echo.
pause
