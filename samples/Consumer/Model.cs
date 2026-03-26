using Legendary.Lexer.Annotations;

namespace ConsumerApp
{
    [Token(@"\w+", IsSkip = false, Priority = 1)]
    public class Identifier { }

    [Token(@"[0-9]+(\.[0-9]+)?", IsSkip = false, Priority = 1)]
    public class Number { }

    [Token(@"\s+", IsSkip = true, Priority = 0)]
    public class Whitespace { }

    [Lexer(typeof(Identifier), typeof(Number), typeof(Whitespace))]
    public partial class MyLexer
    {
        // The generator will create a Tokenize method and TokenType enum
    }
}
