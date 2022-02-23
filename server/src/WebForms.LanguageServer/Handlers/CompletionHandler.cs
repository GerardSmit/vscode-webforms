using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace WebForms.Handlers;

public class CompletionHandler : ICompletionHandler
{
    public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        var list = new List<CompletionItem>();

        return Task.FromResult<CompletionList>(list);
    }

    public CompletionRegistrationOptions GetRegistrationOptions(CompletionCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new CompletionRegistrationOptions
        {
            DocumentSelector = DocumentSelector.ForLanguage("webforms")
        };
    }
}
