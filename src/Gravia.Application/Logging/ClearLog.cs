using MediatR;

namespace Gravia.Application.Logging;

/// <summary>Clears only the process-local history, never the file writer or its queue.</summary>
public sealed record ClearLogCommand : IRequest;

public sealed class ClearLogCommandHandler(ILogJournal journal) : IRequestHandler<ClearLogCommand>
{
    public Task Handle(ClearLogCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        journal.Clear();
        return Task.CompletedTask;
    }
}
