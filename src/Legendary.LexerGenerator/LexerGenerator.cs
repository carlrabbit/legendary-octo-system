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
    public class LexerSourceGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var compilation = context.Compilation;

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

                    // Get token types from attribute constructor args
                    var tokenTypes = new List<INamedTypeSymbol>();
                    foreach (var arg in lex.ConstructorArguments)
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

                    if (!tokenTypes.Any())
                    {
                        // emit diagnostic
                        var diag = Diagnostic.Create(new DiagnosticDescriptor("LLG001", "No tokens", "Lexer has no tokens", "lex", DiagnosticSeverity.Warning, true), @class.Identifier.GetLocation());
                        context.ReportDiagnostic(diag);
                        continue;
                    }

                    var tokenInfos = new List<(string Name, string Pattern, bool IsSkip, int Priority)>();
                    foreach (var tt in tokenTypes)
                    {
                        // find TokenAttribute on the token type
                        var tattr = tt.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, tokenAttr));
                        if (tattr == null)
                        {
                            // emit diagnostic
                            continue;
                        }
                        var pattern = tattr.ConstructorArguments.FirstOrDefault().Value as string ?? string.Empty;
                        bool isSkip = false; int pr = 0;
                        foreach (var named in tattr.NamedArguments)
                        {
                            if (named.Key == "IsSkip" && named.Value.Value is bool b) isSkip = b;
                            if (named.Key == "Priority" && named.Value.Value is int pi) pr = pi;
                        }
                        tokenInfos.Add((tt.Name, pattern, isSkip, pr));
                    }

                    // Generate lexer source
                    var source = GenerateLexer(symbol, tokenInfos);
                    context.AddSource(symbol.Name + ".Lexer.g.cs", SourceText.From(source, Encoding.UTF8));
                }
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // No initialization needed for simple generator
        }

        private string Escape(string s) => s.Replace("\"", "\\\"");

        private string GenerateLexer(INamedTypeSymbol targetClass, List<(string Name, string Pattern, bool IsSkip, int Priority)> tokens)
        {
            var ns = targetClass.ContainingNamespace.IsGlobalNamespace ? "" : "namespace " + targetClass.ContainingNamespace.ToDisplayString() + "\n{\n";
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(ns)) sb.Append(ns);

            sb.Append($"    public partial class {targetClass.Name} \n    {{\n");

            // Token enum
            sb.Append("        public enum TokenType { ");
            foreach (var t in tokens) sb.Append(t.Name).Append(", ");
            sb.Append("EndOfInput }");
            sb.Append("\n\n");

            // SourceSpan and Token types
            sb.Append(@"        public readonly struct SourceSpan { public readonly int Line; public readonly int Column; public readonly int Offset; public readonly int Length; public SourceSpan(int line,int col,int off,int len){Line=line;Column=col;Offset=off;Length=len;} }

        public readonly struct Token { public readonly TokenType Type; public readonly string Text; public readonly SourceSpan Span; public Token(TokenType t,string text,SourceSpan s){Type=t;Text=text;Span=s;} }

        public readonly struct TokenizationError { public readonly SourceSpan Span; public readonly string Text; public readonly string Message; public TokenizationError(SourceSpan s,string text,string m){Span=s;Text=text;Message=m;} }

        public readonly struct LexerResult { public readonly Token[] Tokens; public readonly TokenizationError[] Errors; public LexerResult(Token[] tokens, TokenizationError[] errors){Tokens=tokens;Errors=errors;} }

");

            // Generate Tokenize method using simple regex-based scanning
            sb.Append("        public LexerResult Tokenize(string input, bool collectAllErrors = false)\n        {\n");
            sb.Append("            if (input is null) throw new System.ArgumentNullException(nameof(input));\n");
            sb.Append("            var tokens = new System.Collections.Generic.List<Token>();\n            var errors = new System.Collections.Generic.List<TokenizationError>();\n            int pos = 0; int line=1; int col=1;\n            var src = input; int n = src.Length;\n\n");

            // Precompile patterns in generated code
            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                var pat = Escape(t.Pattern);
                sb.Append($"            System.Text.RegularExpressions.Regex _r{i} = new System.Text.RegularExpressions.Regex(@\"{pat}\", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);\n");
            }

            sb.Append("\n            while (pos < n)\n            {\n                var slice = src.Substring(pos);\n                System.Text.RegularExpressions.Match bestMatch = null; int bestIndex = -1;\n");

            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                sb.Append($"                var m{i} = _r{i}.Match(slice);\n                if (m{i}.Success && m{i}.Index == 0) {{ if (bestMatch==null || m{i}.Length > bestMatch.Length) {{ bestMatch = m{i}; bestIndex = {i}; }} }}\n");
            }

            sb.Append("                if (bestMatch == null)\n                {\n                    // no rule matched\n                    int errLen = System.Math.Min(1, slice.Length);\n                    var text = slice.Substring(0, errLen);\n                    var span = new SourceSpan(line, col, pos, errLen);\n                    errors.Add(new TokenizationError(span, text, \"Unrecognized input: '\" + text + \"'\"));\n                    if (!collectAllErrors) return new LexerResult(tokens.ToArray(), errors.ToArray());\n                    // advance by one\n                    if (text == \"\\n\") { line++; col = 1; } else { col++; }\n                    pos += errLen; continue;\n                }\n                // matched a token\n                switch (bestIndex)\n                {\n");

            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.IsSkip)
                {
                    sb.Append($"                    case {i}: {{ var len = bestMatch.Length; var txt = bestMatch.Value; for(int k=0;k<txt.Length;k++) {{ if (txt[k]=='\\n') {{ line++; col = 1; }} else col++; }} pos += len; break; }}\n");
                }
                else
                {
                    sb.Append($"                    case {i}: {{ var len = bestMatch.Length; var txt = bestMatch.Value; var span = new SourceSpan(line, col, pos, len); tokens.Add(new Token(TokenType.{tokens[i].Name}, txt, span)); for(int k=0;k<txt.Length;k++) {{ if (txt[k]=='\\n') {{ line++; col = 1; }} else col++; }} pos += len; break; }}\n");
                }
            }

            sb.Append(@"                }
            }

            return new LexerResult(tokens.ToArray(), errors.ToArray());
        }
");

            sb.Append("    }\n");
            if (!string.IsNullOrEmpty(ns)) sb.Append("}\n");
            return sb.ToString();
        }
    }
}
