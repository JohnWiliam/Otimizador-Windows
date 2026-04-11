using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Linq;
using System.Threading.Tasks;
using SystemOptimizer.Models;
using SystemOptimizer.Helpers;
using Microsoft.Win32;
using SystemOptimizer.Properties;

namespace SystemOptimizer.Services;

public class TweakService
{
    public List<ITweak> Tweaks { get; private set; } = [];

    public void LoadTweaks()
    {
        Logger.Log("Starting LoadTweaks...");
        Tweaks.Clear();
        AddPrivacyTweaks();
        AddPerformanceTweaks();
        AddNetworkTweaks();
        AddSecurityTweaks();
        AddAppearanceTweaks();
        AddSearchTweaks();
        AddCustomTweaks();
        Logger.Log($"LoadTweaks finished. Loaded {Tweaks.Count} tweaks.");
    }

    public async Task RefreshStatusesAsync()
    {
        await Task.Run(() =>
        {
            Parallel.ForEach(Tweaks, tweak =>
            {
                try { tweak.CheckStatus(); }
                catch (Exception ex) { Logger.Log($"Error checking status {tweak.Id}: {ex.Message}", "ERROR"); }
            });
        });
    }

    private static bool RunCommandChecked(string fileName, string arguments, int timeoutMs = 5000)
    {
        var result = CommandHelper.RunCommandDetailed(fileName, arguments, timeoutMs);
        if (!result.IsSuccess)
        {
            Logger.Log($"Command failed: {fileName} {arguments}. Started={result.Started}, TimedOut={result.TimedOut}, ExitCode={result.ExitCode}, StdErr='{result.StdErr}'", "CMD_FAIL");
        }

        return result.IsSuccess;
    }

    private static string RunPowerShell(string script)
        => CommandHelper.RunCommand("powershell", $"-NoProfile -NonInteractive -Command \"{script}\"").Trim();

    private void AddPrivacyTweaks()
    {
         Tweaks.Add(new RegistryTweak("P1", TweakCategory.Privacy, Resources.P1_Title, Resources.P1_Desc, @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, "DELETE"));
        Tweaks.Add(new RegistryTweak("P2", TweakCategory.Privacy, Resources.P2_Title, Resources.P2_Desc, @"HKLM\SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", 4, 2));
        Tweaks.Add(new RegistryTweak("P3", TweakCategory.Privacy, Resources.P3_Title, Resources.P3_Desc, @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0, "DELETE"));
        Tweaks.Add(new RegistryTweak("P4", TweakCategory.Privacy, Resources.P4_Title, Resources.P4_Desc, @"HKLM\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1, "DELETE"));
        Tweaks.Add(new RegistryTweak("P5", TweakCategory.Privacy, Resources.P5_Title, Resources.P5_Desc, @"HKLM\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", 1, "DELETE"));
        Tweaks.Add(new RegistryTweak("P6", TweakCategory.Privacy, Resources.P6_Title, Resources.P6_Desc, @"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338393Enabled", 0, 1));
        Tweaks.Add(new RegistryTweak("P7", TweakCategory.Privacy, Resources.P7_Title, Resources.P7_Desc, @"HKLM\SOFTWARE\Policies\Microsoft\Windows\OOBE", "DisablePrivacyExperience", 1, "DELETE"));
    }

    private void AddPerformanceTweaks()
    {
         Tweaks.Add(new CustomTweak("PF1", TweakCategory.Performance, Resources.PF1_Title, Resources.PF1_Desc,
            () => {
                const string ultimateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
                var list = CommandHelper.RunCommand("powercfg", "/list");

                if (!list.Contains(ultimateGuid, StringComparison.OrdinalIgnoreCase)
                    && !RunCommandChecked("powercfg", $"-duplicatescheme {ultimateGuid}"))
                {
                    return false;
                }

                if (!RunCommandChecked("powercfg", $"/setactive {ultimateGuid}"))
                {
                    return false;
                }

                var activeScheme = CommandHelper.RunCommand("powercfg", "/getactivescheme");
                return activeScheme.Contains(ultimateGuid, StringComparison.OrdinalIgnoreCase);
            },
            () =>
            {
                const string balancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
                if (!RunCommandChecked("powercfg", $"/setactive {balancedGuid}"))
                {
                    return false;
                }

                var activeScheme = CommandHelper.RunCommand("powercfg", "/getactivescheme");
                return activeScheme.Contains(balancedGuid, StringComparison.OrdinalIgnoreCase);
            },
            () =>
            {
                const string ultimateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
                var res = CommandHelper.RunCommand("powercfg", "/getactivescheme");
                return res.Contains(ultimateGuid, StringComparison.OrdinalIgnoreCase);
            }
        ));

        Tweaks.Add(new CustomTweak("PF2", TweakCategory.Performance, Resources.PF2_Title, Resources.PF2_Desc,
            () => {
                Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", 0, RegistryValueKind.DWord);
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", true)) { key.SetValue("AllowGameDVR", 0, RegistryValueKind.DWord); }
                return true;
            },
            () => {
                Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", 1, RegistryValueKind.DWord);
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", true);
                key?.DeleteValue("AllowGameDVR", false);
                return true;
            },
            () => {
                var v1 = Registry.GetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", null);
                var v2 = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", null);
                return (v1 is int i1 && i1 == 0) && (v2 is int i2 && i2 == 0);
            }
        ));

        Tweaks.Add(new CustomTweak("PF3", TweakCategory.Performance, Resources.PF3_Title, Resources.PF3_Desc,
            () => {
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSpeed", "0", RegistryValueKind.String);
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold1", "0", RegistryValueKind.String);
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold2", "0", RegistryValueKind.String);
                return true;
            },
            () => {
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSpeed", "1", RegistryValueKind.String);
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold1", "6", RegistryValueKind.String);
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold2", "10", RegistryValueKind.String);
                return true;
            },
            () => {
                var speed = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSpeed", null);
                var threshold1 = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold1", null);
                var threshold2 = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold2", null);
                return speed?.ToString() == "0"
                    && threshold1?.ToString() == "0"
                    && threshold2?.ToString() == "0";
            }
        ));

        Tweaks.Add(new RegistryTweak("PF5", TweakCategory.Performance, Resources.PF5_Title, Resources.PF5_Desc,
            @"HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 38, 2));
        Tweaks.Add(new RegistryTweak("PF6", TweakCategory.Performance, Resources.PF6_Title, Resources.PF6_Desc,
            @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", -1, 10));
        Tweaks.Add(new RegistryTweak("PF7", TweakCategory.Performance, Resources.PF7_Title, Resources.PF7_Desc,
            @"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, 1));

        Tweaks.Add(new CustomTweak("PF8", TweakCategory.Performance, Resources.PF8_Title, Resources.PF8_Desc,
            () =>
            {
                using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard", true))
                {
                    key.SetValue("EnableVirtualizationBasedSecurity", 0, RegistryValueKind.DWord);
                    key.SetValue("RequirePlatformSecurityFeatures", 0, RegistryValueKind.DWord);
                }

                using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", true))
                {
                    key.SetValue("Enabled", 0, RegistryValueKind.DWord);
                }

                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DeviceGuard", true))
                {
                    key.SetValue("EnableVirtualizationBasedSecurity", 0, RegistryValueKind.DWord);
                    key.SetValue("HypervisorEnforcedCodeIntegrity", 0, RegistryValueKind.DWord);
                }

                return true;
            },
            () =>
            {
                using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard", true))
                {
                    key.SetValue("EnableVirtualizationBasedSecurity", 1, RegistryValueKind.DWord);
                    key.SetValue("RequirePlatformSecurityFeatures", 1, RegistryValueKind.DWord);
                }

                using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", true))
                {
                    key.SetValue("Enabled", 1, RegistryValueKind.DWord);
                }

                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DeviceGuard", true))
                {
                    key.SetValue("EnableVirtualizationBasedSecurity", 1, RegistryValueKind.DWord);
                    key.SetValue("HypervisorEnforcedCodeIntegrity", 1, RegistryValueKind.DWord);
                }

                return true;
            },
            () =>
            {
                var systemVbs = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", -1);
                var systemPlatform = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard", "RequirePlatformSecurityFeatures", -1);
                var hvci = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", -1);
                var policyVbs = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeviceGuard", "EnableVirtualizationBasedSecurity", -1);
                var policyHvci = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeviceGuard", "HypervisorEnforcedCodeIntegrity", -1);

                return (systemVbs is int i1 && i1 == 0)
                    && (systemPlatform is int i2 && i2 == 0)
                    && (hvci is int i3 && i3 == 0)
                    && (policyVbs is int i4 && i4 == 0)
                    && (policyHvci is int i5 && i5 == 0);
            }
        ));

        Tweaks.Add(new CustomTweak("PF9", TweakCategory.Performance, Resources.PF9_Title, Resources.PF9_Desc,
            () =>
            {
                if (!RunCommandChecked("powercfg", "/hibernate off"))
                {
                    return false;
                }

                var val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", -1);
                return val is int i && i == 0;
            },
            () =>
            {
                if (!RunCommandChecked("powercfg", "/hibernate on"))
                {
                    return false;
                }

                var val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", -1);
                return val is int i && i == 1;
            },
            () => { var val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", -1); return val is int i && i == 0; }
        ));
    }

    private void AddNetworkTweaks()
    {
        Tweaks.Add(new CustomTweak("N1", TweakCategory.Network, Resources.N1_Title, Resources.N1_Desc,
            () => RunCommandChecked("netsh", "int tcp set global autotuninglevel=normal"),
            () => RunCommandChecked("netsh", "int tcp set global autotuninglevel=disabled"),
            () => RunPowerShell("(Get-NetTCPSetting -SettingName Internet).AutoTuningLevelLocal")
                .Equals("Normal", StringComparison.OrdinalIgnoreCase)
        ));

        Tweaks.Add(new CustomTweak("N2", TweakCategory.Network, Resources.N2_Title, Resources.N2_Desc,
            () =>
            {
                if (RunCommandChecked("netsh", "int tcp set supplementary template=internet congestionprovider=cubic"))
                {
                    return true;
                }

                // Fallback para ambientes onde CUBIC não está disponível.
                return RunCommandChecked("netsh", "int tcp set supplementary template=internet congestionprovider=ctcp");
            },
            () => RunCommandChecked("netsh", "int tcp set supplementary template=internet congestionprovider=default"),
            () =>
            {
                var provider = RunPowerShell("(Get-NetTCPSetting -SettingName Internet).CongestionProvider").ToUpperInvariant();
                return provider is "CUBIC" or "CTCP";
            }
        ));

        Tweaks.Add(new CustomTweak("N3", TweakCategory.Network, Resources.N3_Title, Resources.N3_Desc,
            () => RunCommandChecked("netsh", "int tcp set global ecncapability=enabled"),
            () => RunCommandChecked("netsh", "int tcp set global ecncapability=disabled"),
            () => RunPowerShell("(Get-NetTCPSetting -SettingName Internet).EcnCapability")
                .Equals("Enabled", StringComparison.OrdinalIgnoreCase)
        ));

        Tweaks.Add(new CustomTweak("N4", TweakCategory.Network, Resources.N4_Title, Resources.N4_Desc,
            () => RunCommandChecked("netsh", "int tcp set global rss=disabled"),
            () => RunCommandChecked("netsh", "int tcp set global rss=enabled"),
            () => RunPowerShell("(Get-NetOffloadGlobalSetting).ReceiveSideScaling")
                .Equals("Disabled", StringComparison.OrdinalIgnoreCase)
        ));

        Tweaks.Add(new RegistryTweak("N5", TweakCategory.Network, Resources.N5_Title, Resources.N5_Desc, @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Psched", "NonBestEffortLimit", 0, "DELETE"));
    }

    private void AddSecurityTweaks()
    {
        Tweaks.Add(new RegistryTweak("S1", TweakCategory.Security, Resources.S1_Title, Resources.S1_Desc, @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0, 1));
        Tweaks.Add(new RegistryTweak("S2", TweakCategory.Security, Resources.S2_Title, Resources.S2_Desc, @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 255, "DELETE"));
    }

    private void AddAppearanceTweaks()
    {
        Tweaks.Add(new RegistryTweak("A1", TweakCategory.Appearance, Resources.A1_Title, Resources.A1_Desc, @"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, 1));
        Tweaks.Add(new RegistryTweak("A2", TweakCategory.Appearance, Resources.A2_Title, Resources.A2_Desc, @"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0, 1));
        Tweaks.Add(new RegistryTweak("A3", TweakCategory.Appearance, Resources.A3_Title, Resources.A3_Desc, @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2, 3));
    }

    private void AddSearchTweaks()
    {
        Tweaks.Add(new CustomTweak("SCH1", TweakCategory.Search, Resources.S_1_Title, Resources.S_1_Desc,
            () =>
            {
                // Windows 11 moderno: política explícita para remover sugestões/web no Search.
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord);
                // Mantém compatibilidade com comportamento antigo de ocultar caixa de pesquisa da barra.
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0, RegistryValueKind.DWord);
                return true;
            },
            () =>
            {
                using var policyKey = Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Windows\Explorer", true);
                policyKey?.DeleteValue("DisableSearchBoxSuggestions", false);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 1, RegistryValueKind.DWord);
                return true;
            },
            () =>
            {
                var policyValue = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 0);
                var taskbarMode = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", null);
                return (policyValue is int i && i == 1) || (taskbarMode is int mode && mode == 0);
            }
        ));

        Tweaks.Add(new RegistryTweak("SCH2", TweakCategory.Search, Resources.S_2_Title, Resources.S_2_Desc,
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "DisableCloudSearch", 1, 0));

        Tweaks.Add(new RegistryTweak("SCH3", TweakCategory.Search, Resources.S_3_Title, Resources.S_3_Desc,
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, 1));
    }

    private void AddCustomTweaks()
    {
        Tweaks.Add(new CustomTweak("SE1", TweakCategory.Tweaks, Resources.SE1_Title, Resources.SE1_Desc,
            () =>
            {
                var configured = RunCommandChecked("sc", "config SysMain start= disabled");
                try { using var sc = new ServiceController("SysMain"); if (sc.Status != ServiceControllerStatus.Stopped) sc.Stop(); } catch { }
                return configured;
            },
            () =>
            {
                var configured = RunCommandChecked("sc", "config SysMain start= auto");
                try { using var sc = new ServiceController("SysMain"); if (sc.Status != ServiceControllerStatus.Running) sc.Start(); } catch { }
                return configured;
            },
            () =>
            {
                try
                {
                    using var sc = new ServiceController("SysMain");
                    return sc.StartType == ServiceStartMode.Disabled;
                }
                catch
                {
                    return false;
                }
            }
        ));

        Tweaks.Add(new RegistryTweak("SE2", TweakCategory.Tweaks, Resources.SE2_Title, Resources.SE2_Desc,
            @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", "EnablePrefetcher", 0, 3));
    }
}
