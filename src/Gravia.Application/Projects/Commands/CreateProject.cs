using FluentValidation;
using Gravia.Application.Projects.DTOs;
using Gravia.Application.Projects.Services;
using MediatR;

namespace Gravia.Application.Projects.Commands;

public sealed record CreateProjectCommand(string ParentDirectory, string Name) : IRequest<ProjectDto>;

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(command => command.ParentDirectory).NotEmpty();
        RuleFor(command => command.Name).NotEmpty();
    }
}

public sealed class CreateProjectCommandHandler(ProjectService projects)
    : IRequestHandler<CreateProjectCommand, ProjectDto>
{
    public Task<ProjectDto> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        return projects.CreateAsync(request.ParentDirectory, request.Name, cancellationToken);
    }
}
