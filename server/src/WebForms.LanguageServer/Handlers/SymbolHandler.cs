using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using WebForms.Models;
using WebForms.Nodes;

namespace WebForms.Handlers;

public class DocumentSymbolHandler : IDocumentSymbolHandler
{
    private readonly TextDocumentContainer _documentContainer;

    public DocumentSymbolHandler(TextDocumentContainer documentContainer)
    {
        _documentContainer = documentContainer;
    }

    public Task<SymbolInformationOrDocumentSymbolContainer?> Handle(DocumentSymbolParams request, CancellationToken cancellationToken)
    {
        var symbols = new List<SymbolInformationOrDocumentSymbol>();

        if (_documentContainer.Documents.TryGetValue(request.TextDocument.Uri, out var document))
        {
            symbols.AddRange(document.Node.Children.OfType<ISymbolNode>().Select(i => new SymbolInformationOrDocumentSymbol(CreateSymbol(document, i))));
        }

        return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(new SymbolInformationOrDocumentSymbolContainer(symbols));
    }

    private static DocumentSymbol CreateSymbol(Document document, ISymbolNode node)
    {
        var children = node is ContainerNode container
            ? container.Children.OfType<ISymbolNode>().Select(n => CreateSymbol(document, n))
            : Enumerable.Empty<DocumentSymbol>();

        return node.CreateSymbol() with
        {
            Range = node.Range,
            SelectionRange = node.Range,
            Children = Container<DocumentSymbol>.From(children)
        };
    }

    public DocumentSymbolRegistrationOptions GetRegistrationOptions(
        DocumentSymbolCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new DocumentSymbolRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("webforms")
        };
    }
}
