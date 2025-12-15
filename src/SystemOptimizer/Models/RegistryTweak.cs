using Microsoft.Win32;
using System;

namespace SystemOptimizer.Models
{
    public class RegistryTweak : TweakBase
    {
        private readonly string _keyPath;
        private readonly string _valueName;
        private readonly object _optimizedValue;
        private readonly object? _defaultValue; // Null if DELETE
        private readonly RegistryValueKind _valueKind;
        private readonly RegistryHive _hive;

        public RegistryTweak(string id, TweakCategory category, string title, string description,
                             string keyPath, string valueName, object optimizedValue, object? defaultValue, RegistryValueKind kind = RegistryValueKind.DWord)
            : base(id, category, title, description)
        {
            // Normalize path
            if (keyPath.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) || keyPath.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
            {
                _hive = RegistryHive.LocalMachine;
            }
            else if (keyPath.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase) || keyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
            {
                _hive = RegistryHive.CurrentUser;
            }
            else if (keyPath.StartsWith("HKCR", StringComparison.OrdinalIgnoreCase) || keyPath.StartsWith("HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase))
            {
                _hive = RegistryHive.ClassesRoot;
            }
            else if (keyPath.StartsWith("HKU", StringComparison.OrdinalIgnoreCase) || keyPath.StartsWith("HKEY_USERS", StringComparison.OrdinalIgnoreCase))
            {
                _hive = RegistryHive.Users;
            }
            else if (keyPath.StartsWith("HKCC", StringComparison.OrdinalIgnoreCase) || keyPath.StartsWith("HKEY_CURRENT_CONFIG", StringComparison.OrdinalIgnoreCase))
            {
                _hive = RegistryHive.CurrentConfig;
            }
            else
            {
                throw new ArgumentException($"Invalid or unsupported registry hive in path: {keyPath}", nameof(keyPath));
            }

            // Strip prefix to get relative path
            int firstSlash = keyPath.IndexOf('\\');
            if (firstSlash >= 0)
            {
                _keyPath = keyPath.Substring(firstSlash + 1);
            }
            else
            {
                _keyPath = keyPath; // Fallback
            }

            _valueName = valueName;
            _optimizedValue = optimizedValue;
            _defaultValue = defaultValue;
            _valueKind = kind;
        }

        public override (bool Success, string Message) Apply()
        {
            try
            {
                // Use Registry64 to ensure we touch the correct keys on x64 systems
                using var baseKey = RegistryKey.OpenBaseKey(_hive, RegistryView.Registry64);
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
                
                // Allow a slight leniency in verification for complex types, but strict for DWord
                if (IsOptimized) return (true, "Tweak aplicado com sucesso.");
                
                return (false, "O valor foi gravado, mas a verificação de status falhou. Reinicie para confirmar.");
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
                return (false, "Valor restaurado, mas status inconsistente.");
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

                if (key == null)
                {
                    // If key doesn't exist:
                    // If Optimized is DELETE, it's Optimized.
                    // If Default is DELETE, it's Default.
                    // Otherwise Unknown.
                    if (_optimizedValue.ToString() == "DELETE") Status = TweakStatus.Optimized;
                    else if (_defaultValue == null || _defaultValue.ToString() == "DELETE") Status = TweakStatus.Default;
                    else Status = TweakStatus.Unknown; // Should exist but doesn't
                    return;
                }

                var val = key.GetValue(_valueName);

                if (val == null)
                {
                    // Value doesn't exist
                    if (_optimizedValue.ToString() == "DELETE") Status = TweakStatus.Optimized;
                    else if (_defaultValue == null || _defaultValue.ToString() == "DELETE") Status = TweakStatus.Default;
                    else Status = TweakStatus.Modified; 
                }
                else
                {
                    // Value exists
                    // Handle string vs int comparisons carefully
                    string valStr = val.ToString() ?? "";
                    string optStr = _optimizedValue.ToString() ?? "";
                    string defStr = _defaultValue?.ToString() ?? "";

                    if (val.Equals(_optimizedValue) || valStr.Equals(optStr, StringComparison.OrdinalIgnoreCase)) 
                    {
                        Status = TweakStatus.Optimized;
                    }
                    else if (_defaultValue != null && (val.Equals(_defaultValue) || valStr.Equals(defStr, StringComparison.OrdinalIgnoreCase))) 
                    {
                        Status = TweakStatus.Default;
                    }
                    else 
                    {
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
}
