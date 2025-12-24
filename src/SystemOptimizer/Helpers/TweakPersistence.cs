using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using SystemOptimizer.Models;

namespace SystemOptimizer.Helpers
{
    public static class TweakPersistence
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
            "SystemOptimizer", 
            "tweak_settings.json");

        // Salva o estado atual de todos os tweaks ativos
        public static void SaveState(IEnumerable<ITweak> tweaks)
        {
            try
            {
                // Filtra apenas os tweaks que estão HABILITADOS (IsApplied = true)
                // Assumindo que o teu modelo ITweak tem uma propriedade Id e IsApplied
                var activeTweaks = tweaks
                    .Where(t => t.IsApplied)
                    .Select(t => t.Id)
                    .ToList();

                var json = JsonSerializer.Serialize(activeTweaks);
                
                // Garante que a pasta existe
                var dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                File.WriteAllText(ConfigPath, json);
                Logger.Log($"Estado dos tweaks salvo com sucesso. {activeTweaks.Count} tweaks ativos.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao salvar estado dos tweaks: {ex.Message}", "ERROR");
            }
        }

        // Carrega e devolve a lista de IDs dos tweaks que devem estar ativos
        public static List<string> LoadState()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return new List<string>();

                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao carregar estado dos tweaks: {ex.Message}", "ERROR");
                return new List<string>();
            }
        }
    }
}
