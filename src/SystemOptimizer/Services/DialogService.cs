using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Helpers;
using SystemOptimizer.Properties;

namespace SystemOptimizer.Services;

public sealed class DialogService : IDialogService
{
    private readonly IXamlRootProvider _xamlRootProvider;

    public DialogService(IXamlRootProvider xamlRootProvider)
    {
        _xamlRootProvider = xamlRootProvider;
    }

    public async Task ShowMessageAsync(string title, string message, DialogType type = DialogType.Info)
    {
        var xamlRoot = _xamlRootProvider.XamlRoot;
        if (xamlRoot is null)
        {
            Logger.Log($"Dialog sem XamlRoot: {title} - {message}", type.ToString().ToUpperInvariant());
            return;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };

        await dialog.ShowAsync();
    }

    public async Task ShowUpdateDialogAsync(string version, string releaseNotes, Func<IProgress<double>, Task> updateAction)
    {
        var xamlRoot = _xamlRootProvider.XamlRoot;
        if (xamlRoot is null)
        {
            Logger.Log($"Atualização {version} disponível, mas não há XamlRoot para diálogo.", "WARNING");
            return;
        }

        var notes = new TextBlock
        {
            Text = releaseNotes,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
        };

        var dialog = new ContentDialog
        {
            Title = $"{Resources.Settings_Update} {version}",
            Content = notes,
            PrimaryButtonText = Resources.Btn_UpdateAndRestart,
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var progressDialog = new ContentDialog
        {
            Title = Resources.Settings_Update,
            Content = new ProgressRing { IsIndeterminate = true },
            XamlRoot = xamlRoot
        };

        _ = progressDialog.ShowAsync();
        try
        {
            await updateAction(new Progress<double>(p => Logger.Log($"Progresso de atualização: {p:N0}%")));
        }
        finally
        {
            progressDialog.Hide();
        }
    }
}
