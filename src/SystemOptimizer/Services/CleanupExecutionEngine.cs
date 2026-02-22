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
    [DllImport("shell32.dll")]
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
                ExecuteCommand(target, result);
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

    private static void ExecuteCommand(CleanupTarget target, CleanupResult result)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(target.Command))
            {
                result.Failures++;
                return;
            }

            CommandHelper.RunCommand(target.Command, target.Arguments ?? string.Empty);
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
            SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
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
        if (!Directory.Exists(path))
        {
            return;
        }

        var dirInfo = new DirectoryInfo(path);

        try
        {
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    long size = file.Length;
                    file.Delete();
                    if (!file.Exists)
                    {
                        result.BytesRemoved += size;
                        result.ItemsRemoved++;
                    }
                }
                catch (Exception ex)
                {
                    result.ItemsIgnored++;
                    result.Failures++;
                    Logger.Log($"Falha ao remover arquivo '{file.FullName}': {ex.Message}", "WARNING");
                }
            }
        }
        catch (Exception ex)
        {
            result.Failures++;
            Logger.Log($"Erro ao enumerar arquivos em '{path}': {ex.Message}", "ERROR");
        }

        try
        {
            var directories = dirInfo
                .EnumerateDirectories("*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.FullName.Length)
                .ToList();

            foreach (var dir in directories)
            {
                try
                {
                    dir.Delete(true);
                    result.ItemsRemoved++;
                }
                catch (Exception ex)
                {
                    result.ItemsIgnored++;
                    result.Failures++;
                    Logger.Log($"Falha ao remover diretório '{dir.FullName}': {ex.Message}", "WARNING");
                }
            }
        }
        catch (Exception ex)
        {
            result.Failures++;
            Logger.Log($"Erro ao enumerar diretórios em '{path}': {ex.Message}", "ERROR");
        }
    }

    private static void CleanupBrowserCache(CleanupResult result)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        CleanupChromiumBrowser(Path.Combine(localAppData, "Google", "Chrome", "User Data"), result);
        CleanupChromiumBrowser(Path.Combine(localAppData, "Microsoft", "Edge", "User Data"), result);
        CleanupChromiumBrowser(Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data"), result);
        CleanupChromiumBrowser(Path.Combine(localAppData, "Opera Software", "Opera Stable"), result);

        string firefoxPath = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
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

    private static void CleanupChromiumBrowser(string userDataPath, CleanupResult result)
    {
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
                    if (start)
                    {
                        if (sc.Status != ServiceControllerStatus.Running)
                        {
                            sc.Start();
                            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                        }
                    }
                    else
                    {
                        if (sc.Status != ServiceControllerStatus.Stopped)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        }
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
