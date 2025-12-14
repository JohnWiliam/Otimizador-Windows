using System.Threading.Tasks;

namespace SystemOptimizer.Services
{
    public interface IDialogService
    {
        Task ShowMessageAsync(string title, string message);
    }
}
