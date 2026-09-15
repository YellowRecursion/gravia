using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Gravia.Application.Projects.Services;
using Gravia.Domain.CommonExceptions;
using MediatR;

namespace Gravia.Application.Settings.Commands;

public sealed record SetProjectSettingValueCommand(string Id, JsonElement Value) : IRequest;

public sealed class SetProjectSettingValueCommandValidator
    : AbstractValidator<SetProjectSettingValueCommand>
{
    public SetProjectSettingValueCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}

public sealed class SetProjectSettingValueCommandHandler
    : IRequestHandler<SetProjectSettingValueCommand>
{
    public Task Handle(SetProjectSettingValueCommand request, CancellationToken cancellationToken)
    {
        var project = ProjectSession.Current ?? throw new NotFoundException("No project is open.", "project_not_open");
        var setting = project.Settings.Find(request.Id)
                      ?? throw new NotFoundException("The setting is not registered.");
        try
        {
            setting.SetSerializedValue(request.Value);
            return Task.CompletedTask;
        }
        catch (JsonException)
        {
            throw Invalid("value", "invalid_setting_value",
                "The value has an incompatible type or format.");
        }
    }

    private static ValidationException Invalid(string field, string code, string message)
        => new([new ValidationFailure(field, message) { ErrorCode = code }]);
}
