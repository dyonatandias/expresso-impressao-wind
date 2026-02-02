using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace DeliveryPrintClient.Services
{
    /// <summary>
    /// Serviço de logging persistente em arquivo
    /// v2.1.0: Logs em %APPDATA%, buffer remoto para envio ao servidor
    /// </summary>
    public class LogService
    {
        // v2.1.0: Logs em %APPDATA%\DeliveryPrintClient\Logs\
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DeliveryPrintClient",
            "Logs"
        );

        // v2.1.0: Buffer de logs para envio remoto (WARNING e ERROR)
        private static readonly List<RemoteLogEntry> _remoteLogBuffer = new List<RemoteLogEntry>();
        private static readonly object _bufferLock = new object();
        private const int MaxBufferSize = 200;

        // v2.0.0 FIX: Property getter ao invés de static readonly
        private static string CurrentLogFile => Path.Combine(
            LogDirectory,
            $"app-{DateTime.Now:yyyy-MM-dd}.log"
        );

        private static readonly SemaphoreSlim _logSemaphore = new SemaphoreSlim(1, 1);
        private static bool _initialized = false;

        // v2.1.0: Estatísticas para envio remoto
        private static int _jobsProcessados = 0;
        private static int _jobsErro = 0;
        private static DateTime _startTime = DateTime.Now;

        /// <summary>
        /// Inicializa o serviço de log (cria pasta se necessário)
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                // v2.1.0: Migrar logs antigos da pasta do exe
                MigrateOldLogs();

                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                // Limpar logs antigos (manter últimos 7 dias)
                CleanupOldLogs(7);

                _initialized = true;
                _startTime = DateTime.Now;

                LogInfo("=".PadRight(80, '='));
                LogInfo($"Expresso Delivery Print Client v2.1.0 - Iniciado em {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                LogInfo($"Logs em: {LogDirectory}");
                LogInfo($"Config em: {ConfigService.GetConfigDir()}");
                LogInfo("=".PadRight(80, '='));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inicializar LogService: {ex.Message}");
            }
        }

        public static void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        public static void LogSuccess(string message)
        {
            WriteLog("SUCCESS", message);
        }

        public static void LogWarning(string message)
        {
            WriteLog("WARNING", message);
            AddToRemoteBuffer("WARNING", message);
        }

        public static void LogError(string message, Exception? ex = null)
        {
            if (ex != null)
            {
                string fullMessage = $"{message}\n  Exception: {ex.GetType().Name}\n  Message: {ex.Message}\n  StackTrace: {ex.StackTrace}";
                WriteLog("ERROR", fullMessage);
                AddToRemoteBuffer("ERROR", fullMessage);
            }
            else
            {
                WriteLog("ERROR", message);
                AddToRemoteBuffer("ERROR", message);
            }
        }

        /// <summary>
        /// v2.1.0: Incrementa contador de jobs processados
        /// </summary>
        public static void IncrementJobsProcessados()
        {
            Interlocked.Increment(ref _jobsProcessados);
        }

        /// <summary>
        /// v2.1.0: Incrementa contador de jobs com erro
        /// </summary>
        public static void IncrementJobsErro()
        {
            Interlocked.Increment(ref _jobsErro);
        }

        /// <summary>
        /// v2.1.0: Obtém estatísticas para envio remoto
        /// </summary>
        public static (int jobsProcessados, int jobsErro, int uptimeMinutes) GetStats()
        {
            int uptime = (int)(DateTime.Now - _startTime).TotalMinutes;
            return (_jobsProcessados, _jobsErro, uptime);
        }

        /// <summary>
        /// v2.1.0: Obtém e limpa buffer de logs remotos
        /// </summary>
        public static List<RemoteLogEntry> FlushRemoteBuffer()
        {
            lock (_bufferLock)
            {
                var copy = new List<RemoteLogEntry>(_remoteLogBuffer);
                _remoteLogBuffer.Clear();
                return copy;
            }
        }

        /// <summary>
        /// v2.1.0: Adiciona log ao buffer remoto (apenas WARNING e ERROR)
        /// </summary>
        private static void AddToRemoteBuffer(string level, string message)
        {
            lock (_bufferLock)
            {
                if (_remoteLogBuffer.Count >= MaxBufferSize)
                {
                    _remoteLogBuffer.RemoveAt(0); // Remove mais antigo
                }
                _remoteLogBuffer.Add(new RemoteLogEntry
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    Level = level,
                    Message = message.Length > 500 ? message.Substring(0, 500) + "..." : message
                });
            }
        }

        private static void WriteLog(string level, string message)
        {
            if (!_initialized) Initialize();

            _logSemaphore.Wait();
            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] [{level.PadRight(8)}] {message}";

                File.AppendAllText(CurrentLogFile, logEntry + Environment.NewLine);
                Console.WriteLine(logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao escrever log: {ex.Message}");
            }
            finally
            {
                _logSemaphore.Release();
            }
        }

        public static void Flush()
        {
            _logSemaphore.Wait();
            _logSemaphore.Release();
        }

        private static void CleanupOldLogs(int keepDays)
        {
            try
            {
                var logFiles = Directory.GetFiles(LogDirectory, "app-*.log");
                var cutoffDate = DateTime.Now.AddDays(-keepDays);

                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        File.Delete(logFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao limpar logs antigos: {ex.Message}");
            }
        }

        /// <summary>
        /// v2.1.0: Migra logs da pasta do exe para %APPDATA%
        /// </summary>
        private static void MigrateOldLogs()
        {
            try
            {
                string oldLogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (Directory.Exists(oldLogDir) && oldLogDir != LogDirectory)
                {
                    if (!Directory.Exists(LogDirectory))
                    {
                        Directory.CreateDirectory(LogDirectory);
                    }

                    var oldFiles = Directory.GetFiles(oldLogDir, "app-*.log");
                    foreach (var oldFile in oldFiles)
                    {
                        string fileName = Path.GetFileName(oldFile);
                        string newPath = Path.Combine(LogDirectory, fileName);
                        if (!File.Exists(newPath))
                        {
                            try
                            {
                                File.Copy(oldFile, newPath);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch
            {
                // Silent fail - migração de logs não é crítica
            }
        }

        public static string GetCurrentLogPath()
        {
            return CurrentLogFile;
        }

        public static void OpenLogFolder()
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", LogDirectory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao abrir pasta de logs: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// v2.1.0: Entrada de log para envio remoto ao servidor
    /// </summary>
    public class RemoteLogEntry
    {
        public string Timestamp { get; set; } = "";
        public string Level { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
