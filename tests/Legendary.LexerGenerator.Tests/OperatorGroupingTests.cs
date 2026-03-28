using System.Linq;
using Legendary.LL1ParserGenerator;
using Legendary.Lexer.Annotations;
using Xunit;

namespace Legendary.LexerGenerator.Tests
{
    [Token(@"A", IsSkip = false, Priority = 1)]
    public class ATok { }

    [Token(@"B", IsSkip = false, Priority = 1)]
    public class BTok { }

    [NonTerminal("S -> ATok?")]
    public class OptNode { }

    [Parser(new[] { typeof(ATok) }, new[] { typeof(OptNode) })]
    public partial class OptionalParser
    {
        [ParserEntry]
        public partial void Parse(string input);
    }

    [NonTerminal("R -> ATok+")]
    public class RepNode { }

    [Parser(new[] { typeof(ATok) }, new[] { typeof(RepNode) })]
    public partial class RepeatParser
    {
        [ParserEntry]
        public partial void Parse(string input);
    }

    [NonTerminal("G -> ( ATok BTok )+ ")]
    public class GroupNode { }

    [Parser(new[] { typeof(ATok), typeof(BTok) }, new[] { typeof(GroupNode) })]
    public partial class GroupParser
    {
        [ParserEntry]
        public partial void Parse(string input);
    }

    public class OperatorGroupingTests
    {
        [Fact]
        public void Optional_Present_And_Absent()
        {
            var p = new OptionalParser();
            // absent
            p.ParseTokens(new (string, string)[] { });
            Assert.True(p.ParseSucceeded);
            var root0 = Assert.IsType<ParseNode>(p.AST);
            Assert.Equal("OptNode", root0.Type);
            Assert.Empty(root0.Children);

            // present
            p = new OptionalParser();
            p.ParseTokens(new[] { ("ATok", "a") });
            Assert.True(p.ParseSucceeded);
            var root1 = Assert.IsType<ParseNode>(p.AST);
            Assert.Single(root1.Children);
            Assert.Equal("ATok", root1.Children[0].Type);
            Assert.Equal("a", root1.Children[0].Text);
        }

        [Fact]
        public void Repeat_Plus_Works()
        {
            var p = new RepeatParser();
            p.ParseTokens(new[] { ("ATok", "1"), ("ATok", "2"), ("ATok", "3") });
            Assert.True(p.ParseSucceeded);
            var root = Assert.IsType<ParseNode>(p.AST);
            Assert.Equal("RepNode", root.Type);
            Assert.Equal(3, root.Children.Count);
            Assert.Equal("1", root.Children[0].Text);
            Assert.Equal("2", root.Children[1].Text);
            Assert.Equal("3", root.Children[2].Text);
        }

        [Fact]
        public void Grouping_With_Plus_Creates_Group_Children()
        {
            var p = new GroupParser();
            p.ParseTokens(new[] { ("ATok", "a"), ("BTok", "b"), ("ATok", "c"), ("BTok", "d") });
            Assert.True(p.ParseSucceeded);
            var root = Assert.IsType<ParseNode>(p.AST);
            Assert.Equal("GroupNode", root.Type);
            // grouping (A B)+ should produce two group children, each with two terminal children
            Assert.Equal(2, root.Children.Count);
            Assert.Equal(2, root.Children[0].Children.Count);
            Assert.Equal("a", root.Children[0].Children[0].Text);
            Assert.Equal("b", root.Children[0].Children[1].Text);
            Assert.Equal("c", root.Children[1].Children[0].Text);
            Assert.Equal("d", root.Children[1].Children[1].Text);
        }
    }
}
