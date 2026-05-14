using Microsoft.UI.Xaml.Controls;
using SystemOptimizer.Models;
using SystemOptimizer.Properties;
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;

namespace SystemOptimizer.Views.Pages;

public sealed partial class CleanupPage : Page
{
    public CleanupPage()
    {
        InitializeComponent();
        var viewModel = App.GetService<MainViewModel>();
        ContentHost.Children.Add(PageUiFactory.Header(Resources.Cleanup_Title, Resources.Cleanup_CacheDesc));
        ContentHost.Children.Add(new Button { Content = Resources.Btn_RunCleanup, Command = viewModel.RunCleanupCommand });
        ContentHost.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = viewModel.CleanupProgressPercentage });
        ContentHost.Children.Add(new TextBlock { Text = Resources.Cleanup_LogTitle, Style = (Microsoft.UI.Xaml.Style)Microsoft.UI.Xaml.Application.Current.Resources["SubtitleTextBlockStyle"] });

        foreach (CleanupLogItem log in viewModel.CleanupLogs)
        {
            ContentHost.Children.Add(new TextBlock { Text = log.Message, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap });
        }

        if (viewModel.CleanupLogs.Count == 0)
        {
            ContentHost.Children.Add(new TextBlock { Text = Resources.Cleanup_LogEmpty, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap });
        }
    }
}
