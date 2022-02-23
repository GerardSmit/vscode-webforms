using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace WebForms.Handlers;

public class HighlightHandler : IDocumentHighlightHandler
{
    private readonly TextDocumentContainer _documentContainer;

    public HighlightHandler(TextDocumentContainer documentContainer)
    {
        _documentContainer = documentContainer;
    }

    public Task<DocumentHighlightContainer?> Handle(DocumentHighlightParams request, CancellationToken cancellationToken)
    {
        var items = new List<DocumentHighlight>();

        if (_documentContainer.Documents.TryGetValue(request.TextDocument.Uri, out var document))
        {
            foreach (var hit in document.HitRanges)
            {
                if (hit.Range.Includes(request.Position.Line, request.Position.Character))
                {
                    hit.Node.Highlight(items, hit, document, request.Position);
                    break;
                }
            }
        }

        return Task.FromResult<DocumentHighlightContainer?>(items);
    }

    public DocumentHighlightRegistrationOptions GetRegistrationOptions(DocumentHighlightCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new DocumentHighlightRegistrationOptions
        {
            DocumentSelector = DocumentSelector.ForLanguage("webforms")
        };
    }
}
