using Xunit;
using Asterran.Engine.Guardrails;
using Asterran.Engine.Guardrails.Rules;
using System.Collections.Generic;

namespace Asterran.Engine.Guardrails.Test
{
    public class RulesTests
    {
        [Fact]
        public void SecretsRule_DetectsAssignment()
        {
            var rule = new SecretsRule();
            var addedTokens = new List<GenericToken>
            {
                new GenericToken { Type = TokenType.Identifier, Value = "apiKey", LineNumber = 1 },
                new GenericToken { Type = TokenType.Operator, Value = "=", LineNumber = 1 },
                new GenericToken { Type = TokenType.StringLiteral, Value = "secret-token-value-here", LineNumber = 1 }
            };

            var result = rule.Evaluate(addedTokens, addedTokens);
            Assert.True(result.IsViolated);
            Assert.Contains("secret", result.Message);
        }

        [Fact]
        public void SecretsRule_IgnoresShortOrEmptyString()
        {
            var rule = new SecretsRule();
            var addedTokens = new List<GenericToken>
            {
                new GenericToken { Type = TokenType.Identifier, Value = "apiKey", LineNumber = 1 },
                new GenericToken { Type = TokenType.Operator, Value = "=", LineNumber = 1 },
                new GenericToken { Type = TokenType.StringLiteral, Value = "abc", LineNumber = 1 } // too short
            };

            var result = rule.Evaluate(addedTokens, addedTokens);
            Assert.False(result.IsViolated);
        }

        [Fact]
        public void CommandExecutionRule_DetectsShellspawns()
        {
            var rule = new CommandExecutionRule();
            var addedTokens = new List<GenericToken>
            {
                new GenericToken { Type = TokenType.Identifier, Value = "Process.Start", LineNumber = 1 }
            };

            var result = rule.Evaluate(addedTokens, addedTokens);
            Assert.True(result.IsViolated);
            Assert.Contains("Process.Start", result.Message);
        }

        [Fact]
        public void ObsoleteCryptoRule_DetectsMD5()
        {
            var rule = new ObsoleteCryptoRule();
            var addedTokens = new List<GenericToken>
            {
                new GenericToken { Type = TokenType.Identifier, Value = "md5", LineNumber = 1 }
            };

            var result = rule.Evaluate(addedTokens, addedTokens);
            Assert.True(result.IsViolated);
            Assert.Contains("MD5", result.Message);
        }
    }
}
