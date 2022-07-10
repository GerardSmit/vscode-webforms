using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Server;
using WebForms;
using WebForms.Handlers;
using WebForms.Services;

// System.Diagnostics.Debugger.Launch();

var server = await LanguageServer.From(options =>
{
    options
        .WithInput(Console.OpenStandardInput())
        .WithOutput(Console.OpenStandardOutput())
        .WithHandler<HighlightHandler>()
        .WithHandler<RenameHandler>()
        .WithHandler<PrepareRenameHandler>()
        .WithHandler<TextDocumentHandler>()
        .WithHandler<DocumentSymbolHandler>()
        .WithHandler<HoverHandler>()
        .WithHandler<ToggleInspectionsHandler>()
        .WithServices(services =>
        {
            services.AddSingleton<TextDocumentContainer>();
            services.AddSingleton<ProjectService>();
        })
        .OnInitialize((_, request, _) =>
        {
            if (request.Capabilities?.TextDocument != null)
            {
                request.Capabilities.TextDocument.Rename = new RenameCapability
                {
                    PrepareSupport = true,
                    DynamicRegistration = true
                };

                request.Capabilities.TextDocument.DocumentHighlight = new DocumentHighlightCapability();
            }

            return Task.CompletedTask;
        });
});

await server.WaitForExit;
