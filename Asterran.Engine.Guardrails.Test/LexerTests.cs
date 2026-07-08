using Xunit;
using Asterran.Engine.Guardrails;

namespace Asterran.Engine.Guardrails.Test
{
    public class LexerTests
    {
        [Fact]
        public void CSharpLexer_TokenizesCorrectly()
        {
            var lexer = new CSharpLexer();
            string code = "string key = \"secret\"; // C# comment";
            
            var tokens = lexer.Tokenize(code);
            
            Assert.NotEmpty(tokens);
            Assert.Contains(tokens, t => t.Type == TokenType.Keyword && t.Value == "string");
            Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Value == "key");
            Assert.Contains(tokens, t => t.Type == TokenType.Operator && t.Value == "=");
            Assert.Contains(tokens, t => t.Type == TokenType.StringLiteral && t.Value == "secret");
            Assert.Contains(tokens, t => t.Type == TokenType.Comment);
        }

        [Fact]
        public void PythonLexer_TokenizesCorrectly()
        {
            var lexer = new PythonLexer();
            string code = "key = 'secret' # Python comment";
            
            var tokens = lexer.Tokenize(code);
            
            Assert.NotEmpty(tokens);
            Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Value == "key");
            Assert.Contains(tokens, t => t.Type == TokenType.Operator && t.Value == "=");
            Assert.Contains(tokens, t => t.Type == TokenType.StringLiteral && t.Value == "secret");
            Assert.Contains(tokens, t => t.Type == TokenType.Comment);
        }

        [Fact]
        public void JsLexer_TokenizesCorrectly()
        {
            var lexer = new JsLexer();
            string code = "const key = `secret`; // JS comment";
            
            var tokens = lexer.Tokenize(code);
            
            Assert.NotEmpty(tokens);
            Assert.Contains(tokens, t => t.Type == TokenType.Keyword && t.Value == "const");
            Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Value == "key");
            Assert.Contains(tokens, t => t.Type == TokenType.Operator && t.Value == "=");
            Assert.Contains(tokens, t => t.Type == TokenType.StringLiteral && t.Value == "secret");
            Assert.Contains(tokens, t => t.Type == TokenType.Comment);
        }
    }
}
