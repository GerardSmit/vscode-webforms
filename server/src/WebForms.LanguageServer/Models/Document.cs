using Microsoft.CodeAnalysis.CSharp;
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
using DiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace WebForms.Models;

public enum ControlReferenceSource
{
    Project,
    Document
}

public record struct ControlReference(Control Control, ControlReferenceSource Source);

public class Document
{
    private readonly ILanguageServerFacade _languageServer;
    private string _text = "";

    public Document(DocumentUri uri, ILanguageServerFacade languageServer)
    {
        _languageServer = languageServer;
        Uri = uri;
    }

    public DocumentUri Uri { get; }

    public Project? Project { get; set; }

    public bool IsDirty { get; set; }

    public int Version { get; set; }

    public RootNode Node { get; set; } = new();

    public TypeDefinition? Type { get; set; }

    public List<HitRange> HitRanges { get; set; } = new();

    public List<int> Lines { get; set; } = new();

    public Dictionary<string, ControlReference> Controls { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    
    public TokenString Code { get; set; }

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            IsDirty = true;

            var parser = new Parser();
            var lexer = new Lexer(value);

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

            // Controls
            UpdateDiagnostics();
        }
    }

    public void UpdateProject()
    {
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
                Controls[tagPrefix + ":" + control.Name] = new ControlReference(control, ControlReferenceSource.Project);
            }
        }
    }

    public void UpdateDiagnostics()
    {
        var directive = Node.Directives.FirstOrDefault(i => i.DirectiveType is DirectiveType.Control or DirectiveType.Page);

        if (directive != null && directive.Attributes.TryGetValue("inherits", out var inherits))
        {
            Type = Project?.Resolver.ResolveType(inherits);
        }
        
        var diagnostics = new List<Diagnostic>();

        RemoveControls(ControlReferenceSource.Document);

        foreach (var element in Node.AllNodes)
        {
            if (element is DirectiveNode directiveNode)
            {
                if (directiveNode.DirectiveType == DirectiveType.Register &&
                    directiveNode.Attributes.TryGetValue("tagprefix", out var tagPrefix))
                {
                    if (directiveNode.Attributes.TryGetValue("namespace", out var ns) &&
                        Project is {} project &&
                        project.NamespaceControls.TryGetValue(ns.Value, out var controls))
                    {
                        foreach (var control in controls)
                        {
                            Controls[tagPrefix.Value + ":" + control.Name] = new ControlReference(control, ControlReferenceSource.Document);
                        }
                    }
                }
            }
            else if (element is ExpressionNode expressionNode)
            {
                var visitor = new ExpressionVisitor(this, diagnostics, expressionNode.Text.Range);

                if (Type != null)
                {
                    visitor.Inspect(Type, expressionNode.Expression);
                }

                AddDiagnostics(expressionNode.Expression.GetDiagnostics(), diagnostics, visitor);
            }
        }

        var tree = SyntaxFactory.ParseSyntaxTree(Code);
        var rootVisitor = new ExpressionVisitor(this, diagnostics, Code.Range);

        AddDiagnostics(tree.GetDiagnostics(), diagnostics, rootVisitor);
        
        _languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Diagnostics = diagnostics,
            Uri = Uri
        });
    }

    private static void AddDiagnostics(IEnumerable<Microsoft.CodeAnalysis.Diagnostic> statementNode, List<Diagnostic> diagnostics, ExpressionVisitor visitor)
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
                Range = visitor.GetRange(diagnostic.Location.SourceSpan),
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
