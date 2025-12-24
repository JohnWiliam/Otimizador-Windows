using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SystemOptimizer.Helpers;
using Wpf.Ui.Appearance;

namespace SystemOptimizer.ViewModels;

// Classe auxiliar para as opções do ComboBox
public record ThemeOption(string Name, ApplicationTheme Theme);

public partial class SettingsViewModel : ObservableObject
{
    // --- Constantes para Persistência ---
    private const string TaskName = "SystemOptimizer_AutoRun";
    private readonly string _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SystemOptimizer");
    private readonly string _targetExePath;

    [ObservableProperty]
    private string _currentLanguage = "Português";

    public ObservableCollection<string> Languages { get; } = ["Português"];

    // Lista de opções de tema para o ComboBox
    public ObservableCollection<ThemeOption> ThemeOptions { get; } = 
    [
        new("Padrão do Sistema", ApplicationTheme.Unknown),
        new("Claro", ApplicationTheme.Light),
        new("Escuro", ApplicationTheme.Dark)
    ];

    [ObservableProperty]
    private ThemeOption _currentThemeOption;

    // Nova Propriedade para o Switch de Persistência
    [ObservableProperty]
    private bool _isPersistenceEnabled;

    public SettingsViewModel()
    {
        // Define o caminho do executável de destino
        _targetExePath = Path.Combine(_appDataPath, "SystemOptimizer.exe");

        // Define "Padrão do Sistema" como a opção inicial padrão
        // (Ou podes lógica para detetar qual o tema atual e selecionar a opção correta)
        _currentThemeOption = ThemeOptions.First(x => x.Theme == ApplicationTheme.Unknown);
        
        // Aplica o tema inicial
        UpdateTheme(_currentThemeOption.Theme);

        // Verifica o estado atual da persistência ao abrir
        CheckPersistenceStatus();
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

    private void UpdateTheme(ApplicationTheme theme)
    {
        if (theme == ApplicationTheme.Unknown)
        {
            // CORREÇÃO: Usamos o método próprio ApplySystemTheme() em vez de converter manualmente
            ApplicationThemeManager.ApplySystemTheme();
        }
        else
        {
            // Aplica o tema Claro ou Escuro selecionado
            ApplicationThemeManager.Apply(theme);
        }
    }

    // --- Lógica de Persistência ---

    private void CheckPersistenceStatus()
    {
        // Verifica se a tarefa agendada existe
        var res = CommandHelper.RunCommand("schtasks", $"/query /tn \"{TaskName}\"");
        bool exists = !string.IsNullOrWhiteSpace(res) &&
                      !res.Contains("ERRO", StringComparison.OrdinalIgnoreCase) &&
                      !res.Contains("ERROR", StringComparison.OrdinalIgnoreCase) &&
                      !res.Contains("não pode ser encontrado", StringComparison.OrdinalIgnoreCase);
        
        // Define o valor sem disparar o evento OnChanged (para evitar loop na inicialização)
        SetProperty(ref _isPersistenceEnabled, exists, nameof(IsPersistenceEnabled));
    }

    private void EnablePersistence()
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

            // 3. Criar a tarefa agendada
            string cmd = $"/create /tn \"{TaskName}\" /tr \"\\\"{_targetExePath}\\\" --silent\" /sc onlogon /rl HIGHEST /f";
            var res = CommandHelper.RunCommand("schtasks", cmd);

            // Validação simples
            if (res.Contains("ERRO", StringComparison.OrdinalIgnoreCase) || res.Contains("ACCESS DENIED", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Falha ao criar tarefa agendada: " + res);
            }
            
            Logger.Log("Persistência ativada com sucesso.");
        }
        catch (Exception ex)
        {
            Logger.Log($"Erro ao habilitar persistência: {ex.Message}", "ERROR");
            // Se falhar, reverte o switch visualmente (sem loop infinito, pois o check status vai confirmar depois se necessário)
            SetProperty(ref _isPersistenceEnabled, false, nameof(IsPersistenceEnabled));
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
