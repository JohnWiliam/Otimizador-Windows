using System;
using System.Windows;
using SystemOptimizer.Helpers;

namespace SystemOptimizer
{
    public static class Program
    {
        [STAThread] // Essencial para aplicações WPF
        public static void Main(string[] args)
        {
            try
            {
                var app = new App();

                // --- CORREÇÃO CRÍTICA ---
                // Carrega o App.xaml (recursos, estilos, temas). 
                // Sem isso, a MainWindow falha ao tentar renderizar os controles do Wpf.Ui.
                app.InitializeComponent(); 
                
                // Configura os serviços (Injeção de Dependência)
                app.ConfigureServices();

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
}