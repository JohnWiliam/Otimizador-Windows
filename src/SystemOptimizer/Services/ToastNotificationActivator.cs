using System;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;

namespace SystemOptimizer.Services;

[Guid("49755D6D-5507-4C78-9571-042839958055"), ComVisible(true)]
public class ToastNotificationActivator : NotificationActivator
{
    public override void OnActivated(string arguments, NotificationUserInput userInput, string appUserModelId)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null)
            {
                if (mainWindow.WindowState == System.Windows.WindowState.Minimized)
                    mainWindow.WindowState = System.Windows.WindowState.Normal;

                mainWindow.Activate();
                mainWindow.Focus();
            }
        });
    }
}
