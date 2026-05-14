using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;
using SystemOptimizer.Helpers;

namespace SystemOptimizer.Services;

public sealed class UpdateService : IUpdateService, IDisposable
{
    private readonly HttpClient _httpClient;
    private const string RepoOwner = "johnwiliam";
    private const string RepoName = "otimizador-windows";

    public UpdateService()
    {
        _httpClient = new HttpClient();
        // GitHub API exige User-Agent
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("OtimizadorWindows-Updater");
    }

    public async Task<UpdateInfo> CheckForUpdatesAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(url);

            if (release == null) return new UpdateInfo(false, null, null, null);

            var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            
            // Remove 'v' se existir (ex: v2.1.2 -> 2.1.2)
            string cleanTag = release.tag_name.TrimStart('v');
            
            if (Version.TryParse(cleanTag, out var latestVersion) && currentVersion != null)
            {
                if (latestVersion > currentVersion)
                {
                    // Procura o pacote MSIX nativo do WinUI 3.
                    var asset = release.assets.FirstOrDefault(a => a.name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase));
                    if (asset != null)
                    {
                        return new UpdateInfo(true, release.tag_name, release.body, asset.browser_download_url);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao verificar atualizações: {ex.Message}", "ERROR");
        }

        return new UpdateInfo(false, null, null, null);
    }

    public async Task DownloadAndInstallAsync(string downloadUrl, IProgress<double> progress)
    {
        string packagePath = Path.Combine(Path.GetTempPath(), $"SystemOptimizer-{Guid.NewGuid():N}.msix");

        try
        {
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            await using (var stream = await response.Content.ReadAsStreamAsync())
            await using (var fileStream = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                var totalRead = 0L;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        progress?.Report((double)totalRead / totalBytes * 100);
                    }
                }
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = packagePath,
                UseShellExecute = true
            };

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro na instalação da atualização MSIX: {ex.Message}", "ERROR");
            if (File.Exists(packagePath)) File.Delete(packagePath);
            throw;
        }
    }

    // Classes auxiliares para o JSON do GitHub
    public void Dispose() => _httpClient.Dispose();

    private record GitHubRelease(string tag_name, string body, List<GitHubAsset> assets);
    private record GitHubAsset(string browser_download_url, string name);
}
