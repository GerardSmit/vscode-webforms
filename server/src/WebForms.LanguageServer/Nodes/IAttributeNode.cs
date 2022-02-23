using WebForms.Models;

namespace WebForms.Nodes;

public interface IAttributeNode
{
    Dictionary<TokenString, TokenString> Attributes { get; }

    TokenString GetAttribute(string name) => Attributes.TryGetValue(name, out var value) ? value : default;
}