namespace SystemOptimizer.Services;

public sealed class StartupActivationState
{
    public bool OpenSettingsRequested { get; private set; }

    public void RequestOpenSettings() => OpenSettingsRequested = true;

    public void ClearOpenSettingsRequest() => OpenSettingsRequested = false;
}
