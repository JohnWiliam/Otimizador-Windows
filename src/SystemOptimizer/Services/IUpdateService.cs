using System;
using System.Threading;
using System.Threading.Tasks;

namespace SystemOptimizer.Services;

// Propriedades de string agora são anuláveis (string?)
public record UpdateInfo(bool IsAvailable, string? Version, string? ReleaseNotes, string? DownloadUrl);

public interface IUpdateService
{
    Task<UpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
    Task DownloadAndInstallAsync(string downloadUrl, IProgress<double> progress);
}
