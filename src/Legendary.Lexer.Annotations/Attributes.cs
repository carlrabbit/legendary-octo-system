using System;

namespace Legendary.Lexer.Annotations
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class LexerAttribute : Attribute
    {
        public Type[] TokenTypes { get; }
        public LexerAttribute(params Type[] tokenTypes) => TokenTypes = tokenTypes;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TokenAttribute : Attribute
    {
        public string Pattern { get; }
        public bool IsSkip { get; set; }
        public int Priority { get; set; } = 0;
        public TokenAttribute(string pattern) => Pattern = pattern;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class LexerEntryAttribute : Attribute
    {
    }
}
