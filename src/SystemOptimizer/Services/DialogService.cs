using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Appearance; // Necessário para detectar o tema

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
        // 1. Configura Ícone e Cor do Ícone
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

        // 2. Constrói o Layout Interno
        var contentGrid = new Grid
        {
            Margin = new Thickness(0, 10, 0, 0),
            Background = Brushes.Transparent, // Importante: Transparente para ver o fundo do Dialog
            VerticalAlignment = VerticalAlignment.Center
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

        // Usa System.Windows.Controls.TextBlock explicitamente
        var textBlock = new System.Windows.Controls.TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            // Garante que o texto seja legível dependendo do tema, mas sem bloquear o fundo
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"] ?? Brushes.White
        };

        Grid.SetColumn(iconControl, 0);
        Grid.SetColumn(textBlock, 1);

        contentGrid.Children.Add(iconControl);
        contentGrid.Children.Add(textBlock);

        // 3. Configura o Fundo "Acrílico Fake" Uniforme (0.85 Opacidade)
        // Detectamos o tema atual para escolher entre Preto ou Branco como base
        var currentTheme = ApplicationThemeManager.GetAppTheme();
        
        // Se for Dark, usa um cinza bem escuro. Se for Light, usa um cinza quase branco.
        Color baseColor = currentTheme == ApplicationTheme.Dark 
            ? Color.FromRgb(30, 30, 30) 
            : Color.FromRgb(250, 250, 250);

        var acrylicBrush = new SolidColorBrush(baseColor)
        {
            Opacity = 0.85 // Opacidade solicitada
        };

        // 4. Cria o Dialog
        var dialog = new ContentDialog
        {
            Title = title,
            Content = contentGrid,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            DialogMaxWidth = 500,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128)), // Borda sutil
            
            // Aplica o pincel translúcido em TODO o fundo do diálogo
            Background = acrylicBrush 
        };

        await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }
}
