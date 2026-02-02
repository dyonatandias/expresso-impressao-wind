# Expresso Delivery Print Client v2.0.0

Cliente de impressao para Windows - Sistema Delivery 2.0

## Novidades v2.0.0

### API Autenticada
- **Endpoints seguros** - Headers X-API-Key/X-Secret-Key em todas as chamadas
- **Autenticacao automatica** - POST /api/windows-clients/auth ao iniciar
- **Jobs autenticados** - GET /api/windows-clients/jobs com credenciais
- **Status autenticado** - PUT /api/windows-clients/jobs?id={id} para concluir/falhar

### Largura de Papel Dinamica
- **58mm** - Termicas compactas (228 hundredths-of-inch)
- **76mm** - Termicas medias (299 hundredths-of-inch)
- **80mm** - Termicas padrao (315 hundredths-of-inch, default)
- Servidor envia `largura_papel` no job, client respeita automaticamente

### Circuit Breaker
- Pausa automatica apos 5 falhas consecutivas
- Cooldown de 30 segundos antes de tentar novamente
- Log de abertura/fechamento do circuito

### Qualidade
- **Rotacao de log diaria** - Novo arquivo por dia automaticamente
- **Log sincrono** - Sem perda de logs em crash
- **Zero vazamento de recursos** - HICON/GDI handles liberados corretamente
- **Contador de jobs robusto** - Sem parsing fragil de texto
- **Logging unificado** - Todos os erros vao para LogService

## Instalacao Rapida

### 1. Executar Build
```
Clicar direito em: COMPILAR_TUDO.bat
-> "Executar como administrador"
```

**Aguardar 2-3 minutos**

### 2. Executar Aplicativo
```
ExpressoDeliveryPrintClient.exe (na mesma pasta)
```

**Se nao abrir:** Clique direito -> "Executar como administrador"
**Logs:** `Logs/app-YYYY-MM-DD.log` (mesma pasta!)
**Config:** `config.json` (mesma pasta!)

### 3. Configurar
- **URL da API**: https://seu-dominio.com.br
- **API Key**: (pegar no admin -> Impressoras -> Windows Clients)
- **Secret Key**: (pegar no admin -> Impressoras -> Windows Clients)
- **Impressora**: Selecionar da lista

### 4. Salvar e Testar
Clicar em "Salvar" depois "Testar"

**Pronto!**

---

## Requisitos

- Windows 10+ (64-bit)
- .NET SDK 6.0+ (https://dotnet.microsoft.com/download/dotnet/6.0)

---

## Historico de Versoes

### v2.0.0 (01/02/2026) - Atual
- API autenticada com X-API-Key/X-Secret-Key
- Largura de papel dinamica (58/76/80mm)
- Circuit breaker (5 falhas -> 30s cooldown)
- Rotacao de log diaria + log sincrono
- Zero vazamento GDI/HICON
- 7 novos campos no modelo PrintJob

### v1.5.6 (19/11/2025)
- Win32Exception corrigido
- Config e logs locais
- Multi-delivery pronto

---

## Suporte

Website: https://delivery2.agenciaexpresso.com.br

---

**Versao**: 2.0.0
**Data**: 01/02/2026
**Copyright**: (c) 2026 Agencia Expresso
