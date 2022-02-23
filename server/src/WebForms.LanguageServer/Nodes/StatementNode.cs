using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using WebForms.Models;
using SymbolKind = OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind;

namespace WebForms.Nodes;

public class StatementNode : Node
{
    public StatementNode() : base(NodeType.Statement)
    {
    }
    
    public TokenString Text { get; set; }

    public override DocumentSymbol CreateSymbol()
    {
        return new DocumentSymbol
        {
            Name = "Embedded code block",
            Detail = "",
            Kind = SymbolKind.Method,
        };
    }
}
