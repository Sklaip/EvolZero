using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core
{
    public interface ISemanticTokensConsumer
    {
        public void Push(PositionInSources pos, SemanticTokenType tokenType);
    }

    public enum SemanticTokenType
    {
        Comment,
        Keyword,
        String,
        Number,
        Regexp,
        Operator,
        Namespace,
        Type,
        Struct,
        Class,
        Interface,
        Enum,
        TypeParameter,
        Function,
        Method,
        Property,
        Macro,
        Variable,
        Parameter,
        Label,
        Modifier,
        Event,
        EnumMember,
        Field,
        Punctuation
    }
}
