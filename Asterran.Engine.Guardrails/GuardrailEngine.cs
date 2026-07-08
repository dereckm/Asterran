using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Asterran.Engine.Guardrails.Rules;

namespace Asterran.Engine.Guardrails
{

    public class GuardrailEngine
    {
        private readonly Dictionary<string, IGenericLexer> _lexers = new Dictionary<string, IGenericLexer>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IGuardrailRule> _rules = new List<IGuardrailRule>();

        public GuardrailEngine()
        {
            // Register Lexers
            var csLexer = new CSharpLexer();
            _lexers[".cs"] = csLexer;

            var pyLexer = new PythonLexer();
            _lexers[".py"] = pyLexer;

            var jsLexer = new JsLexer();
            _lexers[".js"] = jsLexer;
            _lexers[".ts"] = jsLexer;
            _lexers[".jsx"] = jsLexer;
            _lexers[".tsx"] = jsLexer;

            // Register Rules
            _rules.Add(new SecretsRule());
            _rules.Add(new CommandExecutionRule());
            _rules.Add(new ObsoleteCryptoRule());
        }

        public GuardrailResult Evaluate(string filePath, string newContent, string addedCode)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            if (!_lexers.ContainsKey(ext))
            {
                // No lexer registered for this file extension, skip checks
                return new GuardrailResult { IsViolated = false };
            }

            try
            {
                var lexer = _lexers[ext];
                
                // 1. Tokenize entire new file
                var allTokens = lexer.Tokenize(newContent);
                
                // 2. Tokenize added lines
                var addedTokens = lexer.Tokenize(addedCode);

                // 3. Evaluate rules
                foreach (var rule in _rules)
                {
                    var result = rule.Evaluate(addedTokens, allTokens);
                    if (result.IsViolated)
                    {
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running guardrail engine on {filePath}: {ex.Message}");
            }

            return new GuardrailResult { IsViolated = false };
        }
    }
}
