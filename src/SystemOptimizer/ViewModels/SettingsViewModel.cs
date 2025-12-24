using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SystemOptimizer.Helpers;
using SystemOptimizer.Properties;
using SystemOptimizer.Services;
using Wpf.Ui.Appearance;

namespace SystemOptimizer.ViewModels;

// Classe auxiliar para as opções do ComboBox
public record ThemeOption(string Name, ApplicationTheme Theme);

public partial class SettingsViewModel : ObservableObject
{
    // --- Dependências ---
    private readonly TweakService _tweakService;

    // --- Constantes para Persistência ---
    private const string TaskName = "SystemOptimizer_AutoRun";
    private readonly string _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SystemOptimizer");
    private readonly string _targetExePath;

    // --- Constantes para Atalhos (Manter Instalado) ---
    private readonly string _desktopShortcutPath;
    private readonly string _startMenuShortcutPath;

    [ObservableProperty]
    private string _currentLanguage;

    public ObservableCollection<string> Languages { get; } = ["Português", "English"];

    // Lista de opções de tema para o ComboBox
    public ObservableCollection<ThemeOption> ThemeOptions { get; } = 
    [
        new(Resources.Theme_System, ApplicationTheme.Unknown),
        new(Resources.Theme_Light, ApplicationTheme.Light),
        new(Resources.Theme_Dark, ApplicationTheme.Dark)
    ];

    [ObservableProperty]
    private ThemeOption _currentThemeOption;

    // Propriedade para o Switch de Persistência
    [ObservableProperty]
    private bool _isPersistenceEnabled;

    // --- NOVA PROPRIEDADE: Manter Instalado ---
    [ObservableProperty]
    private bool _isKeepInstalledEnabled;

    // Construtor atualizado com Injeção de Dependência
    public SettingsViewModel(TweakService tweakService)
    {
        _tweakService = tweakService;

        // Inicializa o idioma atual com base na configuração carregada
        _currentLanguage = AppSettings.Current.Language == "en-US" ? "English" : "Português";

        // Define o caminho do executável de destino (C:\ProgramData\SystemOptimizer\SystemOptimizer.exe)
        _targetExePath = Path.Combine(_appDataPath, "SystemOptimizer.exe");

        // Define caminhos dos atalhos
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu); // Geralmente AppData\Roaming\Microsoft\Windows\Start Menu
        
        _desktopShortcutPath = Path.Combine(desktop, "System Optimizer.lnk");
        _startMenuShortcutPath = Path.Combine(startMenu, "Programs", "System Optimizer.lnk");

        // Define "Padrão do Sistema" como a opção inicial padrão
        _currentThemeOption = ThemeOptions.First(x => x.Theme == ApplicationTheme.Unknown);
        
        // Aplica o tema inicial
        UpdateTheme(_currentThemeOption.Theme);

        // Verifica o estado atual das funcionalidades ao abrir
        CheckPersistenceStatus();
        CheckKeepInstalledStatus();
    }

    // Chamado quando a linguagem muda
    partial void OnCurrentLanguageChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        string cultureCode = value == "English" ? "en-US" : "pt-BR";

        // Se a cultura for diferente da salva, salva e avisa
        if (AppSettings.Current.Language != cultureCode)
        {
            AppSettings.Current.Language = cultureCode;
            AppSettings.Save();

            // Mensagem de reinício necessário
            var result = MessageBox.Show(Resources.Msg_RestartRequired, Resources.Msg_RestartTitle, MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                // Reinicia a aplicação
                Process.Start(Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty);
                Application.Current.Shutdown();
            }
        }
    }

    // Este método é chamado automaticamente sempre que o usuário muda a seleção no ComboBox
    partial void OnCurrentThemeOptionChanged(ThemeOption value)
    {
        if (value != null)
        {
            UpdateTheme(value.Theme);
        }
    }

    // Este método é chamado automaticamente quando o Switch de persistência é alterado
    partial void OnIsPersistenceEnabledChanged(bool value)
    {
        if (value)
            EnablePersistence();
        else
            DisablePersistence();
    }

    // --- NOVO: Trigger para "Manter Instalado" ---
    partial void OnIsKeepInstalledEnabledChanged(bool value)
    {
        ManageShortcuts(value);
    }

    private void UpdateTheme(ApplicationTheme theme)
    {
        if (theme == ApplicationTheme.Unknown)
        {
            ApplicationThemeManager.ApplySystemTheme();
        }
        else
        {
            ApplicationThemeManager.Apply(theme);
        }
    }

    // --- Lógica de "Manter Instalado" (Atalhos) ---

    private void CheckKeepInstalledStatus()
    {
        // Consideramos instalado se pelo menos um dos atalhos existir
        bool exists = File.Exists(_desktopShortcutPath) || File.Exists(_startMenuShortcutPath);
        
        // Suprimimos o aviso para atualizar a UI sem disparar o evento OnChanged novamente (loop)
#pragma warning disable MVVMTK0034
        SetProperty(ref _isKeepInstalledEnabled, exists, nameof(IsKeepInstalledEnabled));
#pragma warning restore MVVMTK0034
    }

    private void ManageShortcuts(bool create)
    {
        try
        {
            if (create)
            {
                // 1. Garante que o diretório existe
                if (!Directory.Exists(_appDataPath))
                    Directory.CreateDirectory(_appDataPath);
                
                // 2. Garante que o executável alvo existe (copia o atual se necessário)
                // Isso permite que a função funcione mesmo se a Persistência não tiver sido ativada antes
                if (!File.Exists(_targetExePath))
                {
                    string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                    if (!string.IsNullOrEmpty(currentExe))
                    {
                        File.Copy(currentExe, _targetExePath, true);
                    }
                }

                // 3. Cria atalhos usando PowerShell
                CreateShortcut(_desktopShortcutPath, _targetExePath, "Otimizador do Sistema Windows");
                CreateShortcut(_startMenuShortcutPath, _targetExePath, "Otimizador do Sistema Windows");
                
                Logger.Log("Funcionalidade 'Manter Instalado' ativada. Atalhos criados.");
            }
            else
            {
                // Remove os atalhos
                if (File.Exists(_desktopShortcutPath)) File.Delete(_desktopShortcutPath);
                if (File.Exists(_startMenuShortcutPath)) File.Delete(_startMenuShortcutPath);
                
                Logger.Log("Funcionalidade 'Manter Instalado' desativada. Atalhos removidos.");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao gerenciar atalhos: {ex.Message}", "ERROR");
            
            // Reverte o switch visualmente em caso de erro
#pragma warning disable MVVMTK0034
            SetProperty(ref _isKeepInstalledEnabled, !create, nameof(IsKeepInstalledEnabled));
#pragma warning restore MVVMTK0034
        }
    }

    /// <summary>
    /// Cria um atalho .lnk usando um script PowerShell temporário.
    /// Isso evita a necessidade de adicionar referências COM (Interop) ao projeto.
    /// </summary>
    private void CreateShortcut(string shortcutPath, string targetPath, string description)
    {
        try
        {
            // Script PowerShell para criar o objeto WScript.Shell e salvar o atalho
            string psScript = $@"
                $WshShell = New-Object -comObject WScript.Shell;
                $Shortcut = $WshShell.CreateShortcut('{shortcutPath}');
                $Shortcut.TargetPath = '{targetPath}';
                $Shortcut.Description = '{description}';
                $Shortcut.WorkingDirectory = '{Path.GetDirectoryName(targetPath)}';
                $Shortcut.Save()";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            Logger.Log($"Falha ao criar atalho via PowerShell: {ex.Message}", "ERROR");
            throw; // Relança para ser tratado pelo ManageShortcuts
        }
    }

    // --- Lógica de Persistência (Existente) ---

    private void CheckPersistenceStatus()
    {
        // Verifica se a tarefa agendada existe
        var res = CommandHelper.RunCommand("schtasks", $"/query /tn \"{TaskName}\"");
        bool exists = !string.IsNullOrWhiteSpace(res) &&
                      !res.Contains("ERRO", StringComparison.OrdinalIgnoreCase) &&
                      !res.Contains("ERROR", StringComparison.OrdinalIgnoreCase) &&
                      !res.Contains("não pode ser encontrado", StringComparison.OrdinalIgnoreCase);
        
#pragma warning disable MVVMTK0034
        SetProperty(ref _isPersistenceEnabled, exists, nameof(IsPersistenceEnabled));
#pragma warning restore MVVMTK0034
    }

    // Transformado em async void para permitir aguardar a verificação de status
    private async void EnablePersistence()
    {
        try
        {
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(currentExe)) return;

            // 1. Criar diretório se não existir
            if (!Directory.Exists(_appDataPath))
                Directory.CreateDirectory(_appDataPath);

            // 2. Copiar executável
            File.Copy(currentExe, _targetExePath, true);

            // 3. Salvar o estado atual dos tweaks (Snapshot)
            if (_tweakService.Tweaks.Count == 0) 
                _tweakService.LoadTweaks();

            // Atualiza status real para garantir que salvamos apenas o que está aplicado
            await _tweakService.RefreshStatusesAsync();
            
            TweakPersistence.SaveState(_tweakService.Tweaks);

            // 4. Criar a tarefa agendada
            string cmd = $"/create /tn \"{TaskName}\" /tr \"\\\"{_targetExePath}\\\" --silent\" /sc onlogon /rl HIGHEST /f";
            var res = CommandHelper.RunCommand("schtasks", cmd);

            // Validação simples
            if (res.Contains("ERRO", StringComparison.OrdinalIgnoreCase) || res.Contains("ACCESS DENIED", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Falha ao criar tarefa agendada: " + res);
            }
            
            Logger.Log("Persistência ativada e configurações salvas com sucesso.");
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao habilitar persistência: {ex.Message}", "ERROR");
            
            // Reverte o switch visualmente
#pragma warning disable MVVMTK0034
            SetProperty(ref _isPersistenceEnabled, false, nameof(IsPersistenceEnabled));
#pragma warning restore MVVMTK0034
        }
    }

    private void DisablePersistence()
    {
        try
        {
            CommandHelper.RunCommand("schtasks", $"/delete /tn \"{TaskName}\" /f");
            Logger.Log("Persistência desativada.");
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao desabilitar persistência: {ex.Message}", "ERROR");
        }
    }
}
