using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Mono.Cecil;
using WebForms.Models;
using WebForms.Nodes;
using static OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;
using Diagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;
using DiagnosticSeverity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;

namespace WebForms.Roslyn;

public interface IMethodOrType
{
}

public sealed class TypeContainer
{
    private readonly Document _document;
    private readonly ProjectAssemblyResolver? _resolver;
    private readonly Dictionary<string, CodeType> _types = new();

    public TypeContainer(ProjectAssemblyResolver? resolver, Document document)
    {
        _resolver = resolver;
        _document = document;
    }

    public CodeType? Get(string type)
    {
        return _resolver?.ResolveType(type) is {} resolvedType
            ? Get(resolvedType)
            : null;
    }

    public CodeType Get(TypeReference reference)
    {
        var key = reference.FullName;
        
        if (_types.TryGetValue(key, out var codeType))
        {
            return codeType;
        }

        var control = _document.Controls.Values.Select(i => i.Control).FirstOrDefault(i => i.Type.FullName == key)
            ?? _document.Project?.NamespaceControls.SelectMany(i => i.Value).FirstOrDefault(i => i.Type.FullName == key);

        codeType = new CodeType(this, reference, new List<CodeTypeProperty>(), control);
        _types[key] = codeType;
        return codeType;
    } 
}

public record CodeMethod(string Name, CodeType ReturnType) : IMethodOrType;

public record CodeType(TypeContainer Container, TypeReference Type, List<CodeTypeProperty> CustomProperties, Control? Control) : IMethodOrType
{
    private TypeDefinition? _definition;

    public CodeType? BaseType => Definition.BaseType is { } baseType ? Container.Get(baseType) : null;

    private TypeDefinition Definition => _definition ??= Type.Resolve();

    public string Name => Type.Name;

    public string FullName => Type.FullName;
    
    public IEnumerable<CodeTypeProperty> Properties => Definition.Properties
        .Where(i => !i.GetMethod.IsPrivate)
        .Select(i => new CodeTypeProperty(i.Name, Container.Get(i.PropertyType)))
        .Concat(CustomProperties);
    
    public IEnumerable<CodeTypeProperty> Fields => Definition.Fields
        .Where(i => !i.IsPrivate)
        .Select(i => new CodeTypeProperty(i.Name, Container.Get(i.FieldType)));
    
    public IEnumerable<CodeMethod> Methods => Definition.Methods
        .Where(i => !i.IsPrivate)
        .Select(i => new CodeMethod(i.Name, Container.Get(i.ReturnType)));
    
    public IEnumerable<CodeTypeProperty> Members => Properties.Concat(Fields);
}

public record CodeTypeProperty(string Name, CodeType Type);

public class ExpressionVisitor
{
    private readonly List<Diagnostic> _diagnostics;
    private readonly Document _document;
    private readonly TokenRange _range;
    private readonly Node _node;

    public ExpressionVisitor(Document document, List<Diagnostic> diagnostics, TokenRange range, Node node)
    {
        _diagnostics = diagnostics;
        _range = range;
        _document = document;
        _node = node;
    }
    
    public TypeContainer? TypeContainer { get; set; }

    public IMethodOrType? Inspect(CodeType parent, ExpressionSyntax node)
    {
        switch (node)
        {
            case InvocationExpressionSyntax invocation:
            {
                var result = Inspect(parent, invocation.Expression);

                if (result == null)
                {
                    return null;
                }

                if (result is not CodeMethod method)
                {
                    AddDiagnostic(invocation.Expression.Span, "Method, delegate or event is expected");
                    return null;
                }

                foreach (var argument in invocation.ArgumentList.Arguments)
                {
                    Inspect(parent, argument.Expression);
                }

                return method.ReturnType;
            }
            case IdentifierNameSyntax identifier:
            {
                if (identifier.Identifier.Text == "Item" && TypeContainer != null)
                {
                    var type = _node.GetItemType();

                    if (type != null)
                    {
                        return TypeContainer.Get(type);
                    }
                }
                
                var result = GetMember(parent, identifier.Identifier);
            
                return result;
            }
            case MemberAccessExpressionSyntax member:
            {
                var expression = Inspect(parent, member.Expression);

                if (expression is CodeMethod method)
                {
                    AddDiagnostic(member.Expression.Span, $"{method.Name} is a method, which is not valid in the given context");
                    return null;
                }

                if (expression is not CodeType variable)
                {
                    return null;
                }
            
                return GetMember(variable, member.Name.Identifier);
            }
            default:
                return null;
        }
    }

    private void AddDiagnostic(TextSpan span, string message, DiagnosticSeverity severity = Error)
    {
        _diagnostics.Add(new Diagnostic
        {
            Message = message,
            Range = GetRange(span),
            Severity = severity
        });
    }

    private IMethodOrType? GetMember(CodeType type, SyntaxToken token)
    {
        var name = token.Text;
        var current = type;
        
        while (current != null)
        {
            if (current.Methods.FirstOrDefault(i => i.Name == name) is {} method)
            {
                return method;
            }

            if (current.Members.FirstOrDefault(i => i.Name == name) is {} property)
            {
                return property.Type;
            }

            current = current.BaseType;
        }
        
        AddDiagnostic(token.Span, $"'{type.Name}' does not contain a definition for '{token.Text}'");
        return null;
    }

    public TokenRange GetRange(TextSpan span)
    {
        return new TokenRange(
            GetPosition(span.Start),
            GetPosition(span.End)
        );
    }

    private TokenPosition GetPosition(int offset)
    {
        offset += _range.Start.Offset;
        
        for (var i = _range.End.Line; i >= 0; i--)
        {
            var lineOffset = _document.Lines[i];
            
            if (offset >= lineOffset)
            {
                return new TokenPosition(offset, i, offset - lineOffset);
            }
        }

        return default;
    }
}
