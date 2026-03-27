using System;
using System.Linq;
using Xunit;

namespace Legendary.LexerGenerator.Tests.LexerRuntimeTests
{
    public class LexerBehaviorTests
    {
        private record Token(string Type, string Text, int Line, int Column, int Offset, int Length);
        private record Error(int Line, int Column, int Offset, int Length, string Text, string Message);

        // Minimal test-only lexer implementing the matching rules used by the generator
        private class TestLexer
        {
            private readonly (string Name, string Pattern, bool IsSkip, int Priority, string Kind)[] _rules;
            public TestLexer((string, string, bool, int, string)[] rules) => _rules = rules.Select(r => (r.Item1, r.Item2, r.Item3, r.Item4, r.Item5)).ToArray();

            public (Token[] Tokens, Error[] Errors) Tokenize(string input, bool collectAllErrors = false)
            {
                if (input is null) throw new ArgumentNullException(nameof(input));
                var tokens = new System.Collections.Generic.List<Token>();
                var errors = new System.Collections.Generic.List<Error>();
                int pos = 0; int line = 1; int col = 1; int n = input.Length;
                while (pos < n)
                {
                    // Simple whitespace handling (skip runs of whitespace, handle CRLF as single newline)
                    if (char.IsWhiteSpace(input[pos]))
                    {
                        if (input[pos] == '\r')
                        {
                            if (pos + 1 < n && input[pos + 1] == '\n') { pos += 2; line++; col = 1; continue; }
                            else { pos++; line++; col = 1; continue; }
                        }
                        if (input[pos] == '\n') { pos++; line++; col = 1; continue; }
                        // other spaces
                        int ws = pos + 1; while (ws < n && char.IsWhiteSpace(input[ws]) && input[ws] != '\n' && input[ws] != '\r') ws++;
                        col += ws - pos; pos = ws; continue;
                    }

                    // Handle string literal start (double quoted) if present
                    if (input[pos] == '"')
                    {
                        // find closing quote respecting escapes
                        int j = pos + 1; bool esc = false; bool closed = false;
                        while (j < n)
                        {
                            char cc = input[j];
                            if (esc) { esc = false; j++; continue; }
                            if (cc == '\\') { esc = true; j++; continue; }
                            if (cc == '"') { j++; closed = true; break; }
                            if (cc == '\n') { j++; line++; col = 1; continue; }
                            j++;
                        }
                        if (!closed)
                        {
                            // unterminated string
                            int errLen = Math.Min(1, n - pos);
                            string text = input.Substring(pos, Math.Min(1, n - pos));
                            errors.Add(new Error(line, col, pos, errLen, text, "Unterminated string literal"));
                            if (!collectAllErrors) return (tokens.ToArray(), errors.ToArray());
                            pos = n; break;
                        }
                        // skip string (treat as token only if a String rule exists and not skip)
                        int len = j - pos;
                        bool handled = false;
                        for (int i = 0; i < _rules.Length; i++)
                        {
                            var r = _rules[i];
                            if (r.Kind == "String")
                            {
                                string litTxt = input.Substring(pos, len);
                                if (!r.IsSkip) tokens.Add(new Token(r.Name, litTxt, line, col, pos, len));
                                for (int k = 0; k < len; k++) { if (input[pos + k] == '\n') { line++; col = 1; } else col++; }
                                pos += len; handled = true; break;
                            }
                        }
                        if (handled) continue;
                        // otherwise treat as unrecognized token (or skip)
                        pos += len; continue;
                    }

                    // Handle block comment start '/*' (skip) and detect unterminated
                    if (input[pos] == '/' && pos + 1 < n && input[pos + 1] == '*')
                    {
                        int j = pos + 2; bool closed = false;
                        while (j + 1 < n)
                        {
                            if (input[j] == '*' && input[j + 1] == '/') { j += 2; closed = true; break; }
                            if (input[j] == '\n') { j++; line++; col = 1; continue; }
                            j++;
                        }
                        if (!closed)
                        {
                            errors.Add(new Error(line, col, pos, Math.Min(1, n - pos), "/*", "Unterminated block comment"));
                            if (!collectAllErrors) return (tokens.ToArray(), errors.ToArray());
                            pos = n; break;
                        }
                        // skip comment
                        int len = j - pos;
                        pos += len; continue;
                    }
                    int bestLen = -1; int bestIndex = -1;
                    for (int i = 0; i < _rules.Length; i++)
                    {
                        var r = _rules[i];
                        if (r.Kind == "Keyword" || r.Kind == "Literal")
                        {
                            var lit = r.Pattern;
                            if (pos + lit.Length <= n && input.AsSpan(pos, lit.Length).SequenceEqual(lit.AsSpan()))
                            {
                                int len = lit.Length;
                                if (len > bestLen) { bestLen = len; bestIndex = i; }
                            }
                        }
                        else if (r.Kind == "Identifier")
                        {
                            if (pos < n && (char.IsLetter(input[pos]) || input[pos] == '_'))
                            {
                                int j = pos + 1; while (j < n && (char.IsLetterOrDigit(input[j]) || input[j] == '_')) j++;
                                int len = j - pos; if (len > bestLen) { bestLen = len; bestIndex = i; }
                            }
                        }
                        else if (r.Kind == "Number")
                        {
                            if (pos < n && char.IsDigit(input[pos]))
                            {
                                int j = pos + 1; while (j < n && char.IsDigit(input[j])) j++; if (j < n && input[j] == '.') { int k = j + 1; while (k < n && char.IsDigit(input[k])) k++; j = k; }
                                int len = j - pos; if (len > bestLen) { bestLen = len; bestIndex = i; }
                            }
                        }
                        else if (r.Kind == "Regex")
                        {
                            // Not implemented for tests
                        }
                    }

                    if (bestIndex == -1)
                    {
                        int errLen = Math.Min(1, n - pos);
                        string text = input.Substring(pos, errLen);
                        errors.Add(new Error(line, col, pos, errLen, text, "Unrecognized input"));
                        if (!collectAllErrors) return (tokens.ToArray(), errors.ToArray());
                        if (text == "\n") { line++; col = 1; } else col++;
                        pos += errLen; continue;
                    }

                    var rule = _rules[bestIndex];
                    string txt = input.Substring(pos, bestLen);
                    if (!rule.IsSkip)
                    {
                        tokens.Add(new Token(rule.Name, txt, line, col, pos, bestLen));
                    }
                    for (int k = 0; k < bestLen; k++) { if (input[pos + k] == '\n') { line++; col = 1; } else col++; }
                    pos += bestLen;
                }
                return (tokens.ToArray(), errors.ToArray());
            }
        }

        [Fact]
        public void Keyword_Precedence_Over_Identifier()
        {
            var rules = new (string, string, bool, int, string)[] {
                ("If", "if", false, 0, "Keyword"),
                ("Identifier", "[A-Za-z_][A-Za-z0-9_]*", false, 0, "Identifier")
            };
            var lexer = new TestLexer(rules);
            var (tokens, errors) = lexer.Tokenize("if ifx if");
            Assert.Empty(errors);
            Assert.Equal(3, tokens.Length);
            Assert.Equal("If", tokens[0].Type);
            Assert.Equal("Identifier", tokens[1].Type); // 'ifx'
            Assert.Equal("If", tokens[2].Type);
        }

        [Fact]
        public void Mixed_LineEndings_Position_Tracking()
        {
            var rules = new (string, string, bool, int, string)[] {
                ("Id", "[A-Za-z_][A-Za-z0-9_]*", false, 0, "Identifier"),
                ("WS", "\\s+", true, 0, "Regex")
            };
            var lexer = new TestLexer(rules);
            string input = "one\r\ntwo\nthree\rfour"; // lines: 1,2,3,4
            var (tokens, errors) = lexer.Tokenize(input, collectAllErrors: true);
            Assert.Empty(errors);
            Assert.Equal(4, tokens.Length);
            Assert.Equal("one", tokens[0].Text);
            Assert.Equal(1, tokens[0].Line);
            Assert.Equal(1, tokens[0].Column);
            Assert.Equal("two", tokens[1].Text);
            Assert.Equal(2, tokens[1].Line);
            Assert.Equal(1, tokens[1].Column);
            Assert.Equal("three", tokens[2].Text);
            Assert.Equal(3, tokens[2].Line);
            Assert.Equal(1, tokens[2].Column);
            Assert.Equal("four", tokens[3].Text);
            Assert.Equal(4, tokens[3].Line);
            Assert.Equal(1, tokens[3].Column);
        }

        [Fact]
        public void FailFast_Vs_CollectAll_Errors()
        {
            var rules = new (string, string, bool, int, string)[] {
                ("Id", "[A-Za-z_][A-Za-z0-9_]*", false, 0, "Identifier")
            };
            var lexer = new TestLexer(rules);
            string input = "abc@def@ghi";
            var (tokens1, errors1) = lexer.Tokenize(input, collectAllErrors: false);
            Assert.Single(errors1);
            Assert.Equal(1, tokens1.Length); // 'abc' then stop

            var (tokens2, errors2) = lexer.Tokenize(input, collectAllErrors: true);
            Assert.Equal(3, tokens2.Length); // abc, def, ghi
            Assert.Equal(2, errors2.Length);
        }

        [Fact]
        public void Unterminated_String_Literal_Reported()
        {
            var rules = new (string, string, bool, int, string)[] {
                ("String", "\"(\\\\.|[^\\\"])*\"", false, 0, "String"),
                ("Id", "[A-Za-z_][A-Za-z0-9_]*", false, 0, "Identifier")
            };
            var lexer = new TestLexer(rules);
            string input = "abc \"hello world"; // missing closing quote
            var (tokens, errors) = lexer.Tokenize(input, collectAllErrors: true);
            Assert.Single(errors);
            Assert.Contains("Unterminated string", errors[0].Message);
        }

        [Fact]
        public void Unterminated_Block_Comment_Reported()
        {
            var rules = new (string, string, bool, int, string)[] {
                ("Id", "[A-Za-z_][A-Za-z0-9_]*", false, 0, "Identifier")
            };
            var lexer = new TestLexer(rules);
            string input = "start /* unterminated comment";
            var (tokens, errors) = lexer.Tokenize(input, collectAllErrors: true);
            Assert.Single(errors);
            Assert.Contains("Unterminated block comment", errors[0].Message);
        }

        [Fact]
        public void Unicode_Identifiers_Are_Recognized()
        {
            var rules = new (string, string, bool, int, string)[] {
                ("Id", "[A-Za-z_][A-Za-z0-9_]*", false, 0, "Identifier")
            };
            var lexer = new TestLexer(rules);
            string input = "αβγ δεζ"; // Greek letters
            var (tokens, errors) = lexer.Tokenize(input, collectAllErrors: true);
            Assert.Empty(errors);
            Assert.Equal(2, tokens.Length);
            Assert.Equal("αβγ", tokens[0].Text);
            Assert.Equal("δεζ", tokens[1].Text);
        }
    }
}
