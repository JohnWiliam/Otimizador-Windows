using System;
using Microsoft.Toolkit.Uwp.Notifications; // CORRIGIDO
using SystemOptimizer.Models;

namespace SystemOptimizer.Services;

public class UpdateNotificationService
{
    public void ShowUpdateNotification(UpdateInfo updateInfo)
    {
        if (!updateInfo.IsAvailable) return;

        new ToastContentBuilder()
            .AddText("Atualização Disponível")
            .AddText($"A versão {updateInfo.Version} está pronta para instalar.")
            .AddArgument("action", "update")
            .AddArgument("downloadUrl", updateInfo.DownloadUrl)
            .Show();
    }
}
