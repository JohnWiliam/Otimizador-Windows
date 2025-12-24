using System;
using System.IO;
using System.Text.Json;

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
        try
        {
            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    Current = config;
                }
            }
        }
        catch (Exception)
        {
            // Ignore errors, use default
        }
    }

    public static void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(_configPath);
            if (!Directory.Exists(dir))
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
