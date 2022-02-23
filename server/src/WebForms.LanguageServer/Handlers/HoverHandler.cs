using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace WebForms.Handlers;

public class HoverHandler : IHoverHandler
{
    private readonly TextDocumentContainer _documentContainer;

    public HoverHandler(TextDocumentContainer documentContainer)
    {
        _documentContainer = documentContainer;
    }

    public Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        var items = new List<DocumentHighlight>();

        if (_documentContainer.Documents.TryGetValue(request.TextDocument.Uri, out var document))
        {
            foreach (var hit in document.HitRanges)
            {
                if (hit.Range.Includes(request.Position.Line, request.Position.Character))
                {
                    return Task.FromResult(hit.Node.Hover(items, hit, document, request.Position));
                }
            }
        }

        return Task.FromResult<Hover?>(null);
    }

    public HoverRegistrationOptions GetRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities)
    {
        return new HoverRegistrationOptions
        {
            DocumentSelector = DocumentSelector.ForLanguage("webforms")
        };
    }
}
