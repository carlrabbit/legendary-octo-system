using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Legendary.LexerGenerator
{
    [Generator]
    public class LexerSourceGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Use the compilation provider to run generation in the incremental pipeline
            context.RegisterSourceOutput(context.CompilationProvider, (spc, compilation) =>
            {
                var lexerAttr = compilation.GetTypeByMetadataName("Legendary.Lexer.Annotations.LexerAttribute");
                var tokenAttr = compilation.GetTypeByMetadataName("Legendary.Lexer.Annotations.TokenAttribute");
                if (lexerAttr == null || tokenAttr == null) return;

                // Find classes with [Lexer]
                foreach (var tree in compilation.SyntaxTrees)
                {
                    var model = compilation.GetSemanticModel(tree);
                    var root = tree.GetRoot();
                    var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                    foreach (var @class in classes)
                    {
                        var symbol = model.GetDeclaredSymbol(@class) as INamedTypeSymbol;
                        if (symbol == null) continue;
                        var attrs = symbol.GetAttributes();
                        var lex = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, lexerAttr));
                        if (lex == null) continue;

                        // Parse token rules from the Lexer attribute and validate
                        var tokenInfos = ParseTokenInfos(symbol, lex, tokenAttr, spc, @class.Identifier.GetLocation());
                        if (tokenInfos == null || !tokenInfos.Any())
                        {
                            // emit diagnostic for no tokens
                            var diag = Diagnostic.Create(new DiagnosticDescriptor("LLG001", "No tokens", "Lexer has no tokens", "lex", DiagnosticSeverity.Warning, true), @class.Identifier.GetLocation());
                            spc.ReportDiagnostic(diag);
                            continue;
                        }

                        // Generate models source and lexer source
                        var modelsSource = GenerateModels(symbol, tokenInfos);
                        spc.AddSource(symbol.Name + ".Models.g.cs", SourceText.From(modelsSource, Encoding.UTF8));

                        var source = GenerateLexer(symbol, tokenInfos);
                        spc.AddSource(symbol.Name + ".Lexer.g.cs", SourceText.From(source, Encoding.UTF8));
                    }
                }
            });
        }

        private string Escape(string s) => s.Replace("\"", "\\\"");

        private bool IsPlainLiteral(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            char[] meta = new[] { '.', '^', '$', '*', '+', '?', '(', ')', '[', '{', '|', '\\' };
            return pattern.IndexOfAny(meta) == -1;
        }

        private class TokenInfo
        {
            public string Name { get; }
            public string Pattern { get; }
            public bool IsSkip { get; }
            public int Priority { get; }
            public string MatcherKind { get; }
            // Delimiter and EscapeChar are used for string literal matchers when provided via attribute
            public char Delimiter { get; }
            public char EscapeChar { get; }
            public TokenInfo(string name, string pattern, bool isSkip, int priority, string matcherKind, char delimiter = '\0', char escapeChar = '\0')
            {
                Name = name; Pattern = pattern; IsSkip = isSkip; Priority = priority; MatcherKind = matcherKind;
                Delimiter = delimiter; EscapeChar = escapeChar;
            }
        }

        private List<TokenInfo> ParseTokenInfos(INamedTypeSymbol targetClass, AttributeData lexAttr, INamedTypeSymbol tokenAttr, SourceProductionContext context, Location reportLocation)
        {
            var tokenTypes = new List<INamedTypeSymbol>();
            foreach (var arg in lexAttr.ConstructorArguments)
            {
                if (arg.Kind == TypedConstantKind.Array)
                {
                    foreach (var tc in arg.Values)
                    {
                        if (tc.Value is INamedTypeSymbol nts) tokenTypes.Add(nts);
                    }
                }
                else if (arg.Value is INamedTypeSymbol nts)
                {
                    tokenTypes.Add(nts);
                }
            }

            if (!tokenTypes.Any()) return null;

            var tokenInfos = new List<TokenInfo>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tt in tokenTypes)
            {
                if (tt == null) continue;
                var tattr = tt.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, tokenAttr));
                if (tattr == null) continue;
                var pattern = tattr.ConstructorArguments.FirstOrDefault().Value as string ?? string.Empty;
                bool isSkip = false; int pr = 0; string explicitKind = null;
                char delim = '\0'; char esc = '\0';
                foreach (var named in tattr.NamedArguments)
                {
                    if (named.Key == "IsSkip" && named.Value.Value is bool b) isSkip = b;
                    if (named.Key == "Priority" && named.Value.Value is int pi) pr = pi;
                    if (named.Key == "Kind" && named.Value.Value is string ks) explicitKind = ks;
                    if (named.Key == "Kind" && named.Value.Value != null) explicitKind = named.Value.Value.ToString();
                    if (named.Key == "Delimiter" && named.Value.Value is char cd) delim = cd;
                    if (named.Key == "EscapeChar" && named.Value.Value is char ce) esc = ce;
                }

                // Validate pattern non-empty
                if (string.IsNullOrEmpty(pattern))
                {
                    var d = new DiagnosticDescriptor("LLG003", "Empty pattern", $"Token '{tt.Name}' has an empty pattern", "lex", DiagnosticSeverity.Error, true);
                    context.ReportDiagnostic(Diagnostic.Create(d, reportLocation));
                    // continue collecting others
                }
                else
                {
                    // Validate regex pattern: syntactic validity and whether it can match empty string
                    try
                    {
                        var rx = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                        if (rx.IsMatch(string.Empty))
                        {
                            var d = new DiagnosticDescriptor("LLG005", "Pattern matches empty string", $"Token '{tt.Name}' pattern can match the empty string, which is not allowed", "lex", DiagnosticSeverity.Error, true);
                            context.ReportDiagnostic(Diagnostic.Create(d, reportLocation));
                        }
                    }
                    catch (System.Exception ex)
                    {
                        var d = new DiagnosticDescriptor("LLG004", "Invalid pattern", $"Token '{tt.Name}' has invalid pattern: {ex.Message}", "lex", DiagnosticSeverity.Error, true);
                        context.ReportDiagnostic(Diagnostic.Create(d, reportLocation));
                    }
                }

                // Validate duplicate ids
                if (!seen.Add(tt.Name))
                {
                    var d = new DiagnosticDescriptor("LLG002", "Duplicate token id", $"Duplicate token id: '{tt.Name}'", "lex", DiagnosticSeverity.Error, true);
                    context.ReportDiagnostic(Diagnostic.Create(d, reportLocation));
                    continue;
                }

                // Determine matcher kind: prefer explicit `Kind` if provided, otherwise heuristics
                string kind = explicitKind;
                if (string.IsNullOrEmpty(kind))
                {
                    if (IsPlainLiteral(pattern)) kind = "Literal";
                    else if (pattern == "[A-Za-z_][A-Za-z0-9_]*" || pattern == "[a-zA-Z_][a-zA-Z0-9_]*" || pattern == "[A-Za-z_]\\w*" || pattern == "\\w+") kind = "Identifier";
                    else if (pattern == "\\d+" || pattern == "\\d+(?:\\.\\d+)?" || pattern == "\\d+(\\.\\d+)?") kind = "Number";
                    else if (pattern == "\"([^\\\"\\\\]|\\\\.)*\"" || pattern == "\".*\"") kind = "String";
                    else kind = "Regex";
                }

                tokenInfos.Add(new TokenInfo(tt.Name, pattern, isSkip, pr, kind, delim, esc));
            }

            return tokenInfos;
        }

        private string GenerateModels(INamedTypeSymbol targetClass, List<TokenInfo> tokens)
        {
            var ns = targetClass.ContainingNamespace.IsGlobalNamespace ? "" : "namespace " + targetClass.ContainingNamespace.ToDisplayString() + "\n{\n";
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(ns)) sb.Append(ns);

            // Emit uniquely-named types per-lexer to avoid cross-grammar collisions
            var enumName = targetClass.Name + "TokenType";
            var spanName = targetClass.Name + "SourceSpan";
            var tokenName = targetClass.Name + "Token";
            var errorName = targetClass.Name + "TokenizationError";
            var resultName = targetClass.Name + "LexerResult";

            sb.Append($"    public enum {enumName} {{ ");
            foreach (var t in tokens) sb.Append(t.Name).Append(", ");
            sb.Append("EndOfInput }");
            sb.Append("\n\n");

            sb.Append($@"    public readonly struct {spanName} {{ public readonly int Line; public readonly int Column; public readonly int Offset; public readonly int Length; public {spanName}(int line,int col,int off,int len){{Line=line;Column=col;Offset=off;Length=len;}} }}

            public readonly struct {tokenName} {{ public readonly {enumName} Type; public readonly System.ReadOnlyMemory<char> Text; public readonly {spanName} Span; public {tokenName}({enumName} t, System.ReadOnlyMemory<char> text, {spanName} s){{Type=t;Text=text;Span=s;}} public string TextAsString() => new string(Text.Span); }}

    public readonly struct {errorName} {{ public readonly {spanName} Span; public readonly string Text; public readonly string Message; public {errorName}({spanName} s,string text,string m){{Span=s;Text=text;Message=m;}} }}

    public readonly struct {resultName} {{ public readonly {tokenName}[] Tokens; public readonly {errorName}[] Errors; public {resultName}({tokenName}[] tokens, {errorName}[] errors){{Tokens=tokens;Errors=errors;}} }}

");

            sb.Append("    }\n");
            if (!string.IsNullOrEmpty(ns)) sb.Append("}\n");
            return sb.ToString();
        }

        private string GenerateLexer(INamedTypeSymbol targetClass, List<TokenInfo> tokens)
        {
            var ns = targetClass.ContainingNamespace.IsGlobalNamespace ? "" : "namespace " + targetClass.ContainingNamespace.ToDisplayString() + "\n{\n";
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(ns)) sb.Append(ns);

            sb.Append($"    public partial class {targetClass.Name} \n    {{\n");

            // Static cached regex instances for regex token kinds (avoid per-call allocations)
            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.MatcherKind == "Regex")
                {
                    var pat = Escape(t.Pattern);
                    sb.Append($"        private static readonly System.Text.RegularExpressions.Regex __r{i} = new System.Text.RegularExpressions.Regex(@\"{pat}\", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);\n");
                }
            }

            // Generate Tokenize method using simple regex-based scanning
            var enumName = targetClass.Name + "TokenType";
            var spanName = targetClass.Name + "SourceSpan";
            var tokenName = targetClass.Name + "Token";
            var errorName = targetClass.Name + "TokenizationError";
            var resultName = targetClass.Name + "LexerResult";

            sb.Append($"        public {resultName} Tokenize(string input, bool collectAllErrors = false)\n        {{\n");
            sb.Append("            if (input is null) throw new System.ArgumentNullException(nameof(input));\n");
            sb.Append($"            var tokens = new System.Collections.Generic.List<{tokenName}>();\n            var errors = new System.Collections.Generic.List<{errorName}>();\n            int pos = 0; int line=1; int col=1;\n            var src = input; int n = src.Length;\n\n");

            // Precompute whether patterns are literals and compile regexes for non-literals.
            // For string token kinds, also emit delimiter and escape character values driven by attributes.
            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                var pat = Escape(t.Pattern);
                if (t.MatcherKind == "Literal")
                {
                    sb.Append($"            var lit{i} = @\"{pat}\";\n");
                }
                else if (t.MatcherKind == "Regex")
                {
                    // Regex instances are emitted as static cached fields; no per-call allocation here.
                }
                else if (t.MatcherKind == "String")
                {
                    var delimCode = t.Delimiter == '\0' ? (int)('"') : (int)t.Delimiter;
                    var escCode = t.EscapeChar == '\0' ? (int)('\\') : (int)t.EscapeChar;
                    sb.Append($"            char del{i} = (char){delimCode}; char esc{i} = (char){escCode};\n");
                }
                else
                {
                    // no precomputed state for Identifier/Number
                }
            }

            sb.Append("\n            while (pos < n)\n            {\n                int bestLen = -1; int bestIndex = -1;\n\n                for (int i = 0; i < " + tokens.Count + "; i++)\n                {\n");

            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                sb.Append("                    {");
                if (t.MatcherKind == "Literal")
                {
                    sb.Append($" var span = src.AsSpan(pos); if (span.StartsWith(lit{i})) {{ int len = lit{i}.Length; if (len > bestLen) {{ bestLen = len; bestIndex = {i}; }} }} ");
                }
                else if (t.MatcherKind == "Regex")
                {
                    sb.Append($" var m = __r{i}.Match(src, pos); if (m.Success && m.Index == pos) {{ int len = m.Length; if (len > bestLen) {{ bestLen = len; bestIndex = {i}; }} }} ");
                }
                else if (t.MatcherKind == "Identifier")
                {
                    sb.Append(" if (pos < n) { char c = src[pos]; if (char.IsLetter(c) || c == '_') { int j = pos + 1; while (j < n) { char cc = src[j]; if (!(char.IsLetterOrDigit(cc) || cc == '_')) break; j++; } int len = j - pos; if (len > bestLen) { bestLen = len; bestIndex = " + i + "; } } } ");
                }
                else if (t.MatcherKind == "Number")
                {
                    sb.Append(" if (pos < n && char.IsDigit(src[pos])) { int j = pos+1; while (j < n && char.IsDigit(src[j])) j++; if (j < n && src[j]=='.') { int k = j+1; while (k < n && char.IsDigit(src[k])) k++; j = k; } int len = j - pos; if (len > bestLen) { bestLen = len; bestIndex = " + i + "; } } ");
                }
                else if (t.MatcherKind == "String")
                {
                    sb.Append($" if (pos < n && src[pos]==del{i}) {{ int j = pos+1; bool esc=false; while (j < n) {{ char cc = src[j]; if (esc) {{ esc = false; j++; continue; }} if (cc==esc{i}) {{ esc = true; j++; continue; }} if (cc==del{i}) {{ j++; break; }} j++; }} if (j>pos) {{ int len = j-pos; if (len > bestLen) {{ bestLen = len; bestIndex = {i}; }} }} }} ");
                }
                sb.Append(" }");
                sb.Append("\n");
            }

            sb.Append("                }\n\n                if (bestIndex == -1)\n                {\n                    // no rule matched\n                    int errLen = System.Math.Min(1, n - pos);\n                    string text = string.Create(errLen, (src, pos, errLen), (span, st) => st.Item1.AsSpan(st.Item2, st.Item3).CopyTo(span));\n                    var span = new " + targetClass.Name + "SourceSpan(line, col, pos, errLen);\n                    errors.Add(new " + targetClass.Name + "TokenizationError(span, text, \"Unrecognized input: '\" + text + \"'\"));\n                    if (!collectAllErrors) return new " + targetClass.Name + "LexerResult(tokens.ToArray(), errors.ToArray());\n                    if (text == \"\\n\") { line++; col = 1; } else { col++; }\n                    pos += errLen; continue;\n                }\n                // matched a token\n                switch (bestIndex)\n                {\n");

            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                sb.Append($"                    case {i}: {{\n");
                // Extraction per matcher kind
                if (t.MatcherKind == "Literal")
                {
                    sb.Append($"                        int len = lit{i}.Length; var txt = src.AsMemory(pos, len);\n");
                }
                else if (t.MatcherKind == "Regex")
                {
                    sb.Append($"                        var m = __r{i}.Match(src, pos); int len = m.Length; var txt = src.AsMemory(pos, len);\n");
                }
                else if (t.MatcherKind == "Identifier")
                {
                    sb.Append($"                        int j = pos + 1; while (j < n) {{ char cc = src[j]; if (!(char.IsLetterOrDigit(cc) || cc == '_')) break; j++; }} int len = j - pos; var txt = src.AsMemory(pos, len);\n");
                }
                else if (t.MatcherKind == "Number")
                {
                    sb.Append($"                        int j = pos + 1; while (j < n && char.IsDigit(src[j])) j++; if (j < n && src[j] == '.') {{ int k = j + 1; while (k < n && char.IsDigit(src[k])) k++; j = k; }} int len = j - pos; var txt = src.AsMemory(pos, len);\n");
                }
                else if (t.MatcherKind == "String")
                {
                    sb.Append($"                        int j = pos + 1; bool escflag = false; while (j < n) {{ char cc = src[j]; if (escflag) {{ escflag = false; j++; continue; }} if (cc == esc{i}) {{ escflag = true; j++; continue; }} if (cc == del{i}) {{ j++; break; }} j++; }} int len = j - pos; var txt = src.AsMemory(pos, len);\n");
                }
                else
                {
                    sb.Append($"                        // Unknown matcher kind; treat as single char\n                        var txt = src.AsMemory(pos, 1); int len = 1;\n");
                }

                // If token is not skip, add to tokens; otherwise just advance
                if (!t.IsSkip)
                {
                    sb.Append($"                        var span = new {targetClass.Name}SourceSpan(line, col, pos, len); tokens.Add(new {targetClass.Name}Token({targetClass.Name}TokenType.{t.Name}, txt, span));\n");
                }

                sb.Append($"                        for (int k = 0; k < len; k++) {{ if (txt.Span[k] == '\\n') {{ line++; col = 1; }} else {{ col++; }} }}\n");
                sb.Append($"                        pos += len; break; }}\n");
            }

            sb.Append($@"                }}
            }}

            return new {targetClass.Name}LexerResult(tokens.ToArray(), errors.ToArray());
        }}
");

            sb.Append($"        public {targetClass.Name}LexerResult TokenizeFile(string path, bool collectAllErrors = false)\n        {{\n            if (path is null) throw new System.ArgumentNullException(nameof(path));\n            var text = System.IO.File.ReadAllText(path);\n            return Tokenize(text, collectAllErrors);\n        }}\n\n");

            // Emit token metadata as a nested static class so consumers can query attribute-driven values
            sb.Append($"        public static class {targetClass.Name}TokenMetadata\n        {{\n");

            // IsSkip
            sb.Append("            public static readonly bool[] IsSkipByType = new bool[] { ");
            foreach (var t in tokens) sb.Append(t.IsSkip ? "true, " : "false, ");
            sb.Append("false };\n");

            // Priority
            sb.Append("            public static readonly int[] PriorityByType = new int[] { ");
            foreach (var t in tokens) sb.Append(t.Priority.ToString()).Append(", ");
            sb.Append("0 };\n");

            // Delimiter
            sb.Append("            public static readonly char[] DelimiterByType = new char[] { ");
            foreach (var t in tokens)
            {
                var ch = t.Delimiter == '\0' ? '\0' : t.Delimiter;
                var lit = ch == '\\' ? "'\\\\'" : "'" + ch.ToString().Replace("'", "\\'") + "'";
                sb.Append(lit).Append(", ");
            }
            sb.Append("'\\0' };\n");

            // EscapeChar
            sb.Append("            public static readonly char[] EscapeByType = new char[] { ");
            foreach (var t in tokens)
            {
                var ch = t.EscapeChar == '\0' ? '\0' : t.EscapeChar;
                var lit = ch == '\\' ? "'\\\\'" : "'" + ch.ToString().Replace("'", "\\'") + "'";
                sb.Append(lit).Append(", ");
            }
            sb.Append("'\\0' };\n");

            // Kind
            sb.Append("            public static readonly string[] KindByType = new string[] { ");
            foreach (var t in tokens) sb.Append("\"" + t.MatcherKind + "\", ");
            sb.Append("\"EndOfInput\" };\n\n");

            sb.Append($"            public static bool IsSkip({enumName} t) => IsSkipByType[(int)t];\n");
            sb.Append($"            public static int Priority({enumName} t) => PriorityByType[(int)t];\n");
            sb.Append($"            public static char Delimiter({enumName} t) => DelimiterByType[(int)t];\n");
            sb.Append($"            public static char EscapeChar({enumName} t) => EscapeByType[(int)t];\n");
            sb.Append($"            public static string Kind({enumName} t) => KindByType[(int)t];\n");

            sb.Append("        }\n\n");

            sb.Append("    }\n");
            if (!string.IsNullOrEmpty(ns)) sb.Append("}\n");
            return sb.ToString();
        }
    }
}
