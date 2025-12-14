using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace SystemOptimizer
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
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
                    MessageBox.Show($"O aplicativo falhou ao iniciar e não conseguiu escrever o log.\n\nErro: {ex.Message}", "Erro do Otimizador", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
