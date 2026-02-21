using System;
using System.Collections.Generic;
using System.IO;

namespace SystemOptimizer.Services;

public sealed class WindowsUpdateCleanupTargetProvider : ICleanupTargetProvider
{
    public IEnumerable<CleanupTarget> GetTargets()
    {
        yield return new CleanupTarget
        {
            CategoryName = "Windows Update",
            Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download"),
            Type = CleanupTargetType.Folder,
            Strategy = CleanupExecutionStrategy.CleanupWindowsUpdate
        };
    }
}
