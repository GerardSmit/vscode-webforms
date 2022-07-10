using System.Collections.Generic;
using System.Linq;
using WebForms.Models;
using Xunit;
using Xunit.Abstractions;
using static WebForms.Models.TokenType;

namespace WebForms.Tests;

public class LexerTest
{
    private readonly ITestOutputHelper _output;

    public LexerTest(ITestOutputHelper output)
    {
        _output = output;
    }

    // Directive
    [InlineData(
        @"<%@ Control Language=""C#"" Inherits=""Test.Namespace.Class"" %>",
        new[] { StartDirective, Attribute, Attribute, AttributeValue, Attribute, AttributeValue, EndDirective }
    )]

    // Expression
    [InlineData(
        @"<%= $""{""<%= Test %>""}"" %>",
        new[] { Expression }
    )]
    [InlineData(
        @"<%: Test %>",
        new[] { Expression }
    )]
    [InlineData(
        @"<%-- Test --%>",
        new[] { Comment }
    )]
    [InlineData(
        @"<%# Test %>",
        new[] { EvalExpression }
    )]

    // ASP.NET Control
    [InlineData(
        @"<asp:Literal ID=""Test"" runat=""server"" />",
        new[] { TagOpen, ElementNamespace, ElementName, Attribute, AttributeValue, Attribute, AttributeValue, TagSlashClose }
    )]

    // Void elements
    [InlineData(
        @"<br>",
        new[] { TagOpen, ElementName, TagSlashClose }
    )]
    [InlineData(
        @"<br />",
        new[] { TagOpen, ElementName, TagSlashClose }
    )]
        
    // Normal elements
    [InlineData(
        @"<div>",
        new[] { TagOpen, ElementName, TagClose }
    )]
    [InlineData(
        @"<div></div>",
        new[] { TagOpen, ElementName, TagClose, TagOpenSlash, ElementName, TagClose }
    )]

    // Attribute
    [InlineData(
        @"<div id=test>",
        new[] { TagOpen, ElementName, Attribute, AttributeValue, TagClose }
    )]
    
    // Inline Expression
    [InlineData(
        @"<div id=""Hello<%= World %>"">",
        new[] { TagOpen, ElementName, Attribute, AttributeValue, Expression, TagClose }
    )]

    // Text
    [InlineData(
        @"Hello world",
        new[] { Text }
    )]
        
    // DocType
    [InlineData(
        @"<!DOCTYPE html>",
        new[] { DocType }
    )]
    [InlineData(
        @"<!doctype html>",
        new[] { DocType }
    )]
        
    // Script
    [InlineData(
        @"<script type=""template""><div>Hello world</div></script>",
        new[] { TagOpen, ElementName, Attribute, AttributeValue, TagClose, Text, TagOpenSlash, ElementName, TagClose }
    )]

    // Head
    // https://github.com/dnnsoftware/Dnn.Platform/blob/3fc4cac7012c111e3df276c35505aec284191606/DNN%20Platform/Website/Default.aspx#L5
    [InlineData(
        @"<html <asp:literal id=""attributeList"" runat=""server"" />></html>",
        new[] { TagOpen, ElementName, TagOpen, ElementNamespace, ElementName, Attribute, AttributeValue, Attribute, AttributeValue, TagSlashClose, TagClose, TagOpenSlash, ElementName, TagClose }
    )]

    [Theory]
    public void Parse(string input, TokenType[] expected)
    {
        var lexer = new Lexer(input);

        var result = lexer.GetAll();
        var actual = result.Select(i => i.Type).ToArray();

        PrintValues(expected, actual);

        Assert.Equal(
            expected,
            actual
        );
    }

    // Script
    [InlineData(
        @"<script>Foo<%=Bar%>Baz</script>",
        new object[]
        {
            TagOpen, "",
            ElementName, "script",
            TagClose, "",
            Text, "Foo",
            Expression, "Bar",
            Text, "Baz",
            TagOpenSlash, "",
            ElementName, "script",
            TagClose, ""
        }
    )]
    [Theory]
    public void ParseText(string input, object[] expected)
    {
        var lexer = new Lexer(input);

        var result = lexer.GetAll();
        var actual = result.SelectMany(i => new object[] {i.Type, i.Text.Value}).ToArray();

        PrintValues(actual, expected);

        Assert.Equal(
            expected,
            actual
        );
    }

    // Lines
    [InlineData(
        "<%= 0 %>\n<%= 1 %><%= 1 %>\n<%= 2 %>\n<%= 3 %>",
        new[] { 0, 0, 1, 1, 1, 2, 2, 3 }
    )]
    [InlineData(
        "<%= 0 %>\r\n<%= 1 %><%= 1 %>\r\n<%= 2 %>\r\n<%= 3 %>",
        new[] { 0, 0, 1, 1, 1, 2, 2, 3 }
    )]
    [Theory]
    public void ParseLine(string input, int[] expected)
    {
        var lexer = new Lexer(input);

        var result = lexer.GetAll();
        var actual = result.Select(i => i.Range.Start.Line).ToArray();

        PrintValues(expected, actual);

        Assert.Equal(
            expected,
            actual
        );
    }

    // Lines
    [InlineData(
        @"<%@ Control",
        new[] { 0, 4 }
    )]
    [Theory]
    public void ParseColumn(string input, int[] expected)
    {
        var lexer = new Lexer(input);

        var result = lexer.GetAll();
        var actual = result.Select(i => i.Range.Start.Column).ToArray();

        PrintValues(expected, actual);

        Assert.Equal(
            expected,
            actual
        );
    }

    private void PrintValues<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        _output.WriteLine($"{"Expected",-20}   Actual");
        _output.WriteLine(new string('-', 41));
        _output.WriteLine(string.Join("\n", Merge(expected, actual, (left, right) => $"{left,-20} {(Equals(left, right) ? " " : "!")} {right}")));
    }

    public static IEnumerable<TResult> Merge<TLeft, TRight, TResult>(IEnumerable<TLeft> first, IEnumerable<TRight> second, System.Func<TLeft?, TRight?, TResult> operation)
    {
        using var left = first.GetEnumerator();
        using var right = second.GetEnumerator();

        while (left.MoveNext())
        {
            if (right.MoveNext())
            {
                yield return operation(left.Current, right.Current);
            }
            else
            {
                yield return operation(left.Current, default);
            }
        }
        while (right.MoveNext())
        {
            yield return operation(default, right.Current);
        }
    }
}
