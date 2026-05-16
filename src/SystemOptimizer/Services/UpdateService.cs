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

public sealed class UpdateService : IUpdateService, IDisposable
{
    private readonly HttpClient _httpClient;
    private const string RepoOwner = "johnwiliam";
    private const string RepoName = "otimizador-windows";
    private static readonly Uri LatestReleaseUri = new($"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest");
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
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(LatestReleaseUri);

            if (release == null) return new UpdateInfo(false, null, null, null);

            var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            
            // Remove 'v' se existir (ex: v2.1.2 -> 2.1.2)
            string cleanTag = release.tag_name.TrimStart('v');
            
            if (Version.TryParse(cleanTag, out var latestVersion) && currentVersion != null)
            {
                if (latestVersion > currentVersion)
                {
                    // Procura o asset .exe e aceita somente URLs do próprio GitHub.
                    var asset = release.assets.FirstOrDefault(a =>
                        a.name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        && IsTrustedDownloadUrl(a.browser_download_url));
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
        if (!IsTrustedDownloadUrl(downloadUrl))
        {
            throw new InvalidOperationException("URL de atualização não confiável.");
        }

        string tempDirectory = Path.Combine(Path.GetTempPath(), "SystemOptimizer", "updates");
        Directory.CreateDirectory(tempDirectory);
        string tempFilePath = Path.Combine(tempDirectory, $"update-{Guid.NewGuid():N}.exe");
        string? currentExe = null;
        string? backupExe = null;

        try
        {
            // 1. Download com progresso
            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                if (!IsTrustedDownloadUrl(response.RequestMessage?.RequestUri?.ToString()))
                {
                    throw new InvalidOperationException("Redirecionamento de atualização para host não confiável.");
                }

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                
                await using (var stream = await response.Content.ReadAsStreamAsync())
                await using (var fileStream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    var totalRead = 0L;
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            progress?.Report((double)totalRead / totalBytes * 100);
                        }
                    }
                }
            }

            ValidateDownloadedExecutable(tempFilePath);

            // 2. Substituição do Arquivo (Self-Update)
            using var currentProcess = Process.GetCurrentProcess();
            currentExe = currentProcess.MainModule?.FileName;

            if (string.IsNullOrEmpty(currentExe)) throw new Exception("Não foi possível localizar o executável atual.");
            if (!Path.GetExtension(currentExe).Equals(".exe", StringComparison.OrdinalIgnoreCase)) throw new Exception("Executável atual inválido para atualização.");

            backupExe = currentExe + ".old";
            if (File.Exists(backupExe))
            {
                File.Delete(backupExe);
            }

            File.Move(currentExe, backupExe);
            File.Move(tempFilePath, currentExe);

            // 3. Reinicia a aplicação
            Process.Start(new ProcessStartInfo(currentExe) { UseShellExecute = false });
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro na instalação da atualização: {ex.Message}", "ERROR");

            TryRestoreBackup(currentExe, backupExe);
            TryDeleteFile(tempFilePath);
            throw;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static bool IsTrustedDownloadUrl(string? downloadUrl)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps && AllowedDownloadHosts.Contains(uri.Host);
    }

    private static void ValidateDownloadedExecutable(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> mz = stackalloc byte[2];
        if (stream.Read(mz) != 2 || mz[0] != (byte)'M' || mz[1] != (byte)'Z')
        {
            throw new InvalidDataException("Arquivo de atualização não parece ser um executável Windows válido.");
        }

        using var sha256 = SHA256.Create();
        stream.Position = 0;
        string hash = Convert.ToHexString(sha256.ComputeHash(stream));
        Logger.Log($"Update payload SHA256={hash}", "UPDATE");
    }

    private static void TryRestoreBackup(string? currentExe, string? backupExe)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(currentExe) || string.IsNullOrWhiteSpace(backupExe) || !File.Exists(backupExe))
            {
                return;
            }

            if (File.Exists(currentExe))
            {
                File.Delete(currentExe);
            }

            File.Move(backupExe, currentExe);
        }
        catch (Exception restoreEx)
        {
            Logger.Log($"Falha ao restaurar backup após update: {restoreEx.Message}", "ERROR");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Ignora limpeza de temporário bloqueado.
        }
    }

    // Classes auxiliares para o JSON do GitHub
    private record GitHubRelease(string tag_name, string body, List<GitHubAsset> assets);
    private record GitHubAsset(string browser_download_url, string name);
}
