using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using WebForms.Models;
using WebForms.Nodes;

namespace WebForms;

public class Parser
{
    private readonly ParserContainer _container = new();
    
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
                ConsumeExpression(token);
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

    private void ConsumeExpression(Token token)
    {
        var element = new ExpressionNode
        {
            Range = token.Range,
            Text = token.Text,
            Expression = SyntaxFactory.ParseExpression(token.Text)
        };

        _container.Add(element);
    }

    private void ConsumeStatement(Token token)
    {
        var element = new StatementNode
        {
            Range = token.Range,
            Text = token.Text
        };

        _container.Add(element);
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
                _container.Add(element);
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

        if (pop.Name != name.Text || pop.Namespace != endNamespace)
        {
            return;
        }
        
        pop.Range = pop.Range.WithEnd(endPosition);

        pop.EndTag = new()
        {
            Name = name.Text,
            Namespace = endNamespace,
            Range = new TokenRange(startPosition, lexer.Position)
        };

    }
}
