using Microsoft.Win32;
using System;
using System.Globalization;
using SystemOptimizer.Helpers;

namespace SystemOptimizer.Models;

public class RegistryTweak : TweakBase
{
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

            if (_optimizedValue.ToString() == "DELETE")
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

            if (_defaultValue == null || _defaultValue.ToString() == "DELETE")
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
                Status = (_optimizedValue.ToString() == "DELETE") ? TweakStatus.Optimized : TweakStatus.Default;
                return;
            }

            var val = key.GetValue(_valueName);
            if (val == null)
            {
                Status = (_optimizedValue.ToString() == "DELETE") ? TweakStatus.Optimized : TweakStatus.Default;
            }
            else
            {
                Status = ValuesAreEquivalent(val, _optimizedValue, _valueKind)
                    ? TweakStatus.Optimized
                    : TweakStatus.Default;
            }
        }
        catch
        {
            Status = TweakStatus.Unknown;
        }
    }

    private static bool ValuesAreEquivalent(object currentValue, object optimizedValue, RegistryValueKind valueKind)
    {
        if (valueKind is RegistryValueKind.DWord or RegistryValueKind.QWord)
        {
            return TryConvertToUInt64(currentValue, out ulong current)
                && TryConvertToUInt64(optimizedValue, out ulong optimized)
                && current == optimized;
        }

        return string.Equals(
            Convert.ToString(currentValue, CultureInfo.InvariantCulture),
            Convert.ToString(optimizedValue, CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryConvertToUInt64(object value, out ulong result)
    {
        try
        {
            result = value switch
            {
                int i => unchecked((uint)i),
                uint u => u,
                long l => unchecked((ulong)l),
                ulong ul => ul,
                short s => unchecked((ushort)s),
                ushort us => us,
                byte b => b,
                string text when ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => Convert.ToUInt64(value, CultureInfo.InvariantCulture)
            };
            return true;
        }
        catch
        {
            result = 0;
            return false;
        }
    }
}
