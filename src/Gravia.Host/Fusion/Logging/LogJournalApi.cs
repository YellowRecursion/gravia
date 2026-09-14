using ActualLab.Fusion;
using Gravia.Application.Logging;
using MediatR;

namespace Gravia.Host.Fusion.Logging;

/// <summary>
/// Only the small state is a compute result. Reads use ordinary RPC, avoiding cached copies
/// of the history for every cursor. A Read result always carries its own authoritative bounds.
/// </summary>
public class LogJournalApi(ILogJournal journal, IServiceScopeFactory scopeFactory) : IComputeService
{
    [ComputeMethod]
    public virtual Task<RpcResult<LogState>> GetState(CancellationToken cancellationToken = default)
        => Task.FromResult(RpcResult.Success(journal.GetState()));

    public async Task<RpcResult<LogBatch>> Read(Guid? journalId, long afterSequence,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var batch = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new ReadLogQuery(journalId, afterSequence), cancellationToken);
        return RpcResult.Success(batch);
    }

    public async Task<RpcResult> Clear(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ClearLogCommand(), cancellationToken);
        return RpcResult.Success();
    }
}
