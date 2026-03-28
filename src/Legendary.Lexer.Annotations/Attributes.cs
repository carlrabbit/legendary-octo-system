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

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TerminalAttribute : Attribute
    {
        public string Rule { get; }
        public TerminalAttribute(string rule) => Rule = rule;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class NonTerminalAttribute : Attribute
    {
        public string Rule { get; }
        public NonTerminalAttribute(string rule) => Rule = rule;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ParserAttribute : Attribute
    {
        public Type[] TerminalTypes { get; }
        public Type[] NonTerminalTypes { get; }
        public ParserAttribute(Type[] terminalTypes, Type[] nonTerminalTypes)
        {
            TerminalTypes = terminalTypes ?? Array.Empty<Type>();
            NonTerminalTypes = nonTerminalTypes ?? Array.Empty<Type>();
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ParserEntryAttribute : Attribute
    {
    }
}
