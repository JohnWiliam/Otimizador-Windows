using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Appearance;
using SystemOptimizer.Properties; // Para usar Resources

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

        var currentTheme = ApplicationThemeManager.GetAppTheme();
        Color baseColor = currentTheme == ApplicationTheme.Dark 
            ? Color.FromRgb(32, 32, 32)  
            : Color.FromRgb(248, 248, 248);

        var acrylicBrush = new SolidColorBrush(baseColor) { Opacity = 0.90 };

        var contentGrid = new Grid
        {
            Margin = new Thickness(0), 
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center
        };
        
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconControl = new SymbolIcon
        {
            Symbol = iconSymbol,
            FontSize = 32,
            Foreground = iconColor,
            VerticalAlignment = VerticalAlignment.Center, 
            Margin = new Thickness(0, 0, 16, 0)
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

        var dialog = new ContentDialog
        {
            Title = null, 
            Content = contentGrid,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            DialogMaxWidth = 420, 
            Padding = new Thickness(24, 20, 24, 10),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)),
            Background = acrylicBrush 
        };

        dialog.Resources["ContentDialogTopOverlay"] = Brushes.Transparent;
        dialog.Resources["ContentDialogContentBackground"] = Brushes.Transparent;
        dialog.Resources["ContentDialogBackground"] = Brushes.Transparent;

        await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }

    public async Task ShowUpdateDialogAsync(string version, string releaseNotes, Func<IProgress<double>, Task> updateAction)
    {
        var currentTheme = ApplicationThemeManager.GetAppTheme();
        Color baseColor = currentTheme == ApplicationTheme.Dark 
            ? Color.FromRgb(32, 32, 32)  
            : Color.FromRgb(248, 248, 248);
        var acrylicBrush = new SolidColorBrush(baseColor) { Opacity = 0.95 };

        // StackPanel principal
        var stackPanel = new StackPanel { Margin = new Thickness(0) };

        // --- Cabeçalho ---
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new SymbolIcon
        {
            Symbol = SymbolRegular.ArrowDownload24,
            FontSize = 28,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            Margin = new Thickness(0, 0, 12, 0)
        };

        var titleBlock = new System.Windows.Controls.TextBlock
        {
            // USA RESOURCE: Msg_UpdateAvailable_Title
            Text = string.Format(Resources.Msg_UpdateAvailable_Title, version),
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
        };

        Grid.SetColumn(icon, 0);
        Grid.SetColumn(titleBlock, 1);
        headerGrid.Children.Add(icon);
        headerGrid.Children.Add(titleBlock);

        // --- Notas da Versão ---
        var notesScroll = new ScrollViewer 
        { 
            MaxHeight = 200, 
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 16)
        };
        var notesBlock = new System.Windows.Controls.TextBlock
        {
            Text = releaseNotes,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Opacity = 0.8,
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
        };
        notesScroll.Content = notesBlock;

        // --- Painel de Status/Progresso ---
        var statusPanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 0) };
        var statusText = new System.Windows.Controls.TextBlock 
        { 
            // USA RESOURCE: Msg_Downloading
            Text = Resources.Msg_Downloading, 
            FontSize = 12, 
            Margin = new Thickness(0,0,0,4),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var progressBar = new System.Windows.Controls.ProgressBar 
        { 
            Height = 6, 
            IsIndeterminate = false, 
            Maximum = 100 
        };
        statusPanel.Children.Add(statusText);
        statusPanel.Children.Add(progressBar);

        // --- Botão de Ação Personalizado (CORREÇÃO CS1061) ---
        // Em vez de usar o botão do ContentDialog, criamos um aqui dentro
        // para controlar o clique sem fechar o diálogo.
        var actionButton = new Wpf.Ui.Controls.Button
        {
            // USA RESOURCE: Btn_UpdateAndRestart
            Content = Resources.Btn_UpdateAndRestart,
            Appearance = ControlAppearance.Primary,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            MinWidth = 160
        };

        // Adiciona elementos ao StackPanel
        stackPanel.Children.Add(headerGrid);
        stackPanel.Children.Add(notesScroll);
        stackPanel.Children.Add(statusPanel);
        stackPanel.Children.Add(actionButton); // Botão adicionado ao conteúdo

        var dialog = new ContentDialog
        {
            Title = null,
            Content = stackPanel,
            // Removemos o botão primário nativo para evitar conflito de fechamento
            PrimaryButtonText = "", 
            CloseButtonText = "Cancelar",
            DialogMaxWidth = 500,
            Background = acrylicBrush,
            Padding = new Thickness(24)
        };

        dialog.Resources["ContentDialogTopOverlay"] = Brushes.Transparent;
        dialog.Resources["ContentDialogContentBackground"] = Brushes.Transparent;
        dialog.Resources["ContentDialogBackground"] = Brushes.Transparent;

        // Lógica do Clique no Botão Personalizado
        actionButton.Click += async (s, e) =>
        {
            // 1. Bloqueia UI
            actionButton.IsEnabled = false;
            
            // CORREÇÃO: Usar string.Empty em vez de null para evitar aviso CS8625
            dialog.CloseButtonText = string.Empty; 
            
            statusPanel.Visibility = Visibility.Visible;

            try
            {
                var progress = new Progress<double>(p => progressBar.Value = p);
                
                // 2. Executa Download
                await updateAction(progress);

                // 3. Atualiza Status para Instalação
                // USA RESOURCE: Msg_Installing
                statusText.Text = Resources.Msg_Installing;
                progressBar.IsIndeterminate = true;

                // O aplicativo reiniciará em breve, não precisamos fechar o diálogo manualmente
            }
            catch (Exception ex)
            {
                // Erro: Restaura UI
                // USA RESOURCE: Msg_UpdateError
                statusText.Text = string.Format(Resources.Msg_UpdateError, ex.Message);
                statusText.Foreground = Brushes.Red;
                actionButton.IsEnabled = true;
                dialog.CloseButtonText = "Fechar"; // Permite fechar agora
                progressBar.Visibility = Visibility.Collapsed;
            }
        };

        await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }
}
