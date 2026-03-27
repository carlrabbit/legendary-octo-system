using Legendary.Lexer.Annotations;
using Xunit;

namespace Legendary.LexerGenerator.Tests.AttributeTests
{
    public class TokenAttributeTests
    {
        [Fact]
        public void TokenAttribute_DefaultsAndSetters_Work()
        {
            var attr = new TokenAttribute("if") { Kind = "Keyword", IsSkip = false, Priority = 10, Delimiter = '"', EscapeChar = '\\' };
            Assert.Equal("if", attr.Pattern);
            Assert.Equal("Keyword", attr.Kind);
            Assert.False(attr.IsSkip);
            Assert.Equal(10, attr.Priority);
            Assert.Equal('"', attr.Delimiter);
            Assert.Equal('\\', attr.EscapeChar);
        }
    }
}
