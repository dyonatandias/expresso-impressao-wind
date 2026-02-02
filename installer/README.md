# 📦 Instalador Profissional - Delivery Print Client v1.4.2

**Sistema de instalação automática com dependências**

---

## 📋 Visão Geral

Este diretório contém todos os arquivos necessários para criar um **instalador profissional** do Delivery Print Client que:

- ✅ **Instala automaticamente** Visual C++ Redistributable 2015-2022
- ✅ **Instala automaticamente** .NET Desktop Runtime 6.0
- ✅ **Cria atalhos** (Desktop + Menu Iniciar)
- ✅ **Configura auto-start** no Windows (opcional)
- ✅ **Verifica requisitos** (Windows 10/11 64-bit)
- ✅ **Desinstalador completo** incluído

---

## 🚀 Como Criar o Instalador (Passo a Passo)

### Pré-requisitos

1. **Windows 10/11 64-bit**
2. **.NET SDK 6.0+** - [Download](https://dotnet.microsoft.com/download)
3. **Inno Setup 6** - [Download](https://jrsoftware.org/isdl.php)
4. **PowerShell** (como Administrador)

---

### PASSO 1: Instalar Inno Setup

```powershell
# 1. Baixe Inno Setup 6 de:
https://jrsoftware.org/isdl.php

# 2. Execute o instalador:
innosetup-6.x.x.exe

# 3. Instale na pasta padrão:
C:\Program Files (x86)\Inno Setup 6\
```

**Importante:** Marque a opção **"Install Inno Setup Preprocessor"** durante a instalação.

---

### PASSO 2: Baixar Dependências

```powershell
# No PowerShell (como Administrador):
cd windows-print-client-dotnet\installer
.\download-dependencies.ps1
```

**O que este script faz:**
- 📥 Baixa Visual C++ Redistributable 2015-2022 (x64) (~25 MB)
- 📥 Baixa .NET Desktop Runtime 6.0 (x64) (~55 MB)
- 💾 Salva em `installer/dependencies/`

**Saída esperada:**
```
✅ Todas as dependências foram baixadas!

Arquivos na pasta 'dependencies':
   📦 vc_redist.x64.exe - 25.34 MB
   📦 windowsdesktop-runtime-6.0-win-x64.exe - 54.67 MB
```

---

### PASSO 3: Compilar o Instalador

```powershell
# No PowerShell (como Administrador):
.\build-installer.ps1
```

**O que este script faz:**

1. ✅ Verifica pré-requisitos (Inno Setup, .NET SDK, dependências)
2. 🔨 Compila aplicação .NET em modo Release
3. 📦 Publica como executável standalone (147 MB)
4. 🔨 Compila instalador com Inno Setup
5. 💾 Gera instalador em `installer/output/`

**Saída esperada:**
```
✅ Instalador compilado com sucesso!

📦 Instalador: DeliveryPrintClient-Setup-v1.4.2.exe
   Localização: C:\...\installer\output\DeliveryPrintClient-Setup-v1.4.2.exe
   Tamanho: 230.45 MB
```

---

### PASSO 4: Distribuir o Instalador

```powershell
# O instalador está em:
installer\output\DeliveryPrintClient-Setup-v1.4.2.exe

# Distribua este arquivo único para os usuários!
```

**Como usar (usuário final):**
1. Executar `DeliveryPrintClient-Setup-v1.4.2.exe` **como Administrador**
2. Seguir assistente de instalação
3. Pronto! Aplicativo instalado e configurado

---

## 📂 Estrutura de Arquivos

```
installer/
├── setup.iss                          # Script Inno Setup (configuração do instalador)
├── download-dependencies.ps1          # Script para baixar dependências
├── build-installer.ps1                # Script para compilar instalador
├── README.md                          # Este arquivo
├── dependencies/                      # Pasta criada automaticamente
│   ├── vc_redist.x64.exe             # Visual C++ Redistributable (25 MB)
│   └── windowsdesktop-runtime-6.0-win-x64.exe  # .NET Desktop Runtime (55 MB)
└── output/                            # Pasta criada automaticamente
    └── DeliveryPrintClient-Setup-v1.4.2.exe    # INSTALADOR FINAL (230 MB)
```

---

## ⚙️ Configuração do Instalador (setup.iss)

### Informações do Aplicativo

```pascal
#define MyAppName "Delivery Print Client"
#define MyAppVersion "1.4.2"
#define MyAppPublisher "Agência Expresso"
#define MyAppURL "https://delivery2.agenciaexpresso.com.br"
```

### Pasta de Instalação

```pascal
DefaultDirName={autopf}\Delivery Print Client
// C:\Program Files\Delivery Print Client
```

### Arquivos Incluídos

- ✅ Executável principal (DeliveryPrintClient.exe)
- ✅ Pasta logoparaapp/ (logos)
- ✅ Documentação (LEIA-ME.md, REQUISITOS_WINDOWS.md, etc.)
- ✅ Script de verificação (verificar-requisitos.ps1)

### Atalhos Criados

- 📌 Desktop (opcional)
- 📌 Menu Iniciar
- 📌 Menu Iniciar → Leia-Me
- 📌 Menu Iniciar → Verificar Requisitos
- 📌 Menu Iniciar → Desinstalar

### Auto-Start

```pascal
Root: HKCU;
Subkey: "Software\Microsoft\Windows\CurrentVersion\Run";
ValueName: "Delivery Print Client";
ValueData: "C:\Program Files\Delivery Print Client\DeliveryPrintClient.exe";
```

Configurado apenas se usuário marcar opção no instalador.

---

## 🔍 Verificação de Requisitos Automática

O instalador verifica automaticamente:

### 1. Sistema Operacional
```pascal
if not IsWin64 then
  MsgBox('Requer Windows 10/11 de 64 bits', mbCriticalError, MB_OK);
```

### 2. Visual C++ Redistributable
```pascal
function NeedVCRedist: Boolean;
// Verifica registro:
// HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64
```

Se não instalado → Instala automaticamente de `dependencies/vc_redist.x64.exe`

### 3. .NET Desktop Runtime 6.0
```pascal
function NeedDotNetRuntime: Boolean;
// Executa: dotnet --list-runtimes
// Verifica se contém: Microsoft.WindowsDesktop.App 6.
```

Se não instalado → Instala automaticamente de `dependencies/windowsdesktop-runtime-6.0-win-x64.exe`

---

## 🛠️ Fluxo de Instalação Completo

```
┌─────────────────────────────────────────┐
│ 1. Usuário executa instalador          │
│    (como Administrador)                 │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│ 2. Verificação de requisitos            │
│    ✓ Windows 10/11 64-bit?              │
│    ✓ Permissões de admin?               │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│ 3. Instalação de dependências           │
│    ✓ Visual C++ Redistributable         │
│    ✓ .NET Desktop Runtime 6.0           │
│    (apenas se não instalados)           │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│ 4. Cópia de arquivos                    │
│    → C:\Program Files\Delivery Print... │
│    ✓ DeliveryPrintClient.exe            │
│    ✓ logoparaapp/                       │
│    ✓ Documentação                       │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│ 5. Criação de atalhos                   │
│    ✓ Desktop (se selecionado)           │
│    ✓ Menu Iniciar                       │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│ 6. Configuração de auto-start           │
│    ✓ Registro no Windows Registry       │
│    (se selecionado)                     │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│ 7. Conclusão                            │
│    ✓ Instalação concluída!              │
│    ✓ Executar aplicativo (opcional)     │
└─────────────────────────────────────────┘
```

---

## 🗑️ Desinstalação

O instalador cria um **desinstalador completo** que:

- ✅ Para processo em execução (`taskkill /F /IM DeliveryPrintClient.exe`)
- ✅ Remove todos os arquivos de `C:\Program Files\Delivery Print Client\`
- ✅ Remove atalhos (Desktop + Menu Iniciar)
- ✅ Remove auto-start do Registry
- ✅ Remove configurações de `%APPDATA%\DeliveryPrintClient\` (opcional)

**Como desinstalar:**

1. Painel de Controle → Programas → Desinstalar um programa
2. Selecionar "Delivery Print Client"
3. Clicar em "Desinstalar"

---

## 📊 Tamanhos de Arquivo

| Componente | Tamanho | Descrição |
|------------|---------|-----------|
| **vc_redist.x64.exe** | ~25 MB | Visual C++ Redistributable |
| **.NET Runtime** | ~55 MB | .NET Desktop Runtime 6.0 |
| **DeliveryPrintClient.exe** | ~147 MB | Aplicativo standalone |
| **Logos** | ~0.14 MB | 3 arquivos PNG |
| **Documentação** | ~0.1 MB | Markdown files |
| **Instalador Final** | **~230 MB** | **Tudo incluído** |

---

## ⚠️ Troubleshooting

### Erro: "Inno Setup não encontrado"

**Solução:**
```powershell
# Instale Inno Setup 6:
https://jrsoftware.org/isdl.php

# Verifique instalação:
Test-Path "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
# Deve retornar: True
```

### Erro: "Dependências não encontradas"

**Solução:**
```powershell
# Execute o script de download:
.\download-dependencies.ps1

# Verifique arquivos:
ls .\dependencies\
# Deve mostrar: vc_redist.x64.exe e windowsdesktop-runtime-6.0-win-x64.exe
```

### Erro: ".NET SDK não encontrado"

**Solução:**
```powershell
# Instale .NET 6.0 SDK:
https://dotnet.microsoft.com/download/dotnet/6.0

# Verifique:
dotnet --version
# Exemplo: 6.0.425
```

### Instalador gerado mas muito pequeno (< 100 MB)

**Problema:** Dependências não foram incluídas.

**Solução:**
```powershell
# Verificar se dependências existem:
ls .\dependencies\

# Se não existirem, executar:
.\download-dependencies.ps1

# Recompilar:
.\build-installer.ps1
```

---

## 🔐 Assinatura Digital (Opcional - Avançado)

Para evitar avisos do Windows SmartScreen, você pode **assinar digitalmente** o instalador.

### Pré-requisitos:
- Certificado Code Signing (~$300-500/ano)
- SignTool.exe (Windows SDK)

### Como assinar:
```powershell
# Exemplo com certificado PFX:
signtool sign /f "certificado.pfx" /p "senha" /t http://timestamp.digicert.com "DeliveryPrintClient-Setup-v1.4.2.exe"
```

**Benefícios:**
- ✅ Sem avisos do SmartScreen
- ✅ Aumenta confiança do usuário
- ✅ Instalação mais rápida

**Custo:** ~$300-500/ano (certificado code signing)

---

## 📝 Personalização do Instalador

### Mudar ícone do instalador:

Edite `setup.iss`:
```pascal
SetupIconFile=..\logoparaapp\logo-v2.ico
```

### Mudar pasta de instalação padrão:

```pascal
DefaultDirName={autopf}\Delivery Print Client
```

Opções:
- `{autopf}` = C:\Program Files\
- `{localappdata}` = C:\Users\Usuario\AppData\Local\
- `{userdocs}` = C:\Users\Usuario\Documents\

### Adicionar arquivos extras:

```pascal
[Files]
Source: "..\meu-arquivo.txt"; DestDir: "{app}"; Flags: ignoreversion
```

### Mudar mensagens do instalador:

```pascal
[Messages]
WelcomeLabel1=Bem-vindo ao Instalador!
WelcomeLabel2=Este programa instalará o [name] no seu computador.
```

---

## 🎯 Checklist de Distribuição

Antes de distribuir o instalador, verifique:

- [ ] ✅ Inno Setup 6 instalado
- [ ] ✅ Dependências baixadas (VC++ e .NET Runtime)
- [ ] ✅ Aplicação compilada (Release mode)
- [ ] ✅ Instalador gerado (~230 MB)
- [ ] ✅ Testado em Windows 10 limpo
- [ ] ✅ Testado em Windows 11
- [ ] ✅ Testado instalação completa
- [ ] ✅ Testado desinstalação
- [ ] ✅ Auto-start funciona
- [ ] ✅ Atalhos criados corretamente
- [ ] ✅ Aplicativo executa após instalação

---

## 📞 Suporte

**Problemas com o instalador?**

1. Verifique logs do Inno Setup em `%TEMP%\Setup Log YYYY-MM-DD #XXX.txt`
2. Execute `verificar-requisitos.ps1` após instalação
3. Consulte documentação: `REQUISITOS_WINDOWS.md`

---

## 🚀 Próximos Passos

1. ✅ Criar instalador com `build-installer.ps1`
2. ✅ Testar em máquina limpa
3. ✅ Distribuir para usuários
4. ✅ Coletar feedback
5. 📋 Considerar assinatura digital (opcional)

---

**Versão:** 1.4.2
**Data:** 18/11/2025
**Status:** ✅ Pronto para produção

