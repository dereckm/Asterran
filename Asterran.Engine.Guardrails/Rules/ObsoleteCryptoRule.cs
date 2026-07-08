using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Asterran.Engine.Guardrails.Rules
{
    public class ObsoleteCryptoRule : IGuardrailRule
    {
        public string Name => "Obsolete Cryptography Guardrail";

        public GuardrailResult Evaluate(List<GenericToken> addedTokens, List<GenericToken> allTokens)
        {
            var obsoleteAlgs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "MD5",
                "SHA1",
                "RC2",
                "RC4",
                "TripleDES",
                "DES"
            };

            foreach (var token in addedTokens)
            {
                if (token.Type == TokenType.Identifier || token.Type == TokenType.StringLiteral)
                {
                    string val = token.Value;
                    foreach (var alg in obsoleteAlgs)
                    {
                        string pattern = @"\b" + alg + @"\b";
                        if (Regex.IsMatch(val, pattern, RegexOptions.IgnoreCase))
                        {
                            return new GuardrailResult
                            {
                                IsViolated = true,
                                RuleName = Name,
                                Message = $"Obsolete cryptography warning: Found reference to outdated algorithm '{alg}' in token '{token.Value}' on line {token.LineNumber}."
                            };
                        }
                    }
                }
            }

            return new GuardrailResult { IsViolated = false };
        }
    }
}
