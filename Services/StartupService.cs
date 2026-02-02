using System;
using Microsoft.Win32;

namespace DeliveryPrintClient.Services
{
    /// <summary>
    /// v2.1.0: Serviço simplificado de auto-start
    /// Apenas verifica se o app está no auto-start do Windows (definido pelo instalador Inno Setup).
    /// NÃO modifica o registro nem cria atalhos — isso é responsabilidade do instalador.
    /// </summary>
    public class StartupService
    {
        private const string APP_NAME = "Delivery Print Client";
        private const string REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// Verifica se o aplicativo está no auto-start (definido pelo instalador)
        /// </summary>
        public static bool EstaNoAutoStart()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, false))
                {
                    if (key != null)
                    {
                        object? value = key.GetValue(APP_NAME);
                        return value != null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"Erro ao verificar auto-start: {ex.Message}");
            }
            return false;
        }
    }
}
