using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace SystemOptimizer.Services;

public sealed class ApplicationHostService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
