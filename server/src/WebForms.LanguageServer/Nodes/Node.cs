using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using WebForms.Models;

namespace WebForms.Nodes;

public record HitRange(TokenRange Range, int Type = 0, TokenString? Value = null)
{
    public Node Node { get; set; } = null!;
}

public abstract class Node
{
    protected Node(NodeType type)
    {
        Type = type;
    }

    public NodeType Type { get; }
    
    public TokenRange Range { get; set; }
    
    public ContainerNode? Parent { get; set; }

    public abstract DocumentSymbol CreateSymbol();

    public virtual void AddRanges(ICollection<HitRange> ranges)
    {
    }

    public virtual void Highlight(List<DocumentHighlight> items, HitRange hitRange, Document document,
        Position position)
    {

    }

    public virtual void Rename(Dictionary<DocumentUri, IEnumerable<TextEdit>> items, HitRange hitRange, string newText, Document document)
    {

    }

    public virtual Hover? Hover(List<DocumentHighlight> items, HitRange hit, Document document,
        Position position)
    {
        return null;
    }
}
