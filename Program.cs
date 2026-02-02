using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DeliveryPrintClient.Forms;
using DeliveryPrintClient.Services;

namespace DeliveryPrintClient
{
    static class Program
    {
        // v2.1.0: P/Invoke para trazer janela existente para frente
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        [STAThread]
        static void Main()
        {
            // Visual styles primeiro (necessário para diálogos de instalação)
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // v2.1.0: Mutex para impedir múltiplas instâncias
            using var mutex = new Mutex(true, "Global\\DeliveryPrintClient_SingleInstance", out bool isNewInstance);

            if (!isNewInstance)
            {
                // Já existe uma instância rodando - trazer para frente
                BringExistingInstanceToFront();
                return;
            }

            // Configurar handlers de exceções não capturadas
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // Inicializar sistema de logs
            LogService.Initialize();
            LogService.LogInfo("==================== APLICACAO INICIADA ====================");
            LogService.LogInfo($"Expresso Delivery Print Client v{UpdateService.CurrentVersion} - {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            LogService.LogInfo($"Executando de: {Process.GetCurrentProcess().MainModule?.FileName}");
            LogService.LogInfo($"Config em: {ConfigService.GetConfigPath()}");

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                LogService.LogError("Erro fatal ao iniciar aplicacao", ex);

                MessageBox.Show(
                    $"Erro fatal ao iniciar aplicacao:\n\n{ex.Message}\n\nOs detalhes foram salvos no log.",
                    "Erro Fatal",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                LogService.LogInfo("==================== APLICACAO ENCERRADA ====================");
                LogService.Flush();
            }
        }

        /// <summary>
        /// v2.1.0: Traz a janela da instância existente para frente
        /// </summary>
        private static void BringExistingInstanceToFront()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var processes = Process.GetProcessesByName(currentProcess.ProcessName);

                foreach (var process in processes)
                {
                    if (process.Id != currentProcess.Id && process.MainWindowHandle != IntPtr.Zero)
                    {
                        // Se a janela está minimizada, restaurar
                        if (IsIconic(process.MainWindowHandle))
                        {
                            ShowWindow(process.MainWindowHandle, SW_RESTORE);
                        }
                        SetForegroundWindow(process.MainWindowHandle);
                        return;
                    }
                }

                // Se não encontrou janela principal (pode estar no tray), mostrar mensagem
                MessageBox.Show(
                    "O Delivery Print Client ja esta em execucao.\n\nVerifique o icone na bandeja do sistema (perto do relogio).",
                    "Aplicativo Ja em Execucao",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch
            {
                MessageBox.Show(
                    "O Delivery Print Client ja esta em execucao.",
                    "Aplicativo Ja em Execucao",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }

        static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            LogService.LogError("Excecao nao tratada (UI Thread)", e.Exception);

            MessageBox.Show(
                $"Ocorreu um erro inesperado:\n\n{e.Exception.Message}\n\nO erro foi registrado no log.\n\nClique em OK para continuar.",
                "Erro Nao Esperado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception? ex = e.ExceptionObject as Exception;

            if (ex != null)
            {
                LogService.LogError($"Excecao nao tratada (AppDomain) - IsTerminating: {e.IsTerminating}", ex);
            }
            else
            {
                LogService.LogError($"Excecao desconhecida (AppDomain) - IsTerminating: {e.IsTerminating}");
            }

            if (e.IsTerminating)
            {
                MessageBox.Show(
                    $"Erro fatal que encerrara o aplicativo:\n\n{ex?.Message ?? "Erro desconhecido"}\n\nOs detalhes foram salvos no log.",
                    "Erro Fatal",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Stop
                );
            }
        }
    }
}
