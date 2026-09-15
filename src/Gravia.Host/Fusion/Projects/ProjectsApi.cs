using ActualLab.Fusion;
using Gravia.Application.Projects.Commands;
using Gravia.Application.Projects.DTOs;
using Gravia.Application.Projects.Queries;
using MediatR;

namespace Gravia.Host.Fusion.Projects;

public class ProjectsApi(IServiceScopeFactory scopeFactory) : IComputeService
{
    public Task<RpcResult<ProjectDto>> Create(string parentDirectory, string name, CancellationToken cancellationToken = default)
        => Send(new CreateProjectCommand(parentDirectory, name), cancellationToken);

    public Task<RpcResult<ProjectDto>> Open(string path, CancellationToken cancellationToken = default)
        => Send(new OpenProjectCommand(path), cancellationToken);

    public Task<RpcResult<ProjectDto?>> GetCurrent(CancellationToken cancellationToken = default)
        => Send(new GetCurrentProjectQuery(), cancellationToken);

    public Task<RpcResult<ProjectDto[]>> GetRecent(CancellationToken cancellationToken = default)
        => Send(new GetRecentProjectsQuery(), cancellationToken);

    private async Task<RpcResult<T>> Send<T>(IRequest<T> request, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, cancellationToken);
        return RpcResult.Success(result);
    }
}
