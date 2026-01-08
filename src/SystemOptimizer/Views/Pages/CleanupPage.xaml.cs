using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Input;
using SystemOptimizer.Models;
using SystemOptimizer.Services;
using SystemOptimizer.ViewModels;
using Wpf.Ui.Controls;
using CommunityToolkit.Mvvm.Input;

namespace SystemOptimizer.Views.Pages;

public partial class CleanupPage : Page, INotifyPropertyChanged
{
    private readonly MainViewModel _viewModel;
    private readonly CleanupService _cleanupService;
    
    private bool _isOptionsExpanded = true;
    private bool _isBusyLocal = false;
    
    private bool _cleanTemp = true;
    private bool _cleanSystemTemp = true;
    private bool _cleanPrefetch = true;
    private bool _cleanWindowsUpdate = true;
    private bool _cleanBrowser = true;
    private bool _cleanDns = true;
    private bool _cleanRecycleBin = false;

    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand ExecuteSelectedCleanupCommand { get; }

    public CleanupPage(MainViewModel viewModel, CleanupService cleanupService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _cleanupService = cleanupService;
        DataContext = viewModel;
        
        ExecuteSelectedCleanupCommand = new AsyncRelayCommand(ExecuteCleanupAsync);
        _viewModel.CleanupLogs.CollectionChanged += CleanupLogs_CollectionChanged;
    }

    private async Task ExecuteCleanupAsync()
    {
        if (IsBusyLocal) return;

        try
        {
            IsBusyLocal = true;
            IsOptionsExpanded = false; 
            
            Application.Current.Dispatcher.Invoke(() => _viewModel.CleanupLogs.Clear());

            var options = new CleanupOptions
            {
                CleanUserTemp = CleanTemp,
                CleanSystemTemp = CleanSystemTemp,
                CleanPrefetch = CleanPrefetch,
                CleanWindowsUpdate = CleanWindowsUpdate,
                CleanBrowserCache = CleanBrowser,
                CleanDns = CleanDns,
                CleanRecycleBin = CleanRecycleBin
            };

            await _cleanupService.RunCleanupAsync(options);
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() => 
            {
                _viewModel.CleanupLogs.Add(new CleanupLogItem 
                { 
                    Message = $"Error: {ex.Message}", 
                    Icon = "ErrorCircle24", 
                    StatusColor = "#FF6B6B",
                    IsBold = true 
                });
            });
        }
        finally
        {
            IsBusyLocal = false;
        }
    }

    public bool IsOptionsExpanded
    {
        get => _isOptionsExpanded;
        set { _isOptionsExpanded = value; OnPropertyChanged(); }
    }

    public bool IsBusyLocal
    {
        get => _isBusyLocal;
        set { _isBusyLocal = value; OnPropertyChanged(); }
    }

    public bool CleanTemp { get => _cleanTemp; set { _cleanTemp = value; OnPropertyChanged(); } }
    public bool CleanSystemTemp { get => _cleanSystemTemp; set { _cleanSystemTemp = value; OnPropertyChanged(); } }
    public bool CleanPrefetch { get => _cleanPrefetch; set { _cleanPrefetch = value; OnPropertyChanged(); } }
    public bool CleanWindowsUpdate { get => _cleanWindowsUpdate; set { _cleanWindowsUpdate = value; OnPropertyChanged(); } }
    public bool CleanBrowser { get => _cleanBrowser; set { _cleanBrowser = value; OnPropertyChanged(); } }
    public bool CleanDns { get => _cleanDns; set { _cleanDns = value; OnPropertyChanged(); } }
    public bool CleanRecycleBin { get => _cleanRecycleBin; set { _cleanRecycleBin = value; OnPropertyChanged(); } }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void CleanupLogs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            LogOutput.Document.Blocks.Clear();
        }
        else if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (CleanupLogItem item in e.NewItems)
            {
                AppendLog(item);
            }
        }
    }

    private void AppendLog(CleanupLogItem item)
    {
        var paragraph = new Paragraph();

        paragraph.FontFamily = new FontFamily("Segoe UI");
        paragraph.TextAlignment = TextAlignment.Left;
        paragraph.Margin = new Thickness(0, 0, 0, 4);
        paragraph.LineHeight = 20;

        Brush statusBrush = GetHarmonicBrush(item.StatusColor, item.Message);

        SymbolRegular symbol = SymbolRegular.Info24;
        if (!Enum.TryParse(item.Icon, out SymbolRegular parsedSymbol))
        {
            string msgLower = item.Message.ToLower();
            
            // Lógica bilíngue (PT/EN) para escolha de ícones
            if (msgLower.Contains("concluída") || msgLower.Contains("finished") || msgLower.Contains("sucesso") || msgLower.Contains("success") || msgLower.Contains("removidos") || msgLower.Contains("removed")) 
                symbol = SymbolRegular.Checkmark24;
            else if (msgLower.Contains("erro") || msgLower.Contains("error") || msgLower.Contains("fail")) 
                symbol = SymbolRegular.DismissCircle24;
            else if (msgLower.Contains("lixeira") || msgLower.Contains("bin") || msgLower.Contains("trash") || msgLower.Contains("delete")) 
                symbol = SymbolRegular.Delete24;
            else if (msgLower.Contains("update"))
                symbol = SymbolRegular.ArrowSync24;
            else 
                symbol = SymbolRegular.Info24;
        }
        else
        {
            symbol = parsedSymbol;
        }

        var icon = new SymbolIcon
        {
            Symbol = symbol,
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = statusBrush,
            Margin = new Thickness(0, 0, 0, -2)
        };

        var iconContainer = new InlineUIContainer(icon)
        {
            BaselineAlignment = BaselineAlignment.Center
        };
        paragraph.Inlines.Add(iconContainer);
        paragraph.Inlines.Add(new Run("  "));

        var run = new Run(item.Message)
        {
            BaselineAlignment = BaselineAlignment.Center,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13 
        };

        run.Foreground = statusBrush;

        if (item.IsBold)
        {
            run.FontWeight = FontWeights.SemiBold;
        }

        paragraph.Inlines.Add(run);

        LogOutput.Document.Blocks.Add(paragraph);
        LogOutput.ScrollToEnd();
    }

    private static Brush GetHarmonicBrush(string statusColor, string message)
    {
        string msg = message?.ToLower() ?? "";
        
        // Se vier cor Hex válida do Service, usa ela
        if (!string.IsNullOrEmpty(statusColor) && statusColor.StartsWith("#"))
        {
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(statusColor)); } catch { }
        }

        // Lógica Bilíngue para Cores Harmônicas (Pastel/Flat)
        
        if (msg.Contains("update") || msg.Contains("serviço") || msg.Contains("service"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64B5F6")); // Blue 300

        if (msg.Contains("temp") || msg.Contains("lixeira") || msg.Contains("bin") || msg.Contains("cache") || msg.Contains("prefetch") || msg.Contains("removed") || msg.Contains("removidos"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#81C784")); // Green 300
        
        if (msg.Contains("vazio") || msg.Contains("limpo") || msg.Contains("clean") || msg.Contains("empty"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B0BEC5")); // Blue Gray

        if (msg.Contains("chrome") || msg.Contains("edge") || msg.Contains("firefox") || msg.Contains("browser"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD54F")); // Amber 300

        if (msg.Contains("dns") || msg.Contains("rede") || msg.Contains("network"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9575CD")); // Deep Purple 300

        if (msg.Contains("concluída") || msg.Contains("finished") || msg.Contains("sucesso") || msg.Contains("success") || statusColor == "Green")
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DB6AC")); // Teal 300

        if (msg.Contains("erro") || msg.Contains("error") || msg.Contains("fail") || msg.Contains("negado") || msg.Contains("denied"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E57373")); // Red 300

        if (msg.Contains("iniciando") || msg.Contains("starting") || msg.Contains("parados") || msg.Contains("stopped"))
             return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4FC3F7")); // Light Blue

        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0")); 
    }
}
