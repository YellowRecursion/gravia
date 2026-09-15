using Gravia.Application.Projects.DTOs;
using Gravia.Application.Projects.Services;
using MediatR;

namespace Gravia.Application.Projects.Queries;

public sealed record GetRecentProjectsQuery : IRequest<ProjectDto[]>;

public sealed class GetRecentProjectsQueryHandler(ProjectService projects)
    : IRequestHandler<GetRecentProjectsQuery, ProjectDto[]>
{
    public Task<ProjectDto[]> Handle(GetRecentProjectsQuery request, CancellationToken cancellationToken)
    {
        return projects.GetRecentAsync(cancellationToken);
    }
}
