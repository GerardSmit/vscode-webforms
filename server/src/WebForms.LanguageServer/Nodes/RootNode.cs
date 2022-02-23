using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace WebForms.Nodes;

public class RootNode : ContainerNode
{
    public RootNode()
        : base(NodeType.Root)
    {
    }

    public List<DirectiveNode> Directives { get; set; } = new();

    public List<Node> AllNodes { get; set; } = new();

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