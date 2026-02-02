# CHANGELOG v2.0.0 - Expresso Delivery Print Client

**Data:** 01/02/2026
**Versao anterior:** 1.5.6

---

## Resumo

Upgrade completo do Windows Print Client com 12+ correcoes e melhorias.
Auditoria de codigo identificou bugs criticos, vazamentos de recursos,
endpoints legados e modelos incompletos.

---

## Bugs Criticos Corrigidos

### 1. Largura do papel HARDCODED (PrinterService.cs)
**Bug:** `new PaperSize("80mm", 315, 3150)` — sempre 80mm, ignorava `largura_papel` do servidor.
**Fix:** Metodo `GetPaperSize(int larguraMm)` com mapeamento:
- 58mm -> PaperSize("58mm", 228, 3150)
- 76mm -> PaperSize("76mm", 299, 3150)
- 80mm -> PaperSize("80mm", 315, 3150) (default)

### 2. Log nao rotaciona por dia (LogService.cs)
**Bug:** `static readonly string CurrentLogFile` calculado uma vez na inicializacao.
Apos meia-noite, logs continuavam no arquivo do dia anterior.
**Fix:** Mudou para property getter: `private static string CurrentLogFile =>` recalculado a cada escrita.

### 3. Fire-and-forget no log (LogService.cs)
**Bug:** `Task.Run(async () => ...)` sem await — logs podiam ser perdidos em crash.
**Fix:** I/O sincrono com `_logSemaphore.Wait()` + `File.AppendAllText()`.

### 4. Versao errada em multiplos locais
**Bug:** Label mostrava "v1.5.5" mas .csproj dizia 1.5.6. Versoes inconsistentes.
**Fix:** Atualizado TODOS os locais para "2.0.0":
- DeliveryPrintClient.csproj (Version, FileVersion, AssemblyVersion)
- MainForm.cs (titulo, label, construtor)
- Program.cs (log de inicializacao)
- LogService.cs (header do log)
- app.manifest (assemblyIdentity)

---

## Modelo e API

### 5. PrintJob faltando 7 campos (Config.cs)
**Bug:** Servidor enviava `largura_papel`, `impressora_id`, `prioridade`, `tipo_documento`,
`tentativas`, `documento_id`, `impressora_modelo` — client nao deserializava nenhum.
**Fix:** Adicionados ao PrintJob:
- `largura_papel` (int?) - Largura do papel em mm
- `tipo_documento` (string?) - Tipo do documento
- `documento_id` (int?) - ID do documento
- `prioridade` (int, default 5) - Prioridade do job
- `tentativas` (int, default 0) - Tentativas ja feitas
- `impressora_id` (int?) - ID da impressora associada
- `impressora_modelo` (string?) - Modelo da impressora

Adicionado ao ApiResponse:
- `total` (int) - Total de jobs retornados

### 6. Endpoint legado sem autenticacao (ApiService.cs)
**Bug:** Usava `GET /api/impressoras/windows-client?client_id=xxx` — publico, sem auth.
**Fix:** Migrado para API autenticada:
- `GET /api/windows-clients/jobs` com X-API-Key/X-Secret-Key
- `PUT /api/windows-clients/jobs?id={id}` para concluir/falhar
- `POST /api/windows-clients/auth` para autenticacao

### 7. Logs duplicados em dois locais (ApiService.cs)
**Bug:** ApiService tinha metodo `LogError` proprio que escrevia em AppData.
LogService escrevia em `{BaseDirectory}/Logs/`. Dois locais diferentes.
**Fix:** Removido LogError do ApiService, usa LogService.LogError() em todo lugar.

---

## Qualidade

### 8. Circuit breaker (ApiService.cs)
**Bug:** Se API offline, a cada 2s fazia request que tomava timeout de 10s.
**Fix:** Circuit breaker:
- Apos 5 falhas consecutivas, para requests por 30s
- Log warning quando circuito abre
- Reset automatico apos cooldown

### 9. Parser fragil de texto (MainForm.cs)
**Bug:** `int.Parse(currentText.Split(':')[1].Trim())` — explodia se formato mudasse.
**Fix:** Campo `private int _jobsProcessed = 0` incrementado diretamente.

### 10. Vazamento de icones HICON (MainForm.cs)
**Bug:** `GetHicon()` criava handles Win32 que nunca eram liberados com `DestroyIcon`.
Bitmap e SolidBrush tambem vazavam.
**Fix:** Pattern Clone + DestroyIcon + using:
```csharp
IntPtr hIcon = iconBitmap.GetHicon();
_trayIcon.Icon = (Icon)Icon.FromHandle(hIcon).Clone();
DestroyIcon(hIcon);
```

### 11. Catch blocks silenciosos (ConfigService, StartupService)
**Bug:** `catch { }` e `Console.WriteLine()` em multiplos locais.
**Fix:** Substituidos por `LogService.LogWarning()` e `LogService.LogError()`.

### 12. Filtro ESC/POS incorreto (PrinterService.cs)
**Bug:** Parenteses ambiguos no filtro de caracteres e `\n` faltando.
**Fix:** Parenteses explicitos + `\n` adicionado:
```csharp
if ((c >= 32 && c <= 126) || c == '\t' || c == '\r' || c == '\n' || c >= 160)
```

---

## Database

### 13. Constraint de templates so permitia [58, 80] (Migration 100)
**Bug:** `impressoras_templates_largura_papel_check CHECK (largura_papel = ANY (ARRAY[58, 80]))`
impedia 76mm nos templates.
**Fix:** Migration `100_adicionar_76mm_templates.sql`:
```sql
ALTER TABLE impressoras_templates
  ADD CONSTRAINT impressoras_templates_largura_papel_check
  CHECK (largura_papel = ANY (ARRAY[58, 76, 80]));
```

---

## Build e Distribuicao

### 14. Scripts de build atualizados
- `COMPILAR_TUDO.bat`: versao "v2.0.0"
- `installer/setup.iss`: version "2.0.0", data "01/02/2026"

### 15. Admin UI atualizada
- `WindowsClientsTab.tsx`: versao 2.0.0, highlights das correcoes, URL download v2.0.0

---

## Arquivos Alterados

| Arquivo | Mudancas |
|---------|----------|
| Models/Config.cs | +7 campos PrintJob, +1 campo ApiResponse |
| Services/PrinterService.cs | Paper size dinamico, filtro ESC/POS, log nos catches |
| Services/LogService.cs | Rotacao diaria, sync writes, versao, Flush() |
| Services/ApiService.cs | API autenticada, circuit breaker, log consolidado |
| Services/ConfigService.cs | Console.WriteLine -> LogService |
| Services/StartupService.cs | Console.WriteLine -> LogService, catches logados |
| Forms/MainForm.cs | Versao 2.0.0, job counter, icon leaks, DllImport DestroyIcon |
| Program.cs | Versao 2.0.0, LogService.Flush() no finally |
| DeliveryPrintClient.csproj | Version/FileVersion/AssemblyVersion 2.0.0 |
| app.manifest | assemblyIdentity version 2.0.0.0 |
| COMPILAR_TUDO.bat | Versao v2.0.0 |
| installer/setup.iss | Versao 2.0.0 |
| database/migrations/100_*.sql | Constraint [58, 76, 80] |
| database/schema-complete.sql | Constraint atualizado |
| WindowsClientsTab.tsx | Versao 2.0.0, features, URL download |
| public/downloads/README.md | Pagina de download v2.0.0 |
| LEIA-ME.md | Documentacao v2.0.0 |

---

## Compilacao

Requer maquina Windows com .NET 6.0 SDK:
```bat
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Output: `ExpressoDeliveryPrintClient.exe`

---

**Copyright (c) 2026 Agencia Expresso**
