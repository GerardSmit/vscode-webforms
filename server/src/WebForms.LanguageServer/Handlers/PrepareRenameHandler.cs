using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace WebForms.Handlers;

public class PrepareRenameHandler : IPrepareRenameHandler
{
    private readonly TextDocumentContainer _documentContainer;

    public PrepareRenameHandler(TextDocumentContainer documentContainer)
    {
        _documentContainer = documentContainer;
    }

    public Task<RangeOrPlaceholderRange?> Handle(PrepareRenameParams request, CancellationToken cancellationToken)
    {
        if (_documentContainer.Documents.TryGetValue(request.TextDocument.Uri, out var document))
        {
            foreach (var hit in document.HitRanges)
            {
                if (hit.Range.Includes(request.Position.Line, request.Position.Character))
                {
                    return Task.FromResult<RangeOrPlaceholderRange?>(
                        new RangeOrPlaceholderRange(
                            hit.Range
                        )
                    );
                }
            }
        }

        return Task.FromResult<RangeOrPlaceholderRange?>(null);
    }

    public RenameRegistrationOptions GetRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities)
    {
        return new RenameRegistrationOptions
        {
            DocumentSelector = DocumentSelector.ForLanguage("webforms")
        };
    }
}