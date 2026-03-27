# Research — LL(1) Lexer Generator

## Decision: Performance Goals
- Chosen: Target processing of a 100,000-character input in under 500 ms on standard developer hardware for the generated lexer (SC-004). This is a v1 performance goal, not a hard requirement for the generator itself.

### Rationale
- A lexer is typically linear-time over input size when implemented as a deterministic finite automaton (DFA) or a prioritized rule matcher. Meeting 500 ms on 100k chars is achievable with a single-pass DFA-like approach and careful string/span handling.
- Keeping the generator output dependency-free and allocation-conscious (use `ReadOnlySpan<char>` where applicable) provides low-level performance without external libraries.

### Alternatives Considered
- Use of third-party regex engines (e.g., RE2) — rejected because of runtime dependencies (violates Constitution V: Runtime Dependencies = 0).
- Building a complex prioritized backtracking engine — rejected due to avoidable performance unpredictability and implementation complexity.

---

## Decision: Scale / Scope
- Chosen: Support up to 256 distinct token types per grammar in v1; generator should gracefully handle larger grammars but performance characteristics beyond ~512 token types are "BEST EFFORT" and documented.

### Rationale
- 256 token types covers most DSLs and programming languages for first release while keeping the generated enums and match tables compact.
- Larger grammars increase rule-resolution cost; documenting this avoids surprising behavior.

### Alternatives Considered
- No hard cap: simpler but risks large enum/tablestates causing performance regressions.
- Dynamic runtime rule tables: rejected to preserve zero runtime dependencies and simpler generated code.

---

## Decision: Line-ending Handling and Position Tracking
- Chosen: Normalize reading to a single in-memory string, scan characters tracking line and column with canonical newline recognition (CR, LF, CRLF). Tokens carry 1-based line and column plus 0-based offset and length.

### Rationale
- Simplicity and correctness: canonical newline handling guarantees consistent positions for mixed input.
- Matches requirements FR-005 and FR-006.

---

## Decision: Error Handling Mode
- Chosen: Provide both `FailFast` (stop on first error) and `CollectAll` modes as runtime options on the generated lexer API (FR-010).

### Rationale
- Meets acceptance scenarios and developer expectations; simple to implement by branching on mode while scanning.

---

## Decision: Keyword vs Identifier Precedence
- Chosen: Keywords declared via explicit `Keyword` rule types always take precedence over `Identifier` rules; rule priorities are enforced by the declared integer priority value otherwise (higher wins). (FR-008)

### Rationale
- Deterministic resolution model required by SC-006 and FR-008. Explicit precedence reduces surprises.

---

## Decision: Unicode and Identifier Rules
- Chosen: Default identifier character set includes Unicode letter categories and connecting characters; the Unicode set is configurable via attribute parameters.

### Rationale
- Meets assumption and gives flexibility without extra runtime dependencies.

---

## Open Questions / NEEDS CLARIFICATION (if any remain)
- None — performance and scale clarified for Phase 1.
