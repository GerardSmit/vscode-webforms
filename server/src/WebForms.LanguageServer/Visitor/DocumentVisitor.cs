using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WebForms.Models;
using WebForms.Nodes;
using Diagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;

namespace WebForms.Roslyn;

public class DocumentVisitor : CSharpSyntaxWalker
{
    public const string ExpressionStart = "/*§";
    public const string ExpressionEnd = "*/";

    private readonly TokenRange _range;

    public DocumentVisitor(RootNode node,
        Document document,
        TokenRange range,
        CodeType type,
        TypeContainer typeContainer,
        List<Diagnostic> diagnostics)
    {
        Node = node;
        Document = document;
        _range = range;
        Type = type;
        TypeContainer = typeContainer;
        Diagnostics = diagnostics;
    }

    public CodeType Type {get; }

    public TypeContainer TypeContainer {get; }

    public List<Diagnostic> Diagnostics {get; }

    public RootNode Node { get; }

    public Document Document { get; }

    public override void DefaultVisit(SyntaxNode node)
    {
        VisitTrivia(node);

        if (node is ExpressionSyntax expressionNode)
        {
            var visitor = new ExpressionVisitor(this, false, _range);
            visitor.Inspect(expressionNode);

            return;
        }

        base.DefaultVisit(node);
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        DefaultVisit(node.Expression);
    }

    private void VisitTrivia(SyntaxNode node)
    {
        if (node.HasLeadingTrivia)
        {
            foreach (var trivia in node.GetLeadingTrivia())
            {
                InspectTrivia(trivia);
            }
        }

        if (node.HasTrailingTrivia)
        {
            foreach (var trivia in node.GetTrailingTrivia())
            {
                InspectTrivia(trivia);
            }
        }
    }

    public void InspectTrivia(SyntaxTrivia trivia)
    {
        if (!TryGetValue(trivia, out var node))
        {
            return;
        }

        var visitor = new ExpressionVisitor(this, node.IsEval, node.Text.Range);
        visitor.Inspect(node.Expression);
    }

    private bool TryGetValue(SyntaxTrivia trivia, [NotNullWhen(true)] out ExpressionNode? node)
    {
        if (!trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
        {
            node = null;
            return false;
        }

        var value = trivia.ToString();

        if (!value.StartsWith(ExpressionStart, StringComparison.Ordinal) ||
            !value.EndsWith(ExpressionEnd, StringComparison.Ordinal))
        {
            node = null;
            return false;
        }

        var span = value.AsSpan();
        var idSpan = span[ExpressionStart.Length..^ExpressionEnd.Length];

        if (!int.TryParse(idSpan, out var id) ||
            !Node.Expressions.TryGetValue(id, out var expression))
        {
            node = null;
            return false;
        }

        node = expression;
        return true;
    }

}