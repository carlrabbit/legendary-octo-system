# Contracts — Generated Lexer Public API

## Tokenize API

- Signature: `LexerResult Tokenize(string input, TokenizeMode mode = TokenizeMode.CollectAll)`
  - Throws: `ArgumentNullException` when `input` is `null`.
  - Behavior: reads the entire `input`, returns `Tokens` and `Errors`.

- Signature: `LexerResult TokenizeFile(string path, TokenizeMode mode = TokenizeMode.CollectAll)`
  - Throws: `ArgumentException` / `IOException` when `path` is invalid or unreadable.
  - Behavior: reads file contents and delegates to `Tokenize(string)`.

## Types
- `enum TokenizeMode { FailFast, CollectAll }`
- `struct Token { TokenType Type; string Text; SourceSpan Span; }`
- `struct SourceSpan { int StartLine; int StartColumn; int Offset; int Length; }`
- `struct TokenizationError { SourceSpan Span; string OffendingText; string Message; }`
- `class LexerResult { IReadOnlyList<Token> Tokens; IReadOnlyList<TokenizationError> Errors; }`

## Diagnostics (compile-time)
- Duplicate `TokenRule` identifiers -> `DiagnosticSeverity.Error` at declaration site.
- Any `TokenRule.Pattern` that can match an empty string -> `DiagnosticSeverity.Error`.
- Grammar with zero token rules -> `DiagnosticSeverity.Warning`.

## Guarantees
- `Tokens` are returned in source order.
- Each token's `Span` allows exact reconstruction via `input.Substring(Offset, Length)`.
- `Keyword` rules win over `Identifier` rules when both match the same input.
