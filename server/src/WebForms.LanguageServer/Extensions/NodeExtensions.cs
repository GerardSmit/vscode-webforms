using WebForms.Nodes;

namespace WebForms;

public static class NodeExtensions
{
    public static string? GetItemType(this Node node)
    {
        var current = node;

        while (current != null)
        {
            if (current is HtmlNode htmlNode && htmlNode.Attributes.TryGetValue("ItemType", out var value))
            {
                return value.Value;
            }

            current = current.Parent;
        }

        return null;
    }
}
