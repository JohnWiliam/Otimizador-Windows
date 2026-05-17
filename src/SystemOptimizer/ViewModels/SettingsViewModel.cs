using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; 
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
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
        _ = CheckPersistenceStatusAsync();
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

                Process.Start(currentExe)?.Dispose();
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
        if (value) _ = EnablePersistenceAsync(); else _ = DisablePersistenceAsync();
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
                    if (!string.IsNullOrEmpty(currentExe)) CopyExecutableSafely(currentExe, _targetExePath);
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
        object? shell = null;
        object? shortcut = null;
        try
        {
            string? shortcutDirectory = Path.GetDirectoryName(shortcutPath);
            if (!string.IsNullOrWhiteSpace(shortcutDirectory))
            {
                Directory.CreateDirectory(shortcutDirectory);
            }
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("COM WScript.Shell não disponível.");

            shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Falha ao criar WScript.Shell.");

            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: [shortcutPath])
                ?? throw new InvalidOperationException("Falha ao criar objeto de atalho.");

            var shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, [targetPath]);
            shortcutType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, [description]);
            shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, [Path.GetDirectoryName(targetPath) ?? string.Empty]);
            shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
        }
        catch (Exception ex)
        {
            Logger.Log($"Falha ao criar atalho via COM nativo: {ex.Message}", "ERROR");
            throw;
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private async Task CheckPersistenceStatusAsync()
    {
        var commandResult = await CommandHelper.RunCommandDetailedAsync("schtasks",
        [
            "/query", "/tn", TaskName, "/xml"
        ]).ConfigureAwait(false);

        if (!commandResult.IsSuccess || string.IsNullOrWhiteSpace(commandResult.StdOut))
        {
            Logger.Log("Persistência inválida: tarefa agendada não encontrada ou inacessível.", "WARNING");
            await SetPersistenceEnabledOnUiAsync(false).ConfigureAwait(false);
            return;
        }

        try
        {
            var document = XDocument.Parse(commandResult.StdOut);
            XNamespace ns = document.Root?.GetDefaultNamespace() ?? XNamespace.None;

            string exe = document.Descendants(ns + "Exec").Elements(ns + "Command").FirstOrDefault()?.Value ?? string.Empty;
            string args = document.Descendants(ns + "Exec").Elements(ns + "Arguments").FirstOrDefault()?.Value ?? string.Empty;
            bool hasOnLogon = document.Descendants(ns + "LogonTrigger").Any();
            string runLevel = document.Descendants(ns + "Principal").Elements(ns + "RunLevel").FirstOrDefault()?.Value ?? string.Empty;

            bool isExeValid = PathsAreEquivalent(exe, _targetExePath);
            if (!isExeValid)
                Logger.Log($"Persistência inválida: executável divergente. Esperado '{_targetExePath}', encontrado '{exe}'.", "WARNING");

            bool hasSilentArgument = args.Contains("--silent", StringComparison.OrdinalIgnoreCase);
            if (!hasSilentArgument)
                Logger.Log($"Persistência inválida: argumento '--silent' ausente. Argumentos atuais: '{args}'.", "WARNING");

            if (!hasOnLogon)
                Logger.Log("Persistência inválida: gatilho de logon (onlogon) ausente.", "WARNING");

            bool isHighestRunLevel = runLevel.EndsWith("HighestAvailable", StringComparison.OrdinalIgnoreCase)
                || runLevel.EndsWith("Highest", StringComparison.OrdinalIgnoreCase);
            if (!isHighestRunLevel)
                Logger.Log($"Persistência inválida: nível de execução divergente. Esperado 'Highest', encontrado '{runLevel}'.", "WARNING");

            await SetPersistenceEnabledOnUiAsync(isExeValid && hasSilentArgument && hasOnLogon && isHighestRunLevel).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Log($"Persistência inválida: XML da tarefa agendada não pôde ser lido. {ex.Message}", "WARNING");
            await SetPersistenceEnabledOnUiAsync(false).ConfigureAwait(false);
        }
    }

    private Task SetPersistenceEnabledOnUiAsync(bool isEnabled)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
#pragma warning disable MVVMTK0034
            SetProperty(ref _isPersistenceEnabled, isEnabled, nameof(IsPersistenceEnabled));
#pragma warning restore MVVMTK0034
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(() =>
        {
#pragma warning disable MVVMTK0034
            SetProperty(ref _isPersistenceEnabled, isEnabled, nameof(IsPersistenceEnabled));
#pragma warning restore MVVMTK0034
        }).Task;
    }

    private static void ReleaseComObject(object? value)
    {
        if (value != null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
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

    private async Task EnablePersistenceAsync()
    {
        try
        {
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(currentExe)) return;

            // (a) Garantir diretório
            Logger.Log($"PERSISTENCE_STEP=ensure_directory path='{_appDataPath}'", "PERSISTENCE");
            if (!Directory.Exists(_appDataPath))
                Directory.CreateDirectory(_appDataPath);

            // (b) Preparar binário
            Logger.Log($"PERSISTENCE_STEP=prepare_binary source='{currentExe}' target='{_targetExePath}'", "PERSISTENCE");
            PreparePersistenceBinary(currentExe);

            // (c) Salvar estado dos tweaks
            Logger.Log("PERSISTENCE_STEP=save_tweaks_state", "PERSISTENCE");
            if (_tweakService.Tweaks.Count == 0) _tweakService.LoadTweaks();
            await _tweakService.RefreshStatusesAsync();
            TweakPersistence.SaveState(_tweakService.Tweaks);

            // (d) Criar/atualizar tarefa (etapa final obrigatória)
            Logger.Log($"PERSISTENCE_STEP=task_create task='{TaskName}'", "PERSISTENCE");
            string taskRun = $"\"{_targetExePath}\" --silent";
            var result = await CommandHelper.RunCommandDetailedAsync("schtasks",
            [
                "/create", "/tn", TaskName, "/tr", taskRun, "/sc", "onlogon", "/rl", "HIGHEST", "/f"
            ]);

            Logger.Log($"PERSISTENCE_STEP=task_create result Started={result.Started}, TimedOut={result.TimedOut}, ExitCode={result.ExitCode}, StdOut='{result.StdOut}', StdErr='{result.StdErr}'", "PERSISTENCE");

            if (!result.Started)
                throw new Exception("Falha ao criar tarefa agendada: processo não iniciou.");

            if (result.TimedOut)
                throw new Exception("Falha ao criar tarefa agendada: timeout na execução.");

            if (result.ExitCode != 0)
                throw new Exception($"Falha ao criar tarefa agendada. ExitCode={result.ExitCode}. StdErr={result.StdErr}. StdOut={result.StdOut}");

            Logger.Log("Persistência ativada e configurações salvas com sucesso.");
        }
        catch (Exception ex)
        {
            Logger.Log($"PERSISTENCE_STEP=failed error='{ex.Message}'", "ERROR");
#pragma warning disable MVVMTK0034
            SetProperty(ref _isPersistenceEnabled, false, nameof(IsPersistenceEnabled));
#pragma warning restore MVVMTK0034
        }
    }

    private void PreparePersistenceBinary(string currentExe)
    {
        try
        {
            CopyExecutableSafely(currentExe, _targetExePath);
            Logger.Log("PERSISTENCE_STEP=copy status=overwritten", "PERSISTENCE");
        }
        catch (IOException ex) when (File.Exists(_targetExePath))
        {
            string expectedPath = Path.Combine(_appDataPath, "SystemOptimizer.exe");
            bool hasExpectedPath = PathsAreEquivalent(_targetExePath, expectedPath);
            bool hasExpectedName = string.Equals(
                Path.GetFileName(_targetExePath),
                Path.GetFileName(expectedPath),
                StringComparison.OrdinalIgnoreCase);

            if (hasExpectedPath && hasExpectedName)
            {
                Logger.Log($"PERSISTENCE_STEP=copy status=skipped_locked validated=true message='{ex.Message}'", "PERSISTENCE");
                return;
            }

            throw new IOException(
                $"Binário de persistência bloqueado e não corresponde ao executável esperado. target='{_targetExePath}', current='{currentExe}'.",
                ex);
        }
    }

    private static void CopyExecutableSafely(string sourcePath, string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Executável de origem não encontrado para cópia segura.", sourcePath);
        }

        string? destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new InvalidOperationException("Diretório de destino inválido para cópia do executável.");
        }

        Directory.CreateDirectory(destinationDirectory);
        EnsureDestinationHasSpace(sourcePath, destinationDirectory);
        EnsureFileIsReadable(sourcePath);
        EnsureDestinationWritable(destinationPath);

        string tempPath = Path.Combine(destinationDirectory, $"{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(sourcePath, tempPath, overwrite: false);
            File.Move(tempPath, destinationPath, overwrite: true);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static void EnsureDestinationHasSpace(string sourcePath, string destinationDirectory)
    {
        var sourceInfo = new FileInfo(sourcePath);
        string root = Path.GetPathRoot(Path.GetFullPath(destinationDirectory))
            ?? throw new InvalidOperationException("Não foi possível determinar o volume de destino.");
        var driveInfo = new DriveInfo(root);
        long requiredBytes = sourceInfo.Length + (1024 * 1024);
        if (driveInfo.AvailableFreeSpace < requiredBytes)
        {
            throw new IOException($"Espaço insuficiente para copiar o executável. Necessário: {requiredBytes} bytes; disponível: {driveInfo.AvailableFreeSpace} bytes.");
        }
    }

    private static void EnsureFileIsReadable(string sourcePath)
    {
        using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    private static void EnsureDestinationWritable(string destinationPath)
    {
        if (!File.Exists(destinationPath))
        {
            return;
        }

        using var destinationStream = new FileStream(destinationPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Arquivo temporário bloqueado será limpo pelo sistema posteriormente.
        }
    }

    private async Task DisablePersistenceAsync()
    {
        try
        {
            var result = await CommandHelper.RunCommandDetailedAsync("schtasks", ["/delete", "/tn", TaskName, "/f"]);
            Logger.Log($"Resultado schtasks/delete -> Started={result.Started}, TimedOut={result.TimedOut}, ExitCode={result.ExitCode}, StdOut='{result.StdOut}', StdErr='{result.StdErr}'", "PERSISTENCE");
            Logger.Log("Persistência desativada.");
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao desabilitar persistência: {ex.Message}", "ERROR");
        }
    }
}
