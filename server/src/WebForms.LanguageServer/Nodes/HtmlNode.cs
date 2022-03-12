using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using WebForms.Collections;
using WebForms.Models;
using WebForms.Roslyn;

namespace WebForms.Nodes;

public class HtmlTagNode
{
    public TokenString? Namespace { get; set; }

    public TokenString Name { get; set; }

    public TokenRange Range { get; set; }
}

public class HtmlNode : ContainerNode, IAttributeNode
{
    private const int TypeTag = 0;
    private const int TypeAttribute = 0;
    
    public HtmlNode()
        : base(NodeType.Html)
    {
    }

    public TokenString Name => StartTag.Name;

    public TokenString? Namespace => StartTag.Namespace;

    public HtmlTagNode StartTag { get; set; } = new();

    public HtmlTagNode? EndTag { get; set; }

    public RunAt RunAt { get; set; } = RunAt.Client;

    public Dictionary<TokenString, TokenString> Attributes { get; set; } = new(AttributeCompare.IgnoreCase);

    public CodeType? CodeType { get; set; }
    
    public string? ElementName { get; set; }

    public override DocumentSymbol CreateSymbol()
    {
        var detail = "";

        if (RunAt == RunAt.Client && Attributes.TryGetValue("id", out var id))
        {
            detail += "#" + id;
        }

        if (Attributes.TryGetValue("class", out var className) ||
            Attributes.TryGetValue("cssclass", out className))
        {
            detail += "." + string.Join(".", className.Value.Split(null));
        }

        return new DocumentSymbol
        {
            Name = Name,
            Detail = detail,
            Kind = SymbolKind.Field,
        };
    }

    public override void AddRanges(ICollection<HitRange> ranges)
    {
        ranges.Add(new HitRange(StartTag.Name.Range));

        if (EndTag != null)
        {
            ranges.Add(new HitRange(EndTag.Name.Range, TypeTag));
        }

        foreach (var (key, _) in Attributes)
        {
            ranges.Add(new HitRange(key.Range, TypeAttribute, key));
        }
    }

    public override void Highlight(List<DocumentHighlight> items, HitRange hitRange, Document document,
        Position position)
    {
        if (hitRange.Type == TypeAttribute)
        {
            return;
        }

        items.Add(new DocumentHighlight
        {
            Kind = DocumentHighlightKind.Read,
            Range = StartTag.Name.Range
        });

        if (EndTag != null)
        {
            items.Add(new DocumentHighlight
            {
                Kind = DocumentHighlightKind.Read,
                Range = EndTag.Name.Range
            });
        }
    }

    public override void Rename(Dictionary<DocumentUri, IEnumerable<TextEdit>> items, HitRange hitRange,
        string newText,
        Document document)
    {
        if (hitRange.Type == TypeAttribute)
        {
            return;
        }

        var changes = new List<TextEdit>();

        changes.Add(new TextEdit
        {
            Range = StartTag.Name.Range,
            NewText = newText
        });

        if (EndTag != null)
        {
            changes.Add(new TextEdit
            {
                Range = EndTag.Name.Range,
                NewText = newText
            });
        }

        items[document.Uri] = changes;
    }

    public override Hover? Hover(List<DocumentHighlight> items, HitRange hit, Document document,
        Position position)
    {
        if (CodeType == null)
        {
            return null;
        }

        if (hit.Type == TypeAttribute && hit.Value is {} name)
        {
            if (CodeType.Control?.Properties.TryGetValue(name, out var property) ?? false)
            {
                return new Hover
                {
                    Range = name.Range,
                    Contents = new MarkedStringsOrMarkupContent(
                        new MarkedString("csharp", $"{property.Type.FullName} {name.Value};"),
                        new MarkedString(property.Description)
                    )
                };
            }

            return null;
        }
        
        return new Hover
        {
            Range = (Namespace?.Range ?? Name.Range).WithEnd(Name.Range.End),
            Contents = new MarkedStringsOrMarkupContent(
                new MarkedString("csharp", $"{CodeType.Type.FullName} {ElementName ?? "_"};")
            )
        };
    }
}
