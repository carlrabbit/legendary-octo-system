# Tasks — Implementation (Phase 2)

Priority-ordered, dependency-aware tasks to implement `001-ll1-lexer-generator`.

1. Add failing snapshot tests (Red)
   - Create snapshot tests that assert the exact generated source for a minimal grammar
   - Test: one partial class with two token-rule attributes
   - Framework: xUnit + Verify
   - Status: not-started

2. Implement attribute parsing helpers
   - Parse `TokenRule` attributes from the compilation using Roslyn APIs
   - Validate required fields (Id, Kind, Pattern)
   - Emit compile-time diagnostics for duplicates and empty-match capable patterns
   - Status: not-started

3. Implement rule validation & diagnostics
   - Duplicate `TokenRule.Id` -> Diagnostic(Error)
   - Pattern that can match empty string -> Diagnostic(Error)
   - No token rules -> Diagnostic(Warning)
   - Add unit tests for each diagnostic case
   - Status: not-started

4. Emit `TokenType` enum and supporting models
   - Generate enum with one member per rule + `EndOfInput`
   - Generate `Token`, `SourceSpan`, `TokenizationError`, `LexerResult` types as internal/public as specified
   - Status: not-started

5. Generate lexer scanning code skeleton
   - Emit a deterministic single-pass scanner that consults per-rule matchers
   - Use prioritization table: explicit `Priority` then `Kind` (keywords before identifiers)
   - Use `ReadOnlySpan<char>`-oriented APIs in generated code where practical to avoid allocations
   - Status: not-started

6. Implement token matchers in generated code
   - Keyword: exact string compare
   - Identifier/Literal: pattern-based matcher (simple character-class loops; configurable Unicode categories)
   - String literal: delimiter-aware, escape support, unterminated detection
   - Skip rules: do not add tokens to `Tokens` list
   - Status: not-started

7. Implement position tracking & source span correctness
   - Track line/column/offset correctly across CR, LF, CRLF
   - Ensure `Offset` and `Length` allow `input.Substring(Offset, Length)` equality to `token.Text`
   - Add unit tests for mixed line endings and multi-line tokens
   - Status: not-started

8. Implement error modes (FailFast / CollectAll)
   - FailFast: stop on first unmatched sequence
   - CollectAll: record errors and advance one Unicode scalar before continuing
   - Add tests covering unterminated string literal and multiple unrecognized sequences
   - Status: not-started

9. Implement `TokenizeFile(string path, ...)` API
   - Validate path, propagate IO exceptions appropriately, delegate to `Tokenize(string)`
   - Add tests using temporary files
   - Status: not-started

10. Add performance and allocation-conscious refinements
    - Replace heavy string allocations with `ReadOnlySpan<char>` and `string.Create` where needed
    - Micro-benchmarks for 100k input; ensure target performance is reasonable
    - Status: not-started

11. Add snapshot tests and make generator pass (Green)
    - Run snapshot tests and iterate on generated output until matches expected files
    - Status: not-started

12. Documentation & Quickstart
    - Ensure `quickstart.md` examples compile and run
    - Update README.md with usage and migration notes
    - Status: not-started

13. CI & Release preparation
    - Ensure `dotnet test` and snapshot verification run in CI
    - Add publish pipeline placeholders for NuGet packaging
    - Status: not-started

14. PR and review
    - Open PR against `main` with feature branch `001-ll1-lexer-generator`
    - Add reviewers, link spec and plan artifacts
    - Status: not-started

Notes
- Keep generated code dependency-free and idiomatic C# (nullable enabled).
- Follow Constitution gates: write snapshot tests before implementation, and justify any deviations in Complexity Tracking.
