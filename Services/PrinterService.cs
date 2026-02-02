using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Text;
using DeliveryPrintClient.Models;

namespace DeliveryPrintClient.Services
{
    public class PrinterService
    {
        private string? _contentToPrint;
        private string _printerName;

        public PrinterService(string printerName)
        {
            _printerName = printerName;
        }

        public bool Print(PrintJob job, int delayEntreCopias = 500)
        {
            try
            {
                // Usar impressora específica do job se definida, senão usar padrão
                string printerToUse = !string.IsNullOrEmpty(job.ImpressoraNome)
                    ? job.ImpressoraNome
                    : _printerName;

                if (string.IsNullOrEmpty(printerToUse))
                {
                    throw new Exception("Nenhuma impressora configurada");
                }

                _contentToPrint = job.Conteudo;
                int copias = job.Copias > 0 ? job.Copias : 1;

                // Loop de cópias
                for (int copia = 1; copia <= copias; copia++)
                {
                    if (copia > 1)
                    {
                        // Delay entre cópias (exceto primeira)
                        if (delayEntreCopias > 0)
                        {
                            System.Threading.Thread.Sleep(delayEntreCopias);
                        }
                        Console.WriteLine($"📄 Imprimindo cópia {copia}/{copias} do job #{job.Id}...");
                    }

                    PrintDocument printDoc = new PrintDocument
                    {
                        PrinterSettings = new PrinterSettings
                        {
                            PrinterName = printerToUse
                        }
                    };

                    // Verificar se impressora existe
                    if (!printDoc.PrinterSettings.IsValid)
                    {
                        throw new Exception($"Impressora '{printerToUse}' não encontrada");
                    }

                    // Configurar tamanho do papel (dinâmico conforme largura_papel do servidor)
                    printDoc.DefaultPageSettings.PaperSize = GetPaperSize(job.LarguraPapel ?? 80);
                    printDoc.DefaultPageSettings.Margins = new Margins(5, 5, 5, 5);

                    printDoc.PrintPage += PrintPage;

                    printDoc.Print();
                }

                if (copias > 1)
                {
                    Console.WriteLine($"✅ Job #{job.Id} impresso com sucesso em '{printerToUse}' ({copias} cópia(s))");
                }
                else
                {
                    Console.WriteLine($"✅ Job #{job.Id} impresso com sucesso em '{printerToUse}'");
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao imprimir job #{job.Id}: {ex.Message}");
                throw;
            }
        }

        private void PrintPage(object sender, PrintPageEventArgs e)
        {
            if (e.Graphics == null || _contentToPrint == null)
                return;

            // Configurações para impressora térmica
            Font font = new Font("Courier New", 8, FontStyle.Regular);
            SolidBrush brush = new SolidBrush(Color.Black);

            float yPos = 0;
            float leftMargin = e.MarginBounds.Left;
            float topMargin = e.MarginBounds.Top;

            string[] lines = _contentToPrint.Split('\n');

            foreach (string line in lines)
            {
                // Processar linha (remover caracteres de controle ESC/POS básicos)
                string cleanLine = ProcessEscPosLine(line);

                e.Graphics.DrawString(cleanLine, font, brush, leftMargin, topMargin + yPos);
                yPos += font.GetHeight(e.Graphics);
            }

            e.HasMorePages = false;
        }

        private string ProcessEscPosLine(string line)
        {
            // Processar comandos ESC/POS básicos
            // Melhorado para suportar comandos comuns
            StringBuilder sb = new StringBuilder();
            bool bold = false;
            int fontSize = 1; // 1 = normal, 2 = double width, 3 = double height

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                // Processar sequências ESC (0x1B)
                if (c == 0x1B && i + 1 < line.Length)
                {
                    char next = line[i + 1];
                    
                    // ESC E (bold on/off)
                    if (next == 'E')
                    {
                        bold = !bold;
                        i++; // Pular 'E'
                        continue;
                    }
                    
                    // ESC ! (tamanho de fonte)
                    if (next == '!')
                    {
                        if (i + 2 < line.Length)
                        {
                            byte size = (byte)line[i + 2];
                            fontSize = (size & 0x10) != 0 ? 2 : 1; // Double width
                            i += 2; // Pular '!' e byte de tamanho
                            continue;
                        }
                    }
                    
                    // ESC @ (reset)
                    if (next == '@')
                    {
                        bold = false;
                        fontSize = 1;
                        i++; // Pular '@'
                        continue;
                    }
                    
                    // Outros comandos ESC - pular sequência
                    i++;
                    continue;
                }

                // Manter apenas caracteres imprimíveis
                if ((c >= 32 && c <= 126) || c == '\t' || c == '\r' || c == '\n' || c >= 160)
                {
                    sb.Append(c);
                }
            }

            string result = sb.ToString().TrimEnd();
            
            // Aplicar formatação básica (Windows Printing API não suporta ESC/POS nativo)
            // Nota: A formatação real precisa ser feita no servidor antes de enviar
            return result;
        }

        /// <summary>
        /// Retorna o PaperSize para qualquer largura em mm.
        /// Fórmula: width_hundredths_of_inch = round(mm / 25.4 * 100)
        /// Aceita qualquer valor vindo do servidor — sem hardcode.
        /// Exemplos: 58mm→228, 76mm→299, 80mm→315
        /// </summary>
        private static PaperSize GetPaperSize(int larguraMm)
        {
            // Converter mm para centésimos de polegada (1 inch = 25.4 mm)
            int widthHundredths = (int)Math.Round(larguraMm / 25.4 * 100);

            // Altura generosa para bobina contínua (3150 ≈ 80cm)
            const int heightHundredths = 3150;

            LogService.LogInfo($"PaperSize: {larguraMm}mm -> {widthHundredths} hundredths-of-inch");

            return new PaperSize($"{larguraMm}mm", widthHundredths, heightHundredths);
        }

        public static string[] GetAvailablePrinters()
        {
            try
            {
                string[] printers = new string[PrinterSettings.InstalledPrinters.Count];
                PrinterSettings.InstalledPrinters.CopyTo(printers, 0);
                return printers;
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"Erro ao listar impressoras: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public static string GetDefaultPrinter()
        {
            try
            {
                PrintDocument printDoc = new PrintDocument();
                return printDoc.PrinterSettings.PrinterName;
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"Erro ao obter impressora padrao: {ex.Message}");
                return "";
            }
        }
    }
}
