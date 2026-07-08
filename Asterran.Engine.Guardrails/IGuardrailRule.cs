using System.Collections.Generic;

namespace Asterran.Engine.Guardrails
{
    public class GuardrailResult
    {
        public bool IsViolated { get; set; }
        public string? Message { get; set; }
        public string? RuleName { get; set; }
    }

    public interface IGuardrailRule
    {
        string Name { get; }
        GuardrailResult Evaluate(List<GenericToken> addedTokens, List<GenericToken> allTokens);
    }
}
