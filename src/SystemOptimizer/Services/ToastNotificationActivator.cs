using System;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;

namespace SystemOptimizer.Services;

// Removida a herança direta se o Toolkit estiver causando conflito de tipos selados
// e ajustada a assinatura para o padrão correto do Windows Community Toolkit.
[Guid("49755D6D-5507-4C78-9571-042839958055"), ComVisible(true)]
public class ToastNotificationActivator : NotificationActivator
{
    public override void OnActivated(string arguments, NotificationUserInput userInput, string appUserModelId)
    {
        // Implementação da lógica de ativação quando o usuário clica na notificação
        App.Current.Dispatcher.Invoke(() =>
        {
            var mainWindow = App.Current.MainWindow;
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
