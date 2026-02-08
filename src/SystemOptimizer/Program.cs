using System;
using System.Windows;
using SystemOptimizer.Helpers;
using CommunityToolkit.WinUI.Notifications;
using SystemOptimizer.Services;

namespace SystemOptimizer;

public static class Program
{
    [STAThread] // Essencial para aplicações WPF
    public static void Main(string[] args)
    {
        try
        {
            var isSilent = Array.Exists(args, arg => string.Equals(arg, "--silent", StringComparison.OrdinalIgnoreCase));

            if (isSilent)
            {
                var app = new App();
                app.RunSilentModeWithoutUiAsync().GetAwaiter().GetResult();
                return;
            }

            ToastNotificationManagerCompat.RegisterAumidAndComServer<ToastNotificationActivator>(NotificationConstants.AppId);
            ToastNotificationManagerCompat.RegisterActivator<ToastNotificationActivator>();

            var app = new App();

            // Carrega o App.xaml (recursos, estilos, temas).
            // Sem isso, a MainWindow falha ao tentar renderizar os controles do Wpf.Ui.
            app.InitializeComponent();

            // Inicia a aplicação
            app.Run();
        }
        catch (Exception ex)
        {
            // Se algo der errado antes da janela abrir, mostramos uma mensagem
            // Isso ajuda a diagnosticar erros de inicialização (ex: falta de dll, erro de config)
            MessageBox.Show($"Erro fatal na inicialização:\n\n{ex.Message}\n\n{ex.StackTrace}",
                            "SystemOptimizer - Erro Fatal",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

            // Tenta logar também
            Logger.Log($"FATAL CRASH: {ex}", "CRITICAL");
        }
    }
}
