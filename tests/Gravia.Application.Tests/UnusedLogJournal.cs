using Gravia.Application.Logging;

namespace Gravia.Application.Tests;

// Satisfies Application's ports in unrelated scenario tests without depending on Infrastructure.
// Unexpected journal use must fail, not silently behave like a production implementation.
internal sealed class UnusedLogJournal : ILogJournal
{
    public event Action? Changed
    {
        add => throw new NotSupportedException();
        remove => throw new NotSupportedException();
    }
    public bool TryWrite(LogMessage message) => throw new NotSupportedException();
    public LogBatch Read(Guid? journalId = null, long afterSequence = 0) => throw new NotSupportedException();
    public LogState GetState() => throw new NotSupportedException();
    public LogStatistics GetStatistics() => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
}
