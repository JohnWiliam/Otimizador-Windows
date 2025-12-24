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

        // 2. Constrói o Layout Interno (Compacto)
        var contentGrid = new Grid
        {
            Margin = new Thickness(0), // Sem margens externas extras
            Background = Brushes.Transparent
        };
        
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Ícone (Tamanho reduzido para 32 para ficar proporcional)
        var iconControl = new SymbolIcon
        {
            Symbol = iconSymbol,
            FontSize = 32,
            Foreground = iconColor,
            VerticalAlignment = VerticalAlignment.Center, // Centralizado com o bloco de texto
            Margin = new Thickness(0, 0, 12, 0)
        };

        // StackPanel para Título e Mensagem juntos
        var textStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        // Título (Manual)
        var titleBlock = new System.Windows.Controls.TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 2),
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"] ?? Brushes.White
        };

        // Mensagem (Tamanho 13 para ser mais delicado)
        var messageBlock = new System.Windows.Controls.TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Opacity = 0.9, // Leve transparência para hierarquia visual
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"] ?? Brushes.White
        };

        textStack.Children.Add(titleBlock);
        textStack.Children.Add(messageBlock);

        Grid.SetColumn(iconControl, 0);
        Grid.SetColumn(textStack, 1);

        contentGrid.Children.Add(iconControl);
        contentGrid.Children.Add(textStack);

        // 3. Fundo "Acrílico" Uniforme
        var currentTheme = ApplicationThemeManager.GetAppTheme();
        Color baseColor = currentTheme == ApplicationTheme.Dark 
            ? Color.FromRgb(30, 30, 30) 
            : Color.FromRgb(250, 250, 250);

        var acrylicBrush = new SolidColorBrush(baseColor) { Opacity = 0.85 };

        // 4. Configuração da Caixa de Diálogo
        var dialog = new ContentDialog
        {
            Title = null, // IMPORTANTE: Remove o cabeçalho nativo para evitar o fundo branco
            Content = contentGrid,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            DialogMaxWidth = 400, // Tamanho reduzido (antes era 500 ou padrão)
            Padding = new Thickness(20), // Padding interno balanceado
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128)),
            Background = acrylicBrush 
        };

        await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }
}
