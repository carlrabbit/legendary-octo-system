using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Legendary.LL1ParserGenerator
{
    [Generator]
    public class ParserSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var compilation = context.Compilation;
            var parserAttr = compilation.GetTypeByMetadataName("Legendary.Lexer.Annotations.ParserAttribute");
            var parserEntryAttr = compilation.GetTypeByMetadataName("Legendary.Lexer.Annotations.ParserEntryAttribute");
            if (parserAttr == null || parserEntryAttr == null) return;

            foreach (var tree in compilation.SyntaxTrees)
            {
                var root = tree.GetRoot();
                var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var cls in classDecls)
                {
                    var model = compilation.GetSemanticModel(tree);
                    var sym = model.GetDeclaredSymbol(cls) as INamedTypeSymbol;
                    if (sym == null) continue;
                    var attrs = sym.GetAttributes();
                    var pAttr = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, parserAttr));
                    if (pAttr == null) continue;

                    var terminalTypes = new List<INamedTypeSymbol>();
                    var nonTerminalTypes = new List<INamedTypeSymbol>();
                    if (pAttr.ConstructorArguments.Length >= 1 && pAttr.ConstructorArguments[0].Kind == TypedConstantKind.Array)
                    {
                        foreach (var tc in pAttr.ConstructorArguments[0].Values)
                            if (tc.Value is INamedTypeSymbol nts) terminalTypes.Add(nts);
                    }
                    if (pAttr.ConstructorArguments.Length >= 2 && pAttr.ConstructorArguments[1].Kind == TypedConstantKind.Array)
                    {
                        foreach (var tc in pAttr.ConstructorArguments[1].Values)
                            if (tc.Value is INamedTypeSymbol nts) nonTerminalTypes.Add(nts);
                    }

                    var grammarRules = new List<(string Name, string Rule)>();
                    var terminalAttrSym = compilation.GetTypeByMetadataName("Legendary.Lexer.Annotations.TerminalAttribute");
                    var tokenAttrSym = compilation.GetTypeByMetadataName("Legendary.Lexer.Annotations.TokenAttribute");
                    var nonTerminalAttrSym = compilation.GetTypeByMetadataName("Legendary.Lexer.Annotations.NonTerminalAttribute");

                    foreach (var tt in terminalTypes)
                    {
                        string rule = string.Empty;
                        var tAttr = tt.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, terminalAttrSym));
                        if (tAttr != null && tAttr.ConstructorArguments.Length >= 1)
                            rule = tAttr.ConstructorArguments[0].Value as string ?? string.Empty;
                        else
                        {
                            var tk = tt.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, tokenAttrSym));
                            if (tk != null && tk.ConstructorArguments.Length >= 1)
                                rule = tk.ConstructorArguments[0].Value as string ?? string.Empty;
                        }
                        grammarRules.Add((tt.Name, rule));
                    }

                    foreach (var nt in nonTerminalTypes)
                    {
                        string rule = string.Empty;
                        var nAttr = nt.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, nonTerminalAttrSym));
                        if (nAttr != null && nAttr.ConstructorArguments.Length >= 1)
                            rule = nAttr.ConstructorArguments[0].Value as string ?? string.Empty;
                        grammarRules.Add((nt.Name, rule));
                    }

                    var productions = new Dictionary<string, List<string[]>>();
                    int groupCounter = 0;

                    void ExpandRhsAndAdd(string lhs, string rhs)
                    {
                        var alts = new List<string>();
                        var cur = new StringBuilder();
                        int depth = 0;
                        foreach (var ch in rhs)
                        {
                            if (ch == '|' && depth == 0)
                            {
                                alts.Add(cur.ToString().Trim()); cur.Clear();
                            }
                            else
                            {
                                if (ch == '(') depth++; else if (ch == ')') depth--;
                                cur.Append(ch);
                            }
                        }
                        if (cur.Length > 0) alts.Add(cur.ToString().Trim());

                        foreach (var alt in alts)
                        {
                            var seq = new List<string>();
                            int i = 0;
                            while (i < alt.Length)
                            {
                                while (i < alt.Length && char.IsWhiteSpace(alt[i])) i++;
                                if (i >= alt.Length) break;
                                if (alt[i] == '(')
                                {
                                    int start = i + 1; int d = 1; i++;
                                    while (i < alt.Length && d > 0)
                                    {
                                        if (alt[i] == '(') d++; else if (alt[i] == ')') d--; i++;
                                    }
                                    var content = alt.Substring(start, i - start - (d == 0 ? 0 : 0));
                                    var gname = "__group" + (groupCounter++).ToString();
                                    ExpandRhsAndAdd(gname, content);
                                    seq.Add(gname);
                                }
                                else
                                {
                                    int j = i; while (j < alt.Length && !char.IsWhiteSpace(alt[j])) j++;
                                    seq.Add(alt.Substring(i, j - i));
                                    i = j;
                                }
                            }
                            if (!productions.ContainsKey(lhs)) productions[lhs] = new List<string[]>();
                            productions[lhs].Add(seq.ToArray());
                        }
                    }

                    foreach (var gr in grammarRules)
                    {
                        var lhs = gr.Name;
                        var rhs = gr.Rule.Contains("->") ? gr.Rule.Split(new[] { "->" }, StringSplitOptions.None)[1].Trim() : gr.Rule.Trim();
                        if (string.IsNullOrEmpty(rhs))
                        {
                            if (!productions.ContainsKey(lhs)) productions[lhs] = new List<string[]>();
                            productions[lhs].Add(new string[0]);
                        }
                        else ExpandRhsAndAdd(lhs, rhs);
                    }

                    var conflicts = new List<string>();

                    var methods = cls.Members.OfType<MethodDeclarationSyntax>();
                    MethodDeclarationSyntax entryMethod = null; IMethodSymbol entryMethodSymbol = null;
                    foreach (var m in methods)
                    {
                        var ms = model.GetDeclaredSymbol(m) as IMethodSymbol; if (ms == null) continue;
                        var mattrs = ms.GetAttributes(); if (mattrs.Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, parserEntryAttr))) { entryMethod = m; entryMethodSymbol = ms; break; }
                    }
                    if (entryMethod == null || entryMethodSymbol == null) continue;
                    if (entryMethodSymbol.Parameters.Length != 1 || entryMethodSymbol.Parameters[0].Type.ToDisplayString() != "string") continue;

                    var ns = sym.ContainingNamespace.IsGlobalNamespace ? null : sym.ContainingNamespace.ToDisplayString();
                    var className = sym.Name;
                    var startSymbol = nonTerminalTypes.Count > 0 ? nonTerminalTypes[0].Name : (nonTerminalTypes.Count == 0 && grammarRules.Count > 0 ? grammarRules[0].Name : null);

                    var gen = new StringBuilder();
                    gen.AppendLine("// <auto-generated/>");
                    gen.AppendLine("using System;");
                    gen.AppendLine("using System.Collections.Generic;");
                    gen.AppendLine();
                    if (ns != null) { gen.AppendLine($"namespace {ns}"); gen.AppendLine("{"); }
                    gen.AppendLine($"    public partial class {className}");
                    gen.AppendLine("    {");
                    gen.AppendLine("        public object AST { get; private set; }");
                    gen.AppendLine("        public bool ParseSucceeded { get; private set; }");
                    gen.AppendLine("        public string ParseErrorJson { get; private set; }");

                    gen.AppendLine("        public System.Collections.Generic.Dictionary<string,string> GrammarRules { get; } = new System.Collections.Generic.Dictionary<string,string> {");
                    foreach (var r in grammarRules)
                    {
                        var esc = (r.Rule ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
                        gen.AppendLine($"            [\"{r.Name}\"] = \"{esc}\", ");
                    }
                    gen.AppendLine("        }; ");

                    gen.AppendLine("        public System.Collections.Generic.List<string> LL1Conflicts { get; } = new System.Collections.Generic.List<string> {");
                    foreach (var c in conflicts) { var esc = c.Replace("\"", "\\\""); gen.AppendLine($"            \"{esc}\","); }
                    gen.AppendLine("        }; ");

                    var methodSig = GetMethodSignature(entryMethodSymbol);
                    gen.AppendLine($"        {methodSig}");
                    gen.AppendLine("        {");
                    gen.AppendLine("            throw new NotImplementedException();");
                    gen.AppendLine("        }");
                    gen.AppendLine();

                    gen.AppendLine("        public void ParseTokens(System.Collections.Generic.IEnumerable<(string Type,string Text)> tokens)");
                    gen.AppendLine("        {");
                    gen.AppendLine("            if (tokens == null) throw new ArgumentNullException(nameof(tokens));");
                    gen.AppendLine("            var en = tokens.GetEnumerator();");
                    gen.AppendLine("            bool moved = en.MoveNext();");
                    gen.AppendLine("            var ctx = new ParserContext(en, moved ? (en.Current.Type, en.Current.Text) : (null, null), moved ? 0 : -1);");
                    gen.AppendLine("            bool ok = false; Legendary.LL1ParserGenerator.ParseNode root = null;");
                    if (startSymbol != null) gen.AppendLine($"            try {{ ok = Parse_{startSymbol}(ctx, out root); }} catch {{ ok = false; root = null; }}"); else gen.AppendLine("            ok = false; root = null;");
                    gen.AppendLine("            ParseSucceeded = ok && !ctx.HasMore();");
                    gen.AppendLine("            AST = ParseSucceeded ? (object)root : null;");
                    gen.AppendLine("            if (ParseSucceeded) { ParseErrorJson = null; } else { AST = root != null ? (object)root : null; ParseErrorJson = root != null ? root.ToJson(true) : \"{\\\"error\\\":\\\"parse failed\\\"}\"; }");
                    gen.AppendLine("        }");
                    gen.AppendLine();

                    gen.AppendLine("        private record ParserContext(System.Collections.Generic.IEnumerator<(string Type, string Text)> Enum, (string Type, string Text) Curr, int Index)");
                    gen.AppendLine("        { public void Advance() { if (Enum.MoveNext()) { Curr = (Enum.Current.Type, Enum.Current.Text); Index++; } else { Curr = (null, null); Index = -1; } } public bool HasMore() => Curr.Type != null; }");
                    gen.AppendLine();

                    gen.Append(GenerateNonTerminalParsers(productions, nonTerminalTypes.Select(n => n.Name).ToArray()));

                    gen.AppendLine("    }"); if (ns != null) gen.AppendLine("}");

                    context.AddSource($"{className}_Parser.g.cs", SourceText.From(gen.ToString(), Encoding.UTF8));
                }
            }
        }

        private string GetMethodSignature(IMethodSymbol method)
        {
            var access = method.DeclaredAccessibility.ToString().ToLower();
            var ret = method.ReturnType.ToDisplayString();
            var name = method.Name;
            return $"{access} {ret} {name}(string input)";
        }

        private string GenerateNonTerminalParsers(Dictionary<string, List<string[]>> productions, string[] nonTerminals)
        {
            var sb = new StringBuilder();
            foreach (var nt in nonTerminals)
            {
                sb.AppendLine($"        private bool Parse_{nt}(ParserContext ctx, out Legendary.LL1ParserGenerator.ParseNode node)");
                sb.AppendLine("        {");
                sb.AppendLine($"            node = new Legendary.LL1ParserGenerator.ParseNode(\"{nt}\");");
                sb.AppendLine("            var start = ctx.Index;");
                if (!productions.TryGetValue(nt, out var prods) || prods.Count == 0)
                {
                    sb.AppendLine("            node.Start = start; node.End = start; return true;");
                    sb.AppendLine("        }"); sb.AppendLine(); continue;
                }

                sb.AppendLine("            // try alternatives");
                for (int pi = 0; pi < prods.Count; pi++)
                {
                    var prod = prods[pi];
                    sb.AppendLine(pi == 0 ? "            { // alt 0" : $"            {{ // alt {pi}");
                    sb.AppendLine("                var saveIdx = ctx.Index; var saveCurr = ctx.Curr; var children = new System.Collections.Generic.List<Legendary.LL1ParserGenerator.ParseNode>();");
                    if (prod.Length == 0)
                    {
                        sb.AppendLine("                // empty production");
                        sb.AppendLine("                node.Start = saveIdx; node.End = saveIdx; node.Children.AddRange(children); return true;");
                        sb.AppendLine("            }"); continue;
                    }

                    foreach (var sym in prod)
                    {
                        var s = sym.Trim(); if (s == "") continue;
                        char op = s[s.Length - 1]; bool hasOp = (op == '?' || op == '*' || op == '+');
                        var baseSym = hasOp ? s.Substring(0, s.Length - 1) : s;
                        var isNon = nonTerminals.Contains(baseSym);
                        if (isNon)
                        {
                            if (!hasOp)
                                sb.AppendLine($"                if (!Parse_{baseSym}(ctx, out var c_{baseSym})) {{ ctx.Curr = saveCurr; ctx.Index = saveIdx; goto alt_fail_{pi}; }} else {{ children.Add(c_{baseSym}); }}");
                            else if (op == '?') sb.AppendLine($"                if (Parse_{baseSym}(ctx, out var opt_{baseSym})) {{ children.Add(opt_{baseSym}); }}");
                            else if (op == '+') { sb.AppendLine($"                if (!Parse_{baseSym}(ctx, out var rep0_{baseSym})) {{ ctx.Curr = saveCurr; ctx.Index = saveIdx; goto alt_fail_{pi}; }} else {{ children.Add(rep0_{baseSym}); }}"); sb.AppendLine($"                while(Parse_{baseSym}(ctx, out var repN_{baseSym})) {{ children.Add(repN_{baseSym}); }}"); }
                            else if (op == '*') sb.AppendLine($"                while(Parse_{baseSym}(ctx, out var repZ_{baseSym})) {{ children.Add(repZ_{baseSym}); }}");
                        }
                        else
                        {
                            if (!hasOp)
                                sb.AppendLine($"                if (ctx.Curr.Type == null || !(ctx.Curr.Type == \"{baseSym}\")) {{ ctx.Curr = saveCurr; ctx.Index = saveIdx; goto alt_fail_{pi}; }} var tnode = new Legendary.LL1ParserGenerator.ParseNode(ctx.Curr.Type) {{ Text = ctx.Curr.Text, Start = ctx.Index, End = ctx.Index + 1 }}; children.Add(tnode); ctx.Advance();");
                            else if (op == '?') sb.AppendLine($"                if (ctx.Curr.Type != null && ctx.Curr.Type == \"{baseSym}\") {{ var tnode = new Legendary.LL1ParserGenerator.ParseNode(ctx.Curr.Type) {{ Text = ctx.Curr.Text, Start = ctx.Index, End = ctx.Index + 1 }}; children.Add(tnode); ctx.Advance(); }}");
                            else if (op == '+') sb.AppendLine($"                if (ctx.Curr.Type == null || ctx.Curr.Type != \"{baseSym}\") {{ ctx.Curr = saveCurr; ctx.Index = saveIdx; goto alt_fail_{pi}; }} do {{ var tnode = new Legendary.LL1ParserGenerator.ParseNode(ctx.Curr.Type) {{ Text = ctx.Curr.Text, Start = ctx.Index, End = ctx.Index + 1 }}; children.Add(tnode); ctx.Advance(); }} while (ctx.Curr.Type != null && ctx.Curr.Type == \"{baseSym}\");");
                            else if (op == '*') sb.AppendLine($"                while (ctx.Curr.Type != null && ctx.Curr.Type == \"{baseSym}\") {{ var tnode = new Legendary.LL1ParserGenerator.ParseNode(ctx.Curr.Type) {{ Text = ctx.Curr.Text, Start = ctx.Index, End = ctx.Index + 1 }}; children.Add(tnode); ctx.Advance(); }}");
                        }
                    }

                    sb.AppendLine("                node.Children.AddRange(children); node.Start = saveIdx; node.End = ctx.Index; return true;");
                    sb.AppendLine($"            alt_fail_{pi}: ;");
                    sb.AppendLine("            }");
                }

                sb.AppendLine("            // no alternative matched");
                sb.AppendLine("            node = null; return false;");
                sb.AppendLine("        }"); sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
