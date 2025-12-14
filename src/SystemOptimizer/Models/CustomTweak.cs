using System;

namespace SystemOptimizer.Models
{
    public class CustomTweak : TweakBase
    {
        private readonly Func<bool> _applyAction;
        private readonly Func<bool> _revertAction;
        private readonly Func<bool> _checkAction;

        public CustomTweak(string id, TweakCategory category, string title, string description,
                           Func<bool> apply, Func<bool> revert, Func<bool> check)
            : base(id, category, title, description)
        {
            _applyAction = apply;
            _revertAction = revert;
            _checkAction = check;
        }

        public override (bool Success, string Message) Apply()
        {
            try 
            {
                bool res = _applyAction();
                if (!res) return (false, "Ação retornou falha.");
                
                CheckStatus();
                if (IsOptimized) return (true, "Tweak aplicado com sucesso.");
                return (false, "Ação concluída, mas a verificação de status falhou.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro na execução: {ex.Message}");
            }
        }

        public override (bool Success, string Message) Revert()
        {
            try
            {
                bool res = _revertAction();
                if (!res) return (false, "Ação de restauração falhou.");
                
                CheckStatus();
                if (Status == TweakStatus.Default) return (true, "Tweak restaurado com sucesso.");
                return (false, "Ação concluída, mas status não retornou ao padrão.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro na restauração: {ex.Message}");
            }
        }

        public override void CheckStatus()
        {
            try
            {
                if (_checkAction()) Status = TweakStatus.Optimized;
                else Status = TweakStatus.Default;
            }
            catch
            {
                Status = TweakStatus.Unknown;
            }
        }
    }
}
