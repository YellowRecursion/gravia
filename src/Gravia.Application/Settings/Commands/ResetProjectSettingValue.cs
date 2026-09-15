using FluentValidation;
using Gravia.Application.Projects.Services;
using Gravia.Domain.CommonExceptions;
using MediatR;

namespace Gravia.Application.Settings.Commands;

public sealed record ResetProjectSettingValueCommand(string Id) : IRequest;

public sealed class ResetProjectSettingValueCommandValidator
    : AbstractValidator<ResetProjectSettingValueCommand>
{
    public ResetProjectSettingValueCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}

public sealed class ResetProjectSettingValueCommandHandler
    : IRequestHandler<ResetProjectSettingValueCommand>
{
    public Task Handle(ResetProjectSettingValueCommand request, CancellationToken cancellationToken)
    {
        var project = ProjectSession.Current ?? throw new NotFoundException("No project is open.", "project_not_open");
        var setting = project.Settings.Find(request.Id)
                      ?? throw new NotFoundException("The setting is not registered.");
        setting.Reset();
        return Task.CompletedTask;
    }
}
