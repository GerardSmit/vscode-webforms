using WebForms.Nodes;

namespace WebForms;

internal class ParserContainer
{
    private readonly Stack<HtmlNode> _stack = new();

    public ParserContainer()
    {
        Root = new RootNode();
        Parent = Root;
    }

    public RootNode Root { get; }

    public ContainerNode Parent { get; private set; }

    public HtmlNode? Current { get; private set; }

    public void Add(Node node)
    {
        if (node is DirectiveNode directiveNode)
        {
            Root.Directives.Add(directiveNode);
        }
        
        Root.AllNodes.Add(node);
        Parent.Children.Add(node);
    }

    public void Push(HtmlNode node)
    {
        Add(node);
        
        _stack.Push(node);
        Current = node;
        Parent = node;
    }

    public HtmlNode Pop()
    {
        var current = _stack.Pop();
        Current = _stack.Count > 0 ? _stack.Peek() : null;
        Parent = (ContainerNode?) Current ?? Root;
        return current;
    }
}
