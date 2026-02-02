using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DeliveryPrintClient.Models
{
    public class AppConfig
    {
        // Autenticação (OBRIGATÓRIO)
        [JsonProperty("apiKey")]
        public string ApiKey { get; set; } = "";

        [JsonProperty("secretKey")]
        public string SecretKey { get; set; } = "";

        // Local (preenchido automaticamente após autenticação)
        [JsonProperty("clientLocal")]
        public string? ClientLocal { get; set; }

        // Conexão
        [JsonProperty("apiUrl")]
        public string ApiUrl { get; set; } = "https://delivery2.agenciaexpresso.com.br";

        // Impressão
        [JsonProperty("printerName")]
        public string PrinterName { get; set; } = "";

        // Polling
        [JsonProperty("pollingInterval")]
        public int PollingInterval { get; set; } = 2000; // 2 segundos

        [JsonProperty("pollingTimeout")]
        public int PollingTimeout { get; set; } = 10000; // 10 segundos

        [JsonProperty("maxRetries")]
        public int MaxRetries { get; set; } = 3;

        // Comportamento
        [JsonProperty("autoStart")]
        public bool AutoStart { get; set; } = true;

        [JsonProperty("minimizeToTray")]
        public bool MinimizeToTray { get; set; } = true;

        [JsonProperty("showNotifications")]
        public bool ShowNotifications { get; set; } = true;

        [JsonProperty("autoReconnect")]
        public bool AutoReconnect { get; set; } = true;

        // Logs
        [JsonProperty("enableFileLogging")]
        public bool EnableFileLogging { get; set; } = true;

        [JsonProperty("logLevel")]
        public string LogLevel { get; set; } = "info"; // debug, info, warning, error

        // Impressão
        [JsonProperty("copiasPadrao")]
        public int CopiasPadrao { get; set; } = 1; // Cópias padrão por job (1-10)

        [JsonProperty("delayEntreCopias")]
        public int DelayEntreCopias { get; set; } = 500; // Delay em ms entre cópias

        // v2.1.0: Auto-update
        [JsonProperty("autoUpdate")]
        public bool AutoUpdate { get; set; } = true;

        // v2.1.0: Flag de primeira execução concluída
        [JsonProperty("setupCompleted")]
        public bool SetupCompleted { get; set; } = false;
    }

    public class PrintJob
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("conteudo")]
        public string Conteudo { get; set; } = "";

        [JsonProperty("formato")]
        public string Formato { get; set; } = "text";

        [JsonProperty("impressora_nome")]
        public string? ImpressoraNome { get; set; }

        [JsonProperty("copias")]
        public int Copias { get; set; } = 1;

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        // v2.0.0: Campos adicionais enviados pela API autenticada /api/windows-clients/jobs
        [JsonProperty("largura_papel")]
        public int? LarguraPapel { get; set; }

        [JsonProperty("tipo_documento")]
        public string? TipoDocumento { get; set; }

        [JsonProperty("documento_id")]
        public int? DocumentoId { get; set; }

        [JsonProperty("prioridade")]
        public int Prioridade { get; set; } = 5;

        [JsonProperty("tentativas")]
        public int Tentativas { get; set; } = 0;

        [JsonProperty("impressora_id")]
        public int? ImpressoraId { get; set; }

        [JsonProperty("impressora_modelo")]
        public string? ImpressoraModelo { get; set; }
    }

    public class ApiResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("jobs")]
        public List<PrintJob> Jobs { get; set; } = new List<PrintJob>();

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("error")]
        public string? Error { get; set; }
    }

    public class AuthResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("authenticated")]
        public bool Authenticated { get; set; }

        [JsonProperty("client")]
        public ClientInfo? Client { get; set; }

        [JsonProperty("error")]
        public string? Error { get; set; }
    }

    public class ClientInfo
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("nome")]
        public string Nome { get; set; } = "";

        [JsonProperty("local")]
        public string Local { get; set; } = "";

        [JsonProperty("descricao")]
        public string? Descricao { get; set; }
    }

    // v2.1.0: Modelo de resposta do check-update
    public class UpdateCheckResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("update_available")]
        public bool UpdateAvailable { get; set; }

        [JsonProperty("latest_version")]
        public string LatestVersion { get; set; } = "";

        [JsonProperty("download_url")]
        public string DownloadUrl { get; set; } = "";

        [JsonProperty("release_notes")]
        public string ReleaseNotes { get; set; } = "";

        [JsonProperty("force_update")]
        public bool ForceUpdate { get; set; }

        [JsonProperty("error")]
        public string? Error { get; set; }
    }
}
