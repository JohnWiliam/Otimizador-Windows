using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SystemOptimizer.Helpers;

namespace SystemOptimizer.Services;

public class UpdateService : IUpdateService, IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;
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
                    if (asset != null && IsTrustedGitHubReleaseUrl(asset.browser_download_url))
                    {
                        return new UpdateInfo(true, release.tag_name, release.body, asset.browser_download_url);
                    }

                    Logger.Log("Atualização ignorada: nenhum asset .exe confiável foi encontrado no release.", "WARNING");
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
        if (!IsTrustedGitHubReleaseUrl(downloadUrl))
        {
            throw new InvalidOperationException("URL de atualização não pertence aos hosts esperados do GitHub.");
        }

        string updateWorkDir = Path.Combine(Path.GetTempPath(), "OtimizadorWindows", "Updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(updateWorkDir);
        string newExePath = Path.Combine(updateWorkDir, "SystemOptimizer.update.exe");

        try
        {
            // 1. Download com progresso
            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                
                if (!IsTrustedGitHubReleaseUrl(response.RequestMessage?.RequestUri?.ToString() ?? downloadUrl))
                {
                    throw new InvalidOperationException("Redirecionamento de atualização para host não confiável.");
                }

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(newExePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
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

            ValidateDownloadedExecutable(newExePath);

            // 2. Substituição do arquivo (self-update) via script temporário.
            // O executável em execução não é renomeado diretamente; isso evita deixar o app
            // em estado quebrado se a cópia falhar no meio do processo.
            var currentProcess = Process.GetCurrentProcess();
            var currentExe = currentProcess.MainModule?.FileName;

            if (string.IsNullOrWhiteSpace(currentExe)) throw new Exception("Não foi possível localizar o executável atual.");

            string updaterScript = CreateUpdaterScript(updateWorkDir, currentExe, newExePath, currentProcess.Id);
            using var updaterProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start \"\" /min \"{updaterScript}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = updateWorkDir
            });

            // Fecha a aplicação atual para liberar o arquivo para o helper de atualização.
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro na instalação da atualização: {ex.Message}", "ERROR");
            
            // Limpeza em caso de erro
            TryDeleteDirectory(updateWorkDir);
            throw;
        }
    }

    private static bool IsTrustedGitHubReleaseUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsolutePath.StartsWith($"/{RepoOwner}/{RepoName}/releases/download/", StringComparison.OrdinalIgnoreCase);
        }

        return uri.Host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("github-releases.githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateDownloadedExecutable(string filePath)
    {
        if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
        {
            throw new InvalidOperationException("Arquivo de atualização ausente ou vazio.");
        }

        using var stream = File.OpenRead(filePath);
        Span<byte> mzHeader = stackalloc byte[2];
        if (stream.Read(mzHeader) != 2 || mzHeader[0] != (byte)'M' || mzHeader[1] != (byte)'Z')
        {
            throw new InvalidOperationException("Arquivo de atualização não é um executável PE válido.");
        }

        stream.Position = 0;
        string sha256 = Convert.ToHexString(SHA256.HashData(stream));
        Logger.Log($"Atualização baixada e validada. SHA256={sha256}", "UPDATE");
    }

    private static string CreateUpdaterScript(string updateWorkDir, string currentExe, string newExePath, int processId)
    {
        string scriptPath = Path.Combine(updateWorkDir, "apply-update.cmd");
        string backupPath = currentExe + ".old";
        string script = $"""
@echo off
setlocal
set "CURRENT={currentExe}"
set "NEW={newExePath}"
set "BACKUP={backupPath}"
set "WORKDIR={updateWorkDir}"
for /l %%i in (1,1,60) do (
    tasklist /fi "PID eq {processId}" | find "{processId}" >nul || goto apply
    timeout /t 1 /nobreak >nul
)
exit /b 1
:apply
if exist "%BACKUP%" del /f /q "%BACKUP%" >nul 2>nul
if exist "%CURRENT%" move /y "%CURRENT%" "%BACKUP%" >nul || exit /b 1
move /y "%NEW%" "%CURRENT%" >nul || (
    if exist "%BACKUP%" move /y "%BACKUP%" "%CURRENT%" >nul
    exit /b 1
)
start "" "%CURRENT%"
timeout /t 2 /nobreak >nul
rmdir /s /q "%WORKDIR%" >nul 2>nul
""";
        File.WriteAllText(scriptPath, script);
        return scriptPath;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            Logger.Log($"Falha ao limpar diretório temporário de atualização '{path}': {ex.Message}", "WARNING");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    // Classes auxiliares para o JSON do GitHub
    private record GitHubRelease(string tag_name, string body, List<GitHubAsset> assets);
    private record GitHubAsset(string browser_download_url, string name);
}
