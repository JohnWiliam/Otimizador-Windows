using System;

namespace SystemOptimizer.Services;

public enum CleanupTargetType
{
    File,
    Folder,
    Command
}

public enum CleanupExecutionStrategy
{
    DeleteDirectoryContents,
    ExecuteCommand,
    EmptyRecycleBin,
    CleanupWindowsUpdate,
    CleanupBrowserCache
}

public sealed class CleanupTarget
{
    public required string CategoryName { get; init; }
    public required string Path { get; init; }
    public required CleanupTargetType Type { get; init; }
    public required CleanupExecutionStrategy Strategy { get; init; }
    public string? Command { get; init; }
    public string? Arguments { get; init; }
}

public sealed class CleanupResult
{
    public required string CategoryName { get; init; }
    public long BytesRemoved { get; set; }
    public int ItemsRemoved { get; set; }
    public int ItemsIgnored { get; set; }
    public int Failures { get; set; }
    public TimeSpan Duration { get; set; }
}
