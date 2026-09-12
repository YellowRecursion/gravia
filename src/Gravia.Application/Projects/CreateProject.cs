using FluentValidation;
using Gravia.Domain.Projects;
using Gravia.Domain.Projects.Exceptions;
using MediatR;

namespace Gravia.Application.Projects;

/// <summary>
/// Creates a saved project in a new or empty directory, without activating an editor session.
/// </summary>
public sealed record CreateProjectCommand(string Name, string DirectoryPath) : IRequest<ProjectDetails>;

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .WithErrorCode(ProjectNameRequiredException.ErrorCode)
            .WithMessage("Project name is required.")
            .OverridePropertyName("name");

        RuleFor(command => command.DirectoryPath).Custom((path, context) =>
        {
            if (ProjectDirectory.Validate(path) is { } failure)
                context.AddFailure(failure);
        });
    }
}

public sealed class CreateProjectCommandHandler(IProjectStore store) : IRequestHandler<CreateProjectCommand, ProjectDetails>
{
    public async Task<ProjectDetails> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var project = Project.Create(request.Name);
        var directory = ProjectDirectory.Normalize(request.DirectoryPath);
        await store.CreateAsync(project, directory, cancellationToken);
        return new ProjectDetails(project.Id, project.Name, directory);
    }
}
