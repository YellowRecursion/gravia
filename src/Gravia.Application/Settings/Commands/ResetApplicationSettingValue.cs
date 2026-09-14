using FluentValidation;
using Gravia.Application.Settings.Services;
using Gravia.Domain.CommonExceptions;
using MediatR;

namespace Gravia.Application.Settings.Commands;

public sealed record ResetApplicationSettingValueCommand(string Id) : IRequest;

public sealed class ResetApplicationSettingValueCommandValidator
    : AbstractValidator<ResetApplicationSettingValueCommand>
{
    public ResetApplicationSettingValueCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}

public sealed class ResetApplicationSettingValueCommandHandler(ApplicationSettings settings)
    : IRequestHandler<ResetApplicationSettingValueCommand>
{
    public Task Handle(ResetApplicationSettingValueCommand request, CancellationToken cancellationToken)
    {
        var setting = settings.Find(request.Id)
                      ?? throw new NotFoundException("The setting is not registered.");
        setting.Reset();
        return Task.CompletedTask;
    }
}
