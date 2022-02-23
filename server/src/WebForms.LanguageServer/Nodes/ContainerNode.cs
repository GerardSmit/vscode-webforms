namespace WebForms.Nodes;

public abstract class ContainerNode : Node
{
    protected ContainerNode(NodeType type) : base(type)
    {
    }

    public List<Node> Children { get; set; } = new();
}