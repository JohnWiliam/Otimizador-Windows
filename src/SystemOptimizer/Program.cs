using System;
using System.Windows;
using SystemOptimizer.Helpers;

namespace SystemOptimizer
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // O Toolkit gerencia a ativação automaticamente se configurado corretamente no App.xaml.cs/StartupService
            
            try 
            {
                var app = new App();
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro fatal antes da interface estar disponível: {ex}", "FATAL");
                MessageBox.Show($"Fatal Error: {ex.Message}", "SystemOptimizer Critical Error");
            }
        }
    }
}
