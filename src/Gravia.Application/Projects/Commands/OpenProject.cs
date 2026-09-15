using FluentValidation;
using Gravia.Application.Projects.DTOs;
using Gravia.Application.Projects.Services;
using MediatR;

namespace Gravia.Application.Projects.Commands;

public sealed record OpenProjectCommand(string Path) : IRequest<ProjectDto>;

public sealed class OpenProjectCommandValidator : AbstractValidator<OpenProjectCommand>
{
    public OpenProjectCommandValidator()
    {
        RuleFor(command => command.Path).NotEmpty();
    }
}

public sealed class OpenProjectCommandHandler(ProjectService projects)
    : IRequestHandler<OpenProjectCommand, ProjectDto>
{
    public Task<ProjectDto> Handle(OpenProjectCommand request, CancellationToken cancellationToken)
    {
        return projects.OpenAsync(request.Path, cancellationToken);
    }
}
