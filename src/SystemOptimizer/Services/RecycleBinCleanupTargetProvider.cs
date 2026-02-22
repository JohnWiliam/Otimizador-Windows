using System.Collections.Generic;

namespace SystemOptimizer.Services;

public sealed class RecycleBinCleanupTargetProvider : ICleanupTargetProvider
{
    public IEnumerable<CleanupTarget> GetTargets()
    {
        yield return new CleanupTarget
        {
            CategoryName = "Recycle Bin",
            Path = "shell:RecycleBin",
            Type = CleanupTargetType.Command,
            Strategy = CleanupExecutionStrategy.EmptyRecycleBin
        };
    }
}
