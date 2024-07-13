using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace WebForms.Nodes;

public interface ISymbolNode : INode
{
    DocumentSymbol CreateSymbol();
}
