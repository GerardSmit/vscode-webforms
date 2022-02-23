﻿using WebForms.Models;

namespace WebForms;

public ref struct Lexer
{
    private static readonly ReadOnlyMemory<char> StartDirective = "<%@".ToCharArray();
    private static readonly ReadOnlyMemory<char> StartDocType = "<!DOCTYPE".ToCharArray();
    private static readonly ReadOnlyMemory<char> StartStatement = "<%".ToCharArray();
    private static readonly ReadOnlyMemory<char> End = "%>".ToCharArray();
    private static readonly ReadOnlyMemory<char> StartComment = "<!--".ToCharArray();
    private static readonly ReadOnlyMemory<char> EndComment = "-->".ToCharArray();
    private static readonly ReadOnlyMemory<char> RunAt = "runat".ToCharArray();

    private readonly ReadOnlySpan<char> _startDirective;
    private readonly ReadOnlySpan<char> _startStatement;
    private readonly ReadOnlySpan<char> _end;
    private readonly ReadOnlySpan<char> _startDocType;
    private readonly ReadOnlySpan<char> _startComment;
    private readonly ReadOnlySpan<char> _endComment;
    private readonly ReadOnlySpan<char> _runAt;

    private readonly List<Token> _nodes;
    private readonly ReadOnlySpan<char> _input;
    private int _offset;
    private int _line;
    private int _column;
    private int _nodeOffset;
    private bool _ignoreNewLine;

    public Lexer(ReadOnlySpan<char> input)
    {
        _nodes = new List<Token>();
        _startDirective = StartDirective.Span;
        _startStatement = StartStatement.Span;
        _startDocType = StartDocType.Span;
        _startComment = StartComment.Span;
        _endComment = EndComment.Span;
        _runAt = RunAt.Span;
        _end = End.Span;
        _input = input;
        _line = 0;
        _column = 0;
        _offset = 0;
        _nodeOffset = -1;
        _ignoreNewLine = false;
    }

    public List<int> Lines { get; } = new() { 0 };

    public TokenPosition Position => new(_offset, _line, _column);

    private char Current => _offset < _input.Length ? _input[_offset] : '\0';

    public bool HasNext => _offset < _input.Length || _nodeOffset < _nodes.Count;

    public void Forward()
    {
        _column++;
        CheckNewLine();
        _offset++;
    }

    public void Forward(int length)
    {
        _column += length;
        CheckNewLine();
        _offset += length;
    }

    private void CheckNewLine()
    {
        if (_offset >= _input.Length)
        {
            return;
        }

        var current = Current;
        var isNewLine = current is '\r' or '\n';

        if (_ignoreNewLine)
        {
            _ignoreNewLine = false;
        }
        else if (isNewLine)
        {
            _line++;
            _ignoreNewLine = current == '\r';
            
            Lines.Add(_offset + (_ignoreNewLine ? 2 : 1));
        }

        if (isNewLine)
        {
            _column = 0;
        }
    }

    public List<Token> GetAll()
    {
        while (Consume())
        {
            // next
        }

        return _nodes;
    }

    public Token? Next()
    {
        var result = Peek();

        if (result.HasValue)
        {
            _nodeOffset++;
        }

        return result;
    }

    public Token? Peek()
    {
        var offset = _nodeOffset + 1;

        if (offset >= _nodes.Count && !Consume())
        {
            return null;
        }

        if (offset >= _nodes.Count)
        {
            return null;
        }

        return _nodes[offset];
    }

    private bool Consume()
    {
        if (_offset >= _input.Length)
        {
            return false;
        }

        if (ConsumeComment())
        {
            return true;
        }

        if (ConsumeDirective())
        {
            return true;
        }

        if (ConsumeInline())
        {
            return true;
        }

        if (ConsumeDocType())
        {
            return true;
        }

        if (ConsumeElement())
        {
            return true;
        }

        var start = Position;
        SkipUntil('<');
        AddNode(TokenType.Text, start);
        return true;
    }

    private bool ConsumeComment()
    {
        return Consume(_startComment, _endComment, TokenType.Comment);
    }

    private bool ConsumeDirective()
    {
        if (!Consume(_startDirective, TokenType.StartDirective))
        {
            return false;
        }

        SkipWhiteSpace();

        while (!Consume(_end, TokenType.EndDirective) && ReadAttribute())
        {
            SkipWhiteSpace();
        }

        return true;

    }

    private bool ConsumeDocType()
    {
        if (!Consume(_startDocType, true))
        {
            return false;
        }

        var offsetStart = Position;
        SkipUntil('>');
        AddNode(TokenType.DocType, offsetStart);
        Forward();
        return true;
    }

    private bool ConsumeWebFormsTag()
    {
        return ConsumeElement(true) || ConsumeInline();
    }

    private bool ConsumeElement(bool requireRunAt = false)
    {
        if (requireRunAt)
        {
            if (Current != '<')
            {
                return false;
            }

            var slice = _input[_offset..];
            var last = slice.IndexOf('>');

            if (last == -1)
            {
                return false;
            }

            if (!slice[..last].Contains(_runAt, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var start = Position;

        if (!Consume('<'))
        {
            return false;
        }

        var isClosingTag = Consume('/');
        AddNode(isClosingTag ? TokenType.TagOpenSlash : TokenType.TagOpen, start, default(TokenString));

        start = Position;
        var name = ReadTagName();

        if (name.Value.Length == 0)
        {
            return false;
        }

        if (Current == ':')
        {
            AddNode(TokenType.ElementNamespace, start, name);
            start = Position;
            Forward();
            name = ReadTagName();
        }

        AddNode(TokenType.ElementName, new TokenRange(start, Position), name);

        var isVoidTag = name.Value is "area" or "base" or "br" or "col" or "command" or "embed"
            or "hr" or "img" or "input" or "keygen" or "link" or "meta"
            or "param" or "source" or "track" or "wbr";

        var hasClosing = false;

        if (!isClosingTag)
        {
            SkipWhiteSpace();

            while (ConsumeWebFormsTag() || ReadAttribute())
            {
                SkipWhiteSpace();
            }

            SkipWhiteSpace();

            start = Position;

            if (isVoidTag)
            {
                hasClosing = isVoidTag;
            }

            if (Current == '/')
            {
                hasClosing = true;
                Forward();
                SkipWhiteSpace();
            }

            if (!hasClosing && (
                    name.Value.Equals("script", StringComparison.OrdinalIgnoreCase) ||
                    name.Value.Equals("style", StringComparison.OrdinalIgnoreCase)
                ))
            {
                Consume('>');
                AddNode(TokenType.TagClose, start, default(TokenString));
                start = Position;

                while (SkipUntil('<'))
                {
                    var end = Position;
                    var index = _nodes.Count;

                    if (ConsumeWebFormsTag())
                    {
                        var text = CreateString(start, end);
                        InsertNode(index, TokenType.Text, text.Range, text);
                        start = Position;
                        continue;
                    }

                    Forward();

                    if (!Peek('/'))
                    {
                        continue;
                    }

                    var range = new TokenRange(end, Position);

                    Forward();

                    SkipWhiteSpace();
                    var currentName = ReadTagName();

                    if (name.Value.Equals(currentName.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        var text = CreateString(start, end);
                        AddNode(TokenType.Text, text.Range, text);
                        AddNode(TokenType.TagOpenSlash, range);
                        AddNode(TokenType.ElementName, start, currentName);
                        SkipWhiteSpace();
                        break;
                    }
                }
            }
        }

        if (Consume('>'))
        {
            AddNode(hasClosing ? TokenType.TagSlashClose : TokenType.TagClose, start, default(TokenString));
        }

        return true;
    }

    private bool ConsumeInline()
    {
        var start = Position;

        if (!Consume(_startStatement))
        {
            return false;
        }

        var type = TokenType.Statement;

        if (Consume(':') || Consume('='))
        {
            type = TokenType.Expression;
        }
        else if (Consume('#'))
        {
            type = TokenType.EvalExpression;
        }

        return ConsumeUntil(_startStatement, _end, type, start);
    }

    private bool ConsumeInlineSkipWhiteSpace()
    {
        var result = false;
        SkipWhiteSpace();

        if (!ConsumeInline())
        {
            return false;
        }

        do
        {
            SkipWhiteSpace();
        } while (ConsumeInline());

        return result;
    }

    public bool ReadAttribute()
    {
        // https://www.w3.org/TR/2011/WD-html5-20110525/syntax.html#attributes-0
        ConsumeInlineSkipWhiteSpace();

        if (IsAttributeSeparator(Current))
        {
            return false;
        }

        var start = Position;

        while (_offset < _input.Length && !IsAttributeSeparator(Current))
        {
            Forward();
        }

        AddNode(TokenType.Attribute, start);

        ConsumeInlineSkipWhiteSpace();

        if (!Peek('='))
        {
            return true;
        }

        Forward();

        ConsumeInlineSkipWhiteSpace();
        var current = Current;

        if (current is '"' or '\'')
        {
            Forward();
            start = Position;
            SkipUntil(current, true);
            AddNode(TokenType.AttributeValue, start);
            Forward();
            return true;
        }

        start = Position;

        while (_offset < _input.Length && !IsInvalidAttributeValueCharacter(Current))
        {
            Forward();
        }

        AddNode(TokenType.AttributeValue, start);
        return true;
    }

    public TokenString ReadTagName()
    {
        var start = Position;

        while (_offset < _input.Length && IsTagCharacter(Current))
        {
            Forward();
        }

        return CreateString(start, Position);
    }
        
    private void AddNode(TokenType type, TokenPosition start)
    {
        AddNode(type, start, Position);
    }

    private void AddNode(TokenType type, TokenPosition start, TokenPosition end)
    {
        AddNode(type, new TokenRange(start, end), CreateString(start, end));
    }

    private void InsertNode(int index, TokenType type, TokenRange range, TokenString value = default)
    {
        _nodes.Insert(index, new Token(type, range, value));
    }

    private void AddNode(TokenType type, TokenRange range, TokenString value = default)
    {
        _nodes.Add(new Token(type, range, value));
    }

    private void AddNode(TokenType type, TokenPosition start, TokenString value)
    {
        _nodes.Add(new Token(type, new TokenRange(start, Position), value));
    }

    private TokenString CreateString(TokenPosition start, TokenPosition end)
    {
        return new TokenString(new string(_input.Slice(start.Offset, end.Offset - start.Offset)), new TokenRange(start, end));
    }

    public void SkipWhiteSpace()
    {
        while (_offset < _input.Length && IsSpaceCharacter(Current))
        {
            Forward();
        }
    }

    private static bool IsTagCharacter(char c)
    {
        // https://www.w3.org/TR/2011/WD-html5-20110525/syntax.html#syntax-tag-name
        return c
                is >= (char)0x0030 and <= (char)0x0039 // U+0030 DIGIT ZERO (0) to U+0039 DIGIT NINE (9)
                or >= (char)0x0061 and <= (char)0x007A // U+0061 LATIN SMALL LETTER A to U+007A LATIN SMALL LETTER Z
                or >= (char)0x0041 and <= (char)0x005A // U+0041 LATIN CAPITAL LETTER A to U+005A LATIN CAPITAL LETTER Z
            ;
    }

    private static bool IsSpaceCharacter(char c)
    {
        // https://www.w3.org/TR/2011/WD-html5-20110525/common-microsyntaxes.html#space-character
        return c
                is (char)0x0020 // U+0020 SPACE
                or (char)0x0009 // U+0009 CHARACTER TABULATION (tab)
                or (char)0x000A // U+000A LINE FEED (LF)
                or (char)0x000C // U+000C FORM FEED (FF)
                or (char)0x000D // U+000D CARRIAGE RETURN (CR)
            ;
    }

    private static bool IsAttributeSeparator(char c)
    {
        // https://www.w3.org/TR/2011/WD-html5-20110525/syntax.html#attributes-0
        return IsSpaceCharacter(c) || c
                is (char)0x0000 // U+0000 NULL
                or (char)0x0022 // U+0022 QUOTATION MARK (")
                or (char)0x0027 // U+0027 APOSTROPHE (')
                or (char)0x003E // U+003E GREATER-THAN SIGN (>)
                or (char)0x002F // U+002F SOLIDUS (/)
                or (char)0x003D // U+003D EQUALS SIGN (=)
            ;
    }

    private bool IsInvalidAttributeValueCharacter(char c)
    {
        // https://www.w3.org/TR/2011/WD-html5-20110525/syntax.html#attributes-0
        return IsAttributeSeparator(c) || c
                is (char)0x003C // U+003C LESS-THAN SIGN characters (<)
                or (char)0x003E // U+003E GREATER-THAN SIGN characters (>)
                or (char)0x0060 // U+0060 GRAVE ACCENT characters (`)
            ;
    }

    private bool ConsumeUntil(ReadOnlySpan<char> start, ReadOnlySpan<char> end, TokenType type, TokenPosition offsetStart)
    {
        var textStart = Position;

        if (!SkipUntil(start, end))
        {
            AddNode(type, new TokenRange(offsetStart, Position), CreateString(textStart, Position));
            return true;
        }

        AddNode(type, new TokenRange(offsetStart, Position), CreateString(textStart, Position));
        Forward(end.Length);
        return true;
    }

    private bool Consume(ReadOnlySpan<char> data, ReadOnlySpan<char> end, TokenType type)
    {
        var start = Position;

        if (!Consume(data))
        {
            return false;
        }

        return ConsumeUntil(data, end, type, start);
    }

    private bool Consume(ReadOnlySpan<char> data, TokenType type, bool ignoreCase = false)
    {
        var start = Position;
        if (!Consume(data, ignoreCase))
        {
            return false;
        }
        AddNode(type, start);
        return true;
    }

    private bool Consume(ReadOnlySpan<char> data, bool ignoreCase = false)
    {
        if (!Peek(data, ignoreCase))
        {
            return false;
        }

        Forward(data.Length);
        return true;
    }

    private bool Consume(char c)
    {
        if (!Peek(c))
        {
            return false;
        }

        Forward();
        return true;

    }

    private bool Peek(char c)
    {
        return Current == c;
    }

    private bool Peek(ReadOnlySpan<char> data, bool ignoreCase = false)
    {
        if (_input.Length - _offset < data.Length)
        {
            return false;
        }

        var left = _input.Slice(_offset, data.Length);

        return ignoreCase
            ? left.Equals(data, StringComparison.OrdinalIgnoreCase)
            : left.SequenceEqual(data);
    }

    private bool SkipUntil(ReadOnlySpan<char> start, ReadOnlySpan<char> end)
    {
        var depth = 1;

        for (; _offset < _input.Length; Forward())
        {
            var current = _input[_offset];

            if (current == start[0] && Peek(start))
            {
                depth++;
            }

            if (current == end[0] && Peek(end))
            {
                if (--depth == 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool SkipUntil(char untilChar, bool breakOnNewLine = false)
    {
        for (; _offset < _input.Length; Forward())
        {
            var current = _input[_offset];

            if (breakOnNewLine && current is '\n' or '\r')
            {
                return false;
            }

            if (current == '\\')
            {
                Forward();
                continue;
            }

            if (current == untilChar)
            {
                return true;
            }
        }

        return false;
    }
}
