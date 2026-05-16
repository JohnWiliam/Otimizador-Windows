using CommunityToolkit.WinUI.Notifications;
using SystemOptimizer.Helpers;
using SystemOptimizer.Models;

namespace SystemOptimizer.Services;

public class UpdateNotificationService
{
    public void ShowUpdateNotification(UpdateInfo updateInfo)
    {
        if (!updateInfo.IsAvailable) return;

        var toastBuilder = new ToastContentBuilder()
            .AddText("Atualização disponível")
            .AddText($"Versão {updateInfo.Version} disponível. Abra as configurações para atualizar.")
            .AddArgument("action", "open-settings");

        ToastCompatHelper.Show(toastBuilder);
    }
}
