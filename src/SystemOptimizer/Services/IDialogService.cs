using System.Threading.Tasks;

namespace SystemOptimizer.Services;

public enum DialogType
{
    Info,
    Success,
    Warning,
    Error
}

public interface IDialogService
{
    Task ShowMessageAsync(string title, string message, DialogType type = DialogType.Info);
}
