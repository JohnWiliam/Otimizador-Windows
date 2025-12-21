using System;
using System.Windows;
using System.Linq;
using SystemOptimizer.Services;
using SystemOptimizer.Helpers;

namespace SystemOptimizer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Verifica se o argumento --silent foi passado (usado pelo Agendador de Tarefas)
            if (e.Args.Contains("--silent"))
            {
                RunSilentMode();
            }
            else
            {
                // Modo Normal: Abre a interface gráfica
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
            }
        }

        private void RunSilentMode()
        {
            try
            {
                Logger.Log("Iniciando em Modo Silencioso (Verificacao de Persistencia)...");

                // Inicializa o serviço de tweaks
                var tweakService = new TweakService();
                tweakService.LoadTweaks();

                // LÓGICA DE EXECUÇÃO SILENCIOSA:
                // Como o objetivo é apenas manter a persistência ativa e reaplicar o necessário.
                // Futuramente, você pode implementar um sistema de "Carregar Perfil" aqui
                // para reaplicar automaticamente os tweaks que o usuário salvou.
                
                // Por enquanto, apenas registramos que rodou com sucesso.
                Logger.Log("Tarefas de inicialização silenciosa concluídas.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro no modo silencioso: {ex.Message}", "ERROR");
            }
            finally
            {
                // Garante que o processo seja encerrado imediatamente para não consumir memória
                Shutdown();
            }
        }
    }
}
