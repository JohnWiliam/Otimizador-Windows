using System;
using System.Threading.Tasks;
using SystemOptimizer.Helpers;
using SystemOptimizer.Properties;

namespace SystemOptimizer.Services;

public class StartupTasksService
{
    private readonly IUpdateService _updateService;
    private readonly UpdateNotificationService _notificationService;

    public StartupTasksService(IUpdateService updateService, UpdateNotificationService notificationService)
    {
        _updateService = updateService;
        _notificationService = notificationService;
    }

    public async Task CheckForUpdatesAndNotifyAsync()
    {
        try
        {
            var updateInfo = await _updateService.CheckForUpdatesAsync();
            if (!updateInfo.IsAvailable)
            {
                return;
            }

            string version = updateInfo.Version ?? Resources.Msg_Unknown;
            _notificationService.ShowUpdateAvailableToast(version);
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao checar atualizações no startup: {ex.Message}", "ERROR");
        }
    }
}
