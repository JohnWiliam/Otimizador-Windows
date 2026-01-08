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

public class UpdateService : IUpdateService
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
                    // Procura o asset .exe
                    var asset = release.assets.FirstOrDefault(a => a.name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
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
        string tempFilePath = Path.GetTempFileName();
        string newExePath = tempFilePath + ".exe";

        try
        {
            // 1. Download com progresso
            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(newExePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[8192];
                    var totalRead = 0L;
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (totalBytes != -1)
                        {
                            progress?.Report((double)totalRead / totalBytes * 100);
                        }
                    }
                }
            }

            // 2. Substituição do Arquivo (Self-Update)
            var currentProcess = Process.GetCurrentProcess();
            var currentExe = currentProcess.MainModule?.FileName;

            if (string.IsNullOrEmpty(currentExe)) throw new Exception("Não foi possível localizar o executável atual.");

            // Nome do backup
            var oldExe = currentExe + ".old";

            // Se já existir um .old de uma atualização anterior, tenta deletar
            if (File.Exists(oldExe))
            {
                try { File.Delete(oldExe); } catch { /* Ignora se estiver bloqueado */ }
            }

            // Renomeia o atual para .old (Windows permite renomear executável em uso)
            File.Move(currentExe, oldExe);

            // Move o novo baixado para o local do original
            File.Move(newExePath, currentExe);

            // 3. Reinicia a aplicação
            Process.Start(currentExe);
            
            // Fecha a atual
            currentProcess.Kill();
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro na instalação da atualização: {ex.Message}", "ERROR");
            
            // Limpeza em caso de erro
            if (File.Exists(newExePath)) File.Delete(newExePath);
            throw;
        }
    }

    // Classes auxiliares para o JSON do GitHub
    private record GitHubRelease(string tag_name, string body, List<GitHubAsset> assets);
    private record GitHubAsset(string browser_download_url, string name);
}
