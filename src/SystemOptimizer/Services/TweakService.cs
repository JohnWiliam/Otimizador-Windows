using System.Collections.Generic;
using System.ServiceProcess;
using System.Linq;
using System.Threading.Tasks;
using SystemOptimizer.Models;
using SystemOptimizer.Helpers;
using Microsoft.Win32;
using System;

namespace SystemOptimizer.Services
{
    public class TweakService
    {
        public List<ITweak> Tweaks { get; private set; } = new List<ITweak>();

        public void LoadTweaks()
        {
            Tweaks.Clear();
            AddPrivacyTweaks();
            AddPerformanceTweaks();
            AddNetworkTweaks();
            AddSecurityTweaks();
            AddAppearanceTweaks();
        }

        public async Task RefreshStatusesAsync()
        {
            await Task.Run(() => 
            {
                Parallel.ForEach(Tweaks, tweak => 
                {
                    tweak.CheckStatus();
                });
            });
        }

        // ... (MÉTODOS PRIVACY E PERFORMANCE MANTIDOS IGUAIS AO ANTERIOR) ...
        private void AddPrivacyTweaks()
        {
            Tweaks.Add(new RegistryTweak("P1", TweakCategory.Privacy, "Desativar Telemetria", "Impede o envio de dados diagnósticos para a Microsoft.",
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, "DELETE"));
            Tweaks.Add(new RegistryTweak("P2", TweakCategory.Privacy, "Desativar DiagTrack", "Desabilita o serviço de Experiência do Usuário Conectado.",
                @"HKLM\SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", 4, 2));
            Tweaks.Add(new RegistryTweak("P3", TweakCategory.Privacy, "Desativar Cortana", "Bloqueia o assistente de voz legado e pesquisa web.",
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0, "DELETE"));
            Tweaks.Add(new RegistryTweak("P4", TweakCategory.Privacy, "Desativar ID de Anúncio", "Impede rastreamento comercial entre aplicativos.",
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1, "DELETE"));
            Tweaks.Add(new RegistryTweak("P5", TweakCategory.Privacy, "Desativar Geolocalização", "Bloqueia o rastreamento de localização global do OS.",
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", 1, "DELETE"));
            Tweaks.Add(new RegistryTweak("P6", TweakCategory.Privacy, "Desativar Dicas do Windows", "Remove sugestões 'irritantes' no Menu Iniciar.",
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338393Enabled", 0, 1));
            Tweaks.Add(new RegistryTweak("P7", TweakCategory.Privacy, "Desativar Dados OOBE", "Privacidade durante a configuração inicial do sistema.",
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\OOBE", "DisablePrivacyExperience", 1, "DELETE"));
        }

        private void AddPerformanceTweaks()
        {
            Tweaks.Add(new CustomTweak("PF1", TweakCategory.Performance, "Plano de Energia Ultimate", "Força o plano de desempenho máximo (Ultimate/High).",
                () => { 
                    var list = CommandHelper.RunCommand("powercfg", "/list");
                    string ultimateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
                    if (!list.Contains(ultimateGuid)) CommandHelper.RunCommand("powercfg", $"-duplicatescheme {ultimateGuid}");
                    CommandHelper.RunCommand("powercfg", $"/setactive {ultimateGuid}");
                    var check = CommandHelper.RunCommand("powercfg", "/getactivescheme");
                    if (!check.Contains(ultimateGuid)) CommandHelper.RunCommand("powercfg", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                    return true;
                },
                () => { CommandHelper.RunCommand("powercfg", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e"); return true; },
                () => { var res = CommandHelper.RunCommand("powercfg", "/getactivescheme"); return res.Contains("e9a42b02") || res.Contains("8c5e7fda"); }
            ));

            Tweaks.Add(new CustomTweak("PF2", TweakCategory.Performance, "Desativar GameDVR", "Remove gravação em segundo plano (Aumenta FPS).",
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
                    return (v1 is int i1 && i1 == 0) && (v2 is int i2 && i2 == 0);
                }
            ));

            Tweaks.Add(new CustomTweak("PF3", TweakCategory.Performance, "Input Mouse 1:1", "Remove aprimoramento de precisão (Aceleração).",
                () => {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSpeed", "0", RegistryValueKind.String);
                    Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold1", "0", RegistryValueKind.String);
                    Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold2", "0", RegistryValueKind.String);
                    return true;
                },
                () => { Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSpeed", "1", RegistryValueKind.String); return true; },
                () => { var val = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSpeed", null); return val != null && val.ToString() == "0"; }
            ));

            Tweaks.Add(new CustomTweak("PF4", TweakCategory.Performance, "Desativar SysMain", "Otimiza I/O para SSDs modernos (para o serviço).",
                () => { CommandHelper.RunCommand("sc", "config SysMain start= disabled"); CommandHelper.RunCommandNoWait("sc", "stop SysMain"); return true; },
                () => { CommandHelper.RunCommand("sc", "config SysMain start= auto"); CommandHelper.RunCommandNoWait("sc", "start SysMain"); return true; },
                () => { try { using var sc = new ServiceController("SysMain"); return sc.StartType == ServiceStartMode.Disabled; } catch { return true; } }
            ));

            Tweaks.Add(new RegistryTweak("PF5", TweakCategory.Performance, "Prioridade de CPU", "Ajusta prioridade para Programas vs Serviços (26 hex).",
                @"HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 38, 2));
            Tweaks.Add(new RegistryTweak("PF6", TweakCategory.Performance, "Throttling de Rede", "Remove limite de processamento de pacotes (Index FFFFFF).",
                @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", -1, 10)); 
            Tweaks.Add(new RegistryTweak("PF7", TweakCategory.Performance, "Agendamento GPU", "Habilita agendamento acelerado por hardware (Requer Reinício).",
                @"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, 1));
            
            Tweaks.Add(new CustomTweak("PF8", TweakCategory.Performance, "Desativar VBS / HVCI", "Aumenta FPS, mas reduz a segurança do sistema (REQUER REINÍCIO).",
                () => { using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", true)) { key.SetValue("Enabled", 0, RegistryValueKind.DWord); } return true; },
                () => { using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", true)) { key.SetValue("Enabled", 1, RegistryValueKind.DWord); } return true; },
                () => { var val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", -1); return val is int i && i == 0; }
            ));

            Tweaks.Add(new CustomTweak("PF9", TweakCategory.Performance, "Desativar Hibernação", "Libera GBs de espaço em disco (Remove hiberfil.sys).",
                () => { CommandHelper.RunCommand("powercfg", "/hibernate off"); return true; },
                () => { CommandHelper.RunCommand("powercfg", "/hibernate on"); return true; },
                () => { var val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", -1); return val is int i && i == 0; } 
            ));
        }

        // ==========================================
        // ÁREA DE FOCO: REDE (CORREÇÃO DE ERROS)
        // ==========================================
        private void AddNetworkTweaks()
        {
             // N1: TCP Auto-Tuning
            Tweaks.Add(new CustomTweak("N1", TweakCategory.Network, "TCP Auto-Tuning", "Janela TCP Dinâmica (Essencial para >100Mbps).",
                () => { CommandHelper.RunCommand("netsh", "int tcp set global autotuninglevel=normal"); return true; },
                () => { CommandHelper.RunCommand("netsh", "int tcp set global autotuninglevel=disabled"); return true; },
                () => { 
                    var res = CommandHelper.RunCommand("netsh", "int tcp show global").ToLower();
                    // O valor 'normal' geralmente não é traduzido, mas o label sim. Verificamos apenas se 'normal' aparece na saída.
                    return res.Contains("normal"); 
                } 
            ));

            // N2: CUBIC - Adicionado fallback para 'CTCP' se CUBIC não suportado (Win antigo)
            Tweaks.Add(new CustomTweak("N2", TweakCategory.Network, "Algoritmo CUBIC", "Gestão moderna de congestionamento para alta velocidade.",
                () => { 
                    var res = CommandHelper.RunCommand("netsh", "int tcp set supplementary template=internet congestionprovider=cubic"); 
                    // Se falhar (ex: windows antigo), tenta ctcp
                    if (res.Contains("falha") || res.Contains("failed")) 
                        CommandHelper.RunCommand("netsh", "int tcp set supplementary template=internet congestionprovider=ctcp");
                    return true; 
                },
                () => { CommandHelper.RunCommand("netsh", "int tcp set supplementary template=internet congestionprovider=default"); return true; },
                () => { 
                    // PowerShell retorna o objeto real, mais seguro que parsear texto do netsh
                    var res = CommandHelper.RunCommand("powershell", "(Get-NetTCPSetting -SettingName Internet).CongestionProvider").Trim().ToUpper();
                    return res == "CUBIC" || res == "CTCP"; 
                }
            ));

            // N3: ECN - Correção PT-BR
            Tweaks.Add(new CustomTweak("N3", TweakCategory.Network, "Ativar ECN", "Notificação Explícita de Congestionamento (Menos Perda).",
                () => { CommandHelper.RunCommand("netsh", "int tcp set global ecncapability=enabled"); return true; },
                () => { CommandHelper.RunCommand("netsh", "int tcp set global ecncapability=disabled"); return true; },
                () => { 
                    var res = CommandHelper.RunCommand("netsh", "int tcp show global").ToLower();
                    // PT-BR: "Capability... : habilitado" ou "enabled"
                    return res.Contains("ecn") && (res.Contains("enabled") || res.Contains("habilitado")); 
                } 
            ));

            // N4: RSS - Correção PT-BR
            Tweaks.Add(new CustomTweak("N4", TweakCategory.Network, "Desativar RSS", "Receive Side Scaling (Teste de estabilidade/driver).",
                () => { CommandHelper.RunCommand("netsh", "int tcp set global rss=disabled"); return true; },
                () => { CommandHelper.RunCommand("netsh", "int tcp set global rss=enabled"); return true; },
                () => { 
                    var res = CommandHelper.RunCommand("netsh", "int tcp show global").ToLower();
                    // PT-BR: "Estado... : desabilitado"
                    return res.Contains("rss") && (res.Contains("disabled") || res.Contains("desabilitado"));
                }
            ));

            Tweaks.Add(new RegistryTweak("N5", TweakCategory.Network, "Desativar QoS Limit", "Remove reserva de banda (Packet Scheduler).",
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Psched", "NonBestEffortLimit", 0, "DELETE"));
        }

        private void AddSecurityTweaks()
        {
            Tweaks.Add(new RegistryTweak("S1", TweakCategory.Security, "Mostrar Extensões", "Segurança: Exibe extensões reais (.exe, .bat).",
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0, 1));
            Tweaks.Add(new RegistryTweak("S2", TweakCategory.Security, "Desativar AutoRun", "Segurança: Bloqueia execução automática de USB.",
                @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 255, "DELETE"));
        }

        private void AddAppearanceTweaks()
        {
            Tweaks.Add(new RegistryTweak("A1", TweakCategory.Appearance, "Desativar Transparência", "Aumenta a resposta da UI removendo Acrylic/Mica.",
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, 1));
            Tweaks.Add(new RegistryTweak("A2", TweakCategory.Appearance, "Modo Escuro", "Força tema escuro para aplicativos.",
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0, 1));
            Tweaks.Add(new RegistryTweak("A3", TweakCategory.Appearance, "Efeitos Visuais", "Ajusta para 'Melhor Desempenho' (Parcial).",
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2, 3));
        }
    }
}
