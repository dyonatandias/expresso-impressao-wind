# Expresso Delivery Print Client

Cliente de impressao termica para Windows do sistema [Expresso Delivery](https://delivery2.agenciaexpresso.com.br).

Aplicativo C# .NET 6 WinForms que conecta a impressoras termicas (58mm, 76mm, 80mm) via API REST, recebendo jobs de impressao automaticamente por polling.

## Funcionalidades

- **Polling automatico** - Busca jobs de impressao no servidor a cada N segundos
- **Multiplas impressoras** - Suporte a qualquer impressora termica instalada no Windows
- **Circuit breaker** - Pausa apos 5 falhas consecutivas (cooldown 30s)
- **Config persistente** - Credenciais salvas em `%APPDATA%\DeliveryPrintClient\`
- **Logs remotos** - Erros e estatisticas enviados ao servidor
- **Single instance** - Impede multiplas instancias simultaneas (Mutex)
- **Verificacao de atualizacao** - Notifica quando ha nova versao disponivel
- **Icone personalizado** - Logo embarcado como recurso no assembly
- **Largura dinamica** - Suporte a papeis de 58mm, 76mm e 80mm
- **Copias multiplas** - Configuracao de copias padrao com delay entre impressoes
- **Tray icon** - Minimiza para a bandeja do sistema
- **Auto-start** - Gerenciado pelo instalador Inno Setup

## Requisitos

### Para executar (usuario final)
- Windows 10/11 64-bit
- Impressora termica instalada
- Credenciais de API (obtidas no painel admin)

### Para compilar (desenvolvedor)
- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) ou superior
- Windows 10/11 (para WinForms)
- Ou Linux com cross-compilation (`dotnet publish -r win-x64`)

## Compilacao

### No Windows (recomendado)

```bash
# Restaurar dependencias
dotnet restore

# Build
dotnet build -c Release

# Publicar (single-file, self-contained)
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

O executavel sera gerado em `bin/Release/net6.0-windows/win-x64/publish/`.

### No Linux (cross-compilation)

```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  /p:PublishSingleFile=true \
  /p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish
```

> Nota: Cross-compilacao do Linux nao embute o icone no .exe (NETSDK1074). O icone e carregado em runtime via recurso embarcado.

## Instalador (Inno Setup)

O projeto inclui um script Inno Setup (`installer/setup.iss`) que gera um instalador profissional com:
- Assistente de instalacao em portugues
- Atalhos na area de trabalho e menu iniciar
- Auto-start com Windows (opcional)
- Desinstalador

### Compilar o instalador

**No Windows:**
1. Instale o [Inno Setup 6](https://jrsoftware.org/isdl.php)
2. Abra `installer/setup.iss` no Inno Setup
3. Compile (Ctrl+F9)

**No Linux (via Wine):**
```bash
wine "C:\Program Files\Inno Setup 6\ISCC.exe" installer/setup.iss
```

## Estrutura do Projeto

```
├── Program.cs                 # Entry point + single instance (Mutex)
├── DeliveryPrintClient.csproj # Projeto .NET 6 WinForms
├── app-icon.ico               # Icone da aplicacao (embarcado como recurso)
├── app.manifest               # Manifesto Windows (DPI awareness)
│
├── Forms/
│   └── MainForm.cs            # UI principal (config, status, logs)
│
├── Models/
│   └── Config.cs              # Modelos de configuracao e respostas API
│
├── Services/
│   ├── ApiService.cs          # Cliente HTTP com circuit breaker
│   ├── ConfigService.cs       # Persistencia em %APPDATA% (JSON)
│   ├── HealthCheckService.cs  # Diagnostico de conectividade
│   ├── InstallerService.cs    # Carregamento de icone do assembly
│   ├── LogService.cs          # Logs em arquivo + buffer remoto
│   ├── PrinterService.cs      # Impressao via System.Drawing.Printing
│   ├── StartupService.cs      # Verificacao de auto-start (read-only)
│   └── UpdateService.cs       # Verificacao de versao no servidor
│
├── installer/
│   └── setup.iss              # Script Inno Setup (instalador)
│
└── logoparaapp/               # Logos para o header da aplicacao
```

## Configuracao

Na primeira execucao, configure no aplicativo:

1. **URL da API** - Endereco do servidor (ex: `https://delivery2.agenciaexpresso.com.br`)
2. **API Key** - Chave de autenticacao (gerada no painel admin)
3. **Secret Key** - Chave secreta (gerada no painel admin)
4. **Impressora** - Selecione a impressora termica instalada
5. Clique em **Salvar**

As credenciais sao salvas em `%APPDATA%\DeliveryPrintClient\config.json`.

## API Endpoints Utilizados

| Metodo | Endpoint | Descricao |
|--------|----------|-----------|
| POST | `/api/windows-clients/auth` | Autenticacao com API Key + Secret Key |
| GET | `/api/windows-clients/jobs` | Buscar jobs pendentes de impressao |
| POST | `/api/windows-clients/jobs/[id]/complete` | Marcar job como impresso |
| POST | `/api/windows-clients/jobs/[id]/fail` | Reportar falha na impressao |
| GET | `/api/windows-clients/check-update` | Verificar atualizacao disponivel |
| POST | `/api/windows-clients/logs` | Enviar logs e stats ao servidor |
| GET | `/api/health` | Health check da API |

## Licenca

MIT License - veja [LICENSE.txt](LICENSE.txt)

## Autor

[Agencia Expresso](https://delivery2.agenciaexpresso.com.br)
