using System;
using System.Collections.Generic;
using System.IO;

namespace SystemOptimizer.Services;

public sealed class PrefetchCleanupTargetProvider : ICleanupTargetProvider
{
    public string CategoryKey => "prefetch";

    public IEnumerable<CleanupTarget> GetTargets()
    {
        yield return new CleanupTarget
        {
            CategoryName = "Prefetch",
            Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch"),
            Type = CleanupTargetType.Folder,
            Strategy = CleanupExecutionStrategy.DeleteDirectoryContents
        };
    }
}
