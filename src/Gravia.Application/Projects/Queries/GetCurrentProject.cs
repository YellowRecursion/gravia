using Gravia.Application.Projects.DTOs;
using Gravia.Application.Projects.Services;
using MediatR;

namespace Gravia.Application.Projects.Queries;

public sealed record GetCurrentProjectQuery : IRequest<ProjectDto?>;

public sealed class GetCurrentProjectQueryHandler(ProjectService projects)
    : IRequestHandler<GetCurrentProjectQuery, ProjectDto?>
{
    public Task<ProjectDto?> Handle(GetCurrentProjectQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(projects.GetCurrent());
    }
}
