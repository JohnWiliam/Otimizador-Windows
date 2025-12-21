using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SystemOptimizer.Services;
using SystemOptimizer.Helpers;
using SystemOptimizer.Models;

namespace SystemOptimizer
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Verifica modo silencioso para persistência
            if (args != null && args.Contains("--silent"))
            {
                RunSilentMode();
                return;
            }

            // Modo Normal (GUI)
            try
            {
                var app = new App();
                app.InitializeComponent();
                
                var mainWindow = App.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
                
                app.Run();
            }
            catch (Exception ex)
            {
                LogCrash(ex);
            }
        }

        private static void RunSilentMode()
        {
            try
            {
                // Inicializa serviços sem a UI
                var services = App.ConfigureServices();
                var tweakService = services.GetRequiredService<TweakService>();

                Logger.Log("Iniciando modo silencioso (Persistência)...", "SILENT");

                tweakService.LoadTweaks();

                // Aplica apenas os Tweaks de Pesquisa (SysMain e Prefetch)
                // IDs definidos no TweakService: SE1 (SysMain) e SE2 (Prefetch)
                var searchTweaks = tweakService.Tweaks.Where(t => t.Id == "SE1" || t.Id == "SE2").ToList();

                foreach (var tweak in searchTweaks)
                {
                    Logger.Log($"Aplicando persistência para: {tweak.Title}", "SILENT");
                    tweak.Apply();
                }

                Logger.Log("Modo silencioso concluído.", "SILENT");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro no modo silencioso: {ex.Message}", "SILENT_ERROR");
            }
        }

        private static void LogCrash(Exception ex)
        {
            string logFile = "crash_log.txt";
            string message = $"[{DateTime.Now}] Critical Error:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}\n";
            if (ex.InnerException != null)
            {
                message += $"\nInner Exception:\n{ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
            }
            
            try
            {
                File.WriteAllText(logFile, message);
                MessageBox.Show($"O aplicativo falhou ao iniciar. Veja {logFile} para detalhes.\n\nErro: {ex.Message}", "Erro do Otimizador", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // Fallback se não conseguir escrever o log
            }
        }
    }
}
