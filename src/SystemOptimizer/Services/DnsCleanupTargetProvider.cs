using System.Collections.Generic;

namespace SystemOptimizer.Services;

public sealed class DnsCleanupTargetProvider : ICleanupTargetProvider
{
    public IEnumerable<CleanupTarget> GetTargets()
    {
        yield return new CleanupTarget
        {
            CategoryName = "DNS",
            Path = "dns://cache",
            Type = CleanupTargetType.Command,
            Strategy = CleanupExecutionStrategy.ExecuteCommand,
            Command = "powershell",
            Arguments = "Clear-DnsClientCache"
        };
    }
}
