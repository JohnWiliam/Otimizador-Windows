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
            // Margem reduzida para ficar mais compacto
            paragraph.Margin = new Thickness(0, 0, 0, 2); 
            // Altura de linha menor para visual "subtil"
            paragraph.LineHeight = 18; 

            // --- 1. Determinar a Cor Pastel (Lógica Reforçada) ---
            Brush statusBrush = GetPastelBrush(item.StatusColor);

            // --- 2. Ícone ---
            SymbolRegular symbol = SymbolRegular.Info24;
            if (Enum.TryParse(item.Icon, out SymbolRegular parsedSymbol))
            {
                symbol = parsedSymbol;
            }

            var icon = new SymbolIcon
            {
                Symbol = symbol,
                FontSize = 14, // Ícone ligeiramente menor
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = statusBrush
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
                FontSize = 12 // Fonte subtil tamanho 12
            };

            if (item.IsBold)
            {
                run.FontWeight = FontWeights.SemiBold;
                run.Foreground = statusBrush; // Títulos coloridos
            }
            else
            {
                // Texto normal usa a cor do tema para legibilidade
                run.Foreground = (Brush)FindResource("TextFillColorPrimaryBrush");
            }

            paragraph.Inlines.Add(run);

            LogOutput.Document.Blocks.Add(paragraph);
            LogOutput.ScrollToEnd();
        }

        // --- MÁGICA DAS CORES PASTEL (ATUALIZADA) ---
        private Brush GetPastelBrush(string originalColorName)
        {
            if (string.IsNullOrWhiteSpace(originalColorName))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#85C1E9")); // Default Azul

            string key = originalColorName.ToLower().Trim();

            // 1. Verdes (Sucesso)
            // Deteta: "green", "verde", "success", "sucesso", "ok"
            if (key.Contains("green") || key.Contains("verde") || key.Contains("success") || key.Contains("sucesso") || key.Contains("ok"))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#76D7C4")); // Pastel Mint
            
            // 2. Vermelhos (Erro)
            // Deteta: "red", "vermelho", "error", "erro", "fail", "falha"
            if (key.Contains("red") || key.Contains("vermelho") || key.Contains("error") || key.Contains("erro") || key.Contains("fail"))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1948A")); // Pastel Salmon
            
            // 3. Amarelos (Aviso)
            // Deteta: "yellow", "amarelo", "warn", "aviso", "orange", "laranja"
            if (key.Contains("yellow") || key.Contains("amarelo") || key.Contains("warn") || key.Contains("aviso") || key.Contains("orange"))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7DC6F")); // Pastel Cream

            // 4. Azuis (Info/Default)
            // Deteta: "blue", "azul", "info"
            if (key.Contains("blue") || key.Contains("azul") || key.Contains("info"))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#85C1E9")); // Pastel Blue

            // Fallback: Se for um código HEX que não apanhámos acima, tenta usar, senão devolve azul padrão
            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(originalColorName));
            }
            catch
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#85C1E9")); // Default Pastel Blue
            }
        }
    }
}
