using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Asterran.Engine.Guardrails
{
    public class JsLexer : IGenericLexer
    {
        public List<GenericToken> Tokenize(string code)
        {
            var tokens = new List<GenericToken>();
            if (string.IsNullOrEmpty(code)) return tokens;

            code = code.Replace("\r\n", "\n");
            
            // Define JS/TS token patterns
            var pattern = @"(?<Comment>//.*|/\*[\s\S]*?\*/)" +
                          @"|(?<String>`[\s\S]*?`|""[^""\\]*(?:\\.[^""\\]*)*""|'[^'\\]*(?:\\.[^'\\]*)*')" +
                          @"|(?<Number>\b\d+(?:\.\d+)?\b)" +
                          @"|(?<Operator>===|!==|==|!=|<=|>=|\+=|-=|=|\+|-|\*|/)" +
                          @"|(?<Keyword>\b(import|export|require|const|let|var|function|return|class|async|await|if|else|for|while|try|catch|new|null|undefined|true|false)\b)" +
                          @"|(?<Identifier>[a-zA-Z_][a-zA-Z0-9_\.]*)" +
                          @"|(?<Other>[^\s])";

            var regex = new Regex(pattern, RegexOptions.Compiled);
            
            var lineStarts = new List<int> { 0 };
            for (int i = 0; i < code.Length; i++)
            {
                if (code[i] == '\n') lineStarts.Add(i + 1);
            }

            int GetLineNumber(int index)
            {
                int line = lineStarts.BinarySearch(index);
                return line < 0 ? ~line : line + 1;
            }

            foreach (Match match in regex.Matches(code))
            {
                int lineNum = GetLineNumber(match.Index);
                string val = match.Value;

                if (match.Groups["Comment"].Success)
                {
                    tokens.Add(new GenericToken { Type = TokenType.Comment, Value = val, LineNumber = lineNum });
                }
                else if (match.Groups["String"].Success)
                {
                    string stripped = val.Length >= 2 ? val.Substring(1, val.Length - 2) : val;
                    tokens.Add(new GenericToken { Type = TokenType.StringLiteral, Value = stripped, LineNumber = lineNum });
                }
                else if (match.Groups["Number"].Success)
                {
                    tokens.Add(new GenericToken { Type = TokenType.NumericLiteral, Value = val, LineNumber = lineNum });
                }
                else if (match.Groups["Operator"].Success)
                {
                    tokens.Add(new GenericToken { Type = TokenType.Operator, Value = val, LineNumber = lineNum });
                }
                else if (match.Groups["Keyword"].Success)
                {
                    tokens.Add(new GenericToken { Type = TokenType.Keyword, Value = val, LineNumber = lineNum });
                }
                else if (match.Groups["Identifier"].Success)
                {
                    tokens.Add(new GenericToken { Type = TokenType.Identifier, Value = val, LineNumber = lineNum });
                }
                else if (match.Groups["Other"].Success)
                {
                    tokens.Add(new GenericToken { Type = TokenType.Other, Value = val, LineNumber = lineNum });
                }
            }

            return tokens;
        }
    }
}
