# Expresso Delivery Print Client

Open-source thermal print client for Windows. Connects to the Expresso Delivery system and automatically prints orders on thermal printers.

**Version:** 2.3.0
**License:** MIT
**Platform:** Windows 10+ (64-bit)
**Framework:** .NET 6 (WinForms)

---

## Features

- Automatic order polling and printing (every 2s)
- Dynamic paper width support (58mm / 76mm / 80mm)
- Authenticated API (X-API-Key / X-Secret-Key)
- Circuit breaker (5 failures = 30s cooldown)
- Auto-update support
- System tray operation (minimize to tray)
- Daily log rotation with synchronous writes
- ESC/POS thermal printer support (USB, LPT, Network)
- Single-file standalone executable (no .NET install needed on target)

---

## Quick Start

### Option 1: Download Pre-built Executable

Download the latest release from the admin panel:
**Admin > Impressoras > Windows Clients > Download**

### Option 2: Build from Source

**Requirements:** .NET 6 SDK ([download](https://dotnet.microsoft.com/download/dotnet/6.0))

```cmd
cd windows-print-client-dotnet
COMPILAR_TUDO.bat
```

Output: `ExpressoDeliveryPrintClient.exe` in the same directory.

### Configuration

1. Run `ExpressoDeliveryPrintClient.exe`
2. Fill in:
   - **API URL**: `https://your-domain.com`
   - **API Key**: (from Admin > Impressoras > Windows Clients)
   - **Secret Key**: (from Admin > Impressoras > Windows Clients)
   - **Printer**: Select from list
3. Click **Save**, then **Test**

---

## Project Structure

```
windows-print-client-dotnet/
├── DeliveryPrintClient.csproj    # .NET 6 project
├── Program.cs                    # Entry point
├── Models/
│   └── Config.cs                 # Data models (PrintJob, Config, ApiResponse)
├── Services/
│   ├── ApiService.cs             # HTTP client with auth + circuit breaker
│   ├── ConfigService.cs          # JSON config persistence
│   ├── LogService.cs             # Daily rotating log with sync writes
│   ├── PrinterService.cs         # Windows printing (dynamic paper size)
│   ├── StartupService.cs         # Windows auto-start setup
│   ├── UpdateService.cs          # Auto-update mechanism
│   ├── InstallerService.cs       # Installation helpers
│   └── HealthCheckService.cs     # API health monitoring
├── Forms/
│   └── MainForm.cs               # WinForms GUI
├── installer/
│   └── setup.iss                 # InnoSetup installer script
├── COMPILAR_TUDO.bat             # Build script (Windows)
├── LEIA-ME.md                    # Documentation (Portuguese)
├── CHANGELOG_v2.0.0.md           # Changelog
└── LICENSE.txt                   # MIT License
```

---

## How It Works

```
Delivery System → Order created → Print job queued
                                        ↓
            Windows Client (this) ← Polls every 2s
                                        ↓
                              Sends to thermal printer
                                        ↓
                          Reports status back to server
```

---

## Build (Manual)

```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishTrimmed=false
```

**Dependencies:** Newtonsoft.Json 13.0.3, System.Drawing.Common 7.0.0

---

## Supported Printers

Any Windows-recognized thermal printer:
- USB (e.g., Epson TM-T20, Bematech MP-4200)
- Parallel (LPT)
- Network (TCP/IP, shared)

Supported paper widths: **58mm**, **76mm**, **80mm**

---

## Changelog

### v2.3.0 (05/02/2026)
- Built-in auto-update: download and install new versions from within the app
- Progress bar during update download
- Force update support (mandatory updates)
- Decline option (won't ask again until next version)

### v2.2.0 (05/02/2026)
- Fix: HealthCheckService URL not updating on config save
- Fix: SQL column names corrected in PUT jobs endpoint
- Fix: editingId truthy bug in rule creation
- Fix: Cascade force delete for Windows Clients with printers
- Fix: PE subsystem corrected (GUI instead of Console)

### v2.1.0 (02/02/2026)
- Auto-update support
- Installer service
- Source zip distribution

### v2.0.0 (01/02/2026)
- Authenticated API (X-API-Key / X-Secret-Key)
- Dynamic paper width (58/76/80mm)
- Circuit breaker (5 failures → 30s cooldown)
- Daily log rotation + synchronous writes
- GDI/HICON resource leak fixes
- 7 new fields in PrintJob model

### v1.5.6 (19/11/2025)
- Win32Exception fix
- Local config and logs
- Multi-delivery support

---

## License

MIT License - See [LICENSE.txt](LICENSE.txt)

Copyright (c) 2025-2026 Agencia Expresso
