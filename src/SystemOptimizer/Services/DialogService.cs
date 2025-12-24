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
            ? Color.FromRgb(32, 32, 32)  // Cinza escuro para Dark Mode
            : Color.FromRgb(248, 248, 248); // Branco gelo para Light Mode

        // Cria o efeito "Fake Acrylic" (Cor sólida com transparência)
        var acrylicBrush = new SolidColorBrush(baseColor) { Opacity = 0.85 };

        // 3. Constrói o Layout Interno (Ultra Compacto)
        var contentGrid = new Grid
        {
            Margin = new Thickness(0, 5, 0, 5), // Margens internas mínimas
            Background = Brushes.Transparent
        };
        
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconControl = new SymbolIcon
        {
            Symbol = iconSymbol,
            FontSize = 28, // Ícone ligeiramente menor
            Foreground = iconColor,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 12, 0)
        };

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var titleBlock = new System.Windows.Controls.TextBlock
        {
            Text = title,
            FontSize = 15, // Título compacto
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 3),
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"] ?? Brushes.White
        };

        var messageBlock = new System.Windows.Controls.TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13, // Texto do corpo menor
            Opacity = 0.85,
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
            Title = null, // Remove cabeçalho nativo
            Content = contentGrid,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            
            // Dimensões Compactas
            DialogMaxWidth = 340, 
            DialogMaxHeight = 200, // Limita altura vertical
            
            // Remove espaçamentos padrão do controle que incham a caixa
            Padding = new Thickness(20, 20, 20, 15), 
            
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)),
            
            // Aplica o fundo acrílico em TUDO
            Background = acrylicBrush 
        };

        // 5. TRUQUE CRÍTICO: Sobrescrever os Brushes internos do ContentDialog
        // Isso remove o fundo branco/sólido que a biblioteca aplica automaticamente na área de conteúdo
        // forçando-a a ser transparente para revelar nosso fundo acrílico.
        dialog.Resources["ContentDialogTopOverlay"] = Brushes.Transparent;
        dialog.Resources["ContentDialogContentBackground"] = Brushes.Transparent;
        dialog.Resources["ContentDialogBackground"] = Brushes.Transparent; // Fallback

        await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }
}
