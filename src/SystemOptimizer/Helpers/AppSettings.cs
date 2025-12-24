using System;
using System.IO;
using System.Text.Json;
using System.Globalization; // Necessário para detectar o idioma do sistema

namespace SystemOptimizer.Helpers;

public class AppConfig
{
    public string Language { get; set; } = "pt-BR";
}

public static class AppSettings
{
    private static readonly string _configPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SystemOptimizer",
        "app_settings.json");

    public static AppConfig Current { get; private set; } = new AppConfig();

    public static void Load()
    {
        bool loaded = false;
        try
        {
            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    Current = config;
                    loaded = true;
                }
            }
        }
        catch (Exception)
        {
            // Ignora erros e usa o padrão (ou a lógica abaixo)
        }

        // Se não foi carregado (primeira execução ou arquivo deletado), define baseado no sistema
        if (!loaded)
        {
            var systemCulture = CultureInfo.InstalledUICulture;

            // Verifica se o idioma do sistema começa com "pt" (cobre pt-BR e pt-PT)
            if (systemCulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase))
            {
                Current.Language = "pt-BR";
            }
            else
            {
                // Para qualquer outro idioma, define Inglês como padrão internacional
                Current.Language = "en-US";
            }
        }
    }

    public static void Save()
    {
        try
        {
            if (Path.GetDirectoryName(_configPath) is string dir && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception)
        {
            // Ignore errors
        }
    }
}
