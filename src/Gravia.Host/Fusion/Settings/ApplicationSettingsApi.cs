using System.Text.Json;
using ActualLab.Fusion;
using FluentValidation;
using FluentValidation.Results;
using Gravia.Application.Settings.DTOs;
using Gravia.Application.Settings.Services;
using Gravia.Domain.Settings.Interfaces;
using Gravia.Host.Errors;
using ExceptionHandler = Gravia.Host.Errors.ExceptionHandler;

namespace Gravia.Host.Fusion.Settings;

// TODO: Будет полностью переписан
public class ApplicationSettingsApi(ApplicationSettings settings, ExceptionHandler errors) : IComputeService
{
    [ComputeMethod]
    public virtual Task<SettingSummaryDto[]> GetAll(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(settings.Settings.OrderBy(setting => setting.Id, StringComparer.Ordinal)
            .Select(setting => setting.ToDto()).ToArray());
    }

    [ComputeMethod]
    public virtual Task<SettingDto?> Get(string id, CancellationToken cancellationToken = default)
    {
        var setting = settings.Find(id);
        return Task.FromResult(setting is null ? null : new SettingDto(
            setting.ToDto(), setting.SerializeValue(), setting.SerializeDefaultValue()));
    }

    public Task<ErrorDetails?> SetValue(string id, JsonElement value, CancellationToken cancellationToken = default)
    {
        return Change(id, setting => setting.SetSerializedValue(value), cancellationToken);
    }

    public Task<ErrorDetails?> ResetValue(string id, CancellationToken cancellationToken = default)
    {
        return Change(id, setting => setting.Reset(), cancellationToken);
    }

    private Task<ErrorDetails?> Change(string id, Action<ISetting> change, CancellationToken cancellationToken)
    {
        try
        {
            var setting = settings.Find(id) ?? throw Invalid("id", "setting_not_found", "The setting is not registered.");
            change(setting);
            return Task.FromResult<ErrorDetails?>(null);
        }
        catch (JsonException) { return Task.FromResult<ErrorDetails?>(errors.Handle(
            Invalid("value", "invalid_setting_value", "The value has an incompatible type or format."), cancellationToken)); }
        catch (Exception exception)
        {
            return Task.FromResult<ErrorDetails?>(errors.Handle(exception, cancellationToken));
        }
    }

    private static ValidationException Invalid(string field, string code, string message)
    {
        return new ValidationException([new ValidationFailure(field, message) { ErrorCode = code }]);
    }
}
