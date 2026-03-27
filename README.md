# legendary-octo-system
This is a .dotnet 10 library for generating LL(1) parser generators via source generators. 

## Example
```C# 
[Lexer]
public partial class MyLexer {
  
  [LexImpl]  
	 public static partial MyLexer Lex(string input); 
}
```

## Notes for consumers
- Token `Text` type: generated lexers now store token text as `System.ReadOnlyMemory<char>` to enable zero-copy tokenization and reduce allocations. Use `token.TextAsString()` or `new string(token.Text.Span)` when you need a `string`.
- Metadata: generated lexers expose a nested static `TokenMetadata` helper with accessors for `IsSkip`, `Priority`, `Delimiter`, `EscapeChar`, and `Kind` by token type (e.g. `MyLexerTokenMetadata.IsSkip(MyLexerTokenType.Id)`).

If you depend on string-typed token text, update consumers accordingly or call `TextAsString()` for backwards compatibility.
