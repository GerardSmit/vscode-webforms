using WebForms.Models;

namespace WebForms.Nodes;

public interface INode
{
    NodeType Type { get; }

    TokenRange Range { get; }

    ContainerNode? Parent { get; }
}
