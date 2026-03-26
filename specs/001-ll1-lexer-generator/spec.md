# Feature Specification: LL(1) Lexer Generator

**Feature Branch**: `001-ll1-lexer-generator`  
**Created**: 2026-03-26  
**Status**: Draft  
**Input**: User description: "Create a lexer generator for LL(1) grammars. The lexer splits transforms a multi line string or file into a list of tokens. The lexer is able to trace tokens back to the input text and gives detailed error information upon tokenization errors."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Define Token Rules and Receive a Generated Lexer (Priority: P1)

A .NET developer annotates a partial class with attributes that declare the token rules for their LL(1) grammar (keywords, identifiers, literals, operators, skip rules). On the next build the source generator produces a complete, ready-to-use lexer class — no manual coding, no runtime dependencies.

**Why this priority**: Without a generated lexer there is nothing to test, ship, or extend. Everything else depends on this compile-time code generation step. It is the core deliverable of the library.

**Independent Test**: Can be fully tested by creating a project that references the library, annotating a partial class with at least two token-rule attributes, building the project, and verifying that the generated lexer class compiles and is visible in IntelliSense. Delivers immediate value as a working tokenizer skeleton.

**Acceptance Scenarios**:

1. **Given** a partial class annotated with a grammar attribute and at least one token-rule attribute, **When** the project is built, **Then** a corresponding lexer class is emitted into the compilation with no build errors.
2. **Given** a grammar definition with conflicting or duplicate token-rule declarations, **When** the project is built, **Then** a compile-time diagnostic (warning or error) is emitted at the declaration site and no broken code is generated.
3. **Given** a grammar definition with zero token rules declared, **When** the project is built, **Then** a compile-time warning is emitted indicating the lexer has no rules.

---

### User Story 2 - Tokenize Input and Access Source Position (Priority: P2)

A developer calls the generated lexer with a multi-line string (or a file path) and receives an ordered list of tokens. Each token carries its recognized type, raw text, and exact source position so the developer can map any token back to the original input without additional tracking.

**Why this priority**: Source traceability is the primary differentiator described in the feature request. Without it, the lexer is a bare tokenizer with no diagnostic or IDE-integration value for downstream parser authors.

**Independent Test**: Can be fully tested by calling the generated lexer on a known multi-line input string, iterating the resulting token list, and asserting that each token's reported line, column, and character offset match the expected position in the original string. Delivers a verifiable end-to-end tokenization pipeline.

**Acceptance Scenarios**:

1. **Given** a generated lexer and a multi-line string input, **When** the lexer is invoked, **Then** it returns an ordered list of tokens where each token exposes its type, raw text, start line (1-based), start column (1-based), character offset, and length.
2. **Given** a token in the result list, **When** the developer uses its offset and length to slice the original input string, **Then** the slice equals the token's raw text exactly.
3. **Given** a valid file path, **When** the lexer is invoked with that path, **Then** it reads the file contents and returns the same token-list structure as for a string input.
4. **Given** an empty string input, **When** the lexer is invoked, **Then** it returns an empty token list and no errors.
5. **Given** input with mixed line endings (CRLF, LF, CR), **When** the lexer is invoked, **Then** line and column numbers are correct regardless of the line-ending style used.

---

### User Story 3 - Receive Detailed Tokenization Error Information (Priority: P3)

When the lexer encounters an unrecognized character sequence, it produces a structured error value that identifies the exact source location (line, column, offset), the offending text, and a human-readable message. The developer can choose to fail fast on the first error or collect all errors from a single pass.

**Why this priority**: Detailed error reporting is explicitly required by the feature description and is essential for any tool (IDE, compiler, linter) built on top of the generated lexer.

**Independent Test**: Can be fully tested by passing an input string that contains at least one character not matched by any declared token rule, then asserting that the returned error list is non-empty and contains the correct source position and offending text. Independently demonstrates the error-collection model without requiring a parser.

**Acceptance Scenarios**:

1. **Given** input containing one unrecognized character, **When** the lexer is invoked, **Then** the result contains exactly one error with the correct line, column, offset, the offending character as raw text, and a non-empty message.
2. **Given** input containing multiple unrecognized sequences, **When** the lexer is invoked in error-collecting mode, **Then** all errors are returned in source order and any recognized tokens between errors are still present in the token list.
3. **Given** an unterminated string literal or block comment, **When** the lexer encounters end-of-input before the closing delimiter, **Then** an error is reported at the opening position with a message indicating the unclosed construct.
4. **Given** a tokenization result with errors, **When** the developer inspects an error, **Then** it includes: source span (line, column, offset, length), the offending raw text, and a human-readable diagnostic message.

---

### Edge Cases

- What happens when the input is `null`? (expected: argument exception before tokenization begins)
- How does the lexer handle a file path that does not exist or cannot be read?
- How are keywords distinguished from identifiers when the keyword text also matches the identifier pattern? (priority ordering)
- What is the maximum supported input length?
- How are Unicode characters handled in identifiers and string literals?
- What happens when a token rule's pattern matches an empty string? (expected: compile-time diagnostic)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The library MUST generate a complete lexer class at compile time for each partial class annotated with a grammar-definition attribute.
- **FR-002**: Token rules MUST be declared exclusively via C# attributes applied to the partial class; no base classes, interfaces, or fluent builders are used.
- **FR-003**: The generated lexer MUST accept a multi-line string as input and return an ordered list of tokens.
- **FR-004**: The generated lexer MUST accept a file path as input, read its contents, and return the same ordered token-list structure as for a string input.
- **FR-005**: Each token in the result MUST carry: token type, raw matched text, 1-based start line, 1-based start column, zero-based character offset, and character length.
- **FR-006**: The character offset and length of any token MUST allow exact reconstruction of the token's text by slicing the original input string.
- **FR-007**: The generated lexer MUST support, at minimum, the following token-rule kinds: keyword (exact string match), identifier (pattern-based), integer literal, floating-point literal, string literal (with configurable delimiter and escape), operator/punctuation, and skip rule (whitespace, comments).
- **FR-008**: When multiple token rules could match at the current input position, the rule with the highest declared priority MUST win. Keywords MUST take precedence over identifiers when their pattern would also match.
- **FR-009**: When the lexer encounters an unrecognized character sequence, it MUST produce a structured error value containing: source span (line, column, offset, length), the offending raw text, and a human-readable diagnostic message.
- **FR-010**: The developer MUST be able to choose between fail-fast mode (stop at first error) and error-collecting mode (gather all errors in a single pass while continuing to tokenize).
- **FR-011**: The source generator MUST emit compile-time diagnostics (IDE-visible errors or warnings) for: duplicate token-rule identifiers, token patterns that can match an empty string, and grammar definitions with no token rules.
- **FR-012**: The generated lexer MUST have zero runtime NuGet dependencies.
- **FR-013**: A generated token-type enum MUST be emitted alongside each lexer class, with one member per declared token rule plus a synthetic `EndOfInput` member.

### Key Entities

- **Grammar Definition**: A developer-annotated partial class that declares the full set of token rules for one LL(1) grammar. It is the input artifact consumed by the source generator.
- **Token Rule**: A named rule declared via attribute specifying the pattern kind (keyword, identifier, literal, skip, etc.), its match pattern, and its priority. One rule maps to one enum member.
- **Token**: The output unit of lexing — carries token type (enum value), raw matched text, and a Source Span. Immutable value type.
- **Source Span**: Positional metadata attached to every token and error — contains start line (1-based), start column (1-based), character offset (0-based), and character length.
- **Tokenization Error**: A structured error value produced when no rule matches the current input position — contains a Source Span, the offending raw text, and a human-readable message.
- **Lexer Result**: The aggregate output of a tokenization call — an ordered token list and a (possibly empty) error list, both in source order.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can produce a working generated lexer that compiles successfully with fewer than 10 lines of attribute declarations on a partial class.
- **SC-002**: Every token in the output list can be mapped back to its exact character range in the original input by using only the token's offset and length fields — verified by round-trip slice equality.
- **SC-003**: Tokenization errors contain enough information (line, column, offending text, message) that a developer can identify and locate the problematic input without any additional tooling or debugging.
- **SC-004**: The generated lexer processes a 100,000-character input in under 500 ms on standard developer hardware.
- **SC-005**: Malformed token-rule attribute declarations are surfaced as IDE diagnostics at the declaration site before the project is compiled or run.
- **SC-006**: All token-rule conflict resolution (keyword vs. identifier precedence, priority ordering) is deterministic and documented, producing the same token list for the same input on every invocation.

## Assumptions

- Consumers are .NET 10 developers building parsers, compilers, or linters for custom domain-specific languages.
- The LL(1) constraint governs the parser layer only; the lexer operates on a regular language (finite automaton rules) and has no look-ahead beyond what is needed to distinguish token boundaries.
- Whitespace and comment handling is configured per grammar via skip-rule attributes; the library does not impose a default skip policy.
- Line endings (CRLF, LF, CR) are all recognized as line terminators; line-number counting is correct regardless of which style is present in the input.
- File tokenization reads the entire file into memory before lexing; streaming / incremental tokenization is out of scope for v1.
- Unicode letters and digits are valid identifier characters by default; the exact Unicode category set is configurable via attribute parameters.
- The library targets the Roslyn incremental source-generator API (`IIncrementalGenerator`) and requires no ANTLR, YACC, or other external grammar toolkit.
