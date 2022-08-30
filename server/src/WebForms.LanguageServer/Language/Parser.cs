using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using WebForms.Models;
using WebForms.Nodes;
using WebForms.Roslyn;

namespace WebForms;

public class Parser
{
    private readonly List<Diagnostic> _diagnostics;
    private readonly ParserContainer _rootContainer = new();
    private ParserContainer _container;
    private ParserContainer? _headerContainer;
    private int _expressionId;
    private string? _itemType;

    public Parser(List<Diagnostic> diagnostics)
    {
        _diagnostics = diagnostics;
        _container = _rootContainer;
    }

    public RootNode Root => _container.Root;

    public void Parse(ref Lexer lexer)
    {
        while (lexer.Next() is { } token)
        {
            Consume(ref lexer, token);
        }
    }

    private void Consume(ref Lexer lexer, Token token)
    {
        switch (token.Type)
        {
            case TokenType.Expression:
                ConsumeExpression(ref lexer, token, false);
                break;
            case TokenType.EvalExpression:
                ConsumeExpression(ref lexer, token, true);
                break;
            case TokenType.Statement:
                ConsumeStatement(token);
                break;
            case TokenType.TagOpen:
                ConsumeOpenTag(ref lexer, token.Range.Start);
                break;
            case TokenType.TagOpenSlash:
                ConsumeCloseTag(ref lexer, token.Range.Start);
                break;
            case TokenType.StartDirective:
                ConsumeDirective(ref lexer, token.Range.Start);
                break;
        }
    }

    private void ConsumeExpression(ref Lexer lexer, Token token, bool isEval)
    {
        var id = _expressionId++;
        var element = new ExpressionNode(id)
        {
            Range = token.Range,
            Text = token.Text,
            Expression = SyntaxFactory.ParseExpression(token.Text),
            IsEval = isEval,
            ItemType = isEval ? _itemType : null
        };

        _container.AddExpression(element);
        Root.Expressions[id] = element;

        var codeBuilder = lexer.CodeBuilder;
        var str = id.ToString();
        var length = DocumentVisitor.ExpressionStart.Length + DocumentVisitor.ExpressionEnd.Length + str.Length;

        codeBuilder.Length -= length;
        codeBuilder.Append(DocumentVisitor.ExpressionStart);
        codeBuilder.Append(str);
        codeBuilder.Append(DocumentVisitor.ExpressionEnd);
    }

    private void ConsumeStatement(Token token)
    {
        var element = new StatementNode
        {
            Range = token.Range,
            Text = token.Text
        };

        _container.AddStatement(element);
    }

    private void ConsumeDirective(ref Lexer lexer, TokenPosition startPosition)
    {
        var element = new DirectiveNode
        {
            Range = new TokenRange(startPosition, startPosition)
        };

        var isFirst = true;

        while (lexer.Next() is { } next)
        {
            if (next.Type == TokenType.Attribute)
            {
                TokenString value = default;

                if (lexer.Peek() is { Type: TokenType.AttributeValue } valueNode)
                {
                    lexer.Next();
                    value = valueNode.Text;
                }

                if (isFirst)
                {
                    element.DirectiveType = Enum.TryParse<DirectiveType>(next.Text, true, out var type) ? type : DirectiveType.Unknown;
                    isFirst = false;
                }
                else
                {
                    element.Attributes.Add(next.Text, value);
                }
            }
            else if (next.Type == TokenType.EndDirective)
            {
                element.Range = element.Range.WithEnd(next.Range.End);
                _container.AddDirective(element);
                break;
            }
            else
            {
                Consume(ref lexer, next);
            }
        }
    }

    private void ConsumeOpenTag(ref Lexer lexer, TokenPosition startPosition)
    {
        var element = new HtmlNode
        {
            Range = new TokenRange(startPosition, startPosition)
        };

        if (lexer.Peek() is { Type: TokenType.ElementNamespace } ns)
        {
            element.StartTag.Namespace = ns.Text;
            lexer.Next();
        }

        if (lexer.Peek() is not { Type: TokenType.ElementName } name)
        {
            return;
        }

        lexer.Next();
        element.StartTag.Name = name.Text;
        _container.Push(element);

        while (lexer.Next() is { } next)
        {
            if (next.Type == TokenType.Attribute)
            {
                TokenString value = default;

                if (lexer.Peek() is { Type: TokenType.AttributeValue } valueNode)
                {
                    lexer.Next();
                    value = valueNode.Text;
                }

                if (next.Text.Value.Equals("runat", StringComparison.OrdinalIgnoreCase) &&
                    value.Value.Equals("server", StringComparison.OrdinalIgnoreCase))
                {
                    element.RunAt = RunAt.Server;
                }
                else
                {
                    if (next.Text.Value.Equals("itemtype", StringComparison.OrdinalIgnoreCase))
                    {
                        _itemType = value.Value;
                    }

                    element.Attributes.Add(next.Text, value);
                }
            }
            else if (next.Type == TokenType.TagSlashClose)
            {
                element.Range = element.Range.WithEnd(next.Range.End);
                _container.Pop();
                break;
            }
            else if (next.Type == TokenType.TagClose)
            {
                element.Range = element.Range.WithEnd(next.Range.End);
                break;
            }
            else
            {
                Consume(ref lexer, next);
            }
        }

        element.StartTag.Range = element.Range;

        switch (name.Text.Value)
        {
            case "HeaderTemplate":
                _container = _headerContainer = new ParserContainer(_rootContainer);
                break;
            case "FooterTemplate" when _headerContainer is not null:
                _container = _headerContainer;
                break;
            case "FooterTemplate":
                _diagnostics.Add(new Diagnostic
                {
                    Range = element.Name.Range,
                    Message = "Footer template should be below the header template",
                    Severity = DiagnosticSeverity.Warning
                });
                break;
        }
    }

    private void ConsumeCloseTag(ref Lexer lexer, TokenPosition startPosition)
    {
        TokenString? endNamespace = null;

        if (lexer.Peek() is {Type: TokenType.ElementNamespace} ns)
        {
            endNamespace = ns.Text;
            lexer.Next();
        }

        if (lexer.Peek() is not {Type: TokenType.ElementName} name)
        {
            return;
        }

        if (name.Text.Value is "HeaderTemplate" or "FooterTemplate")
        {
            _container = _rootContainer;
        }

        var endPosition = name.Range.End;
        lexer.Next();

        if (lexer.Peek() is {Type: TokenType.ElementName} end)
        {
            endPosition = end.Range.End;
            lexer.Next();
        }

        if (lexer.Peek() is { Type: TokenType.TagClose })
        {
            lexer.Next();
        }

        var pop = _container.Pop();

        if (pop == null)
        {
            return;
        }

        if (!pop.Name.Value.Equals(name.Text.Value, StringComparison.OrdinalIgnoreCase) ||
            pop.Namespace.HasValue != endNamespace.HasValue ||
            pop.Namespace.HasValue && !pop.Namespace.Value.Value.Equals(endNamespace?.Value, StringComparison.OrdinalIgnoreCase))
        {
            _diagnostics.Add(new Diagnostic
            {
                Range = endNamespace is null
                    ? name.Range
                    : endNamespace.Value.Range.WithEnd(name.Range.End),
                Message = $"Expected end-tag {(pop.Namespace.HasValue ? pop.Namespace.Value + ':' : "")}{pop.Name}, but got {(endNamespace.HasValue ? endNamespace.Value + ':' : "")}{name} instead",
                Severity = DiagnosticSeverity.Warning
            });

            return;
        }
        
        pop.Range = pop.Range.WithEnd(endPosition);

        pop.EndTag = new HtmlTagNode
        {
            Name = name.Text,
            Namespace = endNamespace,
            Range = new TokenRange(startPosition, lexer.Position)
        };
    }
}
