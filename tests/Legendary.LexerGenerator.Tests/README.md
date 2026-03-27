# Legendary.LexerGenerator.Tests

Run tests from the repository root:

```bash
dotnet test tests/Legendary.LexerGenerator.Tests -v minimal
```

This project contains a failing snapshot test that asserts the generated lexer
source. Implement the source generator and update the expected snapshots to
make the tests pass.
Run the snapshot tests (from repository root):

```bash
dotnet test tests/Legendary.LexerGenerator.Tests --verbosity normal
```

Notes:
- The test project uses xUnit and Verify.Xunit.
- The snapshot test `MinimalGrammar_GeneratesExpected` is intentionally failing ("Red") until the source generator emits matching generated code.
- After the generator is implemented, run `dotnet test` and accept or update snapshots via Verify's workflow if needed.
