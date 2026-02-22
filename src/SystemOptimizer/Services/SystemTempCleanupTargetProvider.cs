using System;
using System.Collections.Generic;
using System.IO;
using SystemOptimizer.Properties;

namespace SystemOptimizer.Services;

public sealed class SystemTempCleanupTargetProvider : ICleanupTargetProvider
{
    public IEnumerable<CleanupTarget> GetTargets()
    {
        yield return new CleanupTarget
        {
            CategoryName = Resources.Label_SystemTemp ?? "System Temp",
            Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
            Type = CleanupTargetType.Folder,
            Strategy = CleanupExecutionStrategy.DeleteDirectoryContents
        };

        yield return new CleanupTarget
        {
            CategoryName = "Windows Logs",
            Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs"),
            Type = CleanupTargetType.Folder,
            Strategy = CleanupExecutionStrategy.DeleteDirectoryContents
        };
    }
}
