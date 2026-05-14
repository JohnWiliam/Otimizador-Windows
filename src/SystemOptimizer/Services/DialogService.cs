using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using SystemOptimizer.Properties;

namespace SystemOptimizer.Services;

public sealed class DialogService : IDialogService
{
    public async Task ShowMessageAsync(string title, string message, DialogType type = DialogType.Info)
    {
        var (symbol, color) = type switch
        {
            DialogType.Success => (Symbol.Accept, ColorHelper.FromArgb(255, 16, 124, 16)),
            DialogType.Warning => (Symbol.ReportHacked, ColorHelper.FromArgb(255, 216, 59, 1)),
            DialogType.Error => (Symbol.Cancel, ColorHelper.FromArgb(255, 232, 17, 35)),
            _ => (Symbol.Message, ColorHelper.FromArgb(255, 0, 120, 215))
        };

        var content = new StackPanel { Spacing = 12, MaxWidth = 520 };
        content.Children.Add(new SymbolIcon { Symbol = symbol, Foreground = new SolidColorBrush(color), Width = 36, Height = 36 });
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords });

        await ShowDialogAsync(new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close
        });
    }

    public async Task ShowUpdateDialogAsync(string version, string releaseNotes, Func<IProgress<double>, Task> updateAction)
    {
        var progress = new ProgressBar { Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed };
        var status = new TextBlock { Text = $"Versão {version} disponível.", TextWrapping = TextWrapping.WrapWholeWords };
        var content = new StackPanel { Spacing = 12, MaxWidth = 600 };
        content.Children.Add(status);
        content.Children.Add(new TextBlock { Text = releaseNotes, TextWrapping = TextWrapping.WrapWholeWords, MaxHeight = 240 });
        content.Children.Add(progress);

        var dialog = new ContentDialog
        {
            Title = Resources.Settings_Update,
            Content = content,
            PrimaryButtonText = Resources.Btn_UpdateAndRestart,
            CloseButtonText = Resources.Cleanup_ActionCancel,
            DefaultButton = ContentDialogButton.Primary
        };

        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            progress.Visibility = Visibility.Visible;
            dialog.IsPrimaryButtonEnabled = false;
            var reporter = new Progress<double>(value => progress.Value = Math.Clamp(value, 0, 100));
            await updateAction(reporter);
            dialog.Hide();
        };

        await ShowDialogAsync(dialog);
    }

    private static async Task ShowDialogAsync(ContentDialog dialog)
    {
        if (App.MainWindowInstance?.DialogXamlRoot is not { } xamlRoot)
            return;

        dialog.XamlRoot = xamlRoot;
        await dialog.ShowAsync();
    }
}
