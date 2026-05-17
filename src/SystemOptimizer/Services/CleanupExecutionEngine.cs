using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;
using SystemOptimizer.Helpers;

namespace SystemOptimizer.Services;

public class CleanupExecutionEngine
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint dwFlags);

    const uint SHERB_NOCONFIRMATION = 0x00000001;
    const uint SHERB_NOPROGRESSUI = 0x00000002;
    const uint SHERB_NOSOUND = 0x00000004;

    public async Task<CleanupResult> ExecuteAsync(CleanupTarget target)
    {
        var sw = Stopwatch.StartNew();
        var result = new CleanupResult { CategoryName = target.CategoryName };

        switch (target.Strategy)
        {
            case CleanupExecutionStrategy.DeleteDirectoryContents:
                CleanupDirectory(target.Path, result);
                break;
            case CleanupExecutionStrategy.ExecuteCommand:
                await ExecuteCommandAsync(target, result);
                break;
            case CleanupExecutionStrategy.EmptyRecycleBin:
                EmptyRecycleBin(result);
                break;
            case CleanupExecutionStrategy.CleanupWindowsUpdate:
                await CleanupWindowsUpdateAsync(target.Path, result);
                break;
            case CleanupExecutionStrategy.CleanupBrowserCache:
                CleanupBrowserCache(result);
                break;
            default:
                result.Failures++;
                break;
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    private static async Task ExecuteCommandAsync(CleanupTarget target, CleanupResult result)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(target.Command))
            {
                result.Failures++;
                return;
            }

            var commandResult = await CommandHelper.RunCommandDetailedAsync(target.Command, target.Arguments ?? string.Empty);
            if (!commandResult.IsSuccess)
            {
                result.Failures++;
                Logger.Log($"Comando de limpeza '{target.CategoryName}' falhou. ExitCode={commandResult.ExitCode}, StdErr='{commandResult.StdErr}'", "ERROR");
                return;
            }

            result.ItemsRemoved = 1;
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
                Logger.Log($"Falha ao esvaziar lixeira. HRESULT=0x{hr:X8}, Win32={Marshal.GetLastWin32Error()}", "ERROR");
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

    private static void CleanupDirectory(string path, CleanupResult result)
    {
        if (!Directory.Exists(path) || IsReparsePoint(path))
        {
            return;
        }

        foreach (var filePath in EnumerateFilesWithoutFollowingReparsePoints(path, result))
        {
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

        foreach (var directoryPath in EnumerateDirectoriesWithoutFollowingReparsePoints(path, result).OrderByDescending(d => d.Length))
        {
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

    private static IEnumerable<string> EnumerateFilesWithoutFollowingReparsePoints(string rootPath, CleanupResult result)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            string currentDirectory = pending.Pop();
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(currentDirectory).ToList();
            }
            catch (Exception ex)
            {
                result.Failures++;
                Logger.Log($"Erro ao enumerar arquivos em '{currentDirectory}': {ex.Message}", "ERROR");
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(currentDirectory).ToList();
            }
            catch (Exception ex)
            {
                result.Failures++;
                Logger.Log($"Erro ao enumerar diretórios em '{currentDirectory}': {ex.Message}", "ERROR");
                continue;
            }

            foreach (var directory in directories)
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

    private static IEnumerable<string> EnumerateDirectoriesWithoutFollowingReparsePoints(string rootPath, CleanupResult result)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            string currentDirectory = pending.Pop();
            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(currentDirectory).ToList();
            }
            catch (Exception ex)
            {
                result.Failures++;
                Logger.Log($"Erro ao enumerar diretórios em '{currentDirectory}': {ex.Message}", "ERROR");
                continue;
            }

            foreach (var directory in directories)
            {
                if (IsReparsePoint(directory))
                {
                    result.ItemsIgnored++;
                    Logger.Log($"Diretório reparse point ignorado durante remoção: '{directory}'", "WARNING");
                    continue;
                }

                yield return directory;
                pending.Push(directory);
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

    private static void CleanupBrowserCache(CleanupResult result)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        CleanupChromiumBrowser(Path.Combine(localAppData, "Google", "Chrome", "User Data"), result, ["chrome"]);
        CleanupChromiumBrowser(Path.Combine(localAppData, "Microsoft", "Edge", "User Data"), result, ["msedge"]);
        CleanupChromiumBrowser(Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data"), result, ["brave"]);
        CleanupChromiumBrowser(Path.Combine(localAppData, "Opera Software", "Opera Stable"), result, ["opera"]);

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
            foreach (var dir in Directory.GetDirectories(firefoxPath))
            {
                string cachePath = Path.Combine(dir, "cache2", "entries");
                CleanupDirectory(cachePath, result);
            }
        }
        catch (Exception ex)
        {
            result.Failures++;
            Logger.Log($"Falha ao limpar cache do Firefox: {ex.Message}", "ERROR");
        }
    }

    private static void CleanupChromiumBrowser(string userDataPath, CleanupResult result, string[] processNames)
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
            foreach (var dir in Directory.GetDirectories(userDataPath))
            {
                if (File.Exists(Path.Combine(dir, "Preferences")) || dir.EndsWith("Default") || dir.Contains("Profile"))
                {
                    foreach (var relativePath in cacheRelativePaths)
                    {
                        string cachePath = Path.Combine(dir, relativePath);
                        CleanupDirectory(cachePath, result);
                    }
                }
            }
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

    private static async Task CleanupWindowsUpdateAsync(string wuPath, CleanupResult result)
    {
        if (!Directory.Exists(wuPath))
        {
            return;
        }

        string[] services = ["wuauserv", "bits", "cryptsvc"];
        bool stopped = await ToggleServicesAsync(services, false);
        if (!stopped)
        {
            result.Failures++;
            return;
        }

        try
        {
            CleanupDirectory(wuPath, result);
        }
        finally
        {
            bool started = await ToggleServicesAsync(services, true);
            if (!started)
            {
                result.Failures++;
            }
        }
    }

    private static async Task<bool> ToggleServicesAsync(string[] services, bool start)
    {
        return await Task.Run(() =>
        {
            try
            {
                foreach (var s in services)
                {
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
            catch (Exception ex)
            {
                Logger.Log($"Falha ao {(start ? "iniciar" : "parar")} serviços do Windows Update: {ex.Message}", "ERROR");
                return false;
            }
        });
    }
}
