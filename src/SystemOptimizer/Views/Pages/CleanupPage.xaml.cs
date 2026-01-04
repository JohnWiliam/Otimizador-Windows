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
    private MainViewModel _viewModel;
    private readonly CleanupService _cleanupService;
    
    // Propriedades de Estado da UI
    private bool _isOptionsExpanded = true;
    private bool _isBusyLocal = false;
    
    // Opções de Limpeza
    private bool _cleanTemp = true;
    private bool _cleanSystemTemp = true;
    private bool _cleanPrefetch = true;
    private bool _cleanWindowsUpdate = true;
    private bool _cleanBrowser = true;
    private bool _cleanDns = true;
    private bool _cleanRecycleBin = false;

    public event PropertyChangedEventHandler? PropertyChanged;

    // Comando
    public ICommand ExecuteSelectedCleanupCommand { get; }

    public CleanupPage(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel; // Mantém o VM global como contexto principal
        
        // Serviço Local para controle granular
        _cleanupService = new CleanupService();
        _cleanupService.OnLogItem += Service_OnLogItem;
        
        // Inicializa comando usando AsyncRelayCommand
        ExecuteSelectedCleanupCommand = new AsyncRelayCommand(ExecuteCleanupAsync);

        // Ouve logs do ViewModel (caso outros processos escrevam lá)
        _viewModel.CleanupLogs.CollectionChanged += CleanupLogs_CollectionChanged;
    }

    private void Service_OnLogItem(CleanupLogItem item)
    {
        // Redireciona logs do serviço local para o ViewModel
        Application.Current.Dispatcher.Invoke(() => 
        {
            _viewModel.CleanupLogs.Add(item);
        });
    }

    private async Task ExecuteCleanupAsync()
    {
        if (IsBusyLocal) return;

        try
        {
            IsBusyLocal = true;
            IsOptionsExpanded = false; // Colapsa o card
            
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
            // Loga erro crítico na tela se algo falhar
            Application.Current.Dispatcher.Invoke(() => 
            {
                _viewModel.CleanupLogs.Add(new CleanupLogItem 
                { 
                    Message = $"ERRO CRÍTICO AO INICIAR LIMPEZA: {ex.Message}", 
                    Icon = "ErrorCircle24", 
                    StatusColor = "#FF0000",
                    IsBold = true 
                });
            });
        }
        finally
        {
            IsBusyLocal = false;
        }
    }

    // --- Propriedades de Binding ---

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

    // --- Lógica Existente de Log Visual ---

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
        paragraph.Margin = new Thickness(0, 0, 0, 2);
        paragraph.LineHeight = 18;

        Brush statusBrush = GetSmartPastelBrush(item.StatusColor, item.Message);

        SymbolRegular symbol = SymbolRegular.Info24;
        if (!Enum.TryParse(item.Icon, out SymbolRegular parsedSymbol))
        {
            string msgLower = item.Message.ToLower();
            if (msgLower.Contains("concluída") || msgLower.Contains("sucesso")) symbol = SymbolRegular.CheckmarkCircle24;
            else if (msgLower.Contains("erro") || msgLower.Contains("falha")) symbol = SymbolRegular.ErrorCircle24;
            else if (msgLower.Contains("lixeira") || msgLower.Contains("temp")) symbol = SymbolRegular.Delete24;
            else symbol = SymbolRegular.Info24;
        }
        else
        {
            symbol = parsedSymbol;
        }

        var icon = new SymbolIcon
        {
            Symbol = symbol,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = statusBrush
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
            FontSize = 12
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

    private static Brush GetSmartPastelBrush(string originalColorName, string message)
    {
        string msg = message?.ToLower() ?? "";
        string colorKey = originalColorName?.ToLower() ?? "";

        if (msg.Contains("update") || msg.Contains("wu") || msg.Contains("serviço") || msg.Contains("service"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A9DFBF")); 

        if (msg.Contains("temp") || msg.Contains("tmp") || msg.Contains("lixeira") ||
            msg.Contains("trash") || msg.Contains("cache") || msg.Contains("prefetch") ||
            msg.Contains("log") || msg.Contains("old"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDBB99"));

        if (msg.Contains("chrome") || msg.Contains("edge") || msg.Contains("firefox") ||
            msg.Contains("browser") || msg.Contains("navegador") || msg.Contains("cookie") || msg.Contains("histórico"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9E79F")); 

        if (msg.Contains("dns") || msg.Contains("ip") || msg.Contains("rede") ||
            msg.Contains("explorer") || msg.Contains("sistema") || msg.Contains("system"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D7BDE2"));

        if (msg.Contains("concluída") || msg.Contains("sucesso") || colorKey.Contains("green"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#82E0AA")); 

        if (msg.Contains("erro") || msg.Contains("falha") || msg.Contains("negado") || colorKey.Contains("red"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1948A")); 

        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AED6F1"));
    }
}
