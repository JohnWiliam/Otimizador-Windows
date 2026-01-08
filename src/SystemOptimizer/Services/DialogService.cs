using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
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
            VerticalAlignment = VerticalAlignment.Top, 
            Margin = new Thickness(0, 0, 16, 0)
        };

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var titleBlock = new System.Windows.Controls.TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
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
            Padding = new Thickness(24, 24, 24, 12),
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

        // Grid principal para controle total do layout
        var mainGrid = new Grid { Margin = new Thickness(0) };
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Header
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: Notes (Expands)
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Actions

        // --- 0. Cabeçalho (Com Padding Superior/Lateral) ---
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(24, 24, 24, 16) };
        
        var icon = new SymbolIcon
        {
            Symbol = SymbolRegular.ArrowDownload24,
            FontSize = 28,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var titleBlock = new System.Windows.Controls.TextBlock
        {
            Text = string.Format(Resources.Msg_UpdateAvailable_Title, version),
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
        };

        headerPanel.Children.Add(icon);
        headerPanel.Children.Add(titleBlock);

        // --- 1. Notas da Versão (Markdown Renderizado) ---
        var notesRichTextBox = new System.Windows.Controls.RichTextBox
        {
            MaxHeight = 250, // Altura máxima controlada
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(24, 0, 24, 16),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            IsReadOnly = true,
            IsDocumentEnabled = true, // Permite seleção
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
        };

        // Converte Markdown e atribui ao Document
        notesRichTextBox.Document = RenderMarkdownToFlowDocument(releaseNotes);

        // --- 2. Painel de Ação e Progresso (Padding Inferior reduzido para colar no Footer) ---
        var actionPanel = new StackPanel { Margin = new Thickness(24, 0, 24, 0) };

        // Painel de Status (Download)
        var statusPanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 12) };
        var statusText = new System.Windows.Controls.TextBlock 
        { 
            Text = Resources.Msg_Downloading, 
            FontSize = 12, 
            Margin = new Thickness(0,0,0,4),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var progressBar = new System.Windows.Controls.ProgressBar 
        { 
            Height = 4, 
            IsIndeterminate = false, 
            Maximum = 100 
        };
        statusPanel.Children.Add(statusText);
        statusPanel.Children.Add(progressBar);

        // Botão de Atualizar
        var actionButton = new Wpf.Ui.Controls.Button
        {
            Content = Resources.Btn_UpdateAndRestart,
            Appearance = ControlAppearance.Primary,
            HorizontalAlignment = HorizontalAlignment.Stretch, // Ocupa largura total
            Margin = new Thickness(0, 0, 0, 4) // Pequena margem antes do botão nativo "Cancelar"
        };

        actionPanel.Children.Add(statusPanel);
        actionPanel.Children.Add(actionButton);

        // Montagem do Grid
        Grid.SetRow(headerPanel, 0);
        Grid.SetRow(notesRichTextBox, 1);
        Grid.SetRow(actionPanel, 2);

        mainGrid.Children.Add(headerPanel);
        mainGrid.Children.Add(notesRichTextBox);
        mainGrid.Children.Add(actionPanel);

        var dialog = new ContentDialog
        {
            Title = null,
            Content = mainGrid,
            PrimaryButtonText = "", 
            CloseButtonText = "Cancelar", // Botão nativo no rodapé
            DialogMaxWidth = 500,
            Background = acrylicBrush,
            Padding = new Thickness(0) // IMPORTANTE: Padding zero para controlar margens internamente
        };

        // Ajustes de estilo para transparência
        dialog.Resources["ContentDialogTopOverlay"] = Brushes.Transparent;
        dialog.Resources["ContentDialogContentBackground"] = Brushes.Transparent;
        dialog.Resources["ContentDialogBackground"] = Brushes.Transparent;

        // Lógica do Botão
        actionButton.Click += async (s, e) =>
        {
            actionButton.IsEnabled = false;
            dialog.CloseButtonText = string.Empty; 
            statusPanel.Visibility = Visibility.Visible;

            try
            {
                var progress = new Progress<double>(p => progressBar.Value = p);
                await updateAction(progress);
                statusText.Text = Resources.Msg_Installing;
                progressBar.IsIndeterminate = true;
            }
            catch (Exception ex)
            {
                statusText.Text = string.Format(Resources.Msg_UpdateError, ex.Message);
                statusText.Foreground = Brushes.Red;
                actionButton.IsEnabled = true;
                dialog.CloseButtonText = "Fechar"; 
                progressBar.Visibility = Visibility.Collapsed;
            }
        };

        await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }

    /// <summary>
    /// Converte Markdown básico (Headers, Bullet Points, Negrito) em um FlowDocument para RichTextBox.
    /// </summary>
    private FlowDocument RenderMarkdownToFlowDocument(string markdown)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14
        };

        if (string.IsNullOrWhiteSpace(markdown)) return doc;

        var lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        List list = null; // Para agrupar itens de lista

        foreach (var line in lines)
        {
            string trimLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimLine)) continue;

            // Header (#)
            if (trimLine.StartsWith("#"))
            {
                list = null; // Quebra lista anterior
                string text = trimLine.TrimStart('#', ' ');
                var p = new Paragraph(ParseBold(text))
                {
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 12, 0, 4)
                };
                doc.Blocks.Add(p);
            }
            // List Item (- ou *)
            else if (trimLine.StartsWith("- ") || trimLine.StartsWith("* "))
            {
                if (list == null)
                {
                    list = new List { Margin = new Thickness(0, 0, 0, 8), MarkerStyle = TextMarkerStyle.Disc };
                    doc.Blocks.Add(list);
                }
                string text = trimLine.Substring(1).Trim();
                var li = new ListItem(new Paragraph(ParseBold(text)));
                list.ListItems.Add(li);
            }
            // Texto Normal
            else
            {
                list = null; // Quebra lista anterior
                var p = new Paragraph(ParseBold(trimLine))
                {
                    Margin = new Thickness(0, 0, 0, 4)
                };
                doc.Blocks.Add(p);
            }
        }

        return doc;
    }

    /// <summary>
    /// Processa negrito (**texto**) dentro de uma linha.
    /// </summary>
    private Inline ParseBold(string text)
    {
        // Se não tiver negrito, retorna Run simples
        if (!text.Contains("**")) return new Run(text);

        var span = new Span();
        var parts = Regex.Split(text, @"(\*\*.*?\*\*)");

        foreach (var part in parts)
        {
            if (part.StartsWith("**") && part.EndsWith("**") && part.Length > 4)
            {
                span.Inlines.Add(new Run(part.Substring(2, part.Length - 4)) { FontWeight = FontWeights.Bold });
            }
            else
            {
                if (!string.IsNullOrEmpty(part)) span.Inlines.Add(new Run(part));
            }
        }
        return span;
    }
}
