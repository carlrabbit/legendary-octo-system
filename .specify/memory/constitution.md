<!--
## Sync Impact Report
- **Version change**: (none / template) → 1.0.0
- **Modified principles**: N/A — initial ratification
- **Added sections**: Core Principles (I–V), Technology Stack & Constraints,
  Development Workflow, Governance
- **Removed sections**: None
- **Templates updated**:
  - ✅ `.specify/templates/plan-template.md` — Constitution Check gates filled;
    Technical Context defaults updated for C#/.NET 10
  - ✅ `.specify/templates/tasks-template.md` — Path Conventions updated for
    .NET project structure
  - ✅ `.specify/templates/spec-template.md` — No changes required; template
    is sufficiently generic
- **Deferred TODOs**: None
-->

# Legendary Octo System Constitution

## Core Principles

### I. Source-Generator-First

All lexer and parser code generation MUST occur at compile-time via Roslyn source
generators. Runtime code generation is strictly prohibited. Generated output MUST be
deterministic, reproducible, and inspectable in the build output directory.

**Rationale**: Compile-time generation eliminates runtime overhead, enables full IDE
tooling (IntelliSense, navigation, refactoring) over generated types, and makes the
library safe for AOT compilation and assembly trimming scenarios.

### II. Grammar Modularity

Each supported grammar MUST be defined as a self-contained unit composed of exactly
two components: a lexer specification and a parser specification. Lexer and parser
components MUST NOT share internal mutable state or bleed implementation details
across their boundary. New grammars MUST be addable without modifying the
implementation of any existing grammar.

**Rationale**: Clean lexer/parser separation allows independent testing and evolution
of each grammar, and lets consumers reference only the grammar components they need.

### III. Attribute-Driven Public API

The entire public API surface MUST be expressed through C# attributes (e.g., `[Lexer]`,
`[LexImpl]`). Consumers MUST NOT be required to inherit from a base class, implement an
interface, or use a fluent builder for baseline usage. Generated code MUST be
human-readable, consistently formatted, and debuggable with standard .NET tools.

**Rationale**: Attribute-based APIs minimise consumer boilerplate, pair naturally with
C# `partial` class patterns, and keep the generation contract visible in source control
as plain, diffable text.

### IV. Test-First (NON-NEGOTIABLE)

TDD is mandatory. Tests MUST be authored and confirmed failing before any implementation
code is written (Red). Implementation MUST make tests pass with minimal changes (Green).
Refactoring MUST NOT break passing tests (Refactor).

Source generator output MUST be covered by compilation snapshot tests that assert the
exact generated source text. Any change to generated output that causes snapshot
divergence MUST be treated as a breaking change and bumps the MAJOR version.

**Rationale**: Parser generators are high-correctness components. Snapshot tests provide
a regression net for the generated-code contract and are the primary mechanism for
detecting unintended output changes.

### V. Simplicity & YAGNI

No speculative abstractions are permitted. Every grammar variant, API extension, or
new feature MUST have a documented present use case before implementation begins. The
library MUST carry zero NuGet runtime dependencies beyond the .NET Base Class Library.
Any violation of the simplicity principle MUST be justified and recorded in the
Complexity Tracking table of the relevant feature plan.

**Rationale**: Libraries with minimal dependencies are easier to adopt, audit, and
maintain. Keeping the API surface small and the dependency graph shallow lowers the
barrier to contribution and review.

## Technology Stack & Constraints

- **Target Framework**: .NET 10 (`net10.0`). No downlevel TFM support unless explicitly
  requested and justified.
- **Language**: C# with `<Nullable>enable</Nullable>` and `<LangVersion>latest</LangVersion>`
  enforced in the project file.
- **Source Generator SDK**: `Microsoft.CodeAnalysis.CSharp` (Roslyn). All generators
  MUST implement `IIncrementalGenerator`; the legacy `ISourceGenerator` API MUST NOT
  be used.
- **Distribution**: The library MUST be packaged and distributable as a standalone NuGet
  package. Analyzer/generator assemblies are development-only (`<PrivateAssets>all</PrivateAssets>`).
- **Documentation**: XML documentation comments MUST be present on every public API member.
- **Runtime Dependencies**: Zero — no NuGet packages may be referenced at runtime.
- **Code Coverage**: Core grammar modules MUST maintain ≥ 80% line coverage measured by
  the CI pipeline.

## Development Workflow

- **Branching**: All work MUST occur on feature branches off `main`. Merges require a
  Pull Request with ≥ 1 reviewer approval.
- **Commit Style**: Conventional Commits format is required:
  `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`.
- **Versioning**: Semantic Versioning (MAJOR.MINOR.PATCH).
  - MAJOR: Any breaking change to the public attribute API or to generated code structure.
  - MINOR: New grammar support, new attribute, or backwards-compatible API addition.
  - PATCH: Bug fixes, documentation updates, internal refactors with no visible API impact.
- **CI Gate**: All PRs MUST pass build, unit tests, and snapshot tests before merge.
  A failing snapshot MUST NOT be silently updated; the change MUST be explicitly reviewed.
- **Constitution Check**: Every PR review MUST include a compliance verification against
  this constitution. Non-compliant code MUST NOT be merged without an explicit exception
  recorded in the plan's Complexity Tracking table.

## Governance

This constitution supersedes all other project practices and documentation where conflicts
exist. When a conflict is identified, this document governs.

**Amendment Procedure**:
1. Open a PR modifying `.specify/memory/constitution.md`.
2. State the version bump type (MAJOR/MINOR/PATCH) and rationale in the PR description.
3. Obtain ≥ 1 reviewer approval.
4. Propagate amendments to all dependent templates (`plan-template.md`, `spec-template.md`,
   `tasks-template.md`) in the same PR.
5. Update `LAST_AMENDED_DATE` to the merge date.

**Versioning Policy**: Constitution version follows the same SemVer rules applied to the
codebase. Principle additions or new sections are MINOR bumps. Wording clarifications or
typo fixes are PATCH bumps. Principle removals or redefinitions are MAJOR bumps.

**Compliance Review**: Compliance MUST be verified at every PR review. Any deferred or
waived compliance item MUST appear in the Complexity Tracking table with justification.

**Version**: 1.0.0 | **Ratified**: 2026-03-26 | **Last Amended**: 2026-03-26
