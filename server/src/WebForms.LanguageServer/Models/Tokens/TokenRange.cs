﻿namespace WebForms.Models;

public record struct OffsetRange(int Start, int End);

public record struct TokenRange(TokenPosition Start, TokenPosition End)
{
    public override string ToString()
    {
        return $"{Start} - {End}";
    }

    public bool Includes(int line, int column)
    {
        return (line > Start.Line || line == Start.Line && column >= Start.Column) &&
               (line < End.Line || line == End.Line && column <= End.Column);
    }

    public static implicit operator OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(TokenRange range)
    {
        return new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(range.Start, range.End);
    }

    public static implicit operator OffsetRange(TokenRange range)
    {
        return new OffsetRange(range.Start.Offset, range.End.Offset);
    }

    public TokenRange WithEnd(TokenPosition end)
    {
        return new TokenRange(Start, end);
    }
}
