using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Appearance;

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
        // 1. Configura Ícone e Cor
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

        // 2. Determina o Tema e a Cor de Fundo Base
        var currentTheme = ApplicationThemeManager.GetAppTheme();
        Color baseColor = currentTheme == ApplicationTheme.Dark 
            ? Color.FromRgb(32, 32, 32)  
            : Color.FromRgb(248, 248, 248);

        // Efeito "Fake Acrylic" (0.90 de opacidade)
        var acrylicBrush = new SolidColorBrush(baseColor) { Opacity = 0.90 };

        // 3. Constrói o Layout Interno
        var contentGrid = new Grid
        {
            Margin = new Thickness(0), 
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center // Garante que o grid todo se centralize se a caixa for maior
        };
        
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconControl = new SymbolIcon
        {
            Symbol = iconSymbol,
            FontSize = 32, // Tamanho confortável
            Foreground = iconColor,
            // CORREÇÃO: Alinhamento 'Center' para ficar no meio da altura do texto
            VerticalAlignment = VerticalAlignment.Center, 
            Margin = new Thickness(0, 0, 16, 0) // Removemos a margem superior
        };

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var titleBlock = new System.Windows.Controls.TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"] ?? Brushes.White
        };

        var messageBlock = new System.Windows.Controls.TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Opacity = 0.9,
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"] ?? Brushes.White
        };

        textStack.Children.Add(titleBlock);
        textStack.Children.Add(messageBlock);

        Grid.SetColumn(iconControl, 0);
        Grid.SetColumn(textStack, 1);

        contentGrid.Children.Add(iconControl);
        contentGrid.Children.Add(textStack);

        // 4. Configuração da Caixa de Diálogo
        var dialog = new ContentDialog
        {
            Title = null, 
            Content = contentGrid,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            
            DialogMaxWidth = 420, 
            
            // PADDING REFINADO:
            // Top: 20 (Equilíbrio visual com o título)
            // Bottom: 10 (Menor porque os botões já têm margem, aproximando o conteúdo)
            Padding = new Thickness(24, 20, 24, 10),
            
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)),
            
            Background = acrylicBrush 
        };

        // Sobrescreve brushes para garantir transparência total
        dialog.Resources["ContentDialogTopOverlay"] = Brushes.Transparent;
        dialog.Resources["ContentDialogContentBackground"] = Brushes.Transparent;
        dialog.Resources["ContentDialogBackground"] = Brushes.Transparent;

        await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }
}
