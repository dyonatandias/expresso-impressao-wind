using System;
using System.Threading.Tasks;
using DeliveryPrintClient.Models;

namespace DeliveryPrintClient.Services
{
    /// <summary>
    /// v2.1.0: Serviço de verificação de atualização
    /// Apenas verifica se há nova versão — o download é feito pelo usuário via instalador.
    /// NÃO baixa executáveis nem cria scripts de atualização.
    /// </summary>
    public class UpdateService
    {
        public const string CurrentVersion = "2.1.0";

        private readonly AppConfig _config;
        private readonly ApiService _apiService;
        private DateTime _lastCheck = DateTime.MinValue;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);

        public UpdateService(AppConfig config, ApiService apiService)
        {
            _config = config;
            _apiService = apiService;
        }

        /// <summary>
        /// Verifica se é hora de checar atualização (a cada 30 min)
        /// </summary>
        public bool ShouldCheck()
        {
            return _config.AutoUpdate && (DateTime.Now - _lastCheck) > CheckInterval;
        }

        /// <summary>
        /// Verifica se há atualização disponível no servidor
        /// </summary>
        public async Task<UpdateCheckResponse?> CheckForUpdateAsync()
        {
            try
            {
                _lastCheck = DateTime.Now;
                var response = await _apiService.CheckUpdateAsync(CurrentVersion);

                if (response != null && response.UpdateAvailable)
                {
                    LogService.LogInfo($"Atualizacao disponivel: v{response.LatestVersion} (atual: v{CurrentVersion})");
                    if (!string.IsNullOrEmpty(response.ReleaseNotes))
                    {
                        LogService.LogInfo($"Notas: {response.ReleaseNotes}");
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"Erro ao verificar atualizacao: {ex.Message}");
                return null;
            }
        }
    }
}
