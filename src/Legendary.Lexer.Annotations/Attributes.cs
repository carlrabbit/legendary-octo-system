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
        // Optional explicit kind: "Keyword", "Identifier", "Number", "String", "Operator", "Skip", "Regex"
        public string Kind { get; set; }
        // Optional delimiter for string literals (e.g. '"')
        public char Delimiter { get; set; }
        // Optional escape character for string literals (e.g. '\\')
        public char EscapeChar { get; set; }
        public bool IsSkip { get; set; }
        public int Priority { get; set; } = 0;
        public TokenAttribute(string pattern) => Pattern = pattern;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class LexerEntryAttribute : Attribute
    {
    }
}
