using System.Collections.Concurrent;
using OmniSharp.Extensions.LanguageServer.Protocol;
using WebForms.Models;

namespace WebForms;

public class TextDocumentContainer
{
    public ConcurrentDictionary<DocumentUri, Document> Documents { get; } = new();
}
