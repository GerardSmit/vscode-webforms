using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using WebForms.Collections;
using WebForms.Nodes;
using WebForms.Roslyn;
using static OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;
using Diagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;
using DiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace WebForms.Models;

public enum ControlReferenceSource
{
    Project,
    Document
}

public record struct ControlReference(Control Control, ControlReferenceSource Source, Document? Document = null);

public record struct ControlKey(string Namespace, string Name);

public class Document
{
    private readonly TextDocumentContainer _documentContainer;
    private readonly ILanguageServerFacade _languageServer;
    private string _text = "";
    private readonly string _uriString;
    private TypeDefinition? _type;

    public Document(DocumentUri uri, ILanguageServerFacade languageServer, TextDocumentContainer documentContainer)
    {
        _languageServer = languageServer;
        _documentContainer = documentContainer;
        Uri = uri;
        _uriString = uri.ToString();
    }

    public DocumentUri Uri { get; }

    public Project? Project { get; set; }

    public bool IsDirty { get; set; }

    public int Version { get; set; }

    public RootNode Node { get; set; } = new();

    public TypeDefinition? Type
    {
        get
        {
            UpdateProject();
            return _type;
        }
        set => _type = value;
    }

    public List<HitRange> HitRanges { get; set; } = new();

    public List<int> Lines { get; set; } = new();

    public Dictionary<ControlKey, ControlReference> Controls { get; set; } = new(ControlKeyCompare.OrdinalIgnoreCase);
    
    public TokenString Code { get; set; }

    public Dictionary<string, HtmlNode> Ids { get; set; } = new();

    public bool IsProjectDirty { get; set; } = true;

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            IsDirty = true;
            Update();
        }
    }

    public void Update()
    {
        var diagnostics = new List<Diagnostic>();
        var parser = new Parser(diagnostics);
        var lexer = new Lexer(Text);

        parser.Parse(ref lexer);
        Node = parser.Root;

        Lines = lexer.Lines;
        Code = lexer.Code;

        // Hit ranges
        var hitRanges = new List<HitRange>();
        var collection = new HitRangeCollection(hitRanges);

        foreach (var node in Node.AllNodes)
        {
            collection.CurrentNode = node;
            node.AddRanges(collection);
        }

        hitRanges.Sort(HitRangeComparer.Instance);
        HitRanges = hitRanges;
        
        UpdateDiagnostics(diagnostics);
    }

    public void UpdateProject()
    {
        if (!IsProjectDirty)
        {
            return;
        }

        IsProjectDirty = false;

        RemoveControls(ControlReferenceSource.Project);
        
        if (Project == null)
        {
            return;
        }
        
        foreach (var registration in Project.Registrations)
        {
            if (Project is not { } project ||
                registration is not { Namespace: {} ns, Prefix: {} tagPrefix } ||
                !project.NamespaceControls.TryGetValue(ns, out var controls))
            {
                continue;
            }
            
            foreach (var control in controls)
            {
                Controls[new ControlKey(tagPrefix, control.Name)] = new ControlReference(control, ControlReferenceSource.Project);
            }
        }

        Update();
    }

    private void UpdateDiagnostics(List<Diagnostic> diagnostics)
    {
        var directive = Node.AllDirectives.FirstOrDefault(i => i.DirectiveType is DirectiveType.Control or DirectiveType.Page);

        Type = directive != null && directive.Attributes.TryGetValue("inherits", out var inherits)
            ? Project?.Resolver.ResolveType(inherits)
            : null;

        RemoveControls(ControlReferenceSource.Document);

        if (Project is {} project)
        {
            foreach (var directiveNode in Node.AllNodes.OfType<DirectiveNode>())
            {
                if (directiveNode.DirectiveType != DirectiveType.Register ||
                    !directiveNode.Attributes.TryGetValue("tagprefix", out var tagPrefix))
                {
                    continue;
                }

                if (directiveNode.Attributes.TryGetValue("tagname", out var tagName) &&
                    directiveNode.Attributes.TryGetValue("src", out var src))
                {
                    var path = src.Value;
                    var isRoot = false;

                    if (path.StartsWith("/"))
                    {
                        path = path[1..];
                        isRoot = true;
                    }
                    else if (path.StartsWith("~/"))
                    {
                        path = path[2..];
                        isRoot = true;
                    }

                    if (!isRoot)
                    {
                        path = Path.Combine(Uri.Path, path);
                    }
                    else if (Project != null)
                    {
                        path = Path.Combine(Project.Path, path);
                    }
                    else
                    {
                        continue;
                    }

                    var uri = Uri.With(new DocumentUriComponents
                    {
                        Path = path
                    });

                    var document = _documentContainer.Get(uri);

                    if (document.Type is { } documentType &&
                        project.NamespaceControls.TryGetValue(documentType.Namespace, out var namespaceControls) &&
                        namespaceControls.FirstOrDefault(i => i.Name.Equals(tagName.Value, StringComparison.OrdinalIgnoreCase)) is {} control)
                    {
                        Controls[new ControlKey(tagPrefix.Value, tagName.Value)] = new ControlReference(control, ControlReferenceSource.Document, document);
                    }

                    continue;
                }

                if (!directiveNode.Attributes.TryGetValue("namespace", out var ns) ||
                    !project.NamespaceControls.TryGetValue(ns.Value, out var controls))
                {
                    continue;
                }

                foreach (var control in controls)
                {
                    Controls[new ControlKey(tagPrefix.Value, control.Name)] = new ControlReference(control, ControlReferenceSource.Document);
                }
            }
        }

        CodeType? type = null;
        var container = new TypeContainer(Project?.Resolver, this);
        
        Ids.Clear();
        
        if (Type != null)
        {
            var children = Node.Children;
            
            type = container.Get(Type);

            AddControls(children, type, container, diagnostics);
        }

        var ranges = new
        {
            Js = new List<OffsetRange>(),
            Css = new List<OffsetRange>()
        };

        foreach (var element in Node.AllNodes.OfType<HtmlNode>())
        {
            if (element.RunAt == RunAt.Client)
            {
                List<OffsetRange> target;

                if (element.Name.Value.Equals("script", StringComparison.OrdinalIgnoreCase))
                {
                    target = ranges.Js;
                }
                else if (element.Name.Value.Equals("style", StringComparison.OrdinalIgnoreCase))
                {
                    target = ranges.Css;
                }
                else
                {
                    continue;
                }

                if (element.EndTag == null)
                {
                    continue;
                }

                target.Add(new OffsetRange(
                    element.StartTag.Range.End.Offset,
                    element.EndTag.Range.Start.Offset
                ));
            }

            var control = element.CodeType?.Control;

            if (control == null)
            {
                continue;
            }

            foreach (var (key, attribute) in element.Attributes)
            {
                if (control.Properties.TryGetValue(key, out var property) &&
                    property.IdReference is {} reference)
                {
                    if (!Ids.TryGetValue(attribute.Value, out var idReference))
                    {
                        diagnostics.Add(new Diagnostic
                        {
                            Range = attribute.Range,
                            Message = $"Control with ID '{attribute}' was not found in this file",
                            Severity = Warning
                        });
                    }
                    else if (idReference.CodeType is { } idType &&
                             reference.Type is { } requiredType &&
                             !idType.Type.IsAssignableTo(requiredType))
                    {
                        diagnostics.Add(new Diagnostic
                        {
                            Range = attribute.Range,
                            Message = $"Expected type '{requiredType}', but the control with ID '{attribute}' is type '{idType.FullName}'",
                            Severity = Warning
                        });
                    }
                }
            }
        }

        _documentContainer.Server.SendNotification("webforms/ranges", new
        {
            Document = _uriString,
            Ranges = ranges,
            Versions = new
            {
                Js = ranges.Js.Aggregate(0, HashCode.Combine),
                Css = ranges.Css.Aggregate(0, HashCode.Combine)
            }
        });

        foreach (var expressionNode in Node.AllNodes.OfType<ExpressionNode>())
        {
            AddDiagnostics(expressionNode.Expression.GetDiagnostics(), diagnostics, expressionNode.Range);
        }

        var tree = SyntaxFactory.ParseSyntaxTree(Code, new CSharpParseOptions(documentationMode: DocumentationMode.Diagnose));

        AddDiagnostics(tree.GetDiagnostics(), diagnostics, Code.Range);

        if (type is not null)
        {
            var documentVisitor = new DocumentVisitor(Node, this, Code.Range, type, container, diagnostics);
            documentVisitor.Visit(tree.GetRoot());
        }

        _languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Diagnostics = diagnostics,
            Uri = Uri
        });
    }

    private void AddControls(IEnumerable<Node> children, CodeType type, TypeContainer container,
        List<Diagnostic> diagnostics, CodeType? parent = null)
    {
        foreach (var element in children.OfType<HtmlNode>())
        {
            CodeType controlType;
            CodeType? nextParent;
            string? name = null;

            if (element.RunAt == RunAt.Server && element.Attributes.TryGetValue("itemtype", out var itemType) &&
                container.Get(itemType.Value) is null)
            {
                diagnostics.Add(new Diagnostic
                {
                    Range = itemType.Range,
                    Message = $"Type '{itemType.Value}' could not be found",
                    Severity = Error
                });
            }
            
            if ((element.RunAt == RunAt.Server || parent != null) &&
                element.StartTag.Namespace is { } ns &&
                Controls.TryGetValue(new ControlKey(ns, element.StartTag.Name), out var reference))
            {
                controlType = container.Get(reference.Control.Type);
                nextParent = reference.Control.ChildrenAsProperties ? controlType : null;
            }
            else if (parent?.Properties.FirstOrDefault(i => i.Name.Equals(element.Name.Value, StringComparison.OrdinalIgnoreCase)) is var (_, codeType))
            {
                controlType = codeType;
                nextParent = codeType;
                name = element.Name.Value;
            }
            else
            {
                AddControlWarning(diagnostics, element);
                AddControls(element.Children, type, container, diagnostics);
                continue;
            }

            element.CodeType = controlType;

            if (name == null && element.Attributes.TryGetValue("id", out var id))
            {
                name = id;
                type.CustomProperties.Add(
                    new CodeTypeProperty(id.Value, controlType)
                );
                Ids[id] = element;
            }
            
            element.ElementName = name;
            
            AddControls(element.Children, type, container, diagnostics, nextParent);
        }
    }

    private void AddControlWarning(ICollection<Diagnostic> diagnostics, HtmlNode element)
    {
        if (element.RunAt != RunAt.Server || element.StartTag.Namespace is not { } ns)
        {
            return;
        }

        if (Controls.Any(i => i.Key.Namespace.Equals(ns.Value, StringComparison.OrdinalIgnoreCase)))
        {
            diagnostics.Add(new Diagnostic
            {
                Range = element.Name.Range,
                Message = $"Control '{element.Name}' not found in namespace '{ns}'",
                Severity = Warning
            });
        }
        else
        {
            diagnostics.Add(new Diagnostic
            {
                Range = ns.Range,
                Message = $"Namespace '{ns.Value}' is not defined in this document or web.config",
                Severity = Warning
            });
        }
    }

    public TokenRange GetRange(TokenRange range, TextSpan span)
    {
        return new TokenRange(
            GetPosition(range, span.Start),
            GetPosition(range, span.End)
        );
    }

    private TokenPosition GetPosition(TokenRange range, int offset)
    {
        offset += range.Start.Offset;

        for (var i = range.End.Line; i >= 0; i--)
        {
            var lineOffset = Lines[i];

            if (offset >= lineOffset)
            {
                return new TokenPosition(offset, i, offset - lineOffset);
            }
        }

        return default;
    }

    private void AddDiagnostics(IEnumerable<Microsoft.CodeAnalysis.Diagnostic> statementNode, List<Diagnostic> diagnostics, TokenRange range)
    {
        foreach (var diagnostic in statementNode)
        {
            if (!diagnostic.Location.IsInSource)
            {
                continue;
            }

            diagnostics.Add(new Diagnostic
            {
                Code = diagnostic.Id,
                Range = GetRange(range, diagnostic.Location.SourceSpan),
                Message = diagnostic.GetMessage(),
                Severity = diagnostic.Severity switch
                {
                    DiagnosticSeverity.Hidden => Error,
                    DiagnosticSeverity.Info => Information,
                    DiagnosticSeverity.Warning => Warning,
                    DiagnosticSeverity.Error => Error,
                    _ => throw new ArgumentOutOfRangeException()
                }
            });
        }
    }

    private void RemoveControls(ControlReferenceSource source)
    {
        foreach (var (key, _) in Controls.Where(kv => kv.Value.Source == source).ToArray())
        {
            Controls.Remove(key);
        }
    }
}
