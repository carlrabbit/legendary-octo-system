using System.Collections.Generic;
using System.Text.Json;

namespace Legendary.LL1ParserGenerator
{
    public class ParseNode
    {
        public string Type { get; }
        public string Text { get; set; }
        public List<ParseNode> Children { get; } = new List<ParseNode>();
        // Token index range [Start, End) in the token stream. -1 when unknown.
        public int Start { get; set; } = -1;
        public int End { get; set; } = -1;
        public ParseNode(string type) { Type = type; }
        public override string ToString() => (Text ?? Type) + (Start >= 0 ? $" [{Start},{End})" : string.Empty);

        public string ToJson(bool indented = true)
        {
            var opts = new JsonSerializerOptions { WriteIndented = indented };
            return JsonSerializer.Serialize(this, opts);
        }
    }
}
