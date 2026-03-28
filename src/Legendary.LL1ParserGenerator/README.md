# Legendary LL(1) Parser Generator (Source Generator)

This project contains a Roslyn source generator that:  
- Discovers `Terminal`, `NonTerminal`, and `Parser` annotations.  
- Computes FIRST/FOLLOW sets and LL(1) predict sets and reports conflicts as diagnostics.  
- Generates partial parser implementations that consume token streams and support `?`, `*`, and `+` operators.  
- Emits a `GrammarRules` dictionary and `LL1Conflicts` list on generated parsers.  
- Provides `ITokenAdapter<TLexerToken>` to adapt lexer token types into `(string Type, string Text)`.

Usage notes
- Annotate token/nonterminal classes with `Terminal`/`NonTerminal` attributes (string rule).  
- Annotate a partial parser class with `Parser` attribute referencing terminal/nonterminal types and mark a method with `[ParserEntry]`.
- The generator will produce parser helpers and diagnostics during compilation.

Example grammar (in attributes):

```csharp
[Terminal("NUMBER : [0-9]+")]
public class NumberToken { }

[NonTerminal("Expr -> Number | Number '+' Expr")]
public class ExprNode { }

[Parser(new[] { typeof(NumberToken) }, new[] { typeof(ExprNode) })]
public partial class SampleParser
{
    [ParserEntry]
    public partial void Parse(string input);
}
```

Runtime example (inspect AST / partial AST):

```csharp
var parser = new MyGeneratedParser();
var tokens = lexer.Tokenize(input).Tokens.Select(t => adapter.Adapt(t));
parser.ParseTokens(tokens);
if (parser.ParseSucceeded)
{
    var root = (Legendary.LL1ParserGenerator.ParseNode)parser.AST;
    Console.WriteLine(root.ToJson()); // pretty-printed JSON of AST
}
else
{
    // See partial AST snapshot to understand where parsing failed
    Console.WriteLine("Parse failed. Partial AST:\n" + parser.ParseErrorJson);
}
```

`ParseErrorJson` contains a JSON snapshot of the partial AST when parsing fails — useful when diagnosing grammar or tokenization issues.
