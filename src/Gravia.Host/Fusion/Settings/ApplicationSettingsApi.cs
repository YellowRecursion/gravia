using System.Text.Json;
using ActualLab.Fusion;
using FluentValidation;
using FluentValidation.Results;
using Gravia.Application.Settings.DTOs;
using Gravia.Application.Settings.Services;
using Gravia.Domain.Settings.Interfaces;

namespace Gravia.Host.Fusion.Settings;

// TODO: Будет полностью переписан
public class ApplicationSettingsApi(ApplicationSettings settings) : IComputeService
{
    [ComputeMethod]
    public virtual Task<RpcResult<SettingSummaryDto[]>> GetAll(CancellationToken cancellationToken = default)
    {
        var result = settings.Settings.OrderBy(setting => setting.Id, StringComparer.Ordinal)
            .Select(setting => setting.ToDto()).ToArray();
        return Task.FromResult(RpcResult.Success(result));
    }

    [ComputeMethod]
    public virtual Task<RpcResult<SettingDto?>> Get(string id, CancellationToken cancellationToken = default)
    {
        var setting = settings.Find(id);
        SettingDto? result = setting is null ? null : new SettingDto(
            setting.ToDto(), setting.SerializeValue(), setting.SerializeDefaultValue());
        return Task.FromResult(RpcResult.Success<SettingDto?>(result));
    }

    public Task<RpcResult> SetValue(string id, JsonElement value, CancellationToken cancellationToken = default)
    {
        return Change(id, setting => setting.SetSerializedValue(value), cancellationToken);
    }

    public Task<RpcResult> ResetValue(string id, CancellationToken cancellationToken = default)
    {
        return Change(id, setting => setting.Reset(), cancellationToken);
    }

    private Task<RpcResult> Change(string id, Action<ISetting> change, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var setting = settings.Find(id) ?? throw Invalid("id", "setting_not_found", "The setting is not registered.");
        try
        {
            change(setting);
            return Task.FromResult(RpcResult.Success());
        }
        catch (JsonException)
        {
            throw Invalid("value", "invalid_setting_value", "The value has an incompatible type or format.");
        }
    }

    private static ValidationException Invalid(string field, string code, string message)
    {
        return new ValidationException([new ValidationFailure(field, message) { ErrorCode = code }]);
    }
}
