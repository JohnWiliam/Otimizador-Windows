using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Properties;

namespace SystemOptimizer.Services;

public sealed class DialogService : IDialogService
{
    public async Task ShowMessageAsync(string title, string message, DialogType type = DialogType.Info)
    {
        var dialog = CreateDialog(title, new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, "OK");
        await ShowAsync(dialog);
    }

    public async Task ShowUpdateDialogAsync(string version, string releaseNotes, Func<IProgress<double>, Task> updateAction)
    {
        var progressBar = new ProgressBar { Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed, Height = 6 };
        var status = new TextBlock { Text = Resources.Msg_Downloading, Visibility = Visibility.Collapsed };
        var updateButton = new Button { Content = Resources.Btn_UpdateAndRestart, Style = Application.Current.Resources["AccentButtonStyle"] as Style };

        var content = new StackPanel { Spacing = 14, MinWidth = 420 };
        content.Children.Add(new TextBlock { Text = string.Format(Resources.Msg_UpdateAvailable_Title, version), Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new ScrollViewer
        {
            MaxHeight = 240,
            Content = new TextBlock { Text = releaseNotes, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true }
        });
        content.Children.Add(status);
        content.Children.Add(progressBar);
        content.Children.Add(updateButton);

        var dialog = CreateDialog(Resources.Settings_Update, content, Resources.Cleanup_ActionCancel);

        updateButton.Click += async (_, _) =>
        {
            updateButton.IsEnabled = false;
            status.Visibility = Visibility.Visible;
            progressBar.Visibility = Visibility.Visible;

            var progress = new Progress<double>(value => progressBar.Value = value);
            try
            {
                await updateAction(progress);
                dialog.Hide();
            }
            catch (Exception ex)
            {
                updateButton.IsEnabled = true;
                await ShowMessageAsync(Resources.Msg_ErrorTitle, ex.Message, DialogType.Error);
            }
        };

        await ShowAsync(dialog);
    }

    private static ContentDialog CreateDialog(string title, object content, string closeButtonText)
    {
        return new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainAppWindow?.Content is FrameworkElement root ? root.XamlRoot : null
        };
    }

    private static async Task ShowAsync(ContentDialog dialog)
    {
        if (dialog.XamlRoot is null)
        {
            Logger.Log($"Diálogo ignorado sem XamlRoot: {dialog.Title}", "WARNING");
            return;
        }

        await dialog.ShowAsync();
    }
}
