# Quickstart — LL(1) Lexer Generator

## Example

1. Define a partial class and declare token rules via attributes:

```csharp
[Lexer("MyLang")]
public partial class MyLangLexer
{
    [Keyword("if", Priority = 100)]
    public static partial void IfToken();

    [Identifier(Priority = 10)]
    public static partial void Identifier();

    [IntegerLiteral(Priority = 20)]
    public static partial void Integer();

    [Skip("\s+")]
    public static partial void Whitespace();
}
```

2. Build the project. The source generator emits `MyLangLexer.Generated.cs` and
   an enum `MyLangTokenType`.

3. Use the generated lexer:

```csharp
var input = File.ReadAllText("example.txt");
var result = MyLangLexer.Tokenize(input, mode: TokenizeMode.CollectAll);

foreach (var token in result.Tokens)
{
    Console.WriteLine($"{token.Type} '{token.Text}' @ {token.Span.StartLine}:{token.Span.StartColumn}");
}

if (result.Errors.Any())
{
    foreach (var err in result.Errors)
        Console.WriteLine($"Error: {err.Message} at {err.Span.StartLine}:{err.Span.StartColumn}");
}
```

4. Notes
- `ArgumentNullException` is thrown for `null` input.
- `EndOfInput` token is always appended at the end of the token stream.
- Keyword precedence and rule priority are deterministic and documented in the plan.
