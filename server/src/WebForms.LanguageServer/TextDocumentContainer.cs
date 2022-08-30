using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using WebForms.Models;
using WebForms.Services;

namespace WebForms;

public class TextDocumentContainer
{
    private readonly ProjectService _projectService;

    public TextDocumentContainer(ILanguageServerFacade server, ProjectService projectService)
    {
        Server = server;
        _projectService = projectService;
    }

    public ILanguageServerFacade Server { get; }

    public ConcurrentDictionary<DocumentUri, Document> Documents { get; } = new();

    public Document Get(DocumentUri uri, int? version = null)
    {
        return Documents.AddOrUpdate(
            uri,
            target =>
            {
                var text = File.ReadAllText(target.GetFileSystemPath());
                return CreateDocument(version, text)(target);
            },
            (target, document) =>
            {
                if (string.IsNullOrEmpty(document.Text))
                {
                    document.Text = File.ReadAllText(target.GetFileSystemPath());
                }

                return document;
            });
    }

    public void Update(DocumentUri uri, int? version, string text)
    {
        Documents.AddOrUpdate(
            uri,
            CreateDocument(version, text),
            (_, document) =>
            {
                var sw = Stopwatch.StartNew();
                document.Text = text;
                document.Version = version ?? (document.Version + 1);
                Server.SendNotification("webforms/log", $"Update document: {sw.ElapsedMilliseconds}ms");
                return document;
            });
    }

    public Func<DocumentUri, Document> CreateDocument(int? request, string text)
    {
        return uri =>
        {
            var document = new Document(uri, Server, this);

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
                    document.UpdateProject();
                    project.Documents.Add(document);
                }
            }

            document.Text = text;
            document.Version = request ?? 0;

            return document;
        };
    }
}
