using System;
using System.IO;
using Newtonsoft.Json;
using DeliveryPrintClient.Models;

namespace DeliveryPrintClient.Services
{
    public class ConfigService
    {
        // v2.1.0: Config em %APPDATA% (pasta fixa, preserva entre atualizações)
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DeliveryPrintClient"
        );

        private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

        // Caminho antigo (v2.0.0) - para migração automática
        private static readonly string OldConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config.json"
        );

        public static AppConfig Load()
        {
            try
            {
                // v2.1.0: Migrar config antigo se existir
                MigrateOldConfig();

                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"Erro ao carregar configuracao: {ex.Message}");
            }

            return new AppConfig();
        }

        public static void Save(AppConfig config)
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                LogService.LogError($"Erro ao salvar configuracao: {ex.Message}");
                throw;
            }
        }

        public static string GetConfigPath()
        {
            return ConfigPath;
        }

        public static string GetConfigDir()
        {
            return ConfigDir;
        }

        /// <summary>
        /// v2.1.0: Migra config da pasta do exe (v2.0.0) para %APPDATA% (v2.1.0)
        /// Só migra se o config antigo existe E o novo ainda não existe
        /// </summary>
        private static void MigrateOldConfig()
        {
            try
            {
                if (File.Exists(OldConfigPath) && !File.Exists(ConfigPath))
                {
                    if (!Directory.Exists(ConfigDir))
                    {
                        Directory.CreateDirectory(ConfigDir);
                    }

                    File.Copy(OldConfigPath, ConfigPath);
                    LogService.LogInfo($"Config migrado automaticamente de {OldConfigPath} para {ConfigPath}");

                    // Renomear antigo para .bak (não deletar, por segurança)
                    try
                    {
                        string backupPath = OldConfigPath + ".migrated.bak";
                        if (!File.Exists(backupPath))
                        {
                            File.Move(OldConfigPath, backupPath);
                        }
                    }
                    catch
                    {
                        // Silent fail - não é crítico
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"Erro ao migrar config antigo: {ex.Message}");
            }
        }
    }
}
