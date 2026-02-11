using System;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications; // CORRIGIDO

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
                // Fallback simples de log caso o app falhe na inicialização
                MessageBox.Show($"Fatal Error: {ex.Message}", "SystemOptimizer Critical Error");
            }
        }
    }
}
