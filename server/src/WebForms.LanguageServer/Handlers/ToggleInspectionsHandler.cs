using MediatR;
using OmniSharp.Extensions.JsonRpc;
using WebForms.Services;

namespace WebForms.Handlers;

public record ToggleInspectionsRequest(bool Enabled) : IRequest;

[Parallel]
[Method("webforms/inspections", Direction.ClientToServer)]
public interface IToggleInspectionsHandler : IJsonRpcNotificationHandler<ToggleInspectionsRequest>
{
}

public class ToggleInspectionsHandler : IToggleInspectionsHandler
{
    private readonly ProjectService _projectService;

    public ToggleInspectionsHandler(ProjectService projectService)
    {
        _projectService = projectService;
    }

    public Task<Unit> Handle(ToggleInspectionsRequest request, CancellationToken cancellationToken)
    {
        _projectService.ToggleInspections(request.Enabled);
        return Unit.Task;
    }
}
