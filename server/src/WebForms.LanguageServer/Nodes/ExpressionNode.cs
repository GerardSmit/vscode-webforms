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
}
