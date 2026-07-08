namespace Asterran.Engine.Guardrails
{
    public enum TokenType
    {
        Identifier,
        StringLiteral,
        NumericLiteral,
        Operator,
        Keyword,
        Comment,
        Other
    }

    public class GenericToken
    {
        public TokenType Type { get; set; }
        public string Value { get; set; }
        public int LineNumber { get; set; }

        public override string ToString() => $"[{Type}] {Value} (Line {LineNumber})";
    }
}
