using System;
using System.Collections.Generic;
using SystemOptimizer.Properties;

namespace SystemOptimizer.Services;

public sealed class BrowserCacheCleanupTargetProvider : ICleanupTargetProvider
{
    public IEnumerable<CleanupTarget> GetTargets()
    {
        yield return new CleanupTarget
        {
            CategoryName = Resources.Label_BrowserCache ?? "Browser Cache",
            Path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Type = CleanupTargetType.Folder,
            Strategy = CleanupExecutionStrategy.CleanupBrowserCache
        };
    }
}
