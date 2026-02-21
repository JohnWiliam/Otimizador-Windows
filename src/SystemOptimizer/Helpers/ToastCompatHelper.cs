using System;
using System.Linq;
using System.Reflection;
using CommunityToolkit.WinUI.Notifications;

namespace SystemOptimizer.Helpers;

public static class ToastCompatHelper
{
    public static void Show(ToastContentBuilder builder)
    {
        if (builder == null) return;

        try
        {
            var extensionMethod = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(t => t.IsSealed && t.IsAbstract)
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .FirstOrDefault(m =>
                    m.Name == "Show" &&
                    m.GetParameters().Length >= 1 &&
                    m.GetParameters()[0].ParameterType == typeof(ToastContentBuilder));

            extensionMethod?.Invoke(null, new object?[] { builder });
        }
        catch
        {
            // Ignora falhas para não interromper o app em ambientes sem suporte a toast.
        }
    }

    public static void RegisterActivationHandler(Action<string?> onActivated)
    {
        if (onActivated == null) return;

        try
        {
            var compatType = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .FirstOrDefault(t => t.FullName == "CommunityToolkit.WinUI.Notifications.ToastNotificationManagerCompat");

            var eventInfo = compatType?.GetEvent("OnActivated", BindingFlags.Public | BindingFlags.Static);
            if (eventInfo == null) return;

            var invokeMethod = eventInfo.EventHandlerType?.GetMethod("Invoke");
            var argsType = invokeMethod?.GetParameters().FirstOrDefault()?.ParameterType;
            if (argsType == null) return;

            var dynamicHandler = CreateDynamicHandler(argsType, onActivated);
            if (dynamicHandler != null)
            {
                eventInfo.AddEventHandler(null, dynamicHandler);
            }
        }
        catch
        {
            // Ignora falhas para manter compatibilidade de build/publicação.
        }
    }

    private static Delegate? CreateDynamicHandler(Type argsType, Action<string?> callback)
    {
        var methodInfo = typeof(ToastCompatHelper).GetMethod(nameof(OnToastActivated), BindingFlags.NonPublic | BindingFlags.Static);
        if (methodInfo == null) return null;

        var closedMethod = methodInfo.MakeGenericMethod(argsType);
        return Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(argsType), callback, closedMethod);
    }

    private static void OnToastActivated<TArgs>(Action<string?> callback, TArgs toastArgs)
    {
        if (toastArgs == null)
        {
            callback(null);
            return;
        }

        var argumentProperty = typeof(TArgs).GetProperty("Argument", BindingFlags.Public | BindingFlags.Instance);
        var argument = argumentProperty?.GetValue(toastArgs) as string;
        callback(argument);
    }

    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).Cast<Type>().ToArray();
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }
}
