using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SystemOptimizer.Models;
using SystemOptimizer.ViewModels;
using Wpf.Ui.Controls; 

namespace SystemOptimizer.Views.Pages
{
    public partial class CleanupPage : Page
    {
        public CleanupPage(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CleanupLogs.CollectionChanged += CleanupLogs_CollectionChanged;
        }

        private void CleanupLogs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                LogOutput.Document.Blocks.Clear();
            }
            else if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (CleanupLogItem item in e.NewItems)
                {
                    AppendLog(item);
                }
            }
        }

        private void AppendLog(CleanupLogItem item)
        {
            var paragraph = new Paragraph();
            
            paragraph.FontFamily = new FontFamily("Segoe UI");
            paragraph.TextAlignment = TextAlignment.Left;
            paragraph.Margin = new Thickness(0, 0, 0, 2); 
            paragraph.LineHeight = 18; 

            // --- 1. Lógica Inteligente de Cores (Baseada na mensagem e status) ---
            Brush statusBrush = GetSmartPastelBrush(item.StatusColor, item.Message);

            // --- 2. Ícone ---
            SymbolRegular symbol = SymbolRegular.Info24;
            // Tenta obter ícone do item, ou define ícones baseados no contexto
            if (!Enum.TryParse(item.Icon, out SymbolRegular parsedSymbol))
            {
                // Ícones automáticos se o sistema não enviar um específico
                string msgLower = item.Message.ToLower();
                if (msgLower.Contains("concluída") || msgLower.Contains("sucesso")) symbol = SymbolRegular.CheckmarkCircle24;
                else if (msgLower.Contains("erro") || msgLower.Contains("falha")) symbol = SymbolRegular.ErrorCircle24;
                else if (msgLower.Contains("lixeira") || msgLower.Contains("temp")) symbol = SymbolRegular.Delete24;
                else symbol = SymbolRegular.Info24;
            }
            else
            {
                symbol = parsedSymbol;
            }

            var icon = new SymbolIcon
            {
                Symbol = symbol,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = statusBrush // Ícone colorido
            };

            var iconContainer = new InlineUIContainer(icon)
            {
                BaselineAlignment = BaselineAlignment.Center
            };
            paragraph.Inlines.Add(iconContainer);
            paragraph.Inlines.Add(new Run("  ")); 

            // --- 3. Texto da Mensagem ---
            var run = new Run(item.Message)
            {
                BaselineAlignment = BaselineAlignment.Center,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12 
            };

            // Aplica a cor pastel ao texto também (para ficar tudo coeso e suave)
            // Se preferires o texto branco, muda para: run.Foreground = (Brush)FindResource("TextFillColorPrimaryBrush");
            run.Foreground = statusBrush; 

            if (item.IsBold)
            {
                run.FontWeight = FontWeights.SemiBold;
            }

            paragraph.Inlines.Add(run);

            LogOutput.Document.Blocks.Add(paragraph);
            LogOutput.ScrollToEnd();
        }

        // --- SISTEMA DE CORES INTUITIVAS ---
        private Brush GetSmartPastelBrush(string originalColorName, string message)
        {
            string msg = message?.ToLower() ?? "";
            string colorKey = originalColorName?.ToLower() ?? "";

            // 1. Prioridade: Windows Update / Serviços (VERDE)
            if (msg.Contains("update") || msg.Contains("wu") || msg.Contains("serviço") || msg.Contains("service"))
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A9DFBF")); // Verde Pastel Suave
            }

            // 2. Prioridade: Limpeza / Temporários / Lixeira (LARANJA/PÊSSEGO)
            // Tons quentes para indicar "remoção" ou "arquivos de lixo"
            if (msg.Contains("temp") || msg.Contains("tmp") || msg.Contains("lixeira") || 
                msg.Contains("trash") || msg.Contains("cache") || msg.Contains("prefetch") || 
                msg.Contains("log") || msg.Contains("old"))
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDBB99")); // Pêssego Pastel
            }

            // 3. Prioridade: Navegadores / Internet (AMARELO/CREME)
            if (msg.Contains("chrome") || msg.Contains("edge") || msg.Contains("firefox") || 
                msg.Contains("browser") || msg.Contains("navegador") || msg.Contains("cookie") || msg.Contains("histórico"))
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9E79F")); // Amarelo Creme Pastel
            }

            // 4. Prioridade: Rede / Sistema / Explorer (ROXO/LAVANDA)
            if (msg.Contains("dns") || msg.Contains("ip") || msg.Contains("rede") || 
                msg.Contains("explorer") || msg.Contains("sistema") || msg.Contains("system"))
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D7BDE2")); // Lavanda Pastel
            }

            // 5. Prioridade: Sucesso / Conclusão (VERDE BRILHANTE)
            if (msg.Contains("concluída") || msg.Contains("sucesso") || colorKey.Contains("green"))
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#82E0AA")); // Verde Primavera
            }

            // 6. Prioridade: Erro / Falha (VERMELHO SUAVE)
            if (msg.Contains("erro") || msg.Contains("falha") || msg.Contains("negado") || colorKey.Contains("red"))
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1948A")); // Vermelho Salmão
            }

            // 7. Padrão (AZUL GELO / CINZA AZULADO)
            // Usado para "Iniciando...", linhas pontilhadas ou info genérica
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AED6F1")); 
        }
    }
}
