using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DeliveryPrintClient.Models;

namespace DeliveryPrintClient.Services
{
    /// <summary>
    /// Servico de verificacao e aplicacao de atualizacoes.
    /// v2.1.0: Verifica se ha nova versao no servidor.
    /// v2.3.0: Auto-download e auto-install via batch script.
    /// </summary>
    public class UpdateService
    {
        public const string CurrentVersion = "2.3.0";

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
        /// Verifica se e hora de checar atualizacao (a cada 30 min)
        /// </summary>
        public bool ShouldCheck()
        {
            return _config.AutoUpdate && (DateTime.Now - _lastCheck) > CheckInterval;
        }

        /// <summary>
        /// Verifica se ha atualizacao disponivel no servidor
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

        /// <summary>
        /// v2.3.0: Baixa o novo executavel para pasta temporaria.
        /// Retorna o caminho do arquivo baixado, ou null se falhar.
        /// </summary>
        public async Task<string?> DownloadUpdateAsync(
            string downloadUrl,
            string version,
            IProgress<(long received, long total)>? progress = null,
            CancellationToken cancellation = default)
        {
            string fullUrl = downloadUrl.StartsWith("http")
                ? downloadUrl
                : $"{_config.ApiUrl}{downloadUrl}";

            string updateDir = Path.Combine(Path.GetTempPath(), "DeliveryPrintClient", "update");
            Directory.CreateDirectory(updateDir);

            string fileName = $"ExpressoDeliveryPrintClient-v{version}.exe";
            string tempPath = Path.Combine(updateDir, fileName);

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            LogService.LogInfo($"Baixando atualizacao de: {fullUrl}");

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using var response = await httpClient.GetAsync(fullUrl, HttpCompletionOption.ResponseHeadersRead, cancellation);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            byte[] buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellation)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellation);
                totalRead += bytesRead;
                progress?.Report((totalRead, totalBytes));
            }

            fileStream.Close();

            var fileInfo = new FileInfo(tempPath);
            if (fileInfo.Length < 10 * 1024 * 1024)
            {
                File.Delete(tempPath);
                throw new Exception($"Download incompleto: {fileInfo.Length / 1024 / 1024}MB (esperado >10MB)");
            }

            LogService.LogInfo($"Download concluido: {tempPath} ({fileInfo.Length / 1024 / 1024}MB)");
            return tempPath;
        }

        /// <summary>
        /// v2.3.0: Cria batch script de atualizacao, lanca, e encerra o app.
        /// O batch aguarda o processo encerrar, copia o novo exe, e relanca.
        /// </summary>
        public static void ApplyUpdate(string newExePath)
        {
            string currentExePath = Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new Exception("Nao foi possivel determinar o caminho do executavel atual");

            string currentExeName = Path.GetFileName(currentExePath);
            string batchDir = Path.GetDirectoryName(newExePath)!;
            string batchPath = Path.Combine(batchDir, "update.bat");

            string script = $@"@echo off
chcp 65001 >nul
echo Atualizando Expresso Delivery Print Client...
echo Aguardando aplicativo encerrar...

:waitloop
tasklist /FI ""IMAGENAME eq {currentExeName}"" 2>NUL | find /I ""{currentExeName}"" >NUL
if ""%ERRORLEVEL%""==""0"" (
    timeout /t 1 /nobreak >nul
    goto waitloop
)

echo Aplicativo encerrado. Copiando nova versao...

set RETRIES=0
:copyloop
copy /Y ""{newExePath}"" ""{currentExePath}"" >nul 2>&1
if errorlevel 1 (
    set /a RETRIES+=1
    if %RETRIES% GEQ 10 (
        echo ERRO: Nao foi possivel copiar o arquivo apos 10 tentativas.
        pause
        goto cleanup
    )
    timeout /t 2 /nobreak >nul
    goto copyloop
)

echo Atualizacao concluida! Iniciando nova versao...
start """" ""{currentExePath}""

:cleanup
del ""{newExePath}"" >nul 2>&1
del ""%~f0"" >nul 2>&1
exit
";

            File.WriteAllText(batchPath, script, System.Text.Encoding.GetEncoding(850));

            var psi = new ProcessStartInfo
            {
                FileName = batchPath,
                CreateNoWindow = true,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(psi);

            LogService.LogInfo("Script de atualizacao lancado. Encerrando para atualizar...");
            LogService.Flush();

            Application.Exit();
        }
    }
}
