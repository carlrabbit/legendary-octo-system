namespace Legendary.LL1ParserGenerator
{
    public interface ITokenAdapter<TLexerToken>
    {
        (string Type, string Text) Adapt(TLexerToken token);
    }
}
