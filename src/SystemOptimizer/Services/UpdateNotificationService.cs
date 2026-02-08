using System;
using CommunityToolkit.WinUI.Notifications; // ADICIONADO
using SystemOptimizer.Models;

namespace SystemOptimizer.Services;

public class UpdateNotificationService
{
    public void ShowUpdateNotification(UpdateInfo updateInfo)
    {
        if (!updateInfo.IsAvailable) return;

        // CORREÇÃO: Usando string literal caso o Resource não exista, e adicionando namespace correto para 'Show()'
        new ToastContentBuilder()
            .AddText("Atualização Disponível")
            .AddText($"A versão {updateInfo.Version} está pronta para instalar.")
            .AddArgument("action", "update")
            .AddArgument("downloadUrl", updateInfo.DownloadUrl)
            .Show(); // Agora o método Show() será reconhecido
    }
}