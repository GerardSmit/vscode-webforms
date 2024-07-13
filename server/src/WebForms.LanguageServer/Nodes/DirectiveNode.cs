using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using WebForms.Collections;
using WebForms.Models;
using static WebForms.Nodes.DirectiveType;

namespace WebForms.Nodes;

public class DirectiveNode : Node, IAttributeNode
{
    public DirectiveNode() : base(NodeType.Directive)
    {
    }

    public DirectiveType DirectiveType { get; set; }

    public Dictionary<TokenString, TokenString> Attributes { get; set; } = new(AttributeCompare.IgnoreCase);
}