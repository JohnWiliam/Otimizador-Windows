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
        SymbolRegular iconSymbol = SymbolRegular.Info24;
        Brush iconColor = Brushes.White;

        switch (type)
        {
            case DialogType.Success:
                iconSymbol = SymbolRegular.CheckmarkCircle24;
                iconColor = new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10)); 
                break;
            case DialogType.Warning:
                iconSymbol = SymbolRegular.Warning24;
                iconColor = new SolidColorBrush(Color.FromRgb(0xD8, 0x3B, 0x01)); 
                break;
            case DialogType.Error:
                iconSymbol = SymbolRegular.DismissCircle24;
                iconColor = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23)); 
                break;
            case DialogType.Info:
            default:
                iconSymbol = SymbolRegular.Info24;
                iconColor = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7)); 
                break;
        }

        var contentGrid = new Grid 
        { 
            Margin = new Thickness(0, 10, 0, 0),
            Background = Brushes.Transparent // Garante que o grid não bloqueie o fundo acrílico
        };
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

        // CORREÇÃO: "Efeito Acrílico Fake"
        // Pegamos a cor de fundo padrão da janela (geralmente cinza escuro ou claro)
        // e aplicamos uma opacidade mais forte (0.75).
        // Isso cria um visual de "vidro fumê" uniforme em toda a caixa.
        var baseBrush = (Brush)Application.Current.Resources["ApplicationBackgroundBrush"] 
                        ?? new SolidColorBrush(Color.FromRgb(32, 32, 32));

        var dialog = new ContentDialog
        {
            Title = title,
            Content = contentGrid,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            DialogMaxWidth = 500,
            BorderThickness = new Thickness(1), // Borda fina para definição
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), // Borda sutil
            Background = ApplyOpacity(baseBrush, 0.75) // 75% opaco = efeito translúcido
        };

        await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }

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
