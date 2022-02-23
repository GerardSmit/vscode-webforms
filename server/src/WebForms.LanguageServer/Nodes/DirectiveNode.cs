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

    public override DocumentSymbol CreateSymbol()
    {
        var detail = "";

        if (DirectiveType == Register)
        {
            if (Attributes.TryGetValue("tagprefix", out var tagPrefix))
            {
                detail += tagPrefix;
            }

            if (Attributes.TryGetValue("tagname", out var tagName))
            {
                if (detail.Length > 0) detail += ":";
                detail += tagName;
            }
        }
        else if (DirectiveType is DirectiveType.Control or Page)
        {
            if (Attributes.TryGetValue("inherits", out var tagPrefix))
            {
                detail = tagPrefix;
            }
        }

        return new DocumentSymbol
        {
            Name = DirectiveType.ToString(),
            Detail = detail,
            Kind = DirectiveType switch
            {
                Assembly => SymbolKind.Field,
                DirectiveType.Control => SymbolKind.Class,
                Implements => SymbolKind.Field,
                Import => SymbolKind.Field,
                Master => SymbolKind.Class,
                MasterType => SymbolKind.Class,
                OutputCache => SymbolKind.Class,
                Page => SymbolKind.Class,
                PreviousPageType => SymbolKind.Field,
                Reference => SymbolKind.Field,
                Register => SymbolKind.Field,
                _ => throw new ArgumentOutOfRangeException()
            },
        };
    }
}