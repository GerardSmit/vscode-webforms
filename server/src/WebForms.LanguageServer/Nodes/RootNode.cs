using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace WebForms.Nodes;

public class RootNode : ContainerNode
{
    public RootNode()
        : base(NodeType.Root)
    {
    }

    public List<DirectiveNode> AllDirectives { get; set; } = new();

    public List<HtmlNode> AllHtmlNodes { get; set; } = new();

    public List<Node> AllNodes { get; set; } = new();

    public Dictionary<int, ExpressionNode> Expressions { get; } = new();

    public override DocumentSymbol CreateSymbol()
    {
        return new DocumentSymbol
        {
            Name = "#",
            Detail = "",
            Kind = SymbolKind.Field,
        };
    }
}
