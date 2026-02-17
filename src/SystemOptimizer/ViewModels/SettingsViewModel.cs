using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; 
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
    private readonly IUpdateService _updateService; 
    private readonly IDialogService _dialogService; 

    // --- Constantes para Persistência ---
    private const string TaskName = "SystemOptimizer_AutoRun";
    private readonly string _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SystemOptimizer");
    private readonly string _targetExePath;
    private readonly string _desktopShortcutPath;
    private readonly string _startMenuShortcutPath;

    [ObservableProperty]
    private string _currentLanguage;

    public ObservableCollection<string> Languages { get; } = ["Português", "English"];

    public ObservableCollection<ThemeOption> ThemeOptions { get; } = 
    [
        new(Resources.Theme_System, ApplicationTheme.Unknown),
        new(Resources.Theme_Light, ApplicationTheme.Light),
        new(Resources.Theme_Dark, ApplicationTheme.Dark)
    ];

    [ObservableProperty]
    private ThemeOption _currentThemeOption;

    [ObservableProperty]
    private bool _isPersistenceEnabled;

    [ObservableProperty]
    private bool _isKeepInstalledEnabled;

    // Estado de verificação
    [ObservableProperty]
    private bool _isCheckingForUpdates;

    // Construtor Atualizado
    public SettingsViewModel(TweakService tweakService, IUpdateService updateService, IDialogService dialogService)
    {
        _tweakService = tweakService;
        _updateService = updateService;
        _dialogService = dialogService;

        _currentLanguage = AppSettings.Current.Language == "en-US" ? "English" : "Português";
        _targetExePath = Path.Combine(_appDataPath, "SystemOptimizer.exe");
        
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        _desktopShortcutPath = Path.Combine(desktop, "System Optimizer.lnk");
        _startMenuShortcutPath = Path.Combine(startMenu, "Programs", "System Optimizer.lnk");

        _currentThemeOption = ThemeOptions.First(x => x.Theme == ApplicationTheme.Unknown);
        UpdateTheme(_currentThemeOption.Theme);
        CheckPersistenceStatus();
        CheckKeepInstalledStatus();
    }

    // Chamado quando a linguagem muda
    partial void OnCurrentLanguageChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        string cultureCode = value == "English" ? "en-US" : "pt-BR";
        if (AppSettings.Current.Language != cultureCode)
        {
            AppSettings.Current.Language = cultureCode;
            AppSettings.Save();
            var result = MessageBox.Show(Resources.Msg_RestartRequired, Resources.Msg_RestartTitle, MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes)
            {
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(currentExe))
                {
                    Logger.Log("Caminho do executável atual não encontrado ao reiniciar o aplicativo.", "ERROR");
                    MessageBox.Show("Não foi possível localizar o executável para reiniciar o aplicativo.", Resources.Msg_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Process.Start(currentExe);
                Application.Current.Shutdown();
            }
        }
    }

    partial void OnCurrentThemeOptionChanged(ThemeOption value)
    {
        if (value != null) UpdateTheme(value.Theme);
    }

    partial void OnIsPersistenceEnabledChanged(bool value)
    {
        if (value) EnablePersistence(); else DisablePersistence();
    }

    partial void OnIsKeepInstalledEnabledChanged(bool value)
    {
        ManageShortcuts(value);
    }

    // --- Comando de Verificação de Atualização (CORRIGIDO) ---
    [RelayCommand]
    private async Task CheckForUpdates()
    {
        if (IsCheckingForUpdates) return;

        try
        {
            IsCheckingForUpdates = true;

            var updateInfo = await _updateService.CheckForUpdatesAsync();

            if (updateInfo.IsAvailable)
            {
                // Verifica nulos antes de passar para o DialogService
                // Usamos valores padrão caso a API retorne nulo
                string version = updateInfo.Version ?? "Unknown";
                string releaseNotes = updateInfo.ReleaseNotes ?? "No release notes.";
                string downloadUrl = updateInfo.DownloadUrl ?? string.Empty;

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    await _dialogService.ShowMessageAsync(Resources.Msg_ErrorTitle, "Download URL is missing.", DialogType.Error);
                    return;
                }

                await _dialogService.ShowUpdateDialogAsync(
                    version, 
                    releaseNotes, 
                    async (progress) => 
                    {
                        // Aqui downloadUrl já foi verificado como não nulo/vazio
                        await _updateService.DownloadAndInstallAsync(downloadUrl, progress);
                    });
            }
            else
            {
                await _dialogService.ShowMessageAsync(Resources.Settings_Update, Resources.Msg_UpToDate, DialogType.Success);
            }
        }
        catch (Exception ex)
        {
            // Usando string.Format para o erro
            string errorMsg = string.Format(Resources.Msg_UpdateCheckError, ex.Message);
            await _dialogService.ShowMessageAsync(Resources.Msg_ErrorTitle, errorMsg, DialogType.Error);
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    private void UpdateTheme(ApplicationTheme theme)
    {
        if (theme == ApplicationTheme.Unknown) ApplicationThemeManager.ApplySystemTheme();
        else ApplicationThemeManager.Apply(theme);
    }

    private void CheckKeepInstalledStatus()
    {
        bool exists = File.Exists(_desktopShortcutPath) || File.Exists(_startMenuShortcutPath);
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
                if (!Directory.Exists(_appDataPath)) Directory.CreateDirectory(_appDataPath);
                if (!File.Exists(_targetExePath))
                {
                    string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                    if (!string.IsNullOrEmpty(currentExe)) File.Copy(currentExe, _targetExePath, true);
                }
                CreateShortcut(_desktopShortcutPath, _targetExePath, "Otimizador do Sistema Windows");
                CreateShortcut(_startMenuShortcutPath, _targetExePath, "Otimizador do Sistema Windows");
                Logger.Log("Funcionalidade 'Manter Instalado' ativada. Atalhos criados.");
            }
            else
            {
                if (File.Exists(_desktopShortcutPath)) File.Delete(_desktopShortcutPath);
                if (File.Exists(_startMenuShortcutPath)) File.Delete(_startMenuShortcutPath);
                Logger.Log("Funcionalidade 'Manter Instalado' desativada. Atalhos removidos.");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao gerenciar atalhos: {ex.Message}", "ERROR");
#pragma warning disable MVVMTK0034
            SetProperty(ref _isKeepInstalledEnabled, !create, nameof(IsKeepInstalledEnabled));
#pragma warning restore MVVMTK0034
        }
    }

    private void CreateShortcut(string shortcutPath, string targetPath, string description)
    {
        try
        {
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
            throw;
        }
    }

    private void CheckPersistenceStatus()
    {
        var script = $@"
            $ErrorActionPreference = 'Stop'
            $task = Get-ScheduledTask -TaskName '{TaskName}'
            $action = $task.Actions | Select-Object -First 1
            $hasOnLogonTrigger = $task.Triggers | Where-Object {{ $_.TriggerType -eq 'Logon' }}

            Write-Output ('EXE=' + ($action.Execute ?? ''))
            Write-Output ('ARGS=' + ($action.Arguments ?? ''))
            Write-Output ('HAS_ONLOGON=' + ([bool]$hasOnLogonTrigger))
            Write-Output ('RUNLEVEL=' + ($task.Principal.RunLevel ?? ''))
        ";

        var escapedScript = script.Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", "; ");
        var res = CommandHelper.RunCommand("powershell.exe", $"-NoProfile -Command \"{escapedScript}\"");

        if (string.IsNullOrWhiteSpace(res) ||
            res.Contains("ERRO", StringComparison.OrdinalIgnoreCase) ||
            res.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
            res.Contains("não pode ser encontrado", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Log("Persistência inválida: tarefa agendada não encontrada ou inacessível.", "WARNING");
#pragma warning disable MVVMTK0034
            SetProperty(ref _isPersistenceEnabled, false, nameof(IsPersistenceEnabled));
#pragma warning restore MVVMTK0034
            return;
        }

        var lines = res
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .ToArray();

        string exe = GetTaskInfoValue(lines, "EXE");
        string args = GetTaskInfoValue(lines, "ARGS");
        string hasOnLogon = GetTaskInfoValue(lines, "HAS_ONLOGON");
        string runLevel = GetTaskInfoValue(lines, "RUNLEVEL");

        bool isExeValid = PathsAreEquivalent(exe, _targetExePath);
        if (!isExeValid)
            Logger.Log($"Persistência inválida: executável divergente. Esperado '{_targetExePath}', encontrado '{exe}'.", "WARNING");

        bool hasSilentArgument = args.Contains("--silent", StringComparison.OrdinalIgnoreCase);
        if (!hasSilentArgument)
            Logger.Log($"Persistência inválida: argumento '--silent' ausente. Argumentos atuais: '{args}'.", "WARNING");

        bool isOnLogonTrigger = bool.TryParse(hasOnLogon, out bool hasTrigger) && hasTrigger;
        if (!isOnLogonTrigger)
            Logger.Log("Persistência inválida: gatilho de logon (onlogon) ausente.", "WARNING");

        bool isHighestRunLevel = string.Equals(runLevel, "Highest", StringComparison.OrdinalIgnoreCase);
        if (!isHighestRunLevel)
            Logger.Log($"Persistência inválida: nível de execução divergente. Esperado 'Highest', encontrado '{runLevel}'.", "WARNING");

        bool isValid = isExeValid && hasSilentArgument && isOnLogonTrigger && isHighestRunLevel;
        
#pragma warning disable MVVMTK0034
        SetProperty(ref _isPersistenceEnabled, isValid, nameof(IsPersistenceEnabled));
#pragma warning restore MVVMTK0034
    }

    private static string GetTaskInfoValue(string[] lines, string key)
    {
        string prefix = key + "=";
        var line = lines.FirstOrDefault(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return line?[prefix.Length..].Trim() ?? string.Empty;
    }

    private static bool PathsAreEquivalent(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;

        try
        {
            string leftFull = Path.GetFullPath(left.Trim('"'));
            string rightFull = Path.GetFullPath(right.Trim('"'));
            return string.Equals(leftFull, rightFull, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async void EnablePersistence()
    {
        try
        {
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(currentExe)) return;
            if (!Directory.Exists(_appDataPath)) Directory.CreateDirectory(_appDataPath);
            File.Copy(currentExe, _targetExePath, true);
            if (_tweakService.Tweaks.Count == 0) _tweakService.LoadTweaks();
            await _tweakService.RefreshStatusesAsync();
            TweakPersistence.SaveState(_tweakService.Tweaks);
            string cmd = $"/create /tn \"{TaskName}\" /tr \"\\\"{_targetExePath}\\\" --silent\" /sc onlogon /rl HIGHEST /f";
            var res = CommandHelper.RunCommand("schtasks", cmd);
            if (res.Contains("ERRO", StringComparison.OrdinalIgnoreCase) || res.Contains("ACCESS DENIED", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Falha ao criar tarefa agendada: " + res);
            Logger.Log("Persistência ativada e configurações salvas com sucesso.");
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao habilitar persistência: {ex.Message}", "ERROR");
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
