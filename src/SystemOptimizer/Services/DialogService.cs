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
                iconColor = new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10)); // Verde
                break;
            case DialogType.Warning:
                iconSymbol = SymbolRegular.Warning24;
                iconColor = new SolidColorBrush(Color.FromRgb(0xD8, 0x3B, 0x01)); // Laranja
                break;
            case DialogType.Error:
                iconSymbol = SymbolRegular.DismissCircle24;
                iconColor = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23)); // Vermelho
                break;
            case DialogType.Info:
            default:
                iconSymbol = SymbolRegular.Info24;
                iconColor = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7)); // Azul
                break;
        }

        // Constrói o layout visual
        var contentGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconControl = new SymbolIcon
        {
            Symbol = iconSymbol,
            FontSize = 40,
            Foreground = iconColor,
            VerticalAlignment = VerticalAlignment.Center, 
            Margin = new Thickness(0, 0, 15, 0)
        };

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

        // Configuração do Dialog para estética Mica/Moderno
        var dialog = new ContentDialog
        {
            Title = title,
            Content = contentGrid,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            DialogMaxWidth = 500,
            
            // CORREÇÃO VISUAL: Usa a cor de controle padrão do tema com leve transparência
            // Isso simula o efeito Mica integrado, evitando o bloco sólido opaco.
            Background = ApplyOpacity(
                (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"] ?? new SolidColorBrush(Color.FromRgb(32, 32, 32)), 
                0.95)
        };

        await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }

    // Método auxiliar para adicionar transparência a um Brush existente
    private Brush ApplyOpacity(Brush brush, double opacity)
    {
        if (brush.Clone() is Brush clone)
        {
            clone.Opacity = opacity;
            if (clone.CanFreeze) clone.Freeze();
            return clone;
        }
        return brush;
    }
}
