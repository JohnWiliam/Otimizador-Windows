using System;
using System.Threading;
using System.Threading.Tasks;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace SystemOptimizer.Services;

public class DialogService : IDialogService
{
    private readonly IContentDialogService _contentDialogService;

    // Injeção de dependência do serviço de ContentDialog (configurado no App.xaml.cs)
    public DialogService(IContentDialogService contentDialogService)
    {
        _contentDialogService = contentDialogService;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        // Cria um diálogo moderno que aparece DENTRO da janela, preservando o Mica
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close
        };

        await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }
}
