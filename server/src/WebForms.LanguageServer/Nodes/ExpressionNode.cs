using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using WebForms.Models;

namespace WebForms.Nodes;

public class ExpressionNode : Node
{
    public ExpressionNode()
        : base(NodeType.Expression)
    {
    }

    public TokenString Text { get; set; }

    public ExpressionSyntax Expression { get; set; } = null!;

    public override DocumentSymbol CreateSymbol()
    {
        return new DocumentSymbol
        {
            Name = "Expression",
            Detail = "",
            Kind = SymbolKind.String,
        };
    }

    public override void AddRanges(ICollection<HitRange> ranges)
    {
        ranges.Add(new HitRange(Text.Range));
    }

    public override Hover Hover(List<DocumentHighlight> items, HitRange hit, Document document, Position position)
    {
        var offset = document.Lines[position.Line] + position.Character - Text.Range.Start.Offset;
        var token = Expression.FindToken(offset);
        
        return new Hover
        {
            Contents = new MarkedStringsOrMarkupContent(
                new MarkedString("csharp", token.Text)
            )
        };
    }
}
