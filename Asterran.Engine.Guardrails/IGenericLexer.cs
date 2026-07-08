using System.Collections.Generic;

namespace Asterran.Engine.Guardrails
{
    public interface IGenericLexer
    {
        List<GenericToken> Tokenize(string code);
    }
}
