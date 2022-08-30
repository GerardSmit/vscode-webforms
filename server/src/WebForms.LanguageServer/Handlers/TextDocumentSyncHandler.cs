using System.Diagnostics;
using System.Runtime.InteropServices;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using WebForms.Models;
using WebForms.Services;

namespace WebForms;

internal class TextDocumentHandler : TextDocumentSyncHandlerBase
{
    private readonly TextDocumentContainer _documentContainer;

    public TextDocumentHandler(TextDocumentContainer documentContainer)
    {
        _documentContainer = documentContainer;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new TextDocumentAttributes(uri, "webforms");
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        _documentContainer.Update(
            request.TextDocument.Uri,
            request.TextDocument.Version,
            request.TextDocument.Text
        );

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        var text = request.ContentChanges.FirstOrDefault()?.Text;

        if (text == null)
        {
            return Unit.Task;
        }

        _documentContainer.Update(
            request.TextDocument.Uri,
            request.TextDocument.Version,
            text
        );

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        SynchronizationCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions
        {
            Change = TextDocumentSyncKind.Full,
            DocumentSelector = DocumentSelector.ForLanguage("webforms"),
            Save = new SaveOptions { IncludeText = true }
        };
    }
}
