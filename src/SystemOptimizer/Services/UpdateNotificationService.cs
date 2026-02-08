using System;
using CommunityToolkit.WinUI.Notifications;
using SystemOptimizer.Helpers;
using SystemOptimizer.Properties;

namespace SystemOptimizer.Services;

public class UpdateNotificationService
{
    public void ShowUpdateAvailableToast(string version)
    {
        try
        {
            string title = string.Format(Resources.Msg_UpdateAvailable_Title, version);
            string body = Resources.Msg_UpdateAvailable_ToastBody;

            new ToastContentBuilder()
                .AddArgument("action", "open-settings")
                .AddText(title)
                .AddText(body)
                .Show();
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao exibir notificação de atualização: {ex.Message}", "ERROR");
        }
    }
}
