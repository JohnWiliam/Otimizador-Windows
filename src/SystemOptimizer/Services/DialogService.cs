using System.Threading.Tasks;
using System.Windows;

namespace SystemOptimizer.Services;

public class DialogService : IDialogService
{
    public async Task ShowMessageAsync(string title, string message)
    {
        // We must run UI operations on the UI thread
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            var uiMessageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                ShowTitle = true
            };

            await uiMessageBox.ShowDialogAsync();
        });
    }
}
