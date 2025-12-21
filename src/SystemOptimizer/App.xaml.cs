using System;
using System.IO;
using System.Windows;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;
using SystemOptimizer.Helpers;
using SystemOptimizer.Views.Pages;
using Wpf.Ui; 

namespace SystemOptimizer
{
    public partial class App : Application
    {
        public IServiceProvider? Services { get; private set; }

        public void ConfigureServices()
        {
            var services = new ServiceCollection();

            // 1. ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<TweakViewModel>();

            // 2. Core Services
            services.AddSingleton<TweakService>();
            services.AddSingleton<CleanupService>();

            // 3. UI Services
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<ISnackbarService, SnackbarService>();
            services.AddSingleton<IContentDialogService, ContentDialogService>();

            // 4. Windows & Pages
            services.AddSingleton<MainWindow>();
            services.AddTransient<TweaksPage>();
            services.AddTransient<PerformancePage>();
            services.AddTransient<PrivacyPage>();
            services.AddTransient<NetworkPage>();
            services.AddTransient<SecurityPage>();
            services.AddTransient<CleanupPage>();
            services.AddTransient<AppearancePage>();

            Services = services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Configura tratamento global de erros para evitar fechamento repentino
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;

            base.OnStartup(e);

            if (Services == null) ConfigureServices();

            if (e.Args.Contains("--silent"))
            {
                RunSilentMode();
            }
            else
            {
                if (Services != null)
                {
                    var mainWindow = Services.GetRequiredService<MainWindow>();
                    mainWindow.Show();
                }
            }
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Captura erros que acontecerem na UI (ex: clique de botão que falha)
            string errorMsg = $"Ocorreu um erro inesperado: {e.Exception.Message}";
            Logger.Log(errorMsg, "ERROR");
            MessageBox.Show(errorMsg, "Erro do Sistema", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // Impede o crash total se possível
        }

        private void RunSilentMode()
        {
            try
            {
                Logger.Log("Iniciando Modo Silencioso...");
                if (Services == null) return;

                var tweakService = Services.GetRequiredService<TweakService>();
                tweakService.LoadTweaks();
                
                // Lógica adicional de persistência se necessário
                Logger.Log("Modo Silencioso Concluído.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro no modo silencioso: {ex.Message}", "ERROR");
            }
            finally
            {
                Shutdown();
            }
        }
    }
}