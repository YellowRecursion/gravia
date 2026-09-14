using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Gravia.Application.Settings.Services;
using Gravia.Domain.CommonExceptions;
using MediatR;

namespace Gravia.Application.Settings.Commands;

public sealed record SetApplicationSettingValueCommand(string Id, JsonElement Value) : IRequest;

public sealed class SetApplicationSettingValueCommandValidator
    : AbstractValidator<SetApplicationSettingValueCommand>
{
    public SetApplicationSettingValueCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}

public sealed class SetApplicationSettingValueCommandHandler(ApplicationSettings settings)
    : IRequestHandler<SetApplicationSettingValueCommand>
{
    public Task Handle(SetApplicationSettingValueCommand request, CancellationToken cancellationToken)
    {
        var setting = settings.Find(request.Id)
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
