using System;
using System.Collections.Generic;

namespace Asterran.Engine.Guardrails.Rules
{
    public class CommandExecutionRule : IGuardrailRule
    {
        public string Name => "Command Execution Guardrail";

        public GuardrailResult Evaluate(List<GenericToken> addedTokens, List<GenericToken> allTokens)
        {
            var dangerousProcessIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Process.Start",
                "ProcessStartInfo",
                "subprocess.run",
                "subprocess.Popen",
                "subprocess.call",
                "os.system",
                "child_process.exec",
                "child_process.spawn",
                "child_process.execSync",
                "exec.Command"
            };

            foreach (var token in addedTokens)
            {
                if (token.Type == TokenType.Identifier)
                {
                    if (dangerousProcessIdentifiers.Contains(token.Value))
                    {
                        return new GuardrailResult
                        {
                            IsViolated = true,
                            RuleName = Name,
                            Message = $"System Command Execution Warning: Calling process spawn identifier '{token.Value}' on line {token.LineNumber}."
                        };
                    }
                }
            }

            return new GuardrailResult { IsViolated = false };
        }
    }
}
