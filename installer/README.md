# Instalador - Expresso Delivery Print Client v2.3.0

**Sistema de instalacao automatica para Windows**

---

## Visao Geral

Este diretorio contem todos os arquivos necessarios para criar um **instalador profissional** que:

- Copia aplicacao para pasta do usuario (sem precisar de admin)
- Cria atalhos (Desktop + Menu Iniciar)
- Configura auto-start no Windows (opcional)
- Verifica requisitos (Windows 10/11 64-bit)
- Cria desinstalador completo

**NOTA:** O executavel e self-contained (.NET 6). Nao requer instalacao de runtime separado.

---

## Como Criar o Instalador

### Pre-requisitos

1. **Windows 10/11 64-bit**
2. **.NET SDK 6.0+** - [Download](https://dotnet.microsoft.com/download/dotnet/6.0)
3. **Inno Setup 6** - [Download](https://jrsoftware.org/isdl.php)

---

### Opcao 1: Script Automatico (RECOMENDADO)

O `COMPILAR_TUDO.bat` na raiz do projeto compila o portatil E o instalador automaticamente:

```cmd
cd windows-print-client-dotnet
COMPILAR_TUDO.bat
```

Se InnoSetup estiver instalado, gera ambos:
- `ExpressoDeliveryPrintClient.exe` (portatil)
- `installer\output\ExpressoDeliveryPrintClient-Setup-v2.3.0.exe` (instalador)

---

### Opcao 2: PowerShell (build-installer.ps1)

```powershell
cd windows-print-client-dotnet\installer
.\build-installer.ps1
```

Este script:
1. Verifica pre-requisitos (InnoSetup, .NET SDK)
2. Compila aplicacao .NET em modo Release (self-contained)
3. Compila instalador com Inno Setup
4. Gera portatil + instalador

---

### Opcao 3: Manual (apenas InnoSetup)

Se o exe ja esta compilado:

```cmd
:: 1. Copiar exe para publish/ (referenciado pelo setup.iss)
mkdir publish
copy ExpressoDeliveryPrintClient.exe publish\ExpressoDeliveryPrintClient.exe

:: 2. Compilar instalador
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\setup.iss

:: 3. Resultado em:
dir installer\output\ExpressoDeliveryPrintClient-Setup-v2.3.0.exe
```

---

## Estrutura de Arquivos

```
installer/
  setup.iss                          - Script Inno Setup
  build-installer.ps1                - Script PowerShell para compilar tudo
  README.md                          - Este arquivo
  output/                            - Pasta criada automaticamente
    ExpressoDeliveryPrintClient-Setup-v2.3.0.exe  - INSTALADOR FINAL
```

---

## O Que o Instalador Faz

```
1. Verifica Windows 10/11 64-bit
         |
2. Copia aplicacao para %LOCALAPPDATA%\DeliveryPrintClient
         |
3. Cria atalhos (Desktop + Menu Iniciar)
         |
4. Configura auto-start (opcional, via Registry)
         |
5. Executa aplicativo (opcional)
```

### Pasta de Instalacao

O instalador usa `{localappdata}\DeliveryPrintClient` (sem precisar de admin):
- Exemplo: `C:\Users\Usuario\AppData\Local\DeliveryPrintClient`

### Atalhos Criados

- Desktop (opcional)
- Menu Iniciar
- Menu Iniciar > Desinstalar

### Auto-Start

Se selecionado, adiciona ao Registry:
```
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
  "Delivery Print Client" = "caminho\ExpressoDeliveryPrintClient.exe"
```

---

## Desinstalacao

O instalador cria um desinstalador completo:

1. Painel de Controle > Programas > Desinstalar
2. Selecionar "Expresso Delivery Print Client"
3. Clicar em "Desinstalar"

Remove:
- Todos os arquivos da pasta de instalacao
- Atalhos (Desktop + Menu Iniciar)
- Auto-start do Registry
- Configuracoes de %APPDATA%\DeliveryPrintClient (opcional)

---

## Troubleshooting

### InnoSetup nao encontrado

```cmd
:: Instale InnoSetup 6:
:: https://jrsoftware.org/isdl.php

:: Verifique instalacao:
dir "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

### .NET SDK nao encontrado

```cmd
:: Instale .NET 6.0 SDK:
:: https://dotnet.microsoft.com/download/dotnet/6.0

:: Verifique:
dotnet --version
```

### Instalador gerado mas muito pequeno

Se o instalador tem menos de 50MB, o exe pode nao ter sido copiado para `publish/`.

```cmd
:: Verificar:
dir publish\ExpressoDeliveryPrintClient.exe

:: Se nao existir:
mkdir publish
copy ExpressoDeliveryPrintClient.exe publish\ExpressoDeliveryPrintClient.exe

:: Recompilar:
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" setup.iss
```

---

## Personalizacao

### Mudar pasta de instalacao

Edite `setup.iss`:
```pascal
DefaultDirName={localappdata}\DeliveryPrintClient
```

Opcoes: `{localappdata}`, `{autopf}` (Program Files), `{userdocs}`

### Mudar icone

```pascal
SetupIconFile=..\app-icon.ico
```

---

**Versao:** 2.3.0
**Data:** 05/02/2026
**Exe:** Self-contained (.NET 6) - nao requer runtime separado
