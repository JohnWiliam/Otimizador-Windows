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
    private bool _disposed;
    private string? _latestTrustedDownloadUrl;
    private const string RepoOwner = "johnwiliam";
    private const string RepoName = "otimizador-windows";
    private static readonly Uri GitHubApiLatestReleaseUri = new($"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest");
    private static readonly HashSet<string> AllowedDownloadHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com"
    };

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
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(GitHubApiLatestReleaseUri);

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
                    if (asset != null && IsTrustedDownloadUrl(asset.browser_download_url))
                    {
                        _latestTrustedDownloadUrl = asset.browser_download_url;
                        return new UpdateInfo(true, release.tag_name, release.body, asset.browser_download_url);
                    }

                    Logger.Log("Atualização ignorada: asset ausente ou URL de download não confiável.", "WARNING");
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
        if (!IsTrustedDownloadUrl(downloadUrl)
            || !string.Equals(downloadUrl, _latestTrustedDownloadUrl, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("URL de atualização não confiável ou não validada pela versão mais recente do GitHub.");
        }

        string tempDirectory = Path.Combine(Path.GetTempPath(), "SystemOptimizer", "Updates");
        Directory.CreateDirectory(tempDirectory);
        string newExePath = Path.Combine(tempDirectory, $"SystemOptimizer-{Guid.NewGuid():N}.exe");

        try
        {
            // 1. Download com progresso
            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                await using (var stream = await response.Content.ReadAsStreamAsync())
                await using (var fileStream = new FileStream(newExePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[8192];
                    var totalRead = 0L;
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalRead += bytesRead;

                        if (totalBytes != -1)
                        {
                            progress?.Report((double)totalRead / totalBytes * 100);
                        }
                    }
                }
            }

            EnsurePortableExecutable(newExePath);

            // 2. Substituição do Arquivo (Self-Update)
            using var currentProcess = Process.GetCurrentProcess();
            var currentExe = currentProcess.MainModule?.FileName;

            if (string.IsNullOrEmpty(currentExe)) throw new Exception("Não foi possível localizar o executável atual.");

            var updaterScriptPath = CreateUpdaterScript(currentExe, newExePath, currentProcess.Id);
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                ArgumentList = { "/c", updaterScriptPath },
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(currentExe) ?? Environment.CurrentDirectory
            });

            currentProcess.CloseMainWindow();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro na instalação da atualização: {ex.Message}", "ERROR");

            // Limpeza em caso de erro
            TryDelete(newExePath);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
    }

    private static bool IsTrustedDownloadUrl(string? downloadUrl)
    {
        return Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && AllowedDownloadHosts.Contains(uri.Host)
            && uri.AbsolutePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsurePortableExecutable(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        Span<byte> mzHeader = stackalloc byte[2];
        if (stream.Read(mzHeader) != 2 || mzHeader[0] != 'M' || mzHeader[1] != 'Z')
        {
            throw new InvalidDataException("O arquivo de atualização baixado não é um executável Windows válido.");
        }
    }

    private static string CreateUpdaterScript(string currentExe, string newExePath, int currentProcessId)
    {
        string scriptPath = Path.Combine(Path.GetTempPath(), "SystemOptimizer", "Updates", $"apply-update-{Guid.NewGuid():N}.cmd");
        string oldExe = currentExe + ".old";
        string script = $"""
@echo off
setlocal
set "CURRENT_EXE={currentExe}"
set "NEW_EXE={newExePath}"
set "OLD_EXE={oldExe}"
timeout /t 2 /nobreak >nul
:wait_process
tasklist /fi "PID eq {currentProcessId}" | find "{currentProcessId}" >nul
if not errorlevel 1 (
  timeout /t 1 /nobreak >nul
  goto wait_process
)
if exist "%OLD_EXE%" del /f /q "%OLD_EXE%"
move /y "%CURRENT_EXE%" "%OLD_EXE%"
move /y "%NEW_EXE%" "%CURRENT_EXE%"
start "" "%CURRENT_EXE%"
del /f /q "%~f0"
""";
        File.WriteAllText(scriptPath, script);
        return scriptPath;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Ignora limpeza de arquivo temporário bloqueado.
        }
    }

    // Classes auxiliares para o JSON do GitHub
    private record GitHubRelease(string tag_name, string body, List<GitHubAsset> assets);
    private record GitHubAsset(string browser_download_url, string name);
}
