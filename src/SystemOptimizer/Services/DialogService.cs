using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace SystemOptimizer.Services;

public class DialogService : IDialogService
{
    private readonly IContentDialogService _contentDialogService;

    public DialogService(IContentDialogService contentDialogService)
    {
        _contentDialogService = contentDialogService;
    }

    public async Task ShowMessageAsync(string title, string message, DialogType type = DialogType.Info)
    {
        // Define o ícone e a cor baseados no tipo de mensagem
        SymbolRegular iconSymbol = SymbolRegular.Info24;
        Brush iconColor = Brushes.White;

        switch (type)
        {
            case DialogType.Success:
                iconSymbol = SymbolRegular.CheckmarkCircle24;
                // Verde vibrante para sucesso
                iconColor = new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10)); 
                break;
            case DialogType.Warning:
                iconSymbol = SymbolRegular.Warning24;
                // Amarelo/Laranja para aviso
                iconColor = new SolidColorBrush(Color.FromRgb(0xD8, 0x3B, 0x01)); 
                break;
            case DialogType.Error:
                iconSymbol = SymbolRegular.DismissCircle24;
                // Vermelho para erro
                iconColor = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23)); 
                break;
            case DialogType.Info:
            default:
                iconSymbol = SymbolRegular.Info24;
                // Azul padrão do sistema
                iconColor = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7)); 
                break;
        }

        // Constrói o layout visual do conteúdo (Ícone + Texto)
        var contentGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconControl = new SymbolIcon
        {
            Symbol = iconSymbol,
            FontSize = 40,
            Foreground = iconColor,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 15, 0)
        };

        // CORREÇÃO: Especificando System.Windows.Controls.TextBlock explicitamente para resolver ambiguidade
        var textBlock = new System.Windows.Controls.TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"] ?? Brushes.White
        };

        Grid.SetColumn(iconControl, 0);
        Grid.SetColumn(textBlock, 1);

        contentGrid.Children.Add(iconControl);
        contentGrid.Children.Add(textBlock);

        // Cria o diálogo com o conteúdo personalizado
        var dialog = new ContentDialog
        {
            Title = title,
            Content = contentGrid, // Usa nosso Grid visual em vez de apenas string
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            DialogMaxWidth = 500
        };

        await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }
}
