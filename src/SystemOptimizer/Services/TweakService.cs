using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SystemOptimizer.Models;
using SystemOptimizer.Helpers;
using Microsoft.Win32;
using System.Management;
using System.Diagnostics;
using System.IO;
using SystemOptimizer.Properties;

namespace SystemOptimizer.Services;

public class TweakService
{
    private static string RunCommandSafe(string fileName, string arguments, int timeoutMs = 5000)
    {
        var result = CommandHelper.RunCommandDetailedAsync(fileName, arguments, timeoutMs).GetAwaiter().GetResult();
        if (!result.Started) return string.Empty;
        if (result.TimedOut) return "[TIMEOUT CMD]";
        if (!result.IsSuccess) return string.IsNullOrWhiteSpace(result.StdErr) ? string.Empty : $"[ERRO CMD] {result.StdErr}";
        return result.StdOut;
    }

    private static CommandHelper.CommandResult RunCommandDetailedSafe(string fileName, string arguments, int timeoutMs = 5000)
        => CommandHelper.RunCommandDetailedAsync(fileName, arguments, timeoutMs).GetAwaiter().GetResult();

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
        int maxConcurrency = Math.Clamp(Environment.ProcessorCount, 2, 4);
        using var throttler = new SemaphoreSlim(maxConcurrency);

        var statusTasks = Tweaks.Select(async tweak =>
        {
            await throttler.WaitAsync();
            try
            {
                await Task.Run(tweak.CheckStatus);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error checking status {tweak.Id}: {ex.Message}", "ERROR");
            }
            finally
            {
                throttler.Release();
            }
        });

        await Task.WhenAll(statusTasks);
    }

    private void AddPrivacyTweaks()
    {
         Tweaks.Add(new CustomTweak("P1", TweakCategory.Privacy, Resources.P1_Title,
            Resources.P1_Desc + " (Observação: no Windows Home a telemetria mínima pode permanecer em nível Security; também define LimitDiagnosticLogCollection.)",
            () =>
            {
                using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true);
                key.SetValue("AllowTelemetry", 0, RegistryValueKind.DWord);
                key.SetValue("LimitDiagnosticLogCollection", 1, RegistryValueKind.DWord);
                return true;
            },
            () =>
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true);
                key?.DeleteValue("AllowTelemetry", false);
                key?.DeleteValue("LimitDiagnosticLogCollection", false);
                return true;
            },
            () =>
            {
                var telemetry = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", null);
                var limit = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "LimitDiagnosticLogCollection", null);
                return telemetry is int i1 && i1 == 0 && limit is int i2 && i2 == 1;
            }));
        Tweaks.Add(new RegistryTweak("P2", TweakCategory.Privacy, Resources.P2_Title, Resources.P2_Desc, @"HKLM\SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", 4, 2));
        Tweaks.Add(new CustomTweak("P3", TweakCategory.Privacy, Resources.P3_Title,
            Resources.P3_Desc + " (Windows 11 build 25967+ já remove a Cortana standalone; o tweak é mantido para compatibilidade.)",
            () =>
            {
                if (Environment.OSVersion.Version.Build >= 25967) return true;
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0, RegistryValueKind.DWord);
                return true;
            },
            () =>
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", true);
                key?.DeleteValue("AllowCortana", false);
                return true;
            },
            () =>
            {
                if (Environment.OSVersion.Version.Build >= 25967) return true;
                var value = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", null);
                return value is int i && i == 0;
            }));

        Tweaks.Add(new CustomTweak("P4", TweakCategory.Privacy, Resources.P4_Title, Resources.P4_Desc,
            () =>
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, RegistryValueKind.DWord);
                return true;
            },
            () =>
            {
                using (var policyKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", true))
                    policyKey?.DeleteValue("DisabledByGroupPolicy", false);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 1, RegistryValueKind.DWord);
                return true;
            },
            () =>
            {
                var policy = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", null);
                var user = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", null);
                return policy is int i1 && i1 == 1 && user is int i2 && i2 == 0;
            }));
        Tweaks.Add(new RegistryTweak("P5", TweakCategory.Privacy, Resources.P5_Title, Resources.P5_Desc, @"HKLM\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", 1, RegistryTweak.DeleteValue));
        Tweaks.Add(new RegistryTweak("P6", TweakCategory.Privacy, Resources.P6_Title, Resources.P6_Desc, @"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338393Enabled", 0, 1));
        Tweaks.Add(new RegistryTweak("P7", TweakCategory.Privacy, Resources.P7_Title, Resources.P7_Desc, @"HKLM\SOFTWARE\Policies\Microsoft\Windows\OOBE", "DisablePrivacyExperience", 1, RegistryTweak.DeleteValue));
    }

    private void AddPerformanceTweaks()
    {
         Tweaks.Add(new CustomTweak("PF1", TweakCategory.Performance, Resources.PF1_Title, Resources.PF1_Desc,
            () => {
                var list = RunCommandSafe("powercfg", "/list");
                string ultimateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
                if (!list.Contains(ultimateGuid)) RunCommandSafe("powercfg", $"-duplicatescheme {ultimateGuid}");
                var activateResult = RunCommandDetailedSafe("powercfg", $"/setactive {ultimateGuid}");
                Logger.Log($"Resultado powercfg/setactive(ultimate) -> Started={activateResult.Started}, TimedOut={activateResult.TimedOut}, ExitCode={activateResult.ExitCode}, StdOut='{activateResult.StdOut}', StdErr='{activateResult.StdErr}'", "CMD_POWERCFG");

                var check = RunCommandSafe("powercfg", "/getactivescheme");
                if (!activateResult.IsSuccess || !check.Contains(ultimateGuid))
                {
                    var fallbackResult = RunCommandDetailedSafe("powercfg", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                    Logger.Log($"Resultado powercfg/setactive(fallback) -> Started={fallbackResult.Started}, TimedOut={fallbackResult.TimedOut}, ExitCode={fallbackResult.ExitCode}, StdOut='{fallbackResult.StdOut}', StdErr='{fallbackResult.StdErr}'", "CMD_POWERCFG");
                }
                return true;
            },
            () => { RunCommandSafe("powercfg", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e"); return true; },
            () => { var res = RunCommandSafe("powercfg", "/getactivescheme"); return res.Contains("e9a42b02") || res.Contains("8c5e7fda"); }
        ));

        Tweaks.Add(new CustomTweak("PF2", TweakCategory.Performance, Resources.PF2_Title, Resources.PF2_Desc,
            () => {
                Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", 0, RegistryValueKind.DWord);
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", true)) { key.SetValue("AllowGameDVR", 0, RegistryValueKind.DWord); }
                return true;
            },
            () => {
                Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", 1, RegistryValueKind.DWord);
                try { using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", true); key?.DeleteValue("AllowGameDVR", false); } catch {}
                return true;
            },
            () => {
                var v1 = Registry.GetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", null);
                var v2 = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", null);
                return (v2 is int i2 && i2 == 0) || (v2 == null && v1 is int i1 && i1 == 0);
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

        Tweaks.Add(new RegistryTweak("PF5", TweakCategory.Performance, Resources.PF5_Title,
            Resources.PF5_Desc + " (Pode reduzir a responsividade de serviços/processos em segundo plano em workloads mistos.)",
            @"HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 38, 2));
        Tweaks.Add(new RegistryTweak("PF6", TweakCategory.Performance, Resources.PF6_Title, Resources.PF6_Desc,
            @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), 10));
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
            },
            requiresReboot: true
        ));

        Tweaks.Add(new CustomTweak("PF9", TweakCategory.Performance, Resources.PF9_Title, Resources.PF9_Desc,
            () => { RunCommandSafe("powercfg", "/hibernate off"); return true; },
            () => { RunCommandSafe("powercfg", "/hibernate on"); return true; },
            () => { var val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", -1); return val is int i && i == 0; }
        ));
    }

    private void AddNetworkTweaks()
    {
        Tweaks.Add(new CustomTweak("N1", TweakCategory.Network, Resources.N1_Title, Resources.N1_Desc,
            () => { RunCommandSafe("netsh", "int tcp set global autotuninglevel=normal"); return true; },
            () => { RunCommandSafe("netsh", "int tcp set global autotuninglevel=disabled"); return true; },
            () =>
            {
                var res = RunCommandSafe("netsh", "int tcp show global");
                return res.Contains("normal") || res.Contains("Normal");
            }
        ));

        Tweaks.Add(new CustomTweak("N2", TweakCategory.Network, Resources.N2_Title, Resources.N2_Desc,
            () => {
                var cubicResult = RunCommandDetailedSafe("netsh", "int tcp set supplementary template=internet congestionprovider=cubic");
                Logger.Log($"Resultado netsh/cubic -> Started={cubicResult.Started}, TimedOut={cubicResult.TimedOut}, ExitCode={cubicResult.ExitCode}, StdOut='{cubicResult.StdOut}', StdErr='{cubicResult.StdErr}'", "CMD_NETSH");

                if (!cubicResult.IsSuccess)
                {
                    var fallbackResult = RunCommandDetailedSafe("netsh", "int tcp set supplementary template=internet congestionprovider=ctcp");
                    Logger.Log($"Resultado netsh/ctcp(fallback) -> Started={fallbackResult.Started}, TimedOut={fallbackResult.TimedOut}, ExitCode={fallbackResult.ExitCode}, StdOut='{fallbackResult.StdOut}', StdErr='{fallbackResult.StdErr}'", "CMD_NETSH");
                }
                return true;
            },
            () => { RunCommandSafe("netsh", "int tcp set supplementary template=internet congestionprovider=default"); return true; },
            () =>
            {
                var res = RunCommandSafe("netsh", "int tcp show supplemental")
                    .ToUpperInvariant();
                return res.Contains("CUBIC", StringComparison.OrdinalIgnoreCase)
                    || res.Contains("CTCP", StringComparison.OrdinalIgnoreCase);
            }
        ));

        Tweaks.Add(new CustomTweak("N3", TweakCategory.Network, Resources.N3_Title,
            Resources.N3_Desc + " (Avançado: ECN pode causar fallback lento ou falhas em redes/serviços que descartam ECN-SYN.)",
            () => { RunCommandSafe("netsh", "int tcp set global ecncapability=enabled"); return true; },
            () => { RunCommandSafe("netsh", "int tcp set global ecncapability=disabled"); return true; },
            () =>
            {
                var lines = RunCommandSafe("netsh", "int tcp show global")
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                return lines.Any(line => line.Contains("ECN", StringComparison.OrdinalIgnoreCase)
                    && (line.Contains("enabled", StringComparison.OrdinalIgnoreCase)
                        || line.Contains("habilitado", StringComparison.OrdinalIgnoreCase)));
            }
        ));

        Tweaks.Add(new CustomTweak("N4", TweakCategory.Network, Resources.N4_Title, Resources.N4_Desc,
            () => { RunCommandSafe("netsh", "int tcp set global rss=enabled"); return true; },
            () => { RunCommandSafe("netsh", "int tcp set global rss=default"); return true; },
            () => { var res = RunCommandSafe("netsh", "int tcp show global").ToLowerInvariant(); return res.Contains("rss") && (res.Contains("enabled") || res.Contains("habilitado")); }
        ));

        Tweaks.Add(new RegistryTweak("N5", TweakCategory.Network, Resources.N5_Title, Resources.N5_Desc, @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Psched", "NonBestEffortLimit", 0, RegistryTweak.DeleteValue));
    }

    private void AddSecurityTweaks()
    {
        Tweaks.Add(new RegistryTweak("S1", TweakCategory.Security, Resources.S1_Title, Resources.S1_Desc, @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0, 1));
        Tweaks.Add(new RegistryTweak("S2", TweakCategory.Security, Resources.S2_Title, Resources.S2_Desc, @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 255, RegistryTweak.DeleteValue));
    }

    private void AddAppearanceTweaks()
    {
        Tweaks.Add(new RegistryTweak("A1", TweakCategory.Appearance, Resources.A1_Title, Resources.A1_Desc, @"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, 1));
        Tweaks.Add(new RegistryTweak("A2", TweakCategory.Appearance, Resources.A2_Title, Resources.A2_Desc, @"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0, 1));
        Tweaks.Add(new RegistryTweak("A3", TweakCategory.Appearance, Resources.A3_Title, Resources.A3_Desc, @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2, 3));
    }

    private void AddSearchTweaks()
    {
        // SCH1: DisableSearchBoxSuggestions - Alterado para HKCU\Software\Microsoft\Windows\CurrentVersion\Search para eficácia imediata
        Tweaks.Add(new RegistryTweak("SCH1", TweakCategory.Search, Resources.S_1_Title, Resources.S_1_Desc,
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0, 1));

        // SCH2: DisableCloudSearch
        Tweaks.Add(new RegistryTweak("SCH2", TweakCategory.Search, Resources.S_2_Title, Resources.S_2_Desc,
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "DisableCloudSearch", 1, 0));

        // SCH3: BingSearchEnabled - Refatorado conforme RegistryService de referência
        Tweaks.Add(new RegistryTweak("SCH3", TweakCategory.Search, Resources.S_3_Title, Resources.S_3_Desc,
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, 1));
    }

    private void AddCustomTweaks()
    {
        // SE1: SysMain
        Tweaks.Add(new CustomTweak("SE1", TweakCategory.Tweaks, Resources.SE1_Title, Resources.SE1_Desc,
            () => {
                RunCommandSafe("sc", "config SysMain start= disabled");
                CommandHelper.RunCommandNoWait("sc", "stop SysMain");
                return true;
            },
            () => {
                RunCommandSafe("sc", "config SysMain start= auto");
                CommandHelper.RunCommandNoWait("sc", "start SysMain");
                return true;
            },
            () => { try { using var sc = new ServiceController("SysMain"); return sc.StartType == ServiceStartMode.Disabled; } catch { return false; } }
        ));

        // SE2: Prefetch — só desabilita automaticamente quando o disco do sistema for HDD.
        Tweaks.Add(new CustomTweak("SE2", TweakCategory.Tweaks, Resources.SE2_Title, Resources.SE2_Desc,
            () =>
            {
                if (!IsSystemDriveRotational())
                {
                    Logger.Log("SE2 ignorado: disco do sistema não identificado como HDD rotacional.", "WARNING");
                    return false;
                }

                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", "EnablePrefetcher", 0, RegistryValueKind.DWord);
                return true;
            },
            () =>
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", "EnablePrefetcher", 3, RegistryValueKind.DWord);
                return true;
            },
            () =>
            {
                var value = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", "EnablePrefetcher", 3);
                return value is int i && i == 0;
            }
        ));
    }

    private static bool IsSystemDriveRotational()
    {
        try
        {
            string systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))?.TrimEnd('\\') ?? "C:";
            string driveLetter = systemDrive[..1];

            using var partitionSearcher = new ManagementObjectSearcher(
                "root\\CIMV2",
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}:'}} WHERE AssocClass=Win32_LogicalDiskToPartition");

            using var partitions = partitionSearcher.Get();
            foreach (ManagementObject partition in partitions.Cast<ManagementObject>())
            {
                using (partition)
                {
                    string partitionDeviceId = (partition["DeviceID"] as string)?.Replace("\\", "\\\\") ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(partitionDeviceId)) continue;

                    using var diskSearcher = new ManagementObjectSearcher(
                        "root\\CIMV2",
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionDeviceId}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                    using var disks = diskSearcher.Get();
                    foreach (ManagementObject disk in disks.Cast<ManagementObject>())
                    {
                        using (disk)
                        {
                            string mediaType = disk["MediaType"]?.ToString() ?? string.Empty;
                            string model = disk["Model"]?.ToString() ?? string.Empty;

                            if (mediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                                model.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                                model.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
                            {
                                return false;
                            }

                            return mediaType.Contains("HDD", StringComparison.OrdinalIgnoreCase) ||
                                   mediaType.Contains("Fixed hard disk", StringComparison.OrdinalIgnoreCase) ||
                                   string.IsNullOrWhiteSpace(mediaType);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Falha ao detectar tipo do disco do sistema para SE2 via WMI: {ex.Message}", "WARNING");
        }

        return false;
    }
}
