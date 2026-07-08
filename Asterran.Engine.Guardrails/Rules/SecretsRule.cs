using System;
using System.Collections.Generic;

namespace Asterran.Engine.Guardrails.Rules
{
    public class SecretsRule : IGuardrailRule
    {
        public string Name => "Secrets & Credentials Guardrail";

        public GuardrailResult Evaluate(List<GenericToken> addedTokens, List<GenericToken> allTokens)
        {
            string[] secretKeywords = { "password", "apikey", "secret", "credentials", "token", "pwd" };
            
            for (int i = 0; i < addedTokens.Count; i++)
            {
                var token = addedTokens[i];
                if (token.Type == TokenType.Identifier)
                {
                    string idName = token.Value.ToLower();
                    foreach (var keyword in secretKeywords)
                    {
                        if (idName.Contains(keyword))
                        {
                            // Check if the next tokens represent assignment of a string literal
                            if (i + 2 < addedTokens.Count)
                            {
                                var next1 = addedTokens[i + 1];
                                var next2 = addedTokens[i + 2];
                                
                                if (next1.Type == TokenType.Operator && (next1.Value == "=" || next1.Value == ":=") && 
                                    next2.Type == TokenType.StringLiteral)
                                {
                                    if (!string.IsNullOrEmpty(next2.Value) && next2.Value.Length > 4)
                                    {
                                        return new GuardrailResult
                                        {
                                            IsViolated = true,
                                            RuleName = Name,
                                            Message = $"Hardcoded secret warning: Assigning a string literal to identifier '{token.Value}' on line {token.LineNumber}."
                                        };
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return new GuardrailResult { IsViolated = false };
        }
    }
}
