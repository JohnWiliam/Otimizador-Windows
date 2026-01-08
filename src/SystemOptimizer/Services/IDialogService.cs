using System;
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
    
    // Novo método para o fluxo de atualização
    Task ShowUpdateDialogAsync(string version, string releaseNotes, Func<IProgress<double>, Task> updateAction);
}
