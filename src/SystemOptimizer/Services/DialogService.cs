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
        // 1. Configura Ícone e Cor baseados no tipo
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

        var acrylicBrush = new SolidColorBrush(baseColor) { Opacity = 0.95 };

        // Grid principal para mensagem simples
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
            // Padding ajustado para balancear com o rodapé padrão
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

        // Grid Mestre: Removemos o padding padrão do ContentDialog para ter controle total
        var mainGrid = new Grid { Margin = new Thickness(0) };
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Header
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: Notes (Expands)
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Actions

        // Definição de margens laterais padrão para alinhamento perfeito
        const double sideMargin = 24;

        // --- 0. Cabeçalho ---
        var headerPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            Margin = new Thickness(sideMargin, 24, sideMargin, 16) 
        };
        
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
            MaxHeight = 300, 
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            // Margem inferior ajustada para dar respiro antes dos botões
            Margin = new Thickness(sideMargin, 0, sideMargin, 24), 
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            IsReadOnly = true,
            IsDocumentEnabled = true, 
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
        };

        // Renderiza e atribui o Documento
        notesRichTextBox.Document = RenderMarkdownToFlowDocument(releaseNotes);

        // --- 2. Painel de Ação e Progresso ---
        var actionPanel = new StackPanel 
        { 
            // Margem inferior 0 para "colar" visualmente no rodapé do ContentDialog (onde fica o botão Cancelar)
            Margin = new Thickness(sideMargin, 0, sideMargin, 0) 
        };

        // Painel de Status (Texto + Barra)
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

        // Botão Principal (Atualizar)
        var actionButton = new Wpf.Ui.Controls.Button
        {
            Content = Resources.Btn_UpdateAndRestart,
            Appearance = ControlAppearance.Primary,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            // Sem margem inferior para reduzir o gap com o botão Cancelar nativo
            Margin = new Thickness(0, 0, 0, 0) 
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
            CloseButtonText = "Cancelar",
            DialogMaxWidth = 500,
            Background = acrylicBrush,
            // IMPORTANTE: Padding 0 remove o espaçamento interno do Dialog, 
            // permitindo que nosso Grid controle as bordas com precisão.
            Padding = new Thickness(0) 
        };

        dialog.Resources["ContentDialogTopOverlay"] = Brushes.Transparent;
        dialog.Resources["ContentDialogContentBackground"] = Brushes.Transparent;
        dialog.Resources["ContentDialogBackground"] = Brushes.Transparent;

        // Lógica de Atualização
        actionButton.Click += async (s, e) =>
        {
            actionButton.IsEnabled = false;
            // Remove o texto do botão Cancelar para impedir fechamento durante download
            dialog.CloseButtonText = string.Empty; 
            statusPanel.Visibility = Visibility.Visible;

            try
            {
                var progress = new Progress<double>(p => progressBar.Value = p);
                await updateAction(progress);
                
                statusText.Text = Resources.Msg_Installing;
                progressBar.IsIndeterminate = true;
                // O app vai reiniciar, então não reabilitamos o botão
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
    /// Converte Markdown em FlowDocument com suporte a títulos, listas, links e blocos de código.
    /// </summary>
    private FlowDocument RenderMarkdownToFlowDocument(string markdown)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            TextAlignment = TextAlignment.Left
        };

        if (string.IsNullOrWhiteSpace(markdown)) return doc;

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        List? list = null;
        bool isNumberedList = false;
        bool inCodeBlock = false;
        var codeLines = new List<string>();

        foreach (var rawLine in lines)
        {
            string line = rawLine;
            string trimLine = line.TrimEnd();

            if (trimLine.StartsWith("```", StringComparison.Ordinal))
            {
                if (!inCodeBlock)
                {
                    inCodeBlock = true;
                    codeLines.Clear();
                }
                else
                {
                    inCodeBlock = false;
                    AddCodeBlock(doc, codeLines);
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeLines.Add(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimLine))
            {
                list = null;
                isNumberedList = false;
                doc.Blocks.Add(new Paragraph { Margin = new Thickness(0, 0, 0, 8) });
                continue;
            }

            string trimmed = trimLine.Trim();

            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                list = null;
                isNumberedList = false;
                int level = CountHeadingLevel(trimmed);
                string text = trimmed.TrimStart('#', ' ');
                var p = new Paragraph(ParseInline(text))
                {
                    FontSize = GetHeadingSize(level),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 12, 0, 6)
                };
                doc.Blocks.Add(p);
                continue;
            }

            if (IsBulletLine(trimmed))
            {
                if (list == null || isNumberedList)
                {
                    list = CreateList(TextMarkerStyle.Disc);
                    isNumberedList = false;
                    doc.Blocks.Add(list);
                }
                string text = trimmed.Substring(1).Trim();
                list.ListItems.Add(new ListItem(new Paragraph(ParseInline(text)) { Margin = new Thickness(0) }));
                continue;
            }

            if (TryParseNumberedLine(trimmed, out var numberedText))
            {
                if (list == null || !isNumberedList)
                {
                    list = CreateList(TextMarkerStyle.Decimal);
                    isNumberedList = true;
                    doc.Blocks.Add(list);
                }
                list.ListItems.Add(new ListItem(new Paragraph(ParseInline(numberedText)) { Margin = new Thickness(0) }));
                continue;
            }

            list = null;
            isNumberedList = false;
            doc.Blocks.Add(new Paragraph(ParseInline(trimmed)) { Margin = new Thickness(0, 0, 0, 4) });
        }

        if (inCodeBlock && codeLines.Count > 0)
        {
            AddCodeBlock(doc, codeLines);
        }

        return doc;
    }

    private static List CreateList(TextMarkerStyle markerStyle)
    {
        return new List
        {
            Margin = new Thickness(0, 0, 0, 8),
            MarkerStyle = markerStyle,
            Padding = new Thickness(20, 0, 0, 0)
        };
    }

    private static int CountHeadingLevel(string text)
    {
        int level = 0;
        while (level < text.Length && text[level] == '#') level++;
        return Math.Clamp(level, 1, 4);
    }

    private static double GetHeadingSize(int level)
    {
        return level switch
        {
            1 => 20,
            2 => 18,
            3 => 16,
            _ => 15
        };
    }

    private static bool IsBulletLine(string text)
    {
        return text.StartsWith("- ", StringComparison.Ordinal) || text.StartsWith("* ", StringComparison.Ordinal);
    }

    private static bool TryParseNumberedLine(string text, out string content)
    {
        content = string.Empty;
        var match = Regex.Match(text, @"^\d+\.\s+(.+)$");
        if (!match.Success) return false;
        content = match.Groups[1].Value.Trim();
        return true;
    }

    private void AddCodeBlock(FlowDocument doc, IReadOnlyList<string> codeLines)
    {
        var codeText = string.Join("\n", codeLines);
        var textBlock = new System.Windows.Controls.TextBlock
        {
            Text = codeText,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"] ?? Brushes.White
        };

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 4, 0, 8),
            Child = textBlock
        };

        doc.Blocks.Add(new BlockUIContainer(border));
    }

    private Inline ParseInline(string text)
    {
        if (string.IsNullOrEmpty(text)) return new Run(string.Empty);

        var span = new Span();
        var regex = new Regex(@"(\*\*.*?\*\*|`[^`]+`|\[[^\]]+\]\([^\)]+\))");
        var parts = regex.Split(text);

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;

            if (part.StartsWith("**") && part.EndsWith("**") && part.Length > 4)
            {
                span.Inlines.Add(new Run(part.Substring(2, part.Length - 4)) { FontWeight = FontWeights.Bold });
                continue;
            }

            if (part.StartsWith("`", StringComparison.Ordinal) && part.EndsWith("`", StringComparison.Ordinal) && part.Length > 2)
            {
                span.Inlines.Add(new Run(part.Substring(1, part.Length - 2))
                {
                    FontFamily = new FontFamily("Consolas"),
                    Background = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0))
                });
                continue;
            }

            if (part.StartsWith("[", StringComparison.Ordinal) && part.Contains("](", StringComparison.Ordinal) && part.EndsWith(")", StringComparison.Ordinal))
            {
                var linkMatch = Regex.Match(part, @"^\[(.+)\]\((.+)\)$");
                if (linkMatch.Success)
                {
                    string linkText = linkMatch.Groups[1].Value;
                    string linkUrl = linkMatch.Groups[2].Value;
                    var hyperlink = new Hyperlink(new Run(linkText))
                    {
                        NavigateUri = Uri.TryCreate(linkUrl, UriKind.Absolute, out var uri) ? uri : null
                    };
                    hyperlink.RequestNavigate += (_, e) =>
                    {
                        if (e.Uri != null)
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                        }
                    };
                    span.Inlines.Add(hyperlink);
                    continue;
                }
            }

            span.Inlines.Add(new Run(part));
        }

        return span;
    }
}
