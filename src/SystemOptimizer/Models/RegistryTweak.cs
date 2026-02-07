using Microsoft.Win32;
using System;

namespace SystemOptimizer.Models;

public class RegistryTweak : TweakBase
{
    private readonly string _keyPath;
    private readonly string _valueName;
    private readonly object _optimizedValue;
    private readonly object? _defaultValue; // Null if DELETE is expected/default
    private readonly RegistryValueKind _valueKind;
    private readonly RegistryHive _hive;

    public RegistryTweak(string id, TweakCategory category, string title, string description,
                         string keyPath, string valueName, object optimizedValue, object? defaultValue, RegistryValueKind kind = RegistryValueKind.DWord)
        : base(id, category, title, description)
    {
        // Identificação do Hive
        if (keyPath.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) || keyPath.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
            _hive = RegistryHive.LocalMachine;
        else if (keyPath.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase) || keyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
            _hive = RegistryHive.CurrentUser;
        else if (keyPath.StartsWith("HKCR", StringComparison.OrdinalIgnoreCase) || keyPath.StartsWith("HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase))
            _hive = RegistryHive.ClassesRoot;
        else if (keyPath.StartsWith("HKU", StringComparison.OrdinalIgnoreCase) || keyPath.StartsWith("HKEY_USERS", StringComparison.OrdinalIgnoreCase))
            _hive = RegistryHive.Users;
        else if (keyPath.StartsWith("HKCC", StringComparison.OrdinalIgnoreCase) || keyPath.StartsWith("HKEY_CURRENT_CONFIG", StringComparison.OrdinalIgnoreCase))
            _hive = RegistryHive.CurrentConfig;
        else
            throw new ArgumentException($"Hive de registro desconhecida ou inválida: {keyPath}", nameof(keyPath));

        // Remove o prefixo para obter o caminho relativo
        int firstSlash = keyPath.IndexOf('\\');
        _keyPath = firstSlash >= 0 ? keyPath[(firstSlash + 1)..] : keyPath;

        _valueName = valueName;
        _optimizedValue = optimizedValue;
        _defaultValue = defaultValue;
        _valueKind = kind;
    }

    public override (bool Success, string Message) Apply()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(_hive, RegistryView.Registry64);
            // CreateSubKey garante a criação de toda a árvore se não existir
            using var key = baseKey.CreateSubKey(_keyPath, true);

            if (_optimizedValue.ToString() == "DELETE")
            {
                if (key.GetValue(_valueName) != null)
                    key.DeleteValue(_valueName, false);
            }
            else
            {
                key.SetValue(_valueName, _optimizedValue, _valueKind);
            }

            CheckStatus();

            if (IsOptimized) return (true, "Tweak aplicado com sucesso.");

            return (false, "Comando enviado, mas a verificação de status falhou.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao aplicar: {ex.Message}");
        }
    }

    public override (bool Success, string Message) Revert()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(_hive, RegistryView.Registry64);
            // CreateSubKey aqui também, pois a chave pode ter sido deletada manualmente
            using var key = baseKey.CreateSubKey(_keyPath, true);

            if (_defaultValue == null || _defaultValue.ToString() == "DELETE")
            {
                if (key.GetValue(_valueName) != null)
                    key.DeleteValue(_valueName, false);
            }
            else
            {
                key.SetValue(_valueName, _defaultValue, _valueKind);
            }

            CheckStatus();
            if (Status == TweakStatus.Default) return (true, "Tweak restaurado com sucesso.");
            return (false, "Restaurado, mas status inconsistente.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao restaurar: {ex.Message}");
        }
    }

    public override void CheckStatus()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(_hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(_keyPath, false);

            // Cenário 1: A chave (pasta) não existe
            if (key == null)
            {
                // Se o objetivo era DELETAR, então está otimizado (ou padrão se o padrão era não existir)
                if (_optimizedValue.ToString() == "DELETE") Status = TweakStatus.Optimized;
                else if (_defaultValue == null || _defaultValue.ToString() == "DELETE") Status = TweakStatus.Default;
                else Status = TweakStatus.Unknown; // Deveria existir um valor, mas a chave sumiu
                return;
            }

            var val = key.GetValue(_valueName);

            // Cenário 2: A chave existe, mas o valor não
            if (val == null)
            {
                if (_optimizedValue.ToString() == "DELETE") Status = TweakStatus.Optimized;
                else if (_defaultValue == null || _defaultValue.ToString() == "DELETE") Status = TweakStatus.Default;
                else Status = TweakStatus.Modified;
            }
            else
            {
                // Cenário 3: Valor existe. Comparação ajustada por tipo.
                if (_valueKind == RegistryValueKind.DWord)
                {
                    static uint NormalizeDword(object value)
                    {
                        if (Convert.ToInt64(value) == -1)
                            return uint.MaxValue;

                        return Convert.ToUInt32(value);
                    }

                    uint valNum = NormalizeDword(val);
                    uint optNum = NormalizeDword(_optimizedValue);
                    uint? defNum = _defaultValue == null ? null : NormalizeDword(_defaultValue);

                    if (valNum == optNum)
                        Status = TweakStatus.Optimized;
                    else if (defNum.HasValue && valNum == defNum.Value)
                        Status = TweakStatus.Default;
                    else
                        Status = TweakStatus.Modified;
                }
                else if (_valueKind == RegistryValueKind.QWord)
                {
                    static ulong NormalizeQword(object value)
                    {
                        if (Convert.ToInt64(value) == -1)
                            return ulong.MaxValue;

                        return Convert.ToUInt64(value);
                    }

                    ulong valNum = NormalizeQword(val);
                    ulong optNum = NormalizeQword(_optimizedValue);
                    ulong? defNum = _defaultValue == null ? null : NormalizeQword(_defaultValue);

                    if (valNum == optNum)
                        Status = TweakStatus.Optimized;
                    else if (defNum.HasValue && valNum == defNum.Value)
                        Status = TweakStatus.Default;
                    else
                        Status = TweakStatus.Modified;
                }
                else
                {
                    string valStr = val.ToString() ?? "";
                    string optStr = _optimizedValue.ToString() ?? "";
                    string defStr = _defaultValue?.ToString() ?? "";

                    if (string.Equals(valStr, optStr, StringComparison.InvariantCultureIgnoreCase))
                        Status = TweakStatus.Optimized;
                    else if (_defaultValue != null && string.Equals(valStr, defStr, StringComparison.InvariantCultureIgnoreCase))
                        Status = TweakStatus.Default;
                    else
                        Status = TweakStatus.Modified;
                }
            }
        }
        catch
        {
            Status = TweakStatus.Unknown;
        }
    }
}
