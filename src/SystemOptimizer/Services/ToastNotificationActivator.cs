using System;
using System.Runtime.InteropServices;
using CommunityToolkit.WinUI.Notifications;

namespace SystemOptimizer.Services;

[ComVisible(true)]
[Guid("5F0E7F3A-84F1-4C78-9D5E-1F8F8B5A9D4C")]
public sealed class ToastNotificationActivator : ToastNotificationActivator
{
    public override void OnActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        base.OnActivated(e);
    }
}
