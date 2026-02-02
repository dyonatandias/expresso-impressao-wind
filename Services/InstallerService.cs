using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace DeliveryPrintClient.Services
{
    /// <summary>
    /// v2.1.0: Serviço de ícone da aplicação
    /// Carrega o ícone embarcado no assembly para uso na janela e tray
    /// </summary>
    public class InstallerService
    {
        /// <summary>
        /// Carrega o ícone da aplicação a partir do recurso embarcado
        /// </summary>
        public static Icon? LoadIconFromResource()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var stream = assembly.GetManifestResourceStream("app-icon.ico");

                if (stream != null)
                {
                    return new Icon(stream);
                }
            }
            catch
            {
                // Silent fail - usará ícone padrão do Windows
            }
            return null;
        }

        /// <summary>
        /// Carrega ícone em tamanho específico para tray icon
        /// </summary>
        public static Icon? LoadIconFromResource(int width, int height)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var stream = assembly.GetManifestResourceStream("app-icon.ico");

                if (stream != null)
                {
                    return new Icon(stream, width, height);
                }
            }
            catch
            {
                // Silent fail
            }
            return null;
        }
    }
}
