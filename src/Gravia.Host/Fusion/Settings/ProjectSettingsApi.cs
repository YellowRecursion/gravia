using System.Text.Json;
using ActualLab.Fusion;
using Gravia.Application.Settings.Commands;
using Gravia.Application.Settings.DTOs;
using Gravia.Application.Settings.Queries;
using MediatR;

namespace Gravia.Host.Fusion.Settings;

public class ProjectSettingsApi(IServiceScopeFactory scopeFactory) : IComputeService
{
    [ComputeMethod]
    public virtual async Task<RpcResult<SettingSummaryDto[]>> GetAll(
        CancellationToken cancellationToken = default)
    {
        var result = await Send(new GetProjectSettingsQuery(), cancellationToken);
        return RpcResult.Success(result);
    }

    [ComputeMethod]
    public virtual async Task<RpcResult<SettingDto?>> Get(
        string id,
        CancellationToken cancellationToken = default)
    {
        // All details, including cached misses, depend on the current project's catalog.
        // Invalidating the catalog on a session switch also invalidates every detail.
        await GetAll(cancellationToken);
        var result = await Send(new GetProjectSettingQuery(id), cancellationToken);
        return RpcResult.Success<SettingDto?>(result);
    }

    public async Task<RpcResult> SetValue(
        string id,
        JsonElement value,
        CancellationToken cancellationToken = default)
    {
        await Send(new SetProjectSettingValueCommand(id, value), cancellationToken);
        return RpcResult.Success();
    }

    public async Task<RpcResult> ResetValue(string id, CancellationToken cancellationToken = default)
    {
        await Send(new ResetProjectSettingValueCommand(id), cancellationToken);
        return RpcResult.Success();
    }

    private async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, cancellationToken);
    }

    private async Task Send(IRequest request, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, cancellationToken);
    }
}
