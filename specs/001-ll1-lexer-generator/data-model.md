# Data Model — LL(1) Lexer Generator

## Entities

- Grammar Definition
  - `Name` (string)
  - `Namespace` (string)
  - `TokenRules` (collection of `TokenRule`)
  - Validation: Must contain >= 1 `TokenRule`

- TokenRule
  - `Id` (string) — unique identifier used for enum member name
  - `Kind` (enum) — Keyword, Identifier, IntegerLiteral, FloatLiteral, StringLiteral, Operator, Skip
  - `Pattern` (string) — literal text or regex-like pattern depending on `Kind`
  - `Priority` (int) — higher wins when multiple rules match
  - `IsSkip` (bool) — whether matched tokens are omitted from result
  - Validation: `Pattern` must not match empty string

- TokenType (enum)
  - One enum member per `TokenRule` plus `EndOfInput`

- Token (immutable value type)
  - `Type` (`TokenType`)
  - `Text` (string)
  - `Span` (`SourceSpan`)

- SourceSpan
  - `StartLine` (int, 1-based)
  - `StartColumn` (int, 1-based)
  - `Offset` (int, 0-based)
  - `Length` (int)

- TokenizationError
  - `Span` (`SourceSpan`)
  - `OffendingText` (string)
  - `Message` (string)

- LexerResult
  - `Tokens` (IReadOnlyList<Token>)
  - `Errors` (IReadOnlyList<TokenizationError>)

## Validation Rules
- No duplicate `TokenRule.Id` values (compile-time diagnostic)
- `Pattern` for any rule must not be able to match empty string (diagnostic)
- `Keyword` patterns are exact-match strings; `Identifier` patterns are pattern-based and configurable for Unicode classes

## State Transitions
- Lexer scan: initial state -> scanning -> (on match) emit token & advance offset -> scanning -> (on EOF) emit `EndOfInput` token
- On error in `FailFast` mode: produce single `TokenizationError` and stop
- On error in `CollectAll` mode: record error & advance one code point before continuing
