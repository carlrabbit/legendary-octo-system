using Legendary.Lexer.Annotations;
using Legendary.LL1ParserGenerator;
using Xunit;

namespace Legendary.LexerGenerator.Tests
{
    // Example terminal and non-terminal classes annotated for the generator
    [Terminal("NUMBER : [0-9]+")]
    public class NumberToken { }

    [NonTerminal("Expr -> Number")]
    public class ExprNode { }

    // Partial parser class to be implemented by the source generator
    [Parser(new[] { typeof(NumberToken) }, new[] { typeof(ExprNode) })]
    public partial class SampleParser
    {
        // The generator will implement this method.
        [ParserEntry]
        public partial void Parse(string input);
    }

    public class ParserGeneratorTests
    {
        [Fact]
        public void GeneratedParser_Sets_AST_and_Flags()
        {
            var p = new SampleParser();
            // Use token-stream API to produce a ParseNode AST
            var tokens = new[] { (Type: "NumberToken", Text: "42") };
            p.ParseTokens(tokens);
            Assert.True(p.ParseSucceeded);
            Assert.NotNull(p.AST);
            var root = Assert.IsType<ParseNode>(p.AST);
            // Root should be the non-terminal `ExprNode` and have a child token node with text "42"
            Assert.Equal("ExprNode", root.Type);
            Assert.Single(root.Children);
            Assert.Equal("NumberToken", root.Children[0].Type);
            Assert.Equal("42", root.Children[0].Text);

            // Ensure grammar rules were discovered and exposed
            Assert.NotNull(p.GrammarRules);
            Assert.True(p.GrammarRules.ContainsKey("NumberToken"));
            Assert.Equal("NUMBER : [0-9]+", p.GrammarRules["NumberToken"]);
            Assert.True(p.GrammarRules.ContainsKey("ExprNode"));
            Assert.Equal("Expr -> Number", p.GrammarRules["ExprNode"]);
            // LL(1) conflicts should be empty for this simple grammar
            Assert.NotNull(p.LL1Conflicts);
            Assert.Empty(p.LL1Conflicts);
        }
    }
}
