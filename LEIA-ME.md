# Expresso Delivery Print Client v2.2.0

Cliente de impressao termica para Windows - Sistema Expresso Delivery

---

## Instalacao Rapida

### Opcao 1: Baixar Executavel Pronto

Baixe no painel admin: **Admin > Impressoras > Windows Clients > Download**

Ou use o installer: `ExpressoDeliveryPrintClient-Setup-v2.2.0.exe`

### Opcao 2: Compilar do Codigo Fonte

**Requisitos:** .NET 6 SDK (https://dotnet.microsoft.com/download/dotnet/6.0)

```cmd
cd windows-print-client-dotnet
COMPILAR_TUDO.bat
```

Resultado: `ExpressoDeliveryPrintClient.exe` na mesma pasta.

Para compilacao manual:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

---

## Configuracao

1. Executar `ExpressoDeliveryPrintClient.exe` (se nao abrir: clique direito > Executar como administrador)
2. Preencher:
   - **URL da API**: `https://seu-dominio.com.br`
   - **API Key**: (pegar no Admin > Impressoras > Windows Clients)
   - **Secret Key**: (pegar no Admin > Impressoras > Windows Clients)
   - **Impressora**: Selecionar da lista
3. Clicar em **Salvar** e depois **Testar**

**Logs:** `Logs/app-YYYY-MM-DD.log` (na pasta do executavel)
**Config:** `config.json` (na pasta do executavel)

---

## Funcionalidades

- **Polling automatico** - Busca pedidos a cada 2 segundos
- **Largura de papel dinamica** - 58mm, 76mm ou 80mm (configurado pelo servidor)
- **API autenticada** - Headers X-API-Key / X-Secret-Key
- **Circuit breaker** - Pausa apos 5 falhas, tenta novamente em 30s
- **Auto-update** - Atualiza automaticamente quando nova versao disponivel
- **System tray** - Minimiza para a bandeja do sistema
- **Log diario** - Rotacao automatica por dia, escrita sincrona

---

## Impressoras Suportadas

Qualquer impressora termica reconhecida pelo Windows:
- USB (ex: Epson TM-T20, Bematech MP-4200)
- LPT (porta paralela)
- Rede (TCP/IP ou compartilhada)

Larguras suportadas: **58mm** (32 chars), **76mm** (42 chars), **80mm** (48 chars)

---

## Problemas Comuns

### Impressora nao aparece na lista
Adicionar impressora no Windows primeiro (Painel de Controle > Dispositivos e Impressoras)

### Nao conecta a API
1. Servidor esta rodando?
2. URL correta? (incluir `https://`)
3. API Key e Secret corretos?
4. Porta liberada no firewall?

### Jobs nao imprimem
1. Impressora esta ONLINE?
2. Tem papel?
3. Testar impressao manual pelo Windows

---

## Estrutura do Projeto

```
windows-print-client-dotnet/
├── DeliveryPrintClient.csproj    # Projeto .NET 6
├── Program.cs                    # Ponto de entrada
├── Models/Config.cs              # Modelos (PrintJob, Config, ApiResponse)
├── Services/
│   ├── ApiService.cs             # HTTP com auth + circuit breaker
│   ├── ConfigService.cs          # Persistencia JSON
│   ├── LogService.cs             # Log com rotacao diaria
│   ├── PrinterService.cs         # Impressao Windows (papel dinamico)
│   ├── StartupService.cs         # Auto-start no Windows
│   ├── UpdateService.cs          # Auto-update
│   ├── InstallerService.cs       # Helpers de instalacao
│   └── HealthCheckService.cs     # Monitoramento da API
├── Forms/MainForm.cs             # Interface grafica (WinForms)
├── installer/setup.iss           # Script InnoSetup
├── COMPILAR_TUDO.bat             # Script de build
├── README.md                     # Documentacao (English)
├── CHANGELOG_v2.0.0.md           # Historico de mudancas
└── LICENSE.txt                   # Licenca MIT
```

---

## Fluxo de Operacao

```
Sistema Delivery → Pedido criado → Job entra na fila
                                        |
           Windows Client (este) ← Polling a cada 2s
                                        |
                            Envia para impressora termica
                                        |
                        Reporta status de volta ao servidor
```

---

## Historico de Versoes

### v2.2.0 (05/02/2026) - Atual
- Fix: HealthCheckService URL nao atualizava ao salvar config
- Fix: Colunas SQL corrigidas no PUT jobs (erro_mensagem, processed_at)
- Fix: editingId truthy corrigido na criacao de regras
- Fix: Cascade force delete para Windows Clients com impressoras
- Fix: $queryRawUnsafe array param em impressoras/[id]
- PE subsystem corrigido (GUI em vez de Console)

### v2.1.0 (02/02/2026)
- Suporte a auto-update
- Servico de instalacao
- Distribuicao via ZIP de codigo fonte

### v2.0.0 (01/02/2026)
- API autenticada (X-API-Key / X-Secret-Key)
- Largura de papel dinamica (58/76/80mm)
- Circuit breaker (5 falhas → 30s cooldown)
- Rotacao de log diaria + escrita sincrona
- Correcao vazamento GDI/HICON
- 7 novos campos no modelo PrintJob

### v1.5.6 (19/11/2025)
- Correcao Win32Exception
- Config e logs locais
- Multi-delivery pronto

---

## Licenca

MIT License - Veja [LICENSE.txt](LICENSE.txt)

Copyright (c) 2025-2026 Agencia Expresso

---

**Versao**: 2.2.0
**Data**: 05/02/2026
**Website**: https://delivery2.agenciaexpresso.com.br
