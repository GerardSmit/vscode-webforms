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
    private readonly ILanguageServerFacade _server;
    private readonly ProjectService _projectService;

    public TextDocumentHandler(TextDocumentContainer documentContainer, ILanguageServerFacade server, ProjectService projectService)
    {
        _documentContainer = documentContainer;
        _server = server;
        _projectService = projectService;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new TextDocumentAttributes(uri, "webforms");
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        var text = request.TextDocument.Text;

        _documentContainer.Documents.AddOrUpdate(
            request.TextDocument.Uri,
            CreateDocument(request.TextDocument.Version, text),
            (_, document) =>
            {
                var sw = Stopwatch.StartNew();
                document.Text = request.TextDocument.Text;
                document.Version = request.TextDocument.Version ?? (document.Version + 1);
                _server.SendNotification("webforms/log", $"Open document: {sw.ElapsedMilliseconds}ms");
                return document;
            });

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        var text = request.ContentChanges.FirstOrDefault()?.Text;

        if (text == null)
        {
            return Unit.Task;
        }

        _documentContainer.Documents.AddOrUpdate(
            request.TextDocument.Uri,
            CreateDocument(request.TextDocument.Version, text),
            (_, document) =>
            {
                var sw = Stopwatch.StartNew();
                document.Text = text;
                document.Version = request.TextDocument.Version ?? (document.Version + 1);
                _server.SendNotification("webforms/log", $"Update document: {sw.ElapsedMilliseconds}ms");
                return document;
            });

        return Unit.Task;
    }

    private Func<DocumentUri, Document> CreateDocument(int? request, string text)
    {
        return uri =>
        {
            var document = new Document(uri, _server);

            if (uri.Scheme == "file")
            {
                var path = uri.Path;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    path = path.TrimStart('/', '\\');
                }

                var project = _projectService.GetProject(Path.GetFullPath(path));

                if (project != null)
                {
                    document.Project = project;
                    project.Documents.Add(document);
                }
            }

            document.Text = text;
            document.Version = request ?? 0;

            return document;
        };
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(SynchronizationCapability capability,
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
