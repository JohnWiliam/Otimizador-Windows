using System.Collections.Generic;

namespace SystemOptimizer.Services;

public sealed class DnsCleanupTargetProvider : ICleanupTargetProvider
{
    public string CategoryKey => "dns";

    public IEnumerable<CleanupTarget> GetTargets()
    {
        yield return new CleanupTarget
        {
            CategoryName = "DNS",
            Path = "dns://cache",
            Type = CleanupTargetType.Command,
            Strategy = CleanupExecutionStrategy.ExecuteCommand,
            Command = "ipconfig",
            Arguments = "/flushdns"
        };
    }
}
