using Microsoft.CodeAnalysis.Text;

namespace WebForms.Models;

public record struct TokenPosition(int Offset, int Line, int Column)
{
    public override string ToString()
    {
        return $"{Line}:{Column}";
    }

    public static implicit operator OmniSharp.Extensions.LanguageServer.Protocol.Models.Position(TokenPosition pos)
    {
        return new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position(pos.Line, pos.Column);
    }

    public static implicit operator LinePosition(TokenPosition position)
    {
        return new LinePosition(position.Line, position.Column);
    }
}