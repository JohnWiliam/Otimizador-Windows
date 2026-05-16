namespace SystemOptimizer.Models;

public enum TweakCategory
{
    Privacy,
    Performance,
    Network,
    Security,
    Appearance,
    Services,
    Search,
    Tweaks = Services // Alias legado para compatibilidade com estados/configurações existentes.
}

public enum TweakStatus
{
    Optimized,
    Default,
    Modified,
    Unknown,
    Processing,
    PendingReboot
}

public interface ITweak
{
    string Id { get; }
    TweakCategory Category { get; }
    string Title { get; }
    string Description { get; }
    TweakStatus Status { get; }
    bool IsOptimized { get; }

    (bool Success, string Message) Apply();
    (bool Success, string Message) Revert();
    void CheckStatus();
}
