# Feature Specification: LL(1) Parser Generator

**Feature Branch**: `002-ll1-parser-generator`  
**Created**: 2026-03-28  
**Status**: Draft  
**Input**: User description: "Create a parser generator for LL(1) grammars. Use the already implemented lexer by extending it to support token streaming."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate Parser from LL(1) Grammar (Priority: P1)

An engineer provides a grammar (BNF/EBNF) and runs the generator to produce a parser that consumes tokens from the project's lexer stream.

**Why this priority**: Core user value — delivering a working parser from a formal grammar is the primary outcome.

**Independent Test**: Given a valid LL(1) grammar and a suite of sample inputs, the generated parser accepts all valid inputs and rejects invalid ones according to the grammar.

**Acceptance Scenarios**:

1. **Given** a valid LL(1) grammar and corresponding test inputs, **When** the generator runs, **Then** the parser accepts all valid inputs and rejects invalid inputs in the test suite.
2. **Given** an ambiguous or non-LL(1) grammar, **When** the generator runs, **Then** it reports clear conflicts with locations and suggestions (FIRST/FOLLOW info).

---

### User Story 2 - Token Streaming Integration (Priority: P2)

Developers can plug the existing lexer into the parser runtime so the parser consumes a token stream rather than raw text.

**Why this priority**: Enables incremental parsing and reuse of existing lexer investment.

**Independent Test**: Wire the lexer to the parser runtime and parse a streaming input (token-by-token). The parser should parse inputs correctly when tokens arrive incrementally and only request at most one lookahead token.

**Acceptance Scenarios**:

1. **Given** the project's lexer and a grammar, **When** parsing an input via token streaming, **Then** the parser consumes tokens from the lexer stream and completes successfully for valid inputs.
2. **Given** token stream ends early or contains an unexpected token, **When** parsing, **Then** the parser emits a deterministic error with token location and expected symbols.

---

### User Story 3 - Developer Diagnostics & Fix Guidance (Priority: P3)

When a grammar fails LL(1) checks, the tool helps the developer by showing which productions and token sets cause conflicts and suggests common fixes (left-factoring, remove left recursion).

**Why this priority**: Improves developer productivity and reduces time to fix grammars.

**Independent Test**: Run the tool on known problematic grammars and verify the conflict report includes the offending productions and suggested remediation steps.

**Acceptance Scenarios**:

1. **Given** a grammar with FIRST/FIRST or FIRST/FOLLOW conflicts, **When** the generator analyzes it, **Then** it emits a concise report listing conflicting productions and recommended actions.

---

### Edge Cases

- Ambiguous grammars that are not LL(1) (report and explain).  
- Left-recursive grammars (detect and recommend rewriting).  
- Grammars with epsilon (empty) productions where FOLLOW sets interact with FIRST sets.  
- Large grammars (performance: parsing table generation remains responsive for grammars with up to 500 productions).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST accept a grammar written in a common textual format (BNF or EBNF) as input.
- **FR-002**: The system MUST analyze the grammar and compute FIRST and FOLLOW sets for all nonterminals.
- **FR-003**: The system MUST detect LL(1) conflicts (FIRST/FIRST and FIRST/FOLLOW) and produce actionable diagnostics including production locations and conflicting token sets.
- **FR-004**: The system MUST expose an API that consumes a token stream (provided by the existing lexer) and performs LL(1) parsing with single-token lookahead.
- **FR-005**: The system MUST emit a generated parser source (code generation) as the primary v1 output. The generated parser should include parse tables and integration hooks to consume the project's token types from the `TokenStream` interface. A runtime table-interpreter may be considered in a later iteration.
- **FR-006**: The parser runtime or generated parser MUST support incremental parsing from a streaming token source and only request at most one token of lookahead.
- **FR-007**: The system MUST provide runtime error reporting that includes token position (line/column or lexer token index) and a short list of expected symbols.
- **FR-008**: The tool MUST include a test harness to validate generated parsers against positive and negative example inputs.
- **FR-009**: The generator MUST not modify the existing lexer; it should integrate via a stable token-stream interface on the lexer.

### Key Entities

- **Grammar**: The textual rules supplied by the user (productions, terminals, nonterminals, start symbol).  
- **Production**: A single grammar rule (left-hand nonterminal -> sequence of symbols).  
- **Terminal**: Named token produced by the lexer.  
- **NonTerminal**: Grammar symbol expanded by productions.  
- **FIRST/FOLLOW Sets**: Computed sets used for LL(1) checks.  
- **ParseTable**: Mapping (NonTerminal, Terminal) -> production (or action) for the parser runtime.  
- **ParserRuntime**: The component that consumes a `TokenStream` and implements the LL(1) parsing algorithm.  
- **TokenStream**: An abstraction the lexer implements to supply tokens incrementally.  
- **ParseError**: Structured error with token context and expected symbols.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a curated suite of LL(1) grammars and test inputs, generated parsers accept 100% of valid inputs and reject 100% of invalid inputs in their corresponding test suites.
- **SC-002**: The tool identifies and reports LL(1) conflicts with actionable diagnostics for 100% of non-LL(1) grammars in the test corpus.
- **SC-003**: Integration with the existing lexer permits parsing via token streaming with no more than one token of lookahead; developer test harness demonstrates token-by-token parsing across sample inputs.
- **SC-004**: The generator produces diagnostics and a usable parser for grammars of up to 500 productions within 2 seconds on a typical developer machine.

## Assumptions

- The existing lexer in the repository provides token objects with stable symbolic names and token position metadata (line/column or index).  
- Semantic actions and deep AST transformation strategies are out of scope for the initial delivery — the generator focuses on parse-table generation and producing parse nodes suitable for downstream processing.  
- Backward compatibility: the generator will not change the public API of the lexer; it will integrate via a new `TokenStream` adapter.
- Backward compatibility: the generator will not change the public API of the lexer; it will integrate via a new `TokenStream` adapter.
