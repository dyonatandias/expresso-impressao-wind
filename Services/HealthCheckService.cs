using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace DeliveryPrintClient.Services
{
    /// <summary>
    /// Serviço de verificação de saúde da API
    /// </summary>
    public class HealthCheckService
    {
        private readonly string _baseUrl;
        private readonly HttpClient _httpClient;

        public HealthCheckService(string baseUrl)
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        /// <summary>
        /// Verifica se a API está acessível
        /// </summary>
        public async Task<(bool IsHealthy, string Message)> CheckApiHealthAsync()
        {
            try
            {
                LogService.LogInfo($"Verificando saúde da API: {_baseUrl}");

                // Tentar acessar endpoint de status
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/windows-clients/status");

                if (response.IsSuccessStatusCode)
                {
                    LogService.LogSuccess($"API acessível - Status: {(int)response.StatusCode} {response.ReasonPhrase}");
                    return (true, "API acessível e funcionando");
                }
                else
                {
                    LogService.LogWarning($"API retornou erro - Status: {(int)response.StatusCode} {response.ReasonPhrase}");
                    return (false, $"API retornou erro: {(int)response.StatusCode} {response.ReasonPhrase}");
                }
            }
            catch (HttpRequestException ex)
            {
                LogService.LogError($"Erro de rede ao conectar com API", ex);
                return (false, $"Erro de rede: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                LogService.LogError("Timeout ao conectar com API (10 segundos)");
                return (false, "Timeout: API não respondeu em 10 segundos");
            }
            catch (Exception ex)
            {
                LogService.LogError($"Erro inesperado ao verificar saúde da API", ex);
                return (false, $"Erro: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifica conectividade de rede (ping básico)
        /// </summary>
        public async Task<bool> CheckNetworkConnectivityAsync()
        {
            try
            {
                LogService.LogInfo("Verificando conectividade de rede");

                // Tentar acessar um endpoint público confiável
                var response = await _httpClient.GetAsync("https://www.google.com");

                if (response.IsSuccessStatusCode)
                {
                    LogService.LogSuccess("Conectividade de rede OK");
                    return true;
                }
                else
                {
                    LogService.LogWarning("Sem conectividade de rede");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("Erro ao verificar conectividade de rede", ex);
                return false;
            }
        }

        /// <summary>
        /// Diagnóstico completo (rede + API)
        /// </summary>
        public async Task<string> RunFullDiagnosticsAsync()
        {
            var report = new System.Text.StringBuilder();

            report.AppendLine("========================================");
            report.AppendLine("  DIAGNÓSTICO DE CONECTIVIDADE");
            report.AppendLine("========================================");
            report.AppendLine();

            // 1. Verificar rede
            report.AppendLine("1. Verificando conectividade de rede...");
            var hasNetwork = await CheckNetworkConnectivityAsync();
            report.AppendLine(hasNetwork
                ? "   ✅ Conectividade de rede OK"
                : "   ❌ Sem conectividade de rede");
            report.AppendLine();

            if (!hasNetwork)
            {
                report.AppendLine("⚠️  Verifique sua conexão com internet.");
                return report.ToString();
            }

            // 2. Verificar API
            report.AppendLine($"2. Verificando API ({_baseUrl})...");
            var (isHealthy, message) = await CheckApiHealthAsync();
            report.AppendLine(isHealthy
                ? $"   ✅ {message}"
                : $"   ❌ {message}");
            report.AppendLine();

            if (!isHealthy)
            {
                report.AppendLine("⚠️  Possíveis causas:");
                report.AppendLine("   - Servidor offline ou em manutenção");
                report.AppendLine("   - Firewall bloqueando conexão");
                report.AppendLine("   - URL incorreta nas configurações");
                report.AppendLine($"   - URL configurada: {_baseUrl}");
            }

            report.AppendLine("========================================");

            return report.ToString();
        }
    }
}
