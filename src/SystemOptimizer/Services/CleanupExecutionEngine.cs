using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using SystemOptimizer.Helpers;

namespace SystemOptimizer.Services;

public class CleanupExecutionEngine
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint dwFlags);

    const uint SHERB_NOCONFIRMATION = 0x00000001;
    const uint SHERB_NOPROGRESSUI = 0x00000002;
    const uint SHERB_NOSOUND = 0x00000004;

    public async Task<CleanupResult> ExecuteAsync(CleanupTarget target, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new CleanupResult { CategoryName = target.CategoryName };

        cancellationToken.ThrowIfCancellationRequested();
        switch (target.Strategy)
        {
            case CleanupExecutionStrategy.DeleteDirectoryContents:
                CleanupDirectory(target.Path, result, cancellationToken);
                break;
            case CleanupExecutionStrategy.ExecuteCommand:
                await ExecuteCommandAsync(target, result, cancellationToken);
                break;
            case CleanupExecutionStrategy.EmptyRecycleBin:
                EmptyRecycleBin(result);
                break;
            case CleanupExecutionStrategy.CleanupWindowsUpdate:
                await CleanupWindowsUpdateAsync(target.Path, result, cancellationToken);
                break;
            case CleanupExecutionStrategy.CleanupBrowserCache:
                CleanupBrowserCache(result, cancellationToken);
                break;
            default:
                result.Failures++;
                break;
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    private static async Task ExecuteCommandAsync(CleanupTarget target, CleanupResult result, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(target.Command))
            {
                result.Failures++;
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var commandResult = await CommandHelper.RunCommandDetailedAsync(target.Command, target.Arguments ?? string.Empty);
            cancellationToken.ThrowIfCancellationRequested();
            if (!commandResult.IsSuccess)
            {
                result.Failures++;
                Logger.Log($"Comando de limpeza '{target.CategoryName}' falhou. ExitCode={commandResult.ExitCode}, StdErr='{commandResult.StdErr}'", "ERROR");
                return;
            }

            result.ItemsRemoved = 1;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Failures++;
            Logger.Log($"Falha ao executar comando de limpeza '{target.CategoryName}': {ex.Message}", "ERROR");
        }
    }

    private static void EmptyRecycleBin(CleanupResult result)
    {
        try
        {
            int hr = SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
            if (hr != 0)
            {
                result.Failures++;
                Logger.Log($"Falha ao esvaziar lixeira. HRESULT=0x{hr:X8}, Win32Error={Marshal.GetLastWin32Error()}", "ERROR");
                return;
            }

            result.ItemsRemoved = 1;
        }
        catch (Exception ex)
        {
            result.Failures++;
            Logger.Log($"Falha ao esvaziar lixeira: {ex.Message}", "ERROR");
        }
    }

    private static void CleanupDirectory(string path, CleanupResult result, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(path) || IsReparsePoint(path))
        {
            return;
        }

        foreach (var filePath in EnumerateFilesWithoutFollowingReparsePoints(path, result, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    result.ItemsIgnored++;
                    continue;
                }

                long size = fileInfo.Length;
                fileInfo.Delete();
                result.BytesRemoved += size;
                result.ItemsRemoved++;
            }
            catch (Exception ex)
            {
                result.ItemsIgnored++;
                result.Failures++;
                Logger.Log($"Falha ao remover arquivo '{filePath}': {ex.Message}", "WARNING");
            }
        }

        foreach (var directoryPath in EnumerateDirectoriesWithoutFollowingReparsePoints(path, result, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (IsReparsePoint(directoryPath))
                {
                    result.ItemsIgnored++;
                    continue;
                }

                Directory.Delete(directoryPath, recursive: false);
                result.ItemsRemoved++;
            }
            catch (Exception ex)
            {
                result.ItemsIgnored++;
                result.Failures++;
                Logger.Log($"Falha ao remover diretório '{directoryPath}': {ex.Message}", "WARNING");
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesWithoutFollowingReparsePoints(string rootPath, CleanupResult result, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string currentDirectory = pending.Pop();

            foreach (var file in SafeEnumerateFiles(currentDirectory, result, "ERROR"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return file;
            }

            foreach (var directory in SafeEnumerateDirectories(currentDirectory, result, "ERROR"))
            {
                if (IsReparsePoint(directory))
                {
                    result.ItemsIgnored++;
                    Logger.Log($"Diretório reparse point ignorado durante limpeza: '{directory}'", "WARNING");
                    continue;
                }

                pending.Push(directory);
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesWithoutFollowingReparsePoints(string rootPath, CleanupResult result, CancellationToken cancellationToken)
    {
        var pending = new Stack<(string Path, bool Expanded)>();
        pending.Push((rootPath, false));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (currentDirectory, expanded) = pending.Pop();

            if (expanded)
            {
                if (!string.Equals(currentDirectory, rootPath, StringComparison.OrdinalIgnoreCase))
                {
                    yield return currentDirectory;
                }

                continue;
            }

            pending.Push((currentDirectory, true));
            foreach (var directory in SafeEnumerateDirectories(currentDirectory, result, "ERROR"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsReparsePoint(directory))
                {
                    result.ItemsIgnored++;
                    Logger.Log($"Diretório reparse point ignorado durante remoção: '{directory}'", "WARNING");
                    continue;
                }

                pending.Push((directory, false));
            }
        }
    }


    private static IEnumerable<string> SafeEnumerateFiles(string path, CleanupResult result, string logLevel)
    {
        return SafeEnumerate(path, result, logLevel, Directory.EnumerateFiles, "arquivos");
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path, CleanupResult result, string logLevel)
    {
        return SafeEnumerate(path, result, logLevel, Directory.EnumerateDirectories, "diretórios");
    }

    private static IEnumerable<string> SafeEnumerate(string path, CleanupResult result, string logLevel, Func<string, IEnumerable<string>> enumerate, string itemLabel)
    {
        IEnumerator<string>? enumerator = null;
        try
        {
            enumerator = enumerate(path).GetEnumerator();
        }
        catch (Exception ex)
        {
            result.Failures++;
            Logger.Log($"Erro ao enumerar {itemLabel} em '{path}': {ex.Message}", logLevel);
            yield break;
        }

        using (enumerator)
        {
            while (true)
            {
                string current;
                try
                {
                    if (!enumerator.MoveNext())
                    {
                        yield break;
                    }

                    current = enumerator.Current;
                }
                catch (Exception ex)
                {
                    result.Failures++;
                    Logger.Log($"Erro ao enumerar {itemLabel} em '{path}': {ex.Message}", logLevel);
                    yield break;
                }

                yield return current;
            }
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch
        {
            return true;
        }
    }

    private static void CleanupBrowserCache(CleanupResult result, CancellationToken cancellationToken)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        CleanupChromiumBrowser(Path.Combine(localAppData, "Google", "Chrome", "User Data"), result, ["chrome"], cancellationToken);
        CleanupChromiumBrowser(Path.Combine(localAppData, "Microsoft", "Edge", "User Data"), result, ["msedge"], cancellationToken);
        CleanupChromiumBrowser(Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data"), result, ["brave"], cancellationToken);
        CleanupChromiumBrowser(Path.Combine(localAppData, "Opera Software", "Opera Stable"), result, ["opera"], cancellationToken);

        string firefoxPath = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
        if (IsAnyProcessRunning(["firefox"]))
        {
            result.ItemsIgnored++;
            Logger.Log("Cache do Firefox ignorado porque o navegador está em execução.", "WARNING");
            return;
        }

        if (!Directory.Exists(firefoxPath))
        {
            return;
        }

        try
        {
            foreach (var dir in SafeEnumerateDirectories(firefoxPath, result, "ERROR"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string cachePath = Path.Combine(dir, "cache2", "entries");
                CleanupDirectory(cachePath, result, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Failures++;
            Logger.Log($"Falha ao limpar cache do Firefox: {ex.Message}", "ERROR");
        }
    }

    private static void CleanupChromiumBrowser(string userDataPath, CleanupResult result, string[] processNames, CancellationToken cancellationToken = default)
    {
        if (IsAnyProcessRunning(processNames))
        {
            result.ItemsIgnored++;
            Logger.Log($"Cache Chromium ignorado porque o processo está ativo: {string.Join(", ", processNames)}.", "WARNING");
            return;
        }

        if (!Directory.Exists(userDataPath))
        {
            return;
        }

        string[] cacheRelativePaths =
        [
            Path.Combine("Cache", "Cache_Data"),
            "Code Cache",
            "GPUCache",
            "Service Worker",
            "ShaderCache"
        ];

        try
        {
            foreach (var dir in SafeEnumerateDirectories(userDataPath, result, "ERROR"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(Path.Combine(dir, "Preferences")) || dir.EndsWith("Default") || dir.Contains("Profile"))
                {
                    foreach (var relativePath in cacheRelativePaths)
                    {
                        string cachePath = Path.Combine(dir, relativePath);
                        CleanupDirectory(cachePath, result, cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Failures++;
            Logger.Log($"Falha ao limpar cache Chromium em '{userDataPath}': {ex.Message}", "ERROR");
        }
    }

    private static bool IsAnyProcessRunning(string[] processNames)
    {
        foreach (var processName in processNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                try
                {
                    if (processes.Length > 0)
                    {
                        return true;
                    }
                }
                finally
                {
                    foreach (var process in processes)
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Falha ao verificar processo '{processName}': {ex.Message}", "WARNING");
                return true;
            }
        }

        return false;
    }

    private static async Task CleanupWindowsUpdateAsync(string wuPath, CleanupResult result, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(wuPath))
        {
            return;
        }

        string[] services = ["wuauserv", "bits", "cryptsvc"];
        var serviceStates = CaptureServiceStates(services);
        var shouldRestoreServices = serviceStates.Count > 0;

        try
        {
            bool stopped = await ToggleServicesAsync(services, start: false, cancellationToken);
            if (!stopped)
            {
                result.Failures++;
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            CleanupDirectory(wuPath, result, cancellationToken);
        }
        finally
        {
            if (shouldRestoreServices)
            {
                bool started = await RestoreServicesAsync(serviceStates);
                if (!started)
                {
                    result.Failures++;
                }
            }
        }
    }


    private static Dictionary<string, ServiceControllerStatus> CaptureServiceStates(string[] services)
    {
        var states = new Dictionary<string, ServiceControllerStatus>(StringComparer.OrdinalIgnoreCase);
        foreach (var serviceName in services)
        {
            try
            {
                using var sc = new ServiceController(serviceName);
                sc.Refresh();
                states[serviceName] = sc.Status;
            }
            catch (Exception ex)
            {
                Logger.Log($"Falha ao capturar estado do serviço '{serviceName}': {ex.Message}", "WARNING");
            }
        }

        return states;
    }

    private static async Task<bool> RestoreServicesAsync(IReadOnlyDictionary<string, ServiceControllerStatus> originalStates)
    {
        return await Task.Run(() =>
        {
            var restored = true;
            foreach (var (serviceName, originalStatus) in originalStates)
            {
                try
                {
                    if (originalStatus is not (ServiceControllerStatus.Running or ServiceControllerStatus.StartPending))
                    {
                        continue;
                    }

                    using var sc = new ServiceController(serviceName);
                    sc.Refresh();
                    if (sc.Status != ServiceControllerStatus.Running && sc.Status != ServiceControllerStatus.StartPending)
                    {
                        sc.Start();
                    }

                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                }
                catch (Exception ex)
                {
                    restored = false;
                    Logger.Log($"Falha ao restaurar serviço '{serviceName}' após limpeza: {ex.Message}", "ERROR");
                }
            }

            return restored;
        });
    }

    private static async Task<bool> ToggleServicesAsync(string[] services, bool start, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                foreach (var s in services)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var sc = new ServiceController(s);
                    sc.Refresh();
                    if (start)
                    {
                        if (sc.Status != ServiceControllerStatus.Running && sc.Status != ServiceControllerStatus.StartPending)
                        {
                            sc.Start();
                        }
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    }
                    else
                    {
                        if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                        {
                            sc.Stop();
                        }
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                    }
                }
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Log($"Falha ao {(start ? "iniciar" : "parar")} serviços do Windows Update: {ex.Message}", "ERROR");
                return false;
            }
        });
    }
}
