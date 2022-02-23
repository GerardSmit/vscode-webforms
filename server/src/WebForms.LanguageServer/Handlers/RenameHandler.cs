using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace WebForms.Handlers;

public class RenameHandler : IRenameHandler
{
    private readonly TextDocumentContainer _documentContainer;

    public RenameHandler(TextDocumentContainer documentContainer)
    {
        _documentContainer = documentContainer;
    }

    public Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken cancellationToken)
    {
        var changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>();

        if (_documentContainer.Documents.TryGetValue(request.TextDocument.Uri, out var document))
        {
            foreach (var hit in document.HitRanges)
            {
                if (hit.Range.Includes(request.Position.Line, request.Position.Character))
                {
                    hit.Node.Rename(changes, hit, request.NewName, document);
                    break;
                }
            }
        }

        return Task.FromResult<WorkspaceEdit?>(new WorkspaceEdit
        {
            Changes = changes
        });
    }

    public RenameRegistrationOptions GetRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities)
    {
        return new RenameRegistrationOptions
        {
            DocumentSelector = DocumentSelector.ForLanguage("webforms")
        };
    }
}
