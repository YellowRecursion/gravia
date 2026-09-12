using FluentValidation;
using MediatR;

namespace Gravia.Application.Projects;

/// <summary>Reads a saved project without modifying it or activating an editor session.</summary>
public sealed record OpenProjectCommand(string DirectoryPath) : IRequest<ProjectDetails>;

public sealed class OpenProjectCommandValidator : AbstractValidator<OpenProjectCommand>
{
    public OpenProjectCommandValidator()
    {
        RuleFor(command => command.DirectoryPath).Custom((path, context) =>
        {
            if (ProjectDirectory.Validate(path) is { } failure)
                context.AddFailure(failure);
        });
    }
}

public sealed class OpenProjectCommandHandler(IProjectStore store) : IRequestHandler<OpenProjectCommand, ProjectDetails>
{
    public async Task<ProjectDetails> Handle(OpenProjectCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = ProjectDirectory.Normalize(request.DirectoryPath);
        var project = await store.OpenAsync(directory, cancellationToken);
        return new ProjectDetails(project.Id, project.Name, directory);
    }
}
