using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using WebForms.Models;

namespace WebForms.Nodes;

public class ExpressionNode : Node
{
    public ExpressionNode(int id)
        : base(NodeType.Expression)
    {
        Id = id;
    }

    public int Id { get; }

    public TokenString Text { get; set; }

    public ExpressionSyntax Expression { get; set; } = null!;

    public bool IsEval { get; set; }

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
