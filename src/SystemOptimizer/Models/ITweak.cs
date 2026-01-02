namespace SystemOptimizer.Models;

public enum TweakCategory
{
    Privacy,
    Performance,
    Network,
    Security,
    Appearance,
    Tweaks, // Renomeado de Search para Tweaks no passado, mantido
    Search  // Nova categoria para a Pesquisa Online
}

public enum TweakStatus
{
    Optimized,
    Default,
    Modified,
    Unknown,
    Processing
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
