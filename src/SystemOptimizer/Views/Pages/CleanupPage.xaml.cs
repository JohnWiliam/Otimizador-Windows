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
            
            // Mantendo a formatação de sucesso anterior
            paragraph.FontFamily = new FontFamily("Segoe UI");
            paragraph.TextAlignment = TextAlignment.Left;
            paragraph.Margin = new Thickness(0, 0, 0, 4); 
            paragraph.LineHeight = 22; // Um pouco mais de "ar" entre as linhas

            // --- 1. Determinar a Cor Pastel ---
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
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = statusBrush // Aplica a cor pastel ao ícone
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
                FontFamily = new FontFamily("Segoe UI") 
            };

            // Se for um erro ou sucesso explícito, podemos colorir o texto também,
            // ou manter o texto padrão para leitura e colorir só o ícone.
            // Aqui, vou aplicar a cor pastel ao texto se for Bold (títulos), 
            // caso contrário, uso a cor padrão do tema para leitura fácil.
            if (item.IsBold)
            {
                run.FontWeight = FontWeights.SemiBold;
                run.Foreground = statusBrush; // Títulos ganham a cor
            }
            else
            {
                // Texto normal usa a cor do tema (branco/preto) para melhor contraste
                run.Foreground = (Brush)FindResource("TextFillColorPrimaryBrush");
            }

            paragraph.Inlines.Add(run);

            LogOutput.Document.Blocks.Add(paragraph);
            LogOutput.ScrollToEnd();
        }

        // --- MÁGICA DAS CORES PASTEL ---
        private Brush GetPastelBrush(string originalColorName)
        {
            // Normaliza a string para evitar erros de maiúsculas/minúsculas
            string key = originalColorName?.ToLower()?.Trim() ?? "";

            // Paleta Pastel (Hex codes manuais para tons modernos)
            if (key.Contains("green") || key.Contains("success"))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#76D7C4")); // Verde Menta Suave
            
            if (key.Contains("red") || key.Contains("error") || key.Contains("fail"))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1948A")); // Vermelho Salmão Suave
            
            if (key.Contains("blue") || key.Contains("info"))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#85C1E9")); // Azul Céu Suave
            
            if (key.Contains("yellow") || key.Contains("orange") || key.Contains("warn"))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7DC6F")); // Amarelo Creme

            // Se não reconhecer a cor, tenta converter (fallback) ou devolve cinza claro
            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(originalColorName));
            }
            catch
            {
                return new SolidColorBrush(Colors.LightGray);
            }
        }
    }
}
