using System.Linq;
using Legendary.Lexer.Annotations;
using Legendary.LL1ParserGenerator;
using Xunit;

namespace Legendary.LexerGenerator.Tests
{
    [Token(@"[0-9]+", IsSkip = false, Priority = 1)]
    public class NumberTok { }

    [Token(@"\s+", IsSkip = true, Priority = 0)]
    public class WS { }

    [Lexer(typeof(NumberTok), typeof(WS))]
    public partial class SimpleLexer { }

    [NonTerminal("Expr -> NumberTok+")]
    public class ExprNode { }

    [Parser(new[] { typeof(NumberTok) }, new[] { typeof(ExprNode) })]
    public partial class IntegrationParser
    {
        [ParserEntry]
        public partial void Parse(string input);
    }

    public class IntegrationTests
    {
        [Fact]
        public void EndToEnd_LexerAdapterParser_Works()
        {
            var lexer = new SimpleLexer();
            var res = lexer.Tokenize("42 7 13");
            Assert.Empty(res.Errors);
            var adapter = new SimpleLexerTokenAdapter();
            var tokenStream = res.Tokens.Select(t => adapter.Adapt(t));

            var p = new IntegrationParser();
            p.ParseTokens(tokenStream);
            Assert.True(p.ParseSucceeded);
            Assert.NotNull(p.AST);
            var root = Assert.IsType<ParseNode>(p.AST);
            Assert.Equal("ExprNode", root.Type);
            // Expect three number tokens as children
            Assert.Equal(3, root.Children.Count);
            Assert.Equal("42", root.Children[0].Text);
            Assert.Equal("7", root.Children[1].Text);
            Assert.Equal("13", root.Children[2].Text);
        }
    }
}
