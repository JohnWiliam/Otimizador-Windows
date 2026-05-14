using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SystemOptimizer.Services;

public sealed class DialogService : IDialogService
{
    public async Task ShowMessageAsync(string title, string message, DialogType type = DialogType.Info)
    {
        var dialog = CreateDialog(title, message);
        dialog.PrimaryButtonText = type == DialogType.Error ? "Fechar" : "OK";
        await dialog.ShowAsync();
    }

    public async Task ShowUpdateDialogAsync(string version, string releaseNotes, Func<IProgress<double>, Task> updateAction)
    {
        var progressBar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 8 };
        var status = new TextBlock { Text = "Pronto para baixar a atualização.", TextWrapping = TextWrapping.Wrap };
        var layout = new StackPanel { Spacing = 12 };
        layout.Children.Add(new TextBlock { Text = $"Versão {version}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        layout.Children.Add(new TextBlock { Text = releaseNotes, TextWrapping = TextWrapping.Wrap });
        layout.Children.Add(progressBar);
        layout.Children.Add(status);

        var dialog = CreateDialog("Atualização disponível", layout);
        dialog.PrimaryButtonText = "Baixar e instalar";
        dialog.CloseButtonText = "Depois";
        dialog.DefaultButton = ContentDialogButton.Primary;

        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            dialog.IsPrimaryButtonEnabled = false;
            var progress = new Progress<double>(value =>
            {
                progressBar.Value = Math.Clamp(value, 0, 100);
                status.Text = $"Baixando... {progressBar.Value:N0}%";
            });

            try
            {
                await updateAction(progress);
                status.Text = "Atualização iniciada com sucesso.";
                dialog.Hide();
            }
            catch (Exception ex)
            {
                status.Text = $"Falha ao atualizar: {ex.Message}";
                dialog.IsPrimaryButtonEnabled = true;
            }
        };

        await dialog.ShowAsync();
    }

    private static ContentDialog CreateDialog(string title, object content)
    {
        if (App.Window?.Content is not FrameworkElement root)
        {
            throw new InvalidOperationException("A janela principal ainda não está pronta para exibir diálogos.");
        }

        return new ContentDialog
        {
            Title = title,
            Content = content,
            XamlRoot = root.XamlRoot
        };
    }
}
