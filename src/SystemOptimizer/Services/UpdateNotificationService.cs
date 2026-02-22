using System;
using CommunityToolkit.WinUI.Notifications; // CORRIGIDO
using SystemOptimizer.Helpers;
using SystemOptimizer.Models;

namespace SystemOptimizer.Services;

public class UpdateNotificationService
{
    public void ShowUpdateNotification(UpdateInfo updateInfo)
    {
        if (!updateInfo.IsAvailable) return;

        var toastBuilder = new ToastContentBuilder()
            .AddText("Atualização Disponível")
            .AddText($"A versão {updateInfo.Version} está pronta para instalar.")
            .AddArgument("action", "update")
            .AddArgument("downloadUrl", updateInfo.DownloadUrl);

        ToastCompatHelper.Show(toastBuilder);
    }
}
