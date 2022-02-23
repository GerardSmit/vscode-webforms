using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Mono.Cecil;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using WebForms.Models;

namespace WebForms.Roslyn;

public class ExpressionVisitor
{
    private readonly List<Diagnostic> _diagnostics;
    private readonly Document _document;
    private readonly TokenRange _range;

    public ExpressionVisitor(Document document, List<Diagnostic> diagnostics, TokenRange range)
    {
        _diagnostics = diagnostics;
        _range = range;
        _document = document;
    }

    public object? Inspect(TypeDefinition parent, ExpressionSyntax node)
    {
        if (node is InvocationExpressionSyntax invocation)
        {
            var result = Inspect(parent, invocation.Expression);

            if (result == null)
            {
                return null;
            }

            if (result is not MethodDefinition method)
            {
                _diagnostics.Add(new Diagnostic
                {
                    Message = "Not an method"
                });
                
                return null;
            }

            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                Inspect(parent, argument.Expression);
            }

            return method.ReturnType;
        }

        if (node is IdentifierNameSyntax identifier)
        {
            var name = identifier.Identifier.Text;
            var result = GetMember(parent, name);

            if (result == null)
            {
                _diagnostics.Add(new Diagnostic
                {
                    Message = "Not an member",
                    Range = GetRange(identifier.Identifier.Span)
                });
            }
            
            return result;
        }

        return null;
    }

    private static object? GetMember(TypeDefinition type, string name)
    {
        var result = (object?) type.Methods.FirstOrDefault(i => i.Name == name) ??
                     (object?) type.Properties.FirstOrDefault(i => i.Name == name) ??
                     type.Fields.FirstOrDefault(i => i.Name == name);

        if (result != null)
        {
            return result;
        }

        return type.BaseType?.Resolve() is { } parent
            ? GetMember(parent, name)
            : null;
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
