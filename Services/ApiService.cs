using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using DeliveryPrintClient.Models;

namespace DeliveryPrintClient.Services
{
    public class ApiService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly AppConfig _config;

        // v2.0.0: Circuit breaker - evita martelar API quando offline
        private int _consecutiveFailures = 0;
        private DateTime _circuitOpenUntil = DateTime.MinValue;
        private const int CircuitBreakerThreshold = 5;
        private static readonly TimeSpan CircuitBreakerCooldown = TimeSpan.FromSeconds(30);

        public ApiService(AppConfig config)
        {
            _config = config;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(config.PollingTimeout)
            };
        }

        private bool IsCircuitOpen()
        {
            if (_consecutiveFailures >= CircuitBreakerThreshold)
            {
                if (DateTime.Now < _circuitOpenUntil)
                {
                    return true;
                }
                _consecutiveFailures = CircuitBreakerThreshold - 1;
            }
            return false;
        }

        private void RecordSuccess()
        {
            if (_consecutiveFailures > 0)
            {
                LogService.LogInfo($"Circuit breaker: Conexao restabelecida apos {_consecutiveFailures} falha(s)");
            }
            _consecutiveFailures = 0;
        }

        private void RecordFailure()
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= CircuitBreakerThreshold)
            {
                _circuitOpenUntil = DateTime.Now.Add(CircuitBreakerCooldown);
                LogService.LogWarning($"Circuit breaker ABERTO - {CircuitBreakerThreshold} falhas consecutivas. Proxima tentativa em {CircuitBreakerCooldown.TotalSeconds}s");
            }
        }

        private void AddAuthHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("X-API-Key", _config.ApiKey);
            request.Headers.Add("X-Secret-Key", _config.SecretKey);
        }

        public async Task<AuthResponse> AuthenticateAsync()
        {
            try
            {
                string url = $"{_config.ApiUrl}/api/windows-clients/auth";

                string hostname = Environment.MachineName;
                string os = Environment.OSVersion.ToString();
                string versao = UpdateService.CurrentVersion;

                var payload = new
                {
                    api_key = _config.ApiKey,
                    secret_key = _config.SecretKey,
                    hostname = hostname,
                    sistema_operacional = os,
                    versao_client = versao
                };

                string json = JsonConvert.SerializeObject(payload);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                string responseContent = await response.Content.ReadAsStringAsync();
                AuthResponse? authResponse = JsonConvert.DeserializeObject<AuthResponse>(responseContent);

                RecordSuccess();
                return authResponse ?? new AuthResponse { Success = false, Error = "Resposta invalida" };
            }
            catch (TaskCanceledException ex)
            {
                RecordFailure();
                LogService.LogWarning("Timeout ao autenticar");
                return new AuthResponse { Success = false, Error = "Timeout na autenticacao" };
            }
            catch (HttpRequestException ex)
            {
                RecordFailure();
                LogService.LogError($"Erro de conexao ao autenticar: {ex.Message}");
                return new AuthResponse { Success = false, Error = ex.Message };
            }
            catch (Exception ex)
            {
                RecordFailure();
                LogService.LogError("Erro ao autenticar", ex);
                return new AuthResponse { Success = false, Error = ex.Message };
            }
        }

        public async Task<ApiResponse> FetchJobsAsync()
        {
            if (IsCircuitOpen())
            {
                return new ApiResponse { Success = false, Error = "Circuit breaker aberto - aguardando cooldown" };
            }

            try
            {
                string url = $"{_config.ApiUrl}/api/windows-clients/jobs";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddAuthHeaders(request);

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                ApiResponse? apiResponse = JsonConvert.DeserializeObject<ApiResponse>(content);

                RecordSuccess();
                return apiResponse ?? new ApiResponse { Success = false, Error = "Resposta invalida" };
            }
            catch (TaskCanceledException)
            {
                RecordFailure();
                LogService.LogWarning("Timeout ao buscar jobs");
                return new ApiResponse { Success = false, Error = "Timeout ao conectar com API" };
            }
            catch (HttpRequestException ex)
            {
                RecordFailure();
                LogService.LogError($"Erro de conexao ao buscar jobs: {ex.Message}");
                return new ApiResponse { Success = false, Error = ex.Message };
            }
            catch (Exception ex)
            {
                RecordFailure();
                LogService.LogError("Erro inesperado ao buscar jobs", ex);
                return new ApiResponse { Success = false, Error = ex.Message };
            }
        }

        public async Task<bool> CompleteJobAsync(int jobId)
        {
            try
            {
                string url = $"{_config.ApiUrl}/api/windows-clients/jobs?id={jobId}";

                var payload = new { status = "concluido" };
                string json = JsonConvert.SerializeObject(payload);
                var request = new HttpRequestMessage(HttpMethod.Put, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                AddAuthHeaders(request);

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                return true;
            }
            catch (TaskCanceledException)
            {
                LogService.LogWarning($"Timeout ao completar job {jobId}");
                return false;
            }
            catch (HttpRequestException ex)
            {
                LogService.LogError($"Erro de conexao ao completar job {jobId}: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Erro inesperado ao completar job {jobId}", ex);
                return false;
            }
        }

        public async Task<bool> FailJobAsync(int jobId, string errorMessage)
        {
            try
            {
                string url = $"{_config.ApiUrl}/api/windows-clients/jobs?id={jobId}";

                var payload = new { status = "erro", erro = errorMessage };
                string json = JsonConvert.SerializeObject(payload);
                var request = new HttpRequestMessage(HttpMethod.Put, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                AddAuthHeaders(request);

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                return true;
            }
            catch (TaskCanceledException)
            {
                LogService.LogWarning($"Timeout ao marcar job {jobId} como erro");
                return false;
            }
            catch (HttpRequestException ex)
            {
                LogService.LogError($"Erro de conexao ao marcar job {jobId} como erro: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LogService.LogError($"Erro inesperado ao marcar job {jobId} como erro", ex);
                return false;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                string url = $"{_config.ApiUrl}/api/windows-clients/status";
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"Teste de conexao falhou: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// v2.1.0: Verifica se há atualização disponível
        /// GET /api/windows-clients/check-update?version=X.Y.Z
        /// </summary>
        public async Task<UpdateCheckResponse?> CheckUpdateAsync(string currentVersion)
        {
            try
            {
                string url = $"{_config.ApiUrl}/api/windows-clients/check-update?version={currentVersion}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddAuthHeaders(request);

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<UpdateCheckResponse>(content);
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"Erro ao verificar atualizacao: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// v2.1.0: Envia logs e estatísticas para o servidor
        /// POST /api/windows-clients/logs
        /// </summary>
        public async Task<bool> SendLogsAsync(List<RemoteLogEntry> logs, int jobsProcessados, int jobsErro, int uptimeMinutes)
        {
            try
            {
                string url = $"{_config.ApiUrl}/api/windows-clients/logs";

                var payload = new
                {
                    logs = logs,
                    stats = new
                    {
                        jobs_impressos = jobsProcessados,
                        jobs_erro = jobsErro,
                        uptime_minutes = uptimeMinutes,
                        versao = UpdateService.CurrentVersion
                    }
                };

                string json = JsonConvert.SerializeObject(payload);
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                AddAuthHeaders(request);

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                return true;
            }
            catch (Exception ex)
            {
                // Não logar como erro para evitar loop infinito de logs
                Console.WriteLine($"Erro ao enviar logs remotos: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
