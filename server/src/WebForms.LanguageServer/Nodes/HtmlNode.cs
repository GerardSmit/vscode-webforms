using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using WebForms.Collections;
using WebForms.Models;

namespace WebForms.Nodes;

public class HtmlTagNode
{
    public TokenString? Namespace { get; set; }

    public TokenString Name { get; set; }

    public TokenRange Range { get; set; }
}

public class HtmlNode : ContainerNode, IAttributeNode
{
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
            ranges.Add(new HitRange(EndTag.Name.Range));
        }

        foreach (var (key, _) in Attributes)
        {
            ranges.Add(new HitRange(key.Range, 1, key));
        }
    }

    public override void Highlight(List<DocumentHighlight> items, HitRange hitRange, Document document,
        Position position)
    {
        if (hitRange.Type == 1)
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
        if (hitRange.Type == 1)
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

        if (RunAt == RunAt.Client ||
            StartTag.Namespace is not {} ns ||
            !document.Controls.TryGetValue(StartTag.Namespace + ":" + StartTag.Name, out var reference))
        {
            return null;
        }

        if (hit.Type == 1 && hit.Value is {} name)
        {
            if (reference.Control.Properties.TryGetValue(name, out var property))
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
            Range = ns.Range.WithEnd(StartTag.Name.Range.End),
            Contents = new MarkedStringsOrMarkupContent(
                new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = $"**{reference.Control.Assembly} - {reference.Control.Name}**"
                }
            )
        };
    }
}
