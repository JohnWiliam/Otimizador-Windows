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
            
            // CONFIGURAÇÃO FORÇADA DE FONTE E ALINHAMENTO
            // Isto garante que a fonte seja moderna e o texto fique à esquerda
            paragraph.FontFamily = new FontFamily("Segoe UI");
            paragraph.TextAlignment = TextAlignment.Left;
            paragraph.Margin = new Thickness(0, 0, 0, 4); 
            paragraph.LineHeight = 20; // Espaçamento entre linhas confortável

            // 1. Ícone
            SymbolRegular symbol = SymbolRegular.Info24;
            if (Enum.TryParse(item.Icon, out SymbolRegular parsedSymbol))
            {
                symbol = parsedSymbol;
            }

            var icon = new SymbolIcon
            {
                Symbol = symbol,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Cor do ícone
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(item.StatusColor);
                icon.Foreground = new SolidColorBrush(color);
            }
            catch
            {
                icon.Foreground = Brushes.Gray;
            }

            var iconContainer = new InlineUIContainer(icon)
            {
                BaselineAlignment = BaselineAlignment.Center
            };
            paragraph.Inlines.Add(iconContainer);
            paragraph.Inlines.Add(new Run("  ")); 

            // 2. Texto
            var run = new Run(item.Message)
            {
                BaselineAlignment = BaselineAlignment.Center,
                // Reforça a fonte no texto também
                FontFamily = new FontFamily("Segoe UI") 
            };

            run.Foreground = (Brush)FindResource("TextFillColorPrimaryBrush");

            if (item.IsBold)
            {
                run.FontWeight = FontWeights.SemiBold;
            }

            paragraph.Inlines.Add(run);

            LogOutput.Document.Blocks.Add(paragraph);
            LogOutput.ScrollToEnd();
        }
    }
}
