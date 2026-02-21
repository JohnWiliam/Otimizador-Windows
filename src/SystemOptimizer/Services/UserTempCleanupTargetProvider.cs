using System;
using System.Collections.Generic;
using System.IO;
using SystemOptimizer.Properties;

namespace SystemOptimizer.Services;

public sealed class UserTempCleanupTargetProvider : ICleanupTargetProvider
{
    public IEnumerable<CleanupTarget> GetTargets()
    {
        yield return new CleanupTarget
        {
            CategoryName = Resources.Label_TempFiles ?? "User Temp",
            Path = Path.GetTempPath(),
            Type = CleanupTargetType.Folder,
            Strategy = CleanupExecutionStrategy.DeleteDirectoryContents
        };

        yield return new CleanupTarget
        {
            CategoryName = "WER Reports",
            Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER"),
            Type = CleanupTargetType.Folder,
            Strategy = CleanupExecutionStrategy.DeleteDirectoryContents
        };

        yield return new CleanupTarget
        {
            CategoryName = "CrashDumps",
            Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"),
            Type = CleanupTargetType.Folder,
            Strategy = CleanupExecutionStrategy.DeleteDirectoryContents
        };

        yield return new CleanupTarget
        {
            CategoryName = "Shader Cache (NVIDIA)",
            Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "DXCache"),
            Type = CleanupTargetType.Folder,
            Strategy = CleanupExecutionStrategy.DeleteDirectoryContents
        };

        yield return new CleanupTarget
        {
            CategoryName = "Shader Cache (DirectX)",
            Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"),
            Type = CleanupTargetType.Folder,
            Strategy = CleanupExecutionStrategy.DeleteDirectoryContents
        };

        yield return new CleanupTarget
        {
            CategoryName = "Shader Cache (AMD)",
            Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AMD", "DxCache"),
            Type = CleanupTargetType.Folder,
            Strategy = CleanupExecutionStrategy.DeleteDirectoryContents
        };
    }
}
