using Microsoft.Win32;
using System;

namespace SystemOptimizer.Models;

public class RegistryTweak : TweakBase
{
    public static readonly object DeleteValue = new DeleteRegistryValueSentinel();
    private readonly string _keyPath;
    private readonly string _valueName;
    private readonly object _optimizedValue;
    private readonly object? _defaultValue;
    private readonly RegistryValueKind _valueKind;
    private readonly RegistryHive _hive;
    private readonly RegistryView _view;

    public RegistryTweak(string id, TweakCategory category, string title, string description,
                         string keyPath, string valueName, object optimizedValue, object? defaultValue, RegistryValueKind kind = RegistryValueKind.DWord)
        : base(id, category, title, description)
    {
        if (keyPath.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) || keyPath.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
            _hive = RegistryHive.LocalMachine;
        else if (keyPath.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase) || keyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
            _hive = RegistryHive.CurrentUser;
        else
            _hive = RegistryHive.LocalMachine;

        int firstSlash = keyPath.IndexOf('\\');
        _keyPath = firstSlash >= 0 ? keyPath[(firstSlash + 1)..] : keyPath;

        _valueName = valueName;
        _optimizedValue = optimizedValue;
        _defaultValue = defaultValue;
        _valueKind = kind;
        _view = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32;
    }

    public override (bool Success, string Message) Apply()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(_hive, _view);
            using var key = baseKey.CreateSubKey(_keyPath, true);

            if (IsDeleteSentinel(_optimizedValue))
            {
                key.DeleteValue(_valueName, false);
            }
            else
            {
                key.SetValue(_valueName, _optimizedValue, _valueKind);
            }

            CheckStatus();
            return (true, "Aplicado com sucesso.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro: {ex.Message}");
        }
    }

    public override (bool Success, string Message) Revert()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(_hive, _view);
            using var key = baseKey.OpenSubKey(_keyPath, true);
            if (key == null) return (true, "Já restaurado.");

            if (_defaultValue == null || IsDeleteSentinel(_defaultValue))
            {
                key.DeleteValue(_valueName, false);
            }
            else
            {
                key.SetValue(_valueName, _defaultValue, _valueKind);
            }

            CheckStatus();
            return (true, "Restaurado.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro: {ex.Message}");
        }
    }

    public override void CheckStatus()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(_hive, _view);
            using var key = baseKey.OpenSubKey(_keyPath, false);

            if (key == null)
            {
                Status = IsDeleteSentinel(_optimizedValue) ? TweakStatus.Optimized : TweakStatus.Default;
                return;
            }

            var val = key.GetValue(_valueName);
            if (val == null)
            {
                Status = IsDeleteSentinel(_optimizedValue) ? TweakStatus.Optimized : TweakStatus.Default;
            }
            else
            {
                if (RegistryValuesEqual(val, _optimizedValue))
                    Status = TweakStatus.Optimized;
                else
                    Status = TweakStatus.Default;
            }
        }
        catch
        {
            Status = TweakStatus.Unknown;
        }
    }

    private static bool IsDeleteSentinel(object value)
    {
        return ReferenceEquals(value, DeleteValue);
    }

    private sealed class DeleteRegistryValueSentinel
    {
        public override string ToString() => nameof(DeleteValue);
    }

    private static bool RegistryValuesEqual(object actual, object expected)
    {
        if (actual is int actualInt && expected is int expectedInt)
        {
            return actualInt == expectedInt;
        }

        if (actual is int actualDword && expected is uint expectedDword)
        {
            return unchecked((uint)actualDword) == expectedDword;
        }

        if (actual is string actualString && expected is string expectedString)
        {
            return string.Equals(actualString, expectedString, StringComparison.Ordinal);
        }

        return string.Equals(actual.ToString(), expected.ToString(), StringComparison.Ordinal);
    }
}
