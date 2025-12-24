using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SystemOptimizer.Models;

namespace SystemOptimizer.Helpers;

public static class TweakPersistence
{
    // Caminho: C:\ProgramData\SystemOptimizer\tweak_settings.json
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
        "SystemOptimizer", 
        "tweak_settings.json");

    /// <summary>
    /// Salva o estado atual (IDs) de todos os tweaks que estão otimizados.
    /// </summary>
    public static void SaveState(IEnumerable<ITweak> tweaks)
    {
        try
        {
            // CORREÇÃO DE ERRO: Usamos 'IsOptimized' conforme definido na interface ITweak
            var activeTweaks = tweaks
                .Where(t => t.IsOptimized) 
                .Select(t => t.Id)
                .ToList();

            var json = JsonSerializer.Serialize(activeTweaks, new JsonSerializerOptions { WriteIndented = true });
            
            // CORREÇÃO DE AVISO: Verificação de nulo para evitar warning CS8604
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(ConfigPath, json);
            Logger.Log($"Estado salvo: {activeTweaks.Count} tweaks ativos salvos em {ConfigPath}.");
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao salvar persistência de tweaks: {ex.Message}", "ERROR");
        }
    }

    /// <summary>
    /// Carrega a lista de IDs dos tweaks que deveriam estar ativos.
    /// </summary>
    public static List<string> LoadState()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return [];

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao carregar persistência de tweaks: {ex.Message}", "ERROR");
            return [];
        }
    }
}
